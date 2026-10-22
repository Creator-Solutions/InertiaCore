using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using InertiaCore.Extensions;
using InertiaCore.Pagination;
using NUnit.Framework;

namespace InertiaCoreTests.Pagination
{
    [TestFixture]
    public class UnitTestPaginationExtensions
    {
        [Test]
        public void ClampPage_ClampToMaxPage_ReturnsClampedPage()
        {
            var items = Enumerable.Range(1, 30).Select(x => new TestItem(x));
            var query = items.AsQueryable();

            var result = query.ToInertiaPageAsync(5, 10).Result;

            Assert.That(result.Meta.CurrentPage, Is.EqualTo(3)); // Page size is clamped to 10
            Assert.That(result.Meta.PerPage, Is.EqualTo(10));   // Max page size is 10
            Assert.That(result.Meta.Total, Is.EqualTo(30));     // Total items is 30
            Assert.That(result.Meta.LastPage, Is.EqualTo(3));   // Last page is 3
            Assert.That(result.Data, Has.Count.EqualTo(10));    // 10 items on page 3
        }

        [Test]
        public void PageSize_ClampToDefaultPageSize_ReturnsDefaultPageSize()
        {
            var items = Enumerable.Range(1, 30).Select(x => new TestItem(x));
            var query = items.AsQueryable();

            var result = query.ToInertiaPageAsync(1, 0).Result; // 0 page size uses default (15)

            Assert.That(result.Meta.PerPage, Is.EqualTo(15));   // Default page size is 15
            Assert.That(result.Meta.Total, Is.EqualTo(30));     // Total items is 30
            Assert.That(result.Meta.LastPage, Is.EqualTo(2));   // Last page is 2
            Assert.That(result.Data, Has.Count.EqualTo(15));    // 15 items on page 1
        }

        [Test]
        public void PageSize_ClampToMinPageSize_ReturnsMinPageSize()
        {
            var items = Enumerable.Range(1, 30).Select(x => new TestItem(x));
            var query = items.AsQueryable();

            var result = query.ToInertiaPageAsync(1, 5).Result;

            Assert.That(result.Meta.PerPage, Is.EqualTo(5));    // Page size is clamped to 5
            Assert.That(result.Meta.Total, Is.EqualTo(30));     // Total items is 30
            Assert.That(result.Meta.LastPage, Is.EqualTo(6));   // Last page is 6
            Assert.That(result.Data, Has.Count.EqualTo(5));     // 5 items on page 1
        }

        [Test]
        public void PageNumber_ClampToMaxPageNumber_ReturnsClampedPageNumber()
        {
            var items = Enumerable.Range(1, 30).Select(x => new TestItem(x));
            var query = items.AsQueryable();

            var result = query.ToInertiaPageAsync(5, 10).Result; // There are only 3 pages

            Assert.That(result.Meta.CurrentPage, Is.EqualTo(3)); // Page number is clamped to max page (3)
            Assert.That(result.Meta.PerPage, Is.EqualTo(10));   // Page size is 10
            Assert.That(result.Meta.Total, Is.EqualTo(30));     // Total items is 30
            Assert.That(result.Meta.LastPage, Is.EqualTo(3));   // Last page is 3
            Assert.That(result.Data, Has.Count.EqualTo(10));    // 10 items on page 3
        }

        [Test]
        public void PageNumber_ClampToMinPageNumber_ReturnsMinPageNumber()
        {
            var items = Enumerable.Range(1, 30).Select(x => new TestItem(x));
            var query = items.AsQueryable();

            var result = query.ToInertiaPageAsync(-5, 10).Result; // Page number is clamped to min page (1)

            Assert.That(result.Meta.CurrentPage, Is.EqualTo(1)); // Page number is clamped to min page (1)
            Assert.That(result.Meta.PerPage, Is.EqualTo(10));   // Page size is 10
            Assert.That(result.Meta.Total, Is.EqualTo(30));     // Total items is 30
            Assert.That(result.Meta.LastPage, Is.EqualTo(3));   // Last page is 3
            Assert.That(result.Data, Has.Count.EqualTo(10));    // 10 items on page 1
        }

        [Test]
        public void OrderingWarning_ThrowsWhenRequireOrdering_IsTrue_AndQueryIsNotOrdered()
        {
            var items = new List<TestItem>
            {
                new TestItem(1),
                new TestItem(2),
                new TestItem(3)
            };
            var query = items.AsQueryable(); // Not ordered

            var ex = Assert.Throws<InvalidOperationException>(() => query.ToInertiaPageAsync(1, 10).Result);
            Assert.That(ex.Message, Does.Contain("The query is not ordered."));
        }

