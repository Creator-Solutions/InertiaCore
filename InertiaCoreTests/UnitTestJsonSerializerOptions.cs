using System.Text.Json;
using System.Text.Json.Serialization;
using InertiaCore;
using InertiaCore.Extensions;
using InertiaCore.Models;
using InertiaCore.Services;
using InertiaCore.Ssr;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;

namespace InertiaCoreTests;

public class SourceGeneratedProps
{
    public string FirstName { get; set; } = "Ada";
    public int ItemCount { get; set; } = 7;
}

[JsonSerializable(typeof(SourceGeneratedProps))]
[JsonSerializable(typeof(Dictionary<string, object?>))]
public partial class SourceGeneratedContext : JsonSerializerContext
{
}

[TestFixture]
public class UnitTestJsonSerializerOptions
{
    [Test]
    public void SourceGeneratedContext_SerializesProps()
    {
        var contextAccessor = new Mock<IHttpContextAccessor>();
        var gateway = new Gateway(new InertiaSsrClient(new HttpClient()), null, null, null);

        var inertiaOptions = new InertiaOptions
        {
            JsonSerializerOptions = new JsonSerializerOptions
            {
                TypeInfoResolver = SourceGeneratedContext.Default
            }
        };

        var options = new Mock<IOptions<InertiaOptions>>();
        options.SetupGet(x => x.Value).Returns(inertiaOptions);

        var state = new InertiaState();
        var jsonOptions = new Mock<IOptions<JsonOptions>>();
        jsonOptions.SetupGet(x => x.Value).Returns(new JsonOptions());

        var factory = new ResponseFactory(
            contextAccessor.Object,
            gateway,
            options.Object,
            state,
            jsonOptions.Object);

        var response = factory.Render("Test/Page", new SourceGeneratedProps());
        Assert.That(response, Is.Not.Null);
    }
}
