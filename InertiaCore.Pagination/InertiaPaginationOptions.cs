using System;

namespace InertiaCore.Pagination
{
    /// <summary>
    /// Options for <c>ToInertiaPageAsync</c> ... configured via AddInertiaPagination.
    /// </summary>
    public sealed class InertiaPaginationOptions
    {
        public int DefaultPageSize { get; set; } = 15;
        public int MaxPageSize { get; set; } = 100;
        public string PageParameterName { get; set; } = "page";
        public bool RequireOrdering { get; set; } = true;
    }
}
