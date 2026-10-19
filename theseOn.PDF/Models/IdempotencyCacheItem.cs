using System.Text.Json.Serialization;

namespace theseOn.PDF.Models;

public class IdempotencyCacheItem
{
    public ProcessStatus Status { get; set; }
    public string PayloadHash { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string ContentType { get; set; } = "application/json";
    public string? ResponseBody { get; set; }
    public Dictionary<string, string> ResponseHeaders { get; set; } = [];
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProcessStatus
{
    Processing,
    Completed
}
