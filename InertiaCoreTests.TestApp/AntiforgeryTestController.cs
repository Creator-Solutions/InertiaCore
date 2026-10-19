using InertiaCore;
using Microsoft.AspNetCore.Mvc;

namespace InertiaCoreTests.TestApp;

/// <summary>Test-only MVC endpoints for the IN-64 anti-forgery bridge.</summary>
[ApiController]
[AutoValidateAntiforgeryToken]
public class AntiforgeryTestController : ControllerBase
{
    [HttpPost("/csrf-mvc")]
    public IActionResult Store()
    {
        AntiForgeryProbe.HandlerRuns++;
        return Ok();
    }

    /// <summary>Signs in (no real principal) and rotates the XSRF token, as the docs advise.</summary>
    [HttpPost("/csrf-signin")]
    public IActionResult SignIn()
    {
        Inertia.RotateAntiforgery();
        return Ok();
    }
}

/// <summary>Tracks how often an anti-forgery test endpoint handler executed.</summary>
public static class AntiForgeryProbe
{
    public static int HandlerRuns;
}
