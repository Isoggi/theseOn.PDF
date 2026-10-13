# theseOn.PDF

An ASP.NET Core Web API that generates A4 PDF documents from an HTML fragment or a rendered web page using PuppeteerSharp and headless Chromium.

## Features

- Convert submitted HTML into a PDF using the shared project template.
- Render a URL using the loaded page, preserving its styles, scripts, and runtime DOM state.
- Optionally select one element from a URL page with a CSS selector, such as `#root`, `main`, `article`, or `section`.
- Return the generated document as `application/pdf`.

## Requirements

- .NET 8 SDK
- Network access on the first run so PuppeteerSharp can download its compatible Chromium browser.

## Run locally

From the repository root:

```powershell
dotnet restore .\theseOn.PDF.sln
dotnet run --project .\theseOn.PDF\theseOn.PDF.csproj
```

The HTTP launch profile listens on `http://localhost:5227`. In Development, Swagger UI is available at `http://localhost:5227/swagger`.

## API

### `POST /api/pdf`

Send JSON with exactly one of `html` or `url`. The response is a PDF attachment named `document.pdf` for HTML input, or `<host>.pdf` for URL input.

#### Convert HTML

```http
POST http://localhost:5227/api/pdf
Content-Type: application/json

{
  "html": "<h1>Invoice</h1><p>Thank you for your order.</p>"
}
```

HTML is inserted into the shared template at [`theseOn.PDF/Templates/PdfTemplate.html`](theseOn.PDF/Templates/PdfTemplate.html). As with any HTML rendering service, only submit HTML you trust.

#### Convert a whole page

```http
POST http://localhost:5227/api/pdf
Content-Type: application/json

{
  "url": "https://example.com"
}
```

The API renders the loaded page directly, so its stylesheets, scripts, and runtime DOM state remain available while generating the PDF. Screen media styles are used for URL rendering.

#### Convert one element from a page

```http
POST http://localhost:5227/api/pdf
Content-Type: application/json

{
  "url": "https://example.com",
  "selector": "#root"
}
```

`selector` is optional and accepts a CSS selector. For example, use `main`, `article`, or `section` instead of `#root` when that is the element you want. The matching element is rendered in its original page context, including ancestor styling; unrelated sibling content is hidden. If the selector is invalid or does not match an element, the API returns `400 Bad Request`. When omitted, the whole page is rendered.

The request examples in [`theseOn.PDF/theseOn.PDF.http`](theseOn.PDF/theseOn.PDF.http) can be used with the VS Code REST Client extension or a compatible HTTP editor.

## Configuration and behavior

- The shared PDF template is [`theseOn.PDF/Templates/PdfTemplate.html`](theseOn.PDF/Templates/PdfTemplate.html). It is copied to build and publish output.
- PDFs use A4 paper with print backgrounds enabled.
- URL navigation waits for network activity to settle before rendering. Some pages that continuously poll or load resources may time out.
- The service uses PuppeteerSharp 20.2.0 and downloads the corresponding Chromium build on first use.

## Security

The URL endpoint makes outbound requests from the server. Do not expose it as an unrestricted public service: validate or allow-list permitted destinations, and block access to loopback, private, link-local, and cloud metadata addresses to reduce server-side request forgery (SSRF) risk. Consider authentication, rate limits, and request-size limits for production use.

See [`SECURITY.md`](SECURITY.md) for vulnerability reporting.

## Contributing

See [`CONTRIBUTING.md`](CONTRIBUTING.md) for the contribution process and [`CODE_OF_CONDUCT.md`](CODE_OF_CONDUCT.md) for community expectations.

## License

This project is licensed under the MIT License. See [`LICENSE`](LICENSE).
