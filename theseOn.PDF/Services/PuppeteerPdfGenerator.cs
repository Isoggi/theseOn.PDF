using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace theseOn.PDF.Services;

public sealed class PuppeteerPdfGenerator : IPdfGenerator, IAsyncDisposable
{
    private const string ContentPlaceholder = "{{content}}";
    private const string BaseUrlPlaceholder = "{{baseUrl}}";
    private const string StylesPlaceholder = "{{pageStyles}}";
    private const string ScriptsPlaceholder = "{{pageScripts}}";
    private const int NavigationTimeoutMilliseconds = 30_000;
    private readonly Lazy<Task<IBrowser>> _browser = new(LaunchBrowserAsync);

    public async Task<Stream?> GenerateAsync(string? html, CancellationToken cancellationToken)
    {
        return await GenerateAsync(html, null, null, cancellationToken);
    }

    public async Task<Stream?> GenerateAsync(string? url, string? selector, CancellationToken cancellationToken)
    {
        return await GenerateAsync(null, url, selector, cancellationToken);
    }

    public async Task<Stream?> GenerateAsync(
        string? html,
        string? url,
        string? selector,
        CancellationToken cancellationToken)
    {
        var browser = await _browser.Value.WaitAsync(cancellationToken);
        var page = await browser.NewPageAsync().WaitAsync(cancellationToken);

        try
        {
            if (url is not null)
            {
                var sourceUri = new Uri(url, UriKind.Absolute);
                if (sourceUri.Scheme != Uri.UriSchemeHttp && sourceUri.Scheme != Uri.UriSchemeHttps)
                {
                    throw new ArgumentException("Only HTTP and HTTPS URLs can be rendered.", nameof(url));
                }

                await page.GoToAsync(url, new NavigationOptions
                {
                    Timeout = NavigationTimeoutMilliseconds,
                    WaitUntil = [WaitUntilNavigation.Networkidle2]
                }).WaitAsync(cancellationToken);

                await page.EmulateMediaTypeAsync(MediaType.Screen);
                if (!string.IsNullOrWhiteSpace(selector))
                {
                    await page.WaitForSelectorAsync(selector).WaitAsync(cancellationToken);

                    var selectedElementFound = await page.EvaluateFunctionAsync<bool>(
                        """
                        selector => {
                            try {
                                let element = document.querySelector(selector);
                                if (!element) {
                                    return false;
                                }

                                while (element !== document.body) {
                                    const parent = element.parentElement;
                                    if (!parent) {
                                        break;
                                    }

                                    for (const sibling of parent.children) {
                                        if (sibling !== element) {
                                            sibling.style.setProperty('display', 'none', 'important');
                                        }
                                    }

                                    element = parent;
                                }

                                return true;
                            } catch {
                                return false;
                            }
                        }
                        """,
                        selector).WaitAsync(cancellationToken);

                    if (!selectedElementFound)
                    {
                        return null;
                    }
                }

                return await page.PdfStreamAsync(CreatePdfOptions()).WaitAsync(cancellationToken);
            }

            var content = html ?? throw new ArgumentException(
                "HTML content is required when a URL is not provided.",
                nameof(html));

            var templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "PdfTemplate.html");
            var template = await File.ReadAllTextAsync(templatePath, cancellationToken);
            if (!template.Contains(ContentPlaceholder, StringComparison.Ordinal) ||
                !template.Contains(BaseUrlPlaceholder, StringComparison.Ordinal) ||
                !template.Contains(StylesPlaceholder, StringComparison.Ordinal) ||
                !template.Contains(ScriptsPlaceholder, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"The PDF template must contain {ContentPlaceholder}, {BaseUrlPlaceholder}, " +
                    $"{StylesPlaceholder}, and {ScriptsPlaceholder}.");
            }

            var document = template
                .Replace(BaseUrlPlaceholder, string.Empty, StringComparison.Ordinal)
                .Replace(StylesPlaceholder, string.Empty, StringComparison.Ordinal)
                .Replace(ContentPlaceholder, content, StringComparison.Ordinal);
            document = document.Replace(ScriptsPlaceholder, string.Empty, StringComparison.Ordinal);

            await page.SetContentAsync(document, new NavigationOptions
            {
                Timeout = NavigationTimeoutMilliseconds,
                WaitUntil = [WaitUntilNavigation.Networkidle2]
            }).WaitAsync(cancellationToken);

            return await page.PdfStreamAsync(CreatePdfOptions()).WaitAsync(cancellationToken);
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser.IsValueCreated && !_browser.Value.IsFaulted && !_browser.Value.IsCanceled)
        {
            var browser = await _browser.Value;
            await browser.CloseAsync();
        }
    }

    private static async Task<IBrowser> LaunchBrowserAsync()
    {
        var browserFetcher = new BrowserFetcher();
        await browserFetcher.DownloadAsync();
        return await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });
    }

    private static PdfOptions CreatePdfOptions() => new()
    {
        Format = PaperFormat.A4,
        PrintBackground = true,
        PreferCSSPageSize = true,
    };
}