        [Test]
        public void NoOrderingWarning_WhenRequireOrdering_IsFalse_AndQueryIsNotOrdered()
        {
            var items = new List<TestItem>
            {
                new TestItem(1),
                new TestItem(2),
                new TestItem(3)
            };
            var query = items.AsQueryable(); // Not ordered

            // Configure options to not require ordering
            var options = new InertiaPaginationOptions { RequireOrdering = false };

            var result = query.ToInertiaPageAsync(1, 10).Result;

            Assert.That(result.Meta.CurrentPage, Is.EqualTo(1)); // Page number is 1
            Assert.That(result.Meta.PerPage, Is.EqualTo(10));   // Page size is 10
            Assert.That(result.Meta.Total, Is.EqualTo(3));     // Total items is 3
            Assert.That(result.Meta.LastPage, Is.EqualTo(1));   // Last page is 1
            Assert.That(result.Data, Has.Count.EqualTo(3));     // All 3 items on page 1
        }

        [Test]
        public void BuildLink_GeneratesCorrectUrl_WhenRequestIsNull()
        {
            var options = new InertiaPaginationOptions { PageParameterName = "page" };
            var path = "/items";

            var link = InertiaCore.Extensions.InertiaPaginationExtensions.BuildLink(null, options, path, 2);

            Assert.That(link, Is.EqualTo("/items?page=2"));
        }

        [Test]
        public void BuildLink_GeneratesCorrectUrl_WhenRequestHasQueryParameters()
        {
            var options = new InertiaPaginationOptions { PageParameterName = "page" };
            var path = "/items";
            var request = new DefaultHttpContext().Request;
            request.QueryString = new QueryString("?filter=abc&sort=desc");

            var link = InertiaCore.Extensions.InertiaPaginationExtensions.BuildLink(request, options, path, 2);

            Assert.That(link, Is.EqualTo("/items?filter=abc&sort=desc&page=2"));
        }

        [Test]
        public void BuildLink_GeneratesCorrectUrl_WhenPageNumberIsOne()
        {
            var options = new InertiaPaginationOptions { PageParameterName = "page" };
            var path = "/items";
            var request = new DefaultHttpContext().Request;
            request.QueryString = new QueryString("?filter=abc&sort=desc");

            var link = InertiaCore.Extensions.InertiaPaginationExtensions.BuildLink(request, options, path, 1);

            Assert.That(link, Is.EqualTo("/items?filter=abc&sort=desc")); // No page param for page 1
        }

        [Test]
        public void BuildPath_GeneratesCorrectPath_WhenRequestHasNoPath()
        {
            var options = new InertiaPaginationOptions();
            var path = InertiaCore.Extensions.InertiaPaginationExtensions.BuildPath(null, options);

            Assert.That(path, Is.EqualTo("/"));
        }

        [Test]
        public void BuildPath_GeneratesCorrectPath_WhenRequestHasSingleSegmentPath()
        {
            var options = new InertiaPaginationOptions();
            var request = new DefaultHttpContext().Request;
            request.Path = new PathString("/api");
            var path = InertiaCore.Extensions.InertiaPaginationExtensions.BuildPath(request, options);

            Assert.That(path, Is.EqualTo("/api"));
        }

        [Test]
        public void BuildPath_GeneratesCorrectPath_WhenRequestHasMultipleSegmentPath()
        {
            var options = new InertiaPaginationOptions();
            var request = new DefaultHttpContext().Request;
            request.Path = new PathString("/api/v1/items");
            var path = InertiaCore.Extensions.InertiaPaginationExtensions.BuildPath(request, options);

            Assert.That(path, Is.EqualTo("/api/v1/items"));
        }

        [Test]
        public void BuildPath_GeneratesCorrectPath_WhenRequestHasMultipleSegmentsWithSameName()
        {
            var options = new InertiaPaginationOptions();
            var request = new DefaultHttpContext().Request;
            request.Path = new PathString("/api/v1/api/v1/items");
            var path = InertiaCore.Extensions.InertiaPaginationExtensions.BuildPath(request, options);

            Assert.That(path, Is.EqualTo("/api/v1/api/v1/items")); // Duplicates preserved
        }
    }

    public sealed class TestItem
    {
        public int Id { get; set; }
        public TestItem(int id) => Id = id;
    }
}

