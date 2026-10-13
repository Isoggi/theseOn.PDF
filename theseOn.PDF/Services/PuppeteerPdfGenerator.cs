using System.Text.Encodings.Web;
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

    public async Task<byte[]?> GenerateAsync(
        string? html,
        string? url,
        string? selector,
        CancellationToken cancellationToken)
    {
        var browser = await _browser.Value.WaitAsync(cancellationToken);
        var page = await browser.NewPageAsync().WaitAsync(cancellationToken);

        try
        {
            string content;
            var pageStyles = string.Empty;
            var pageScripts = string.Empty;
            string? baseUrl = null;

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

                if (string.IsNullOrWhiteSpace(selector))
                {
                    content = await page.EvaluateExpressionAsync<string>(
                        "document.body ? document.body.innerHTML : ''").WaitAsync(cancellationToken);
                }
                else
                {
                    var selectedContent = await page.EvaluateFunctionAsync<string?>(
                        """
                        selector => {
                            try {
                                return document.querySelector(selector)?.outerHTML ?? null;
                            } catch {
                                return null;
                            }
                        }
                        """,
                        selector).WaitAsync(cancellationToken);

                    if (selectedContent is null)
                    {
                        return null;
                    }

                    content = selectedContent;
                }

                pageStyles = await page.EvaluateExpressionAsync<string>(
                    "Array.from(document.head.querySelectorAll('style, link[rel~=\"stylesheet\"]'), element => element.outerHTML).join('\\n')")
                    .WaitAsync(cancellationToken);
                pageScripts = await page.EvaluateExpressionAsync<string>(
                    "Array.from(document.head.querySelectorAll('script'), element => element.outerHTML).join('\\n')")
                    .WaitAsync(cancellationToken);
                baseUrl = page.Url;
            }
            else
            {
                content = html ?? throw new ArgumentException(
                    "HTML content is required when a URL is not provided.",
                    nameof(html));
            }

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

            var baseTag = baseUrl is null
                ? string.Empty
                : $"<base href=\"{HtmlEncoder.Default.Encode(baseUrl)}\">";
            var document = template
                .Replace(BaseUrlPlaceholder, baseTag, StringComparison.Ordinal)
                .Replace(StylesPlaceholder, pageStyles, StringComparison.Ordinal)
                .Replace(ContentPlaceholder, content, StringComparison.Ordinal);
            document = document.Replace(ScriptsPlaceholder, pageScripts, StringComparison.Ordinal);
            Console.WriteLine(document);
            await page.SetContentAsync(document, new NavigationOptions
            {
                Timeout = NavigationTimeoutMilliseconds,
                WaitUntil = [WaitUntilNavigation.Networkidle2]
            }).WaitAsync(cancellationToken);

            return await page.PdfDataAsync(new PdfOptions
            {
                Format = PaperFormat.A4,
                PrintBackground = true,
                PreferCSSPageSize = true,
            }).WaitAsync(cancellationToken);
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
}
