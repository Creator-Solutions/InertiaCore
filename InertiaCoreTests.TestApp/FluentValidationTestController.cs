using InertiaCore.FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace InertiaCoreTests.TestApp;

/// <summary>Test-only MVC endpoints for the IN-62 FluentValidation attribute.</summary>
[ApiController]
public class FluentValidationTestController : ControllerBase
{
    [HttpPost("/fluent-mvc")]
    [InertiaFluentValidation<FluentOrder>]
    public IActionResult Store([FromBody] FluentOrder order)
    {
        FluentProbe.HandlerRuns++;
        return Ok();
    }

    [HttpPost("/fluent-mvc-strict")]
    [InertiaFluentValidation<FluentOrder>(RuleSets = new[] { "strict" })]
    public IActionResult StoreStrict([FromBody] FluentOrder order)
    {
        FluentProbe.HandlerRuns++;
        return Ok();
    }
}
