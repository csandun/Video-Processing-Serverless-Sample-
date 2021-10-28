using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace Csandun.VideoProcessor
{
    public class ProcessVideoFunctionApp
    {
        private readonly ILogger<ProcessVideoFunctionApp> _logger;

        public ProcessVideoFunctionApp(ILogger<ProcessVideoFunctionApp> log)
        {
            _logger = log;
        }


        #region  Orchestrator
        [FunctionName(nameof(ProcessVideoOrchestrator))]
        public async Task<List<VideoFileInfo>> ProcessVideoOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {

            int[] bitRates = { 100, 120, 1000 };
            var inputs = context.GetInput<ProcessVideoInputModel>();
            var outputs = new List<VideoFileInfo>();
            var transcodes = new List<Task<VideoFileInfo>>();


            // transcode
            var transcodedvideo = await context.CallActivityAsync<VideoFileInfo>(nameof(TranscodeVideo), inputs);
            outputs.Add(transcodedvideo);

            foreach (var bitRate in bitRates)
            {
                transcodedvideo.BitRate = bitRate;
                // extract thumbnail
                var thumbnailPng = context.CallActivityAsync<VideoFileInfo>(nameof(ExtractThumbnailAsync), transcodedvideo);
                transcodes.Add(thumbnailPng);
            }

            var outputsofThum = (await Task.WhenAll<VideoFileInfo>(transcodes)).First();
            outputs.Add(outputsofThum);

            // prepend intro
            var prepentIntro = await context.CallActivityAsync<VideoFileInfo>(nameof(PrependIntro), transcodedvideo);
            outputs.Add(prepentIntro);

            outputs.ToList().ForEach(o =>
            {
                System.Console.WriteLine(o.Path);
            });


            return outputs;
        }
        #endregion

        #region  activity functions
        [FunctionName(nameof(TranscodeVideo))]
        public async Task<VideoFileInfo> TranscodeVideo([ActivityTrigger] VideoFileInfo model)
        {
            _logger.LogInformation($"Processing start - transcode video {model.Path}.");
            await Task.Delay(5000);
            var fileName = Path.GetFileNameWithoutExtension(model.Path);
            var videoInfo = new VideoFileInfo()
            {
                Path = fileName + "-transcoded.mp4"
            };
            return videoInfo;
        }

        [FunctionName(nameof(ExtractThumbnailAsync))]
        public async Task<VideoFileInfo> ExtractThumbnailAsync([ActivityTrigger] VideoFileInfo model)
        {
            _logger.LogInformation($"Processing start - Extracting video {model.Path}.");
            await Task.Delay(10000);
            var fileName = Path.GetFileNameWithoutExtension(model.Path) + "-" + model.BitRate + "-extracted.png";
            var fileInfo = new VideoFileInfo() { Path = fileName, BitRate = model.BitRate };
            return fileInfo;
        }

        [FunctionName(nameof(PrependIntro))]
        public async Task<VideoFileInfo> PrependIntro([ActivityTrigger] VideoFileInfo model)
        {
            _logger.LogInformation($"Processing start - Extracting video {model.Path}.");
            await Task.Delay(5000);
            var fileName = Path.GetFileNameWithoutExtension(model.Path) + "-" + model.BitRate + "-prepended.txt";
            var fileInfo = new VideoFileInfo() { Path = fileName, BitRate = model.BitRate };
            return fileInfo;
        }

        #endregion

        #region  starter function
        [FunctionName(nameof(ProcessVideoStarter))]
        public async Task<IActionResult> ProcessVideoStarter(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req,
            [DurableClient] IDurableOrchestrationClient starter)
        {
            var filePath = req.GetQueryParameterDictionary()["video"];

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return new NotFoundResult();
            }

            var pathModel = new ProcessVideoInputModel(filePath);
            string instanceId = await starter.StartNewAsync<ProcessVideoInputModel>(nameof(ProcessVideoOrchestrator), null, pathModel);

            _logger.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
        #endregion
    }
}