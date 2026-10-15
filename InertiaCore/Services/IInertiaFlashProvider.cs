namespace InertiaCore.Services;

/// <summary>
/// Persists flash data across requests for Inertia.
/// <para>
/// The default implementation stores flash in <c>TempData</c> (falling back to
/// request items when TempData is unavailable). Register a custom implementation
/// to integrate a different store (for example a session or distributed cache).
/// </para>
/// </summary>
public interface IInertiaFlashProvider
{
    /// <summary>
    /// Returns the flash data stored for the current request and clears it, so it is
    /// shown once. A prefetch request (<c>Purpose: prefetch</c>) returns an empty
    /// result and does <b>not</b> clear the data, so the eventual real visit still
    /// receives it.
    /// </summary>
    /// <returns>The flash data for the current request.</returns>
    IReadOnlyDictionary<string, object?> Pull();

    /// <summary>
    /// Stores a flash value to be shown on the next request.
    /// </summary>
    /// <param name="key">The flash key.</param>
    /// <param name="value">The flash value.</param>
    void Flash(string key, object? value);

    /// <summary>
    /// Keeps the current flash data available for one more request. Used on a
    /// version-mismatch <c>409</c> so flash and errors survive the follow-up reload.
    /// </summary>
    void Reflash();
}
