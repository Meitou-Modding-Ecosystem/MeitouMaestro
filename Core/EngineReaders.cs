using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;

namespace Maestro.Core;

public static class EngineReaders
{
    private static string? selected;

    public static void Select(string directory)
    {
        directory = Path.GetFullPath(directory);
        foreach (var name in new[] { "Meitou.Core", "Meitou.Data" })
        {
            var file = Path.Combine(directory, name + ".dll");
            if (!File.Exists(file)) throw new FileNotFoundException($"Choose the extracted MeitouClient v0.20.1 game folder containing {name}.dll.");
            string? version = FileVersionInfo.GetVersionInfo(file).ProductVersion;
            if (version?.Split('+')[0] != "0.20.1")
                throw new InvalidDataException($"Expected v0.20.1 of {name}. Found {version ?? "unknown version"}.");
        }
        if (selected == null) AssemblyLoadContext.Default.Resolving += Resolve;
        else if (selected != directory)
            throw new InvalidOperationException("Restart Maestro to switch the game reader directory.");
        selected = directory;
    }

    private static Assembly? Resolve(AssemblyLoadContext context, AssemblyName name)
    {
        if (selected == null || name.Name is not ("Meitou.Core" or "Meitou.Data" or "NVorbis" or "StbImageSharp")) return null;
        string file = Path.Combine(selected, name.Name + ".dll");
        return File.Exists(file) ? context.LoadFromAssemblyPath(file) : null;
    }
}
