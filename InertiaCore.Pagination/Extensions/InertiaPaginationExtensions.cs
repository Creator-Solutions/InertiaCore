using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using InertiaCore.Models;
using InertiaCore.Pagination;

namespace InertiaCore.Extensions
{
    /// <summary>
    /// Extension methods for adding Inertia pagination services and middleware.
    /// </summary>
    public static class InertiaPaginationServiceCollectionExtensions
    {
        /// <summary>
        /// Adds Inertia pagination services to the DI container.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Optional configuration for pagination options.</param>
        public static IServiceCollection AddInertiaPagination(this IServiceCollection services, Action<InertiaPaginationOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            
            // Register IHttpContextAccessor for ambient request access
            services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            
            // Configure pagination options
            services.AddOptions<InertiaPaginationOptions>().Configure(configure);
            
            return services;
        }
    }

    /// <summary>
    /// Extension methods for adding Inertia pagination middleware.
    /// </summary>
    public static class InertiaPaginationApplicationBuilderExtensions
    {
        /// <summary>
        /// Adds middleware to set up ambient pagination context for the request.
        /// </summary>
        /// <param name="app">The application builder.</param>
        public static IApplicationBuilder UseInertiaPagination(this IApplicationBuilder app)
        {
            ArgumentNullException.ThrowIfNull(app);
            
            var accessor = app.ApplicationServices.GetRequiredService<IHttpContextAccessor>();
            var options = app.ApplicationServices.GetRequiredService<IOptions<InertiaPaginationOptions>>().Value;
            
            InertiaPaginationAmbient.Configure(accessor, options);
            
            return app;
        }
    }
}

namespace InertiaCore.Pagination
{
    internal static class InertiaPaginationExtensions
    {
        /// <summary>
        /// Paginate an <see cref="IQueryable{T}"/> to an <see cref="InertiaPagedResult{T}"/>
        /// using the provided page number and page size.
        /// </summary>
        /// <typeparam name="T">The type of elements in the query.</typeparam>
        /// <param name="query">The query to paginate.</param>
        /// <param name="page">The page number (1‑based). Values ≤ 0 are clamped to 1.</param>
        /// <param name="pageSize">The number of items per page. 0 means use <see cref="InertiaPaginationOptions.DefaultPageSize"/>.</param>
        /// <returns>A page result with data, links, and metadata.</returns>
        public static async Task<InertiaPagedResult<T>> ToInertiaPageAsync<T>(this IQueryable<T> query, int page = 1, int pageSize = 0, CancellationToken cancellationToken = default)
            => await ToInertiaPageAsync(query, null, null, page, pageSize, cancellationToken).ConfigureAwait(false);

        /// <summary>
        /// Paginate an <see cref="IQueryable{T}"/> to an <see cref="InertiaPagedResult{T}"/>
        /// using the provided page number and page size, with an optional <see cref="HttpRequest"/> for link generation.
        /// </summary>
        /// <typeparam name="T">The type of elements in the query.</typeparam>
        /// <param name="query">The query to paginate.</param>
        /// <param name="request">The HTTP request for generating links. <see langword="null"/> uses ambient context.</param>
        /// <param name="page">The page number (1‑based). Values ≤ 0 are clamped to 1.</param>
        /// <param name="pageSize">The number of items per page. 0 means use <see cref="InertiaPaginationOptions.DefaultPageSize"/>.</param>
        public static async Task<InertiaPagedResult<T>> ToInertiaPageAsync<T>(this IQueryable<T> query, HttpRequest? request, int page = 1, int pageSize = 0, CancellationToken cancellationToken = default)
            => await ToInertiaPageAsync(query, request, null, page, pageSize, cancellationToken).ConfigureAwait(false);

        /// <summary>
        /// Paginate an <see cref="IQueryable{T}"/> to an <see cref="InertiaPagedResult{TProjection}"/>
        /// using a projection expression and the provided page number and page size.
        /// </summary>
        /// <typeparam name="T">The type of elements in the query.</typeparam>
        /// <typeparam name="TProjection">The projected type.</typeparam>
        /// <param name="query">The query to paginate.</param>
        /// <param name="selector">Projection expression for SQL-side mapping.</param>
        /// <param name="page">The page number (1‑based). Values ≤ 0 are clamped to 1.</param>
        /// <param name="pageSize">The number of items per page. 0 means use <see cref="InertiaPaginationOptions.DefaultPageSize"/>.</param>
        public static async Task<InertiaPagedResult<TProjection>> ToInertiaPageAsync<T, TProjection>(this IQueryable<T> query, Expression<Func<T, TProjection>> selector, int page = 1, int pageSize = 0, CancellationToken cancellationToken = default)
            => await ToInertiaPageAsync(query, null, selector, page, pageSize, cancellationToken).ConfigureAwait(false);

        /// <summary>
        /// Paginate an <see cref="IQueryable{T}"/> to an <see cref="InertiaPagedResult{TProjection}"/>
        /// using a projection expression and the provided page number and page size, with an optional <see cref="HttpRequest"/> for link generation.
        /// </summary>
        /// <typeparam name="T">The type of elements in the query.</typeparam>
        /// <typeparam name="TProjection">The projected type.</typeparam>
        /// <param name="query">The query to paginate.</param>
        /// <param name="request">The HTTP request for generating links. <see langword="null"/> uses ambient context.</param>
        /// <param name="selector">Projection expression for SQL-side mapping.</param>
        /// <param name="page">The page number (1‑based). Values ≤ 0 are clamped to 1.</param>
        /// <param name="pageSize">The number of items per page. 0 means use <see cref="InertiaPaginationOptions.DefaultPageSize"/>.</param>
        public static async Task<InertiaPagedResult<TProjection>> ToInertiaPageAsync<T, TProjection>(this IQueryable<T> query, HttpRequest? request, Expression<Func<T, TProjection>> selector, int page = 1, int pageSize = 0, CancellationToken cancellationToken = default)
            => await ToInertiaPageAsync(query, request, selector, page, pageSize, cancellationToken).ConfigureAwait(false);

