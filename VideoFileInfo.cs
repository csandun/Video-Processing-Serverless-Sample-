public class VideoFileInfo
{
    public string Path { get; set; }
    
    public int BitRate { get; set; }

    public VideoFileInfo(string location, int bitRate)
    {
        Path = location;
        BitRate = bitRate;
    }

    public VideoFileInfo()
    {
        
    }
}