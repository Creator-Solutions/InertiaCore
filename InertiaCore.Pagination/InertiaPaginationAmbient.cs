using Microsoft.AspNetCore.Http;

namespace InertiaCore.Pagination
{
    /// <summary>
    /// Ambient current-request accessor used by the parameterless ToInertiaPageAsync overloads.
    /// Wired by UseInertiaPagination(); unit tests can configure it directly via the internal entry point.
    /// </summary>
    internal static class InertiaPaginationAmbient
    {
        private static IHttpContextAccessor? _accessor;
        private static InertiaPaginationOptions? _options;

        internal static void Configure(IHttpContextAccessor? accessor, InertiaPaginationOptions? options)
        {
            _accessor = accessor;
            _options = options;
        }

        internal static (HttpRequest? Request, InertiaPaginationOptions Options) Resolve(HttpRequest? explicitRequest)
        {
            var request = explicitRequest ?? _accessor?.HttpContext?.Request;
            return (request, _options ??= new InertiaPaginationOptions());
        }
    }
}
