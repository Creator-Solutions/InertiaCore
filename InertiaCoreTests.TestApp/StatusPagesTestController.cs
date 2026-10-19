using InertiaCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InertiaCoreTests.TestApp;

/// <summary>Test-only MVC endpoints for the IN-65 status-page and auth-redirect tests.</summary>
public class StatusPagesTestController : ControllerBase
{
    /// <summary>Requires authentication, so an unauthenticated request challenges (redirects to login).</summary>
    [Authorize]
    [HttpGet("/auth/secure")]
    public IActionResult Secure() => Ok();

    /// <summary>Requires the <c>admin</c> role, so a signed-in non-admin is forbidden (403).</summary>
    [Authorize(Roles = "admin")]
    [HttpGet("/auth/forbid")]
    public IActionResult AdminOnly() => Ok();

    /// <summary>Signs in a plain (non-admin) principal so the role-protected endpoint can be exercised.</summary>
    [HttpPost("/auth/signin")]
    public async Task<IActionResult> SignIn()
    {
        var identity = new System.Security.Claims.ClaimsIdentity(
            new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "tester") },
            authenticationType: CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new System.Security.Claims.ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        return Ok();
    }
}

