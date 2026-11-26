using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using InertiaCore;
using InertiaCore.Extensions;
using InertiaCore.Models;
using InertiaCore.Services;
using InertiaCore.Ssr;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;

namespace InertiaCore.Benchmarks;

public class Program
{
    public static void Main(string[] args) => BenchmarkRunner.Run<RenderBenchmarks>();
}

public class RenderBenchmarks
{
    private IResponseFactory _factory = null!;
    private object _small = null!;
    private object _medium = null!;
    private object _large = null!;

    [GlobalSetup]
    public void Setup()
    {
        var contextAccessor = new Mock<IHttpContextAccessor>();
        var gateway = new Gateway(new InertiaSsrClient(new HttpClient()), null, null, null);
        var options = new Mock<IOptions<InertiaOptions>>();
        options.SetupGet(x => x.Value).Returns(new InertiaOptions());
        var state = new InertiaState();
        var jsonOptions = new Mock<IOptions<JsonOptions>>();
        jsonOptions.SetupGet(x => x.Value).Returns(new JsonOptions());

        _factory = new ResponseFactory(
            contextAccessor.Object,
            gateway,
            options.Object,
            state,
            jsonOptions.Object);

        _small = new { Title = "Hello", Count = 42 };
        _medium = Enumerable.Range(0, 100).Select(i => new { Id = i, Name = $"Item {i}" }).ToArray();
        _large = Enumerable.Range(0, 1000).Select(i => new { Id = i, Name = $"Item {i}", Tags = new[] { "a", "b" } }).ToArray();
    }

    [Benchmark]
    public object Small() => _factory.Render("Page", _small);

    [Benchmark]
    public object Medium() => _factory.Render("Page", new { Items = _medium });

    [Benchmark]
    public object Large() => _factory.Render("Page", new { Items = _large });
}