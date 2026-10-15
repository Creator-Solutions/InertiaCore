namespace InertiaCore.Utils;

public static class InertiaHeader
{
    public const string Inertia = "X-Inertia";

    public const string ErrorBag = "X-Inertia-Error-Bag";

    public const string Location = "X-Inertia-Location";

    public const string Version = "X-Inertia-Version";

    public const string PartialComponent = "X-Inertia-Partial-Component";

    public const string PartialOnly = "X-Inertia-Partial-Data";

    public const string PartialExcept = "X-Inertia-Partial-Except";

    public const string Reset = "X-Inertia-Reset";

    public const string InfiniteScrollMergeIntent = "X-Inertia-Infinite-Scroll-Merge-Intent";

    public const string ExceptOnceProps = "X-Inertia-Except-Once-Props";

    /// <summary>Request/response header indicating a Precognition validation request.</summary>
    public const string Precognition = "Precognition";

    /// <summary>Request header listing the comma-separated fields to validate.</summary>
    public const string PrecognitionValidateOnly = "Precognition-Validate-Only";

    /// <summary>Response header set to <c>true</c> when precognitive validation passes.</summary>
    public const string PrecognitionSuccess = "Precognition-Success";
}
