using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using InertiaCore;
using InertiaCore.Contracts;
using InertiaCore.Pagination;
using InertiaCore.Props;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace InertiaCoreTests.Pagination
{
    [TestFixture]
    public class UnitTestCursorPagination
    {
        private class Item
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Group { get; set; }
        }

        private static IDataProtectionProvider CreateProtector()
        {
            var services = new ServiceCollection();
            services.AddDataProtection();
            var sp = services.BuildServiceProvider();
            return sp.GetRequiredService<IDataProtectionProvider>();
        }

        private class FakeInertia : IInertia
        {
            public ScrollProp? Last { get; private set; }
            public Task<IActionResult> Render(string component, object? props = null) => Task.FromResult<IActionResult>(new StatusCodeResult(200));
            public Task<IActionResult> Render<TProps>(TProps props) => Task.FromResult<IActionResult>(new StatusCodeResult(200));
            public IActionResult Location(string url) => new StatusCodeResult(409);
            public void Share(string key, object? value) { }
            public void Share(IDictionary<string, object?> value) { }
            public void Share<T>(T value) where T : class { }
            public void Flash(string key, object? value) { }
            public void Version(string? version) { }
            public void Version(Func<string?> versionFunc) { }
            public string? GetVersion() => null;
            public AlwaysProp Always(object? value) => new AlwaysProp(value);
            public AlwaysProp Always(Func<object?> callback) => new AlwaysProp(callback);
            public AlwaysProp Always(Func<Task<object?>> callback) => new AlwaysProp(callback);
            public LazyProp Lazy(Func<object?> callback) => new LazyProp(callback);
            public LazyProp Lazy(Func<Task<object?>> callback) => new LazyProp(callback);
            public DeferredProp Defer(Func<object?> factory) => new DeferredProp(factory);
            public DeferredProp Defer(Func<Task<object?>> factory) => new DeferredProp(factory);
            public DeferredProp Defer(Func<object?> factory, string group) => new DeferredProp(factory, group);
            public DeferredProp Defer(Func<object?> factory, string group, bool rescue) => new DeferredProp(factory, group, rescue);
            public DeferredProp Defer(Func<Task<object?>> factory, string group) => new DeferredProp(factory, group);
            public DeferredProp Defer(Func<Task<object?>> factory, string group, bool rescue) => new DeferredProp(factory, group, rescue);
            public OnceProp Once(Func<object?> callback) => new OnceProp(callback);
            public OnceProp Once(Func<Task<object?>> callback) => new OnceProp(callback);
            public MergeProp Merge(object? value) => new MergeProp(value);
            public MergeProp Merge(Func<object?> callback) => new MergeProp(callback);
            public MergeProp Merge(Func<Task<object?>> callback) => new MergeProp(callback);
            public MergeProp DeepMerge(object? value) => new MergeProp(value, true);
            public MergeProp DeepMerge(Func<object?> callback) => new MergeProp(callback, true);
            public MergeProp DeepMerge(Func<Task<object?>> callback) => new MergeProp(callback, true);
            public ScrollProp Scroll(object? items, ScrollMetadata metadata, string wrapper = "data") { Last = new ScrollProp(items, metadata, wrapper); return Last; }
            public ScrollProp Scroll(Func<object?> items, ScrollMetadata metadata, string wrapper = "data") { var v = items(); Last = new ScrollProp(v, metadata, wrapper); return Last; }
            public ScrollProp Scroll(Func<Task<object?>> items, ScrollMetadata metadata, string wrapper = "data") { var v = items().GetAwaiter().GetResult(); Last = new ScrollProp(v, metadata, wrapper); return Last; }
            public IInertiaProp<T> Prop<T>() where T : class => new ResolvedProp<T>();
            public LazyProp Lazy<T>() where T : class => new LazyProp(() => null);
            public DeferredProp Defer<T>(string group = "default") where T : class => new DeferredProp(() => null, group);
            public OnceProp Once<T>() where T : class => new OnceProp(() => null);
            public void EncryptHistory(bool encrypt = true) { }
            public void ClearHistory(bool clear = true) { }
        }

        [Test]
        public async Task CursorForwardTraversal_1000Rows_Size25_FirstPageAndNext()
        {
            var items = Enumerable.Range(1, 1000).Select(i => new Item { Id = i, Name = $"Item{i:D4}", Group = i % 10 }).ToList();
            var inertia = new FakeInertia();
            var dp = CreateProtector();

            await inertia.ToInertiaCursorPageAsync(items, new List<OrderBy> { new OrderBy(nameof(Item.Id), SortDirection.Ascending) }, null, 25, "cursor", "data", default, dp);
            var scroll = inertia.Last!;
            var list = ((IEnumerable<Item>)scroll.Invoke()!).ToList();
            Assert.That(list.Count, Is.EqualTo(25));
            Assert.That(list[0].Id, Is.EqualTo(1));
            Assert.That(list[24].Id, Is.EqualTo(25));
            var meta = scroll.Metadata;
            Assert.That(meta.CurrentPage, Is.EqualTo(1));
            Assert.That(meta.PreviousPage, Is.Null);
            Assert.That(meta.NextPage, Is.Not.Null);
        }

        [Test]
        public async Task CursorDescending_OrderByDesc()
        {
            var items = Enumerable.Range(1, 100).Select(i => new Item { Id = i, Name = $"Item{i}", Group = 1 }).ToList();
            var inertia = new FakeInertia();
            var dp = CreateProtector();

            await inertia.ToInertiaCursorPageAsync(items, new List<OrderBy> { new OrderBy(nameof(Item.Id), SortDirection.Descending) }, null, 10, "cursor", "data", default, dp);
            var list = ((IEnumerable<Item>)inertia.Last!.Invoke()!).ToList();
            Assert.That(list[0].Id, Is.EqualTo(100));
            Assert.That(list[9].Id, Is.EqualTo(91));
        }

        [Test]
        public void CursorWithDuplicates_RequiresTieBreaker()
        {
            var items = Enumerable.Range(1, 20).Select(i => new Item { Id = i, Name = "Same", Group = 1 }).ToList();
            var inertia = new FakeInertia();
            var dp = CreateProtector();

            var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await inertia.ToInertiaCursorPageAsync(items, new List<OrderBy> { new OrderBy(nameof(Item.Name), SortDirection.Ascending) }, null, 10, "cursor", "data", default, dp));
            Assert.That(ex!.Message, Does.Contain("duplicate"));
        }

        [Test]
        public async Task CursorWithTieBreaker_Works()
        {
            var items = Enumerable.Range(1, 20).Select(i => new Item { Id = i, Name = "Same", Group = 1 }).ToList();
            var inertia = new FakeInertia();
            var dp = CreateProtector();

            await inertia.ToInertiaCursorPageAsync(items, new List<OrderBy> { new OrderBy(nameof(Item.Name), SortDirection.Ascending) { TieBreakerProperty = nameof(Item.Id) } }, null, 10, "cursor", "data", default, dp);
            var list = ((IEnumerable<Item>)inertia.Last!.Invoke()!).ToList();
            Assert.That(list.Count, Is.EqualTo(10));
            Assert.That(list[0].Id, Is.EqualTo(1));
        }

        [Test]
        public void CursorTampered_ThrowsInertiaCursorException()
        {
            var items = Enumerable.Range(1, 10).Select(i => new Item { Id = i, Name = $"Item{i}", Group = 1 }).ToList();
            var inertia = new FakeInertia();
            var dp = CreateProtector();

            Assert.ThrowsAsync<InertiaCursorException>(async () =>
                await inertia.ToInertiaCursorPageAsync(items, new List<OrderBy> { new OrderBy(nameof(Item.Id), SortDirection.Ascending) }, "not-a-valid-token", 5, "cursor", "data", default, dp));
        }
    }
}
