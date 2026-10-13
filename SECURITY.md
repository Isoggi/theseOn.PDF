# Security Policy

## Reporting a vulnerability

Please do not report exploitable vulnerabilities in a public issue. Use GitHub's **Report a vulnerability** option on the repository's Security tab to send a private report to the maintainers. Include the affected component, steps to reproduce, and potential impact where possible.

If private vulnerability reporting is unavailable, contact a repository maintainer privately through GitHub and ask for a secure reporting channel. Do not include exploit details in a public issue.

## Scope and security notes

The URL-to-PDF endpoint makes outbound network requests on behalf of the server. Deployments should restrict allowed destinations and block loopback, private, link-local, and cloud metadata addresses. Do not expose an unrestricted instance to untrusted callers.

HTML and pages rendered by Chromium can execute scripts. Only render trusted content, and run the service with least privilege and appropriate network isolation.
