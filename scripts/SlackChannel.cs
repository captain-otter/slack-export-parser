using Newtonsoft.Json;

public class SlackChannel
{
    [JsonProperty("name")]
    public string Name { get; set; }
}