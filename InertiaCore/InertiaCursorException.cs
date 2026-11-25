namespace InertiaCore;

/// <summary>
/// Thrown when a cursor token cannot be read. <c>UseInertia()</c> turns this into the
/// standard Inertia validation response instead of an unhandled exception.
/// </summary>
public sealed class InertiaCursorException : Exception
{
    /// <summary>
    /// Creates an exception for a cursor that is missing, altered, or no longer readable.
    /// </summary>
    public InertiaCursorException()
        : base("The cursor token is invalid. It may have been tampered with or may have expired.")
    {
    }

    /// <summary>
    /// Creates an exception with an additional detail message.
    /// </summary>
    public InertiaCursorException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Creates an exception with an additional detail message and an inner exception.
    /// </summary>
    public InertiaCursorException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
