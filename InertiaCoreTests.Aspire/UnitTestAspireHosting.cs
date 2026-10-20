using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.JavaScript;
using InertiaCore.Aspire.Hosting;

namespace InertiaCoreTests;

/// <summary>
/// Unit tests for the Aspire hosting extension methods. The distributed application
/// model is built with the manifest publisher so no child process is launched, and the
/// environment callbacks are invoked directly to inspect the values that would be
/// injected into the web project.
/// </summary>
[TestFixture]
public class UnitTestAspireHosting
{
    private static IDistributedApplicationBuilder CreateBuilder() =>
        DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = new[] { "--publisher", "manifest" },
        });

    private static Dictionary<string, object> EvaluateEnvironment(IResource resource)
    {
        var result = new Dictionary<string, object>();
        var context = new EnvironmentCallbackContext(
            new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run),
            resource,
            result,
            CancellationToken.None);

        foreach (var annotation in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            annotation.Callback(context).GetAwaiter().GetResult();
        }

        return result;
    }

    [Test]
    [Description("SC: AddInertiaViteApp adds a Vite app resource with an HTTP endpoint.")]
    public void AddInertiaViteApp_AddsViteResourceWithEndpoint()
    {
        var builder = CreateBuilder();

        var vite = builder.AddInertiaViteApp("vite", "./frontend");

        Assert.That(vite.Resource, Is.InstanceOf<ViteAppResource>());
        Assert.That(vite.Resource.Annotations.OfType<EndpointAnnotation>().Any(e => e.Name == "http"), Is.True);
    }

    [Test]
    [Description("SC: AddInertiaViteApp forwards the dev script name to the run script annotation.")]
    public void AddInertiaViteApp_ForwardsRunScriptName()
    {
        var builder = CreateBuilder();

        var vite = builder.AddInertiaViteApp("vite", "./frontend", "start");

        var runScript = vite.Resource.Annotations.OfType<JavaScriptRunScriptAnnotation>().Single();
        Assert.That(runScript.ScriptName, Is.EqualTo("start"));
    }

    [Test]
    [Description("SC: AddInertiaViteApp defaults the run script to dev.")]
    public void AddInertiaViteApp_DefaultsRunScriptToDev()
    {
        var builder = CreateBuilder();

        var vite = builder.AddInertiaViteApp("vite", "./frontend");

        var runScript = vite.Resource.Annotations.OfType<JavaScriptRunScriptAnnotation>().Single();
        Assert.That(runScript.ScriptName, Is.EqualTo("dev"));
    }

    [Test]
    [Description("SC: AddInertiaSsrApp adds a Node app resource with an HTTP endpoint and a health check.")]
    public void AddInertiaSsrApp_AddsNodeResourceWithEndpointAndHealthCheck()
    {
        var builder = CreateBuilder();

        var ssr = builder.AddInertiaSsrApp("ssr", "./frontend");

        Assert.That(ssr.Resource, Is.InstanceOf<NodeAppResource>());
        Assert.That(ssr.Resource.Annotations.OfType<EndpointAnnotation>().Any(e => e.Name == "http"), Is.True);
        Assert.That(ssr.Resource.Annotations.OfType<HealthCheckAnnotation>().Any(), Is.True);
    }

    [Test]
    [Description("SC: AddInertiaSsrApp allocates a random port and injects it through PORT.")]
    public void AddInertiaSsrApp_InjectsPortEnvironmentVariable()
    {
        var builder = CreateBuilder();

        var ssr = builder.AddInertiaSsrApp("ssr", "./frontend");

        var environment = EvaluateEnvironment(ssr.Resource);
        Assert.That(environment.ContainsKey("PORT"), Is.True);
    }

    [Test]
    [Description("SC: AddInertiaSsrApp launches node with the given SSR script relative to the app directory.")]
    public void AddInertiaSsrApp_UsesGivenScriptPath()
    {
        var builder = CreateBuilder();

        var ssr = builder.AddInertiaSsrApp("ssr", "./frontend", "server/entry.mjs");

        var executable = ssr.Resource.Annotations.OfType<ExecutableAnnotation>().Single();
        Assert.That(executable.Command, Is.EqualTo("node"));
        Assert.That(executable.WorkingDirectory, Does.EndWith("frontend").Or.EndWith("frontend" + Path.DirectorySeparatorChar),
            "working directory should be the app directory");
    }

    [Test]
    [Description("SC: WithInertiaReferences makes the web project wait for both Node resources.")]
    public void WithInertiaReferences_WaitsForBothResources()
    {
        var builder = CreateBuilder();
        var vite = builder.AddInertiaViteApp("vite", "./frontend");
        var ssr = builder.AddInertiaSsrApp("ssr", "./frontend");
        var web = builder.AddProject("web", "fake.csproj", options => options.ExcludeLaunchProfile = true);

        web.WithInertiaReferences(vite, ssr);

        var waits = web.Resource.Annotations.OfType<WaitAnnotation>().Select(w => w.Resource.Name).ToList();
        Assert.That(waits, Does.Contain("vite"));
        Assert.That(waits, Does.Contain("ssr"));
    }

    [Test]
    [Description("SC: WithInertiaReferences references both resources for service discovery.")]
    public void WithInertiaReferences_ReferencesBothResources()
    {
        var builder = CreateBuilder();
        var vite = builder.AddInertiaViteApp("vite", "./frontend");
        var ssr = builder.AddInertiaSsrApp("ssr", "./frontend");
        var web = builder.AddProject("web", "fake.csproj", options => options.ExcludeLaunchProfile = true);

        web.WithInertiaReferences(vite, ssr);

        var references = web.Resource.Annotations.OfType<EndpointReferenceAnnotation>()
            .Select(a => a.Resource.Name).ToList();
        Assert.That(references, Does.Contain("vite"));
        Assert.That(references, Does.Contain("ssr"));
    }

    [Test]
    [Description("SC: WithInertiaReferences enables SSR through the Inertia configuration setting.")]
    public void WithInertiaReferences_EnablesSsr()
    {
        var builder = CreateBuilder();
        var vite = builder.AddInertiaViteApp("vite", "./frontend");
        var ssr = builder.AddInertiaSsrApp("ssr", "./frontend");
        var web = builder.AddProject("web", "fake.csproj", options => options.ExcludeLaunchProfile = true);

        web.WithInertiaReferences(vite, ssr);

        var environment = EvaluateEnvironment(web.Resource);
        Assert.That(environment[InertiaHostingExtensions.SsrEnabledSetting], Is.EqualTo("true"));
    }

    [Test]
    [Description("SC: WithInertiaReferences injects the Vite dev server URL from the Vite endpoint.")]
    public void WithInertiaReferences_InjectsViteHotUrl()
    {
        var builder = CreateBuilder();
        var vite = builder.AddInertiaViteApp("vite", "./frontend");
        var ssr = builder.AddInertiaSsrApp("ssr", "./frontend");
        var web = builder.AddProject("web", "fake.csproj", options => options.ExcludeLaunchProfile = true);

        web.WithInertiaReferences(vite, ssr);

        var environment = EvaluateEnvironment(web.Resource);
        Assert.That(environment[InertiaHostingExtensions.ViteHotUrlSetting], Is.InstanceOf<ReferenceExpression>());
    }

    [Test]
    [Description("SC: WithInertiaReferences injects the SSR render gateway URL from the SSR endpoint.")]
    public void WithInertiaReferences_InjectsSsrUrl()
    {
        var builder = CreateBuilder();
        var vite = builder.AddInertiaViteApp("vite", "./frontend");
        var ssr = builder.AddInertiaSsrApp("ssr", "./frontend");
        var web = builder.AddProject("web", "fake.csproj", options => options.ExcludeLaunchProfile = true);

        web.WithInertiaReferences(vite, ssr);

        var environment = EvaluateEnvironment(web.Resource);
        var ssrUrl = environment[InertiaHostingExtensions.SsrUrlSetting];
        Assert.That(ssrUrl, Is.InstanceOf<ReferenceExpression>());
        Assert.That(((ReferenceExpression)ssrUrl).Format, Does.EndWith("/render"));
    }
}
