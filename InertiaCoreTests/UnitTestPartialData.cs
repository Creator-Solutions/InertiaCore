using InertiaCore.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace InertiaCoreTests;

public partial class Tests
{
    [Test]
    [Description("Test if the props contain only the specified partial data.")]
    public async Task TestPartialOnlyData()
    {
        var response = _factory.Render("Test/Page", new
        {
            Test = "Test",
            TestPartial = "Partial",
            TestFunc = new Func<string>(() =>
            {
                Assert.Fail();
                return "Func";
            }),
            TestLazy = _factory.Lazy(() =>
            {
                Assert.Fail();
                return "Lazy";
            })
        });

        var headers = new HeaderDictionary
        {
            { "X-Inertia-Partial-Data", "testPartial" },
            { "X-Inertia", "true" },
            { "X-Inertia-Partial-Component", "Test/Page" }
        };

        var context = PrepareContext(headers);

        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.That(page?.Props, Is.EqualTo(new Dictionary<string, object?>
        {
            { "testPartial", "Partial" },
            { "errors", new Dictionary<string, string>(0) }
        }));
    }

    [Test]
    [Description("Test if the props contain all the unspecified data")]
    public async Task TestPartialFullData()
    {
        var response = _factory.Render("Test/Page", new
        {
            Test = "Test",
            TestPartial = "Partial",
            TestFunc = new Func<string>(() => "Func"),
            TestLazy = _factory.Lazy(() => "Lazy")
        });

        var headers = new HeaderDictionary
        {
            { "X-Inertia", "true" },
            { "X-Inertia-Partial-Component", "Test/Page" }
        };

        var context = PrepareContext(headers);

        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.That(page?.Props, Is.EqualTo(new Dictionary<string, object?>
        {
            { "test", "Test" },
            { "testPartial", "Partial" },
            { "testFunc", "Func" },
            { "testLazy", "Lazy" },
            { "errors", new Dictionary<string, string>(0) }
        }));
    }

    [Test]
    [Description("Test if the props contain none of the except data")]
    public async Task TestPartialExceptData()
    {
        var response = _factory.Render("Test/Page", new
        {
            Test = "Test",
            TestPartial = "Partial",
            TestFunc = new Func<string>(() =>
            {
                Assert.Fail();
                return "Func";
            }),
            TestLazy = _factory.Lazy(() =>
            {
                Assert.Fail();
                return "Lazy";
            })
        });

        var headers = new HeaderDictionary
        {
            { "X-Inertia-Partial-Except", "TestFunc,TestLazy" },
            { "X-Inertia", "true" },
            { "X-Inertia-Partial-Component", "Test/Page" }
        };

        var context = PrepareContext(headers);

        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.That(page?.Props, Is.EqualTo(new Dictionary<string, object?>
        {
            { "test", "Test" },
            { "testPartial", "Partial" },
            { "errors", new Dictionary<string, string>(0) }
        }));
    }

    [Test]
    [Description("Test if the props contain the correct data when using only and except data")]
    public async Task TestPartialOnlyAndExceptData()
    {
        var response = _factory.Render("Test/Page", new
        {
            Test = "Test",
            TestPartial = "Partial",
            TestFunc = new Func<string>(() =>
            {
                Assert.Fail();
                return "Func";
            }),
            TestLazy = _factory.Lazy(() =>
            {
                Assert.Fail();
                return "Lazy";
            })
        });

        var headers = new HeaderDictionary
        {
            { "X-Inertia-Partial-Data", "Test,TestFunc,TestLazy" },
            { "X-Inertia-Partial-Except", "TestFunc,TestLazy" },
            { "X-Inertia", "true" },
            { "X-Inertia-Partial-Component", "Test/Page" }
        };

        var context = PrepareContext(headers);

        response.SetContext(context);
        await response.ProcessResponse();

        var page = response.GetJson().Value as Page;

        Assert.That(page?.Props, Is.EqualTo(new Dictionary<string, object?>
        {
            { "test", "Test" },
            { "errors", new Dictionary<string, string>(0) }
        }));
    }

    [Test]
    [Description("SC-08: a full-page (non-Inertia) request ignores partial headers.")]
    public async Task TestPartialHeadersIgnoredForNonInertiaRequest()
    {
        var response = _factory.Render("Test/Page", new
        {
            Test = "Test",
            TestPartial = "Partial",
            TestLazy = _factory.Lazy(() =>
            {
                Assert.Fail("A full-page load must not resolve lazy props.");
                return "Lazy";
            })
        });

        // Partial headers are present, but X-Inertia is not: this must be treated as a
        // full page load, so no partial filtering happens and lazy props stay excluded.
        var headers = new HeaderDictionary
        {
            { "X-Inertia-Partial-Data", "testPartial" },
            { "X-Inertia-Partial-Component", "Test/Page" }
        };

        var context = PrepareContext(headers);

        response.SetContext(context);
        await response.ProcessResponse();

        var page = (response.GetResult() as ViewResult)?.Model as Page;

        Assert.Multiple(() =>
        {
            Assert.That(page?.Props, Does.ContainKey("test"));
            Assert.That(page?.Props, Does.ContainKey("testPartial"));
            Assert.That(page?.Props, Does.Not.ContainKey("testLazy"));
        });
    }
}
