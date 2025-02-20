using System.Collections.Generic;
using Newtonsoft.Json;

public class SlackMessage
{
    [JsonProperty("text")]
    public string Text { get; set; }

    [JsonProperty("files")]
    public List<SlackFile> SlackFiles { get; set; }
}