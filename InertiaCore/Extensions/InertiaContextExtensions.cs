using System;
using InertiaCore.Utils;
using Microsoft.AspNetCore.Http;

namespace InertiaCore.Extensions;

/// <summary>
/// Inertia helpers for <see cref="HttpContext"/>.
/// </summary>
public static class InertiaContextExtensions
{
    /// <summary>
    /// Whether the current request is an Inertia prefetch request (the client sent
    /// <c>Purpose: prefetch</c>). Prefetch requests should not trigger side effects
    /// such as consuming flash data.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns><see langword="true"/> when the request is a prefetch request.</returns>
    public static bool IsInertiaPrefetch(this HttpContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        return context.IsPrefetchRequest();
    }

    /// <summary>
    /// Marks the redirect target as preserving the current request's URL fragment.
    /// Call this before returning a redirect; the next request's page object will
    /// carry <c>preserveFragment: true</c>.
    /// </summary>
    /// <param name="context">The HTTP context of the redirecting request.</param>
    public static void PreserveInertiaFragment(this HttpContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        FragmentPreservation.Set(context);
    }
}
