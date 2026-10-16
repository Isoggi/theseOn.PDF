using Microsoft.AspNetCore.Mvc;
using theseOn.PDF.Models;
using theseOn.PDF.Services;

namespace theseOn.PDF.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public sealed class PDFController(IPdfGenerator pdfGenerator) : ControllerBase
    {
        /// <summary>Generates an A4 PDF from supplied HTML or a web page URL.</summary>
        /// <param name="request">The HTML fragment or absolute HTTP(S) URL to render.</param>
        /// <param name="cancellationToken">Cancels the request when the client disconnects.</param>
        [HttpPost]
        [Produces("application/pdf")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Generate(
            [FromBody] GeneratePdfRequest request,
            CancellationToken cancellationToken)
        {
            var hasHtml = !string.IsNullOrWhiteSpace(request.Html);
            var hasUrl = !string.IsNullOrWhiteSpace(request.Url);

            if (hasHtml == hasUrl)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid PDF request",
                    Detail = "Provide exactly one of 'html' or 'url'."
                });
            }

            if (hasHtml && !string.IsNullOrWhiteSpace(request.Selector))
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid PDF request",
                    Detail = "The 'selector' field can only be used with a URL."
                });
            }

            Uri? sourceUri = null;
            if (hasUrl &&
                (!Uri.TryCreate(request.Url, UriKind.Absolute, out sourceUri) ||
                 sourceUri is null ||
                 (sourceUri.Scheme != Uri.UriSchemeHttp && sourceUri.Scheme != Uri.UriSchemeHttps)))
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid URL",
                    Detail = "The 'url' value must be an absolute HTTP or HTTPS URL."
                });
            }

            var pdf = hasHtml ? await pdfGenerator.GenerateAsync(
                html: request.Html,
                cancellationToken: cancellationToken) : await pdfGenerator.GenerateAsync(
                url: sourceUri!.AbsoluteUri,
                selector: request.Selector,
                cancellationToken: cancellationToken);

            if (pdf is null)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Element not found",
                    Detail = "The CSS selector is invalid or did not match an element on the page."
                });
            }

            return File(pdf, "application/pdf", string.Format("{0}.pdf", sourceUri?.Host ?? "document"));
        }
    }
}
