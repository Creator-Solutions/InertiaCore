namespace InertiaCoreTests;

public partial class Tests
{
    [Test]
    [Description("Test if the generated HTML contains valid page data.")]
    public async Task TestHtml()
    {
        var html = await _factory.Html(new { Test = "Test" });

        Assert.That(html.ToString(),
            Is.EqualTo("<script data-page=\"app\" type=\"application/json\">{\"test\":\"Test\"}</script>\n<div id=\"app\"></div>"));
    }
}