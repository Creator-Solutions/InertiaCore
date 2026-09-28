using System.Globalization;
using System.Linq;
using System.Text;

namespace InertiaCore.SourceGenerators;

internal static class IdentifierSanitizer
{
    public static string ToIdentifier(string component)
    {
        if (string.IsNullOrEmpty(component))
            return "_";

        var cleaned = new StringBuilder(component.Length);

        foreach (var c in component)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
                cleaned.Append(c);
            else
                cleaned.Append('_');
        }

        var segments = cleaned.ToString()
            .Split('_')
            .Where(s => s.Length > 0)
            .ToArray();

        if (segments.Length == 0)
            return "_";

        var result = new StringBuilder(component.Length);
        foreach (var segment in segments)
        {
            if (segment.Length == 0) continue;

            result.Append(char.ToUpper(segment[0], CultureInfo.InvariantCulture));

            if (segment.Length > 1)
                result.Append(segment.Substring(1));
        }

        var id = result.ToString();

        if (id.Length > 0 && char.IsDigit(id[0]))
            id = "@" + id;

        return id;
    }
}
