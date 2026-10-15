using InertiaCore.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace InertiaCoreTests.TestApp;

/// <summary>Test-only endpoints for IN-53 protocol integration tests.</summary>
[ApiController]
public class ProtocolTestController : ControllerBase
{
    [HttpGet("/protocol/redirect-fragment")]
    public IActionResult RedirectFragment() => Redirect("/protocol/target#section");

    [HttpGet("/protocol/redirect-preserve")]
    public IActionResult RedirectPreserve()
    {
        HttpContext.PreserveInertiaFragment();
        return Redirect("/protocol/target");
    }
}
