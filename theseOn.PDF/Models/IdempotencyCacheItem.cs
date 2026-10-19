using System;
using System.Diagnostics;

namespace theseOn.PDF.Models;

public class IdempotencyCacheItem
{
    public ProcessStatus Status { get; set; } = ProcessStatus.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string ContentType { get; set; } = "application/json";
    public string? ResponseBody { get; set; }

}

public readonly struct ProcessStatus
{
    private readonly string _status;
    private ProcessStatus(string status) => _status = status;
    public static ProcessStatus Processed => new("PROCESSED");
    public static ProcessStatus Completed => new("COMPLETED");
    public static ProcessStatus Empty => new(string.Empty);

    public static implicit operator string(ProcessStatus status) => status._status ?? string.Empty;

    public override string ToString() => _status ?? string.Empty;
}
