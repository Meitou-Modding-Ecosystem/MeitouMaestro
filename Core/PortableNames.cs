namespace Maestro.Core;

internal static class PortableNames
{
    private static readonly HashSet<string> Devices = new(StringComparer.OrdinalIgnoreCase)
    {
        "AUX", "CON", "PRN", "NUL", "CONIN$", "CONOUT$"
    };

    public static void Check(string component)
    {
        var stem = component.Split('.')[0].TrimEnd(' ');
        bool port = stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
            || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) && "123456789¹²³".Contains(stem[3]);
        if (component.Length == 0 || component.Length > 255 || component.EndsWith('.') || component.EndsWith(' ')
            || component.IndexOfAny(['<', '>', ':', '"', '/', '\\', '|', '?', '*']) >= 0
            || component.Any(c => c < 32) || port || Devices.Contains(stem))
            throw new InvalidDataException($"'{component}' cannot be extracted as a Windows filename.");
    }

    public static string[] ProjectPaths(string root, IEnumerable<string>? pending = null)
    {
        Check(Path.GetFileName(Path.TrimEndingDirectorySeparator(root)));
        var paths = SafeFiles.Walk(root).Select(f => Path.GetRelativePath(root, f).Replace(Path.DirectorySeparatorChar, '/'))
            .Where(p => !p.StartsWith(".maestro-", StringComparison.OrdinalIgnoreCase)).ToArray();
        var spellings = new Dictionary<string, (string Path, bool IsFile)>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths.Concat(pending ?? []))
        {
            string prefix = "";
            var segments = path.Split('/');
            for (int i = 0; i < segments.Length; ++i)
            {
                Check(segments[i]);
                prefix = i == 0 ? segments[i] : prefix + "/" + segments[i];
                bool isFile = i == segments.Length - 1;
                if (spellings.TryGetValue(prefix, out var prior) && (prior.Path != prefix || prior.IsFile != isFile))
                    throw new InvalidDataException($"Windows extraction conflict between '{prior.Path}' and '{prefix}'.");
                spellings[prefix] = (prefix, isFile);
            }
        }
        return paths;
    }
}
