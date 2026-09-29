namespace InertiaCore.Props;

/// <summary>
/// Internal marker implemented by props whose resolution failures should be rescued:
/// the exception is logged, the prop is omitted, and its key is reported through the
/// page object's <c>rescuedProps</c> array instead of failing the whole request.
/// See: https://inertiajs.com/deferred-props#error-handling
/// </summary>
internal interface IRescuable
{
    /// <summary>Whether resolution errors should be rescued for this prop.</summary>
    bool ShouldRescue { get; }
}
