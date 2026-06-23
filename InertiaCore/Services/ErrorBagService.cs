namespace InertiaCore.Services;

public interface IErrorBagService
{
    /// <summary>
    /// Gets or sets the name of the current error bag for this request.
    /// </summary>
    string CurrentBagName { get; set; }

    /// <summary>
    /// Adds errors for a specific bag.
    /// </summary>
    void AddErrors(string bagName, Dictionary<string, string[]> errors);

    /// <summary>
    /// Retrieves errors for a specific bag (or null if none).
    /// </summary>
    Dictionary<string, string[]>? GetErrors(string bagName);

    /// <summary>
    /// Clears errors for a specific bag.
    /// </summary>
    void ClearErrors(string bagName);
}

public class ErrorBagService : IErrorBagService
{
    private readonly Dictionary<string, Dictionary<string, string[]>> _errors = new();
    public string CurrentBagName { get; set; } = "default"; // fallback

    public void AddErrors(string bagName, Dictionary<string, string[]> errors)
    {
        if (errors == null || errors.Count == 0) return;
        _errors[bagName] = errors;
    }

    public Dictionary<string, string[]>? GetErrors(string bagName)
    {
        _errors.TryGetValue(bagName, out var errors);
        return errors;
    }

    public void ClearErrors(string bagName)
    {
        _errors.Remove(bagName);
    }
}