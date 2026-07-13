using System.Collections;
using System.Reflection;

namespace DtrCheck.Core.Fhir;

public static class FieldPathResolver
{
    /// <summary>
    /// Resolves a dotted path like "name.0.family" against a Firely FHIR POCO,
    /// case-insensitively (FHIR JSON uses lowerCamelCase, POCO properties are
    /// PascalCase). Numeric segments index into a list. Returns null on any
    /// missing/out-of-range segment, matching the Node resolveFieldPath contract.
    /// </summary>
    public static object? Resolve(object? root, string path)
    {
        object? current = root;
        foreach (var part in path.Split('.'))
        {
            if (current is null) return null;

            if (int.TryParse(part, out var index))
            {
                if (current is not IEnumerable enumerable || current is string) return null;
                var i = 0;
                object? found = null;
                var matched = false;
                foreach (var item in enumerable)
                {
                    if (i == index) { found = item; matched = true; break; }
                    i++;
                }
                if (!matched) return null;
                current = found;
                continue;
            }

            var property = current.GetType().GetProperty(
                part,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property is null) return null;
            current = property.GetValue(current);
        }
        return current;
    }
}
