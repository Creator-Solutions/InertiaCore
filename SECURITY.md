# Security Policy

## Supported Versions

Security fixes are applied to the latest released version of
`InertiaCore.AspNetCore`. Older versions may not receive fixes; please upgrade to
the latest release before reporting.

| Version | Supported |
| ------- | --------- |
| Latest release | Yes |
| Older releases | No |

## Reporting a Vulnerability

**Please do not report security vulnerabilities through public GitHub issues,
discussions, or pull requests.**

Report privately using GitHub's [Private Vulnerability Reporting](https://docs.github.com/en/code-security/security-advisories/guidance-on-reporting-and-writing-information-about-vulnerabilities/privately-reporting-a-security-vulnerability)
("Report a vulnerability" under the repository's **Security** tab). This keeps the
details private until a fix is available.

If you cannot use GitHub Security Advisories, contact the maintainers listed in the
repository metadata (`InertiaCore.AspNetCore` on NuGet) to arrange a private channel.

### What to include

- A description of the issue and the affected component (for example the SSR
  gateway, Vite helper, middleware, or a specific public API).
- The version(s) affected and the target framework(s).
- Reproduction steps or a minimal proof of concept, if possible.
- The impact you believe the issue has.
- Any suggested remediation.

### What to expect

- **Acknowledgement** within a reasonable time of the report.
- An initial **assessment** (severity, affected versions, whether it is
  reproducible).
- Regular updates while the issue is investigated.
- **Coordinated disclosure**: once a fix is available, we will publish a security
  advisory and credit you unless you ask to remain anonymous.

Please give us a reasonable window to release a fix before public disclosure.

## Scope

This policy covers the `InertiaCore.AspNetCore` library in this repository. Issues
in third-party dependencies should be reported to the relevant upstream project;
if they affect this library, we still appreciate a heads-up.

The following are **not** considered vulnerabilities in this library:

- Applications passing untrusted input into `Inertia.Location`/redirect helpers or
  into Inertia component names; the library documents that these values must be
  validated by the application.
- Raw HTML emitted by a trusted, configured SSR renderer or supplied by the
  application through the default view/SSR rendering path.
- Missing CSRF protection when the application has not configured ASP.NET Core
  antiforgery (see the README's "CSRF / Antiforgery" section).
