namespace theseOn.PDF.Services;

public interface IPdfGenerator
{
    Task<Stream?> GenerateAsync(
        string? html,
        CancellationToken cancellationToken);
    Task<Stream?> GenerateAsync(
            string? url,
        string? selector,
        CancellationToken cancellationToken);
    Task<Stream?> GenerateAsync(
        string? html,
        string? url,
        string? selector,
        CancellationToken cancellationToken);
}
