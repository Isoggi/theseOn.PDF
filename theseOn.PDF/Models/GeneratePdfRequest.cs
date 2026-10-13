namespace theseOn.PDF.Models;

/// <summary>Input for generating a PDF from HTML or a web page URL.</summary>
public sealed record GeneratePdfRequest
{
    /// <summary>An HTML fragment to place in the shared PDF template.</summary>
    public string? Html { get; init; }

    /// <summary>An absolute HTTP or HTTPS URL whose rendered body is placed in the shared PDF template.</summary>
    public string? Url { get; init; }
}