        /// <summary>
        /// Internal implementation shared by all overloads.
        /// </summary>
        private static async Task<InertiaPagedResult<TItem>> ToInertiaPageAsync<T, TItem>(this IQueryable<T> query, HttpRequest? request, Expression<Func<T, TItem>>? selector, int page, int pageSize, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var (resolvedRequest, options) = InertiaPaginationAmbient.Resolve(request);
            (int total, int perPage, int currentPage, int lastPage) = ResolvePaging(query, options, page, pageSize);

            // Compute data range
            (int from, int to) = total == 0
                ? (null, null)
                : (currentPage * perPage - perPage + 1, currentPage * perPage);

            // Fetch data
            List<TItem> items = total == 0
                ? new List<TItem>()
                : selector is not null
                    ? await query.Select(selector).Skip((currentPage - 1) * perPage).Take(perPage).ToListAsync(cancellationToken).ConfigureAwait(false)
                    : await ((IQueryable<TItem>)(object)query).Skip((currentPage - 1) * perPage).Take(perPage).ToListAsync(cancellationToken).ConfigureAwait(false);

            // Build result
            var result = new InertiaPagedResult<TItem>
            {
                Data = items,
                Links = new InertiaPaginationLinks
                {
                    First = lastPage > 0 && currentPage != 1 ? BuildLink(resolvedRequest, options, BuildPath(resolvedRequest, options), 1) : null,
                    Last = lastPage > 0 && currentPage != lastPage ? BuildLink(resolvedRequest, options, BuildPath(resolvedRequest, options), lastPage) : null,
                    Prev = currentPage > 1 ? BuildLink(resolvedRequest, options, BuildPath(resolvedRequest, options), currentPage - 1) : null,
                    Next = currentPage < lastPage ? BuildLink(resolvedRequest, options, BuildPath(resolvedRequest, options), currentPage + 1) : null,
                },
                Meta = new InertiaPaginationMeta
                {
                    CurrentPage = currentPage,
                    LastPage = lastPage,
                    PerPage = perPage,
                    Total = total,
                    From = from,
                    To = to,
                    Path = BuildPath(resolvedRequest, options)
                }
            };

            return result;
        }

        private static (int Total, int PerPage, int CurrentPage, int LastPage) ResolvePaging<T>(IQueryable<T> query, InertiaPaginationOptions options, int page, int pageSize)
        {
            var total = query.Count();
            var perPage = pageSize > 0 ? pageSize : options.DefaultPageSize;
            perPage = perPage < 1 ? 1 : perPage;
            if (options.MaxPageSize >= 1 && perPage > options.MaxPageSize) perPage = options.MaxPageSize;

            var lastPage = total == 0 ? 0 : (int)Math.Ceiling(total / (double)perPage);
            var currentPage = lastPage == 0 ? 0 : Math.Clamp(page < 1 ? 1 : page, 1, lastPage);

            return (total, perPage, currentPage, lastPage);
        }

        private static string BuildPath(HttpRequest? request, InertiaPaginationOptions options)
        {
            if (request is not { Path.HasValue: true }) return "/";

            var path = request.Path.Value;
            var query = request.Query;

            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var distinctSegments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var normalizedSegments = new List<string>();
            foreach (var segment in segments)
            {
                if (distinctSegments.Add(segment)) normalizedSegments.Add(segment);
            }

            var sb = new StringBuilder();
            if (normalizedSegments.Count > 0)
            {
                sb.Append('/');
                sb.Append(string.Join('/', normalizedSegments));
            }
            else
            {
                sb.Append('/');
            }

            return sb.ToString();
        }

        private static string BuildLink(HttpRequest? request, InertiaPaginationOptions options, string path, int pageNumber)
        {
            if (request is null)
                return pageNumber == 1 ? path : path + "?" + options.PageParameterName + "=" + pageNumber;

            var parameters = new List<KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues>>();

            foreach (var pair in request.Query)
            {
                if (!string.Equals(pair.Key, options.PageParameterName, StringComparison.OrdinalIgnoreCase))
                    parameters.Add(pair);
            }

            parameters.Add(new KeyValuePair<string, StringValues>(options.PageParameterName, pageNumber));
            var queryString = QueryString.Create(parameters);
            return path + queryString.Value;
        }

        private static bool IsOrdered<T>(IQueryable<T> query)
        {
            var expression = query.Expression;
            if (expression is null) return false;

            try
            {
                var visitor = new OrderByVisitor();
                visitor.Visit(expression);
                return visitor.FoundOrderBy;
            }
            catch
            {
                return false;
            }
        }

        private sealed class OrderByVisitor : ExpressionVisitor
        {
            public bool FoundOrderBy { get; private set; }

            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                if (node.Method.Name.Contains("OrderBy", StringComparison.OrdinalIgnoreCase))
                    FoundOrderBy = true;
                return base.VisitMethodCall(node);
            }
        }
    }
}
