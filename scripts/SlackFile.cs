using Newtonsoft.Json;

public class SlackFile
{
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("url_private_download")]
    public string DownloadUrl { get; set; }

}