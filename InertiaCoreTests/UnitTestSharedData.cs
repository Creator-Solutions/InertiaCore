using InertiaCore.Models;

namespace InertiaCoreTests;

public partial class Tests
{
    [Test]
    [Description("Test if shared data is merged with the props properly.")]
    public async Task TestSharedProps()
    {
        var response = _factory.Render("Test/Page", new
        {
            Test = "Test"
        });

        _factory.Share("TestShared", "Shared");

        var context = PrepareContext();

        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.That(page?.Props, Is.EqualTo(new Dictionary<string, object?>
        {
            { "test", "Test" },
            { "testShared", "Shared" },
            { "errors", new Dictionary<string, object?>() }
        }));
    }
}
