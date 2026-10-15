using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace InertiaCoreTests;

/// <summary>Test-only [ApiController] used by the Precognition MVC integration tests.</summary>
[ApiController]
[Route("mvc-precognition")]
public class PrecognitionTestController : ControllerBase
{
    public static int ActionRuns;

    [HttpPost("create")]
    public IActionResult Create([FromBody] PrecognitionMvcInput input)
    {
        ActionRuns++;
        // Plain text (non-empty) so the test is not affected by the adapter's
        // empty-response redirect or the framework JSON result executor.
        return Content("ok");
    }
}

/// <summary>Input model for <see cref="PrecognitionTestController"/>.</summary>
public sealed class PrecognitionMvcInput
{
    [Required]
    public string? Name { get; set; }

    [System.ComponentModel.DataAnnotations.Range(1, 120)]
    public int Age { get; set; }
}
