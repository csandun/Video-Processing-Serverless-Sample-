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

        [FunctionName(nameof(ProcessVideoOrchestrator))]
        public async Task<List<string>> ProcessVideoOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var inputs = context.GetInput<ProcessVideoInputModel>();
            var outputs = new List<string>();

            // transcode
            var transcodedvideo = await context.CallActivityAsync<string>(nameof(TranscodeVideo), inputs);
            outputs.Add(transcodedvideo);

            // extract thumbnail
            var thumbnailPng = await context.CallActivityAsync<string>(nameof(ExtractThumbnail), transcodedvideo);
            outputs.Add(thumbnailPng);

            // prepend intro
            var prepentIntro = await context.CallActivityAsync<string>(nameof(PrependIntro), transcodedvideo);
            outputs.Add(prepentIntro);

            outputs.ToList().ForEach(o => {
                System.Console.WriteLine(o);
            });
            

            return outputs;
        }

        [FunctionName(nameof(TranscodeVideo))]
        public string TranscodeVideo([ActivityTrigger] ProcessVideoInputModel model)
        {
            _logger.LogInformation($"Processing start - transcode video {model.Path}.");
            Thread.Sleep(10000);
            var fileName = Path.GetFileName(model.Path);
            return Path.Combine(fileName, "mp4");
        }

        [FunctionName(nameof(ExtractThumbnail))]
        public string ExtractThumbnail([ActivityTrigger] string transcodepath)
        {
            _logger.LogInformation($"Processing start - Extracting video {transcodepath}.");
            Thread.Sleep(90000);
            var fileName = Path.GetFileName(transcodepath);
            return Path.Combine(fileName, "png");
        }

        [FunctionName(nameof(PrependIntro))]
        public string PrependIntro([ActivityTrigger] string transcodepath)
        {
            _logger.LogInformation($"Processing start - Extracting video {transcodepath}.");
            Thread.Sleep(60000);
            var fileName = Path.GetFileName(transcodepath);
            return Path.Combine(fileName, "txt");
        }

        [FunctionName(nameof(ProcessVideoStarter))]
        public async Task<IActionResult> ProcessVideoStarter(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req,
            [DurableClient] IDurableOrchestrationClient starter)
        {
            var filePath = req.GetQueryParameterDictionary()["video"];

            if(string.IsNullOrWhiteSpace(filePath)){
                return new NotFoundResult();
            }

            var pathModel = new ProcessVideoInputModel(filePath);
            string instanceId = await starter.StartNewAsync<ProcessVideoInputModel>(nameof(ProcessVideoOrchestrator), null, pathModel);

            _logger.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}