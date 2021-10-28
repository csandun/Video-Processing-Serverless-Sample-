namespace Csandun.VideoProcessor
{
    public class ProcessVideoInputModel
    {
        public string Path { get; private set; }
        public string Name { get; set; }
        
        
        public ProcessVideoInputModel(string path)
        {
            Path = path;
        }
    }
}