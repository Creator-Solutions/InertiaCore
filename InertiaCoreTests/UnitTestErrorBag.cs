using InertiaCore.Models;
using Microsoft.AspNetCore.Http;

namespace InertiaCoreTests;

public partial class Tests
{
    [Test]
    [Description("Request without X-Inertia-Error-Bag returns flat errors.")]
    public async Task TestErrorsWithoutErrorBag()
    {
        var response = _factory.Render("Test/Page", new
        {
            Test = "Test"
        });

        var headers = new HeaderDictionary
        {
            { "X-Inertia", "true" }
        };

        var context = PrepareContext(headers, new Dictionary<string, string>
        {
            { "Field", "Error" }
        });

        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.That(page?.Props, Is.EqualTo(new Dictionary<string, object?>
        {
            { "test", "Test" },
            {
                "errors", new Dictionary<string, object?>
                {
                    { "field", "Error" }
                }
            }
        }));
    }

    [Test]
    [Description("Request with X-Inertia-Error-Bag scopes errors under the bag name.")]
    public async Task TestErrorsWithNamedErrorBag()
    {
        var response = _factory.Render("Test/Page", new
        {
            Test = "Test"
        });

        var headers = new HeaderDictionary
        {
            { "X-Inertia", "true" },
            { "X-Inertia-Error-Bag", "myForm" }
        };

        var context = PrepareContext(headers, new Dictionary<string, string>
        {
            { "Field", "Error" }
        });

        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.That(page?.Props, Is.EqualTo(new Dictionary<string, object?>
        {
            { "test", "Test" },
            {
                "errors", new Dictionary<string, object?>
                {
                    {
                        "myForm", new Dictionary<string, string>
                        {
                            { "field", "Error" }
                        }
                    }
                }
            }
        }));
    }

    [Test]
    [Description("Error bag with empty ModelState returns empty errors.")]
    public async Task TestErrorBagWithEmptyModelState()
    {
        var response = _factory.Render("Test/Page", new
        {
            Test = "Test"
        });

        var headers = new HeaderDictionary
        {
            { "X-Inertia", "true" },
            { "X-Inertia-Error-Bag", "myForm" }
        };

        var context = PrepareContext(headers);

        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.That(page?.Props, Is.EqualTo(new Dictionary<string, object?>
        {
            { "test", "Test" },
            { "errors", new Dictionary<string, object?>() }
        }));
    }

    [Test]
    [Description("Error bag name is preserved as provided (case-sensitive per protocol).")]
    public async Task TestErrorBagCaseSensitivity()
    {
        var response = _factory.Render("Test/Page", new
        {
            Test = "Test"
        });

        var headers = new HeaderDictionary
        {
            { "X-Inertia", "true" },
            { "X-Inertia-Error-Bag", "MyForm" }
        };

        var context = PrepareContext(headers, new Dictionary<string, string>
        {
            { "Field", "Error" }
        });

        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        var errors = page?.Props["errors"] as Dictionary<string, object?>;
        Assert.That(errors, Is.Not.Null);
        Assert.That(errors!.ContainsKey("MyForm"), Is.True);
        Assert.That(errors.ContainsKey("myForm"), Is.False);
    }
}
