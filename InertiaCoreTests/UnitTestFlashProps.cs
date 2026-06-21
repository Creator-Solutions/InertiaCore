using InertiaCore.Models;
using InertiaCore.Services;

namespace InertiaCoreTests;

public partial class Tests
{
    [Test]
    [Description("Flash prop appears in the response for the current request.")]
    public async Task TestFlashPropAppearsInResponse()
    {
        var response = _factory.Render("Test/Page", new
        {
            Test = "Test"
        });

        _factory.Share("flashMessage", "This is a flash");

        var context = PrepareContext();

        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.That(page?.Props, Is.EqualTo(new Dictionary<string, object?>
        {
            { "test", "Test" },
            { "flashMessage", "This is a flash" },
            { "errors", new Dictionary<string, object?>() }
        }));
    }

    [Test]
    [Description("Flash prop is overridden by component prop with the same key.")]
    public async Task TestComponentPropOverridesFlash()
    {
        var response = _factory.Render("Test/Page", new
        {
            Key = "Component Value"
        });

        _factory.Share("Key", "Flash Value");

        var context = PrepareContext();

        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.That(page?.Props["key"], Is.EqualTo("Component Value"));
    }

    [Test]
    [Description("Multiple flash props are all included.")]
    public async Task TestMultipleFlashProps()
    {
        var response = _factory.Render("Test/Page", new
        {
            Test = "Test"
        });

        _factory.Share("Flash1", "Value1");
        _factory.Share("Flash2", "Value2");

        var context = PrepareContext();

        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.That(page?.Props, Is.EqualTo(new Dictionary<string, object?>
        {
            { "test", "Test" },
            { "flash1", "Value1" },
            { "flash2", "Value2" },
            { "errors", new Dictionary<string, object?>() }
        }));
    }

    [Test]
    [Description("Null flash value is preserved in the response.")]
    public async Task TestNullFlashValue()
    {
        var response = _factory.Render("Test/Page", new
        {
            Test = "Test"
        });

        _factory.Share("NullKey", null);

        var context = PrepareContext();

        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.That(page?.Props.ContainsKey("nullKey"), Is.True);
        Assert.That(page?.Props["nullKey"], Is.Null);
    }
}
