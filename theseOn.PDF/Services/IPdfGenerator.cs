namespace theseOn.PDF.Services;

public interface IPdfGenerator
{
    Task<byte[]?> GenerateAsync(
        string? html,
        string? url,
        string? selector,
        CancellationToken cancellationToken);
}
