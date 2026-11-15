using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Maestro.Core;

public static class SafeFiles
{
    public static IEnumerable<string> Walk(string folder)
    {
        foreach (var path in Directory.EnumerateFileSystemEntries(folder))
        {
            var flags = File.GetAttributes(path);
            if ((flags & FileAttributes.ReparsePoint) != 0) continue;
            if ((flags & FileAttributes.Directory) == 0) { yield return path; continue; }
            if (Path.GetFileName(path) is ".git" or ".maestro-transaction") continue;
            foreach (var file in Walk(path)) yield return file;
        }
    }
    public static string Target(string root, string relative)
    {
        if (Path.IsPathRooted(relative) || relative.Contains('\\') || relative.Contains(':')) throw new InvalidDataException("Expected a project-relative path.");
        string full = root;
        foreach (var component in relative.Split('/'))
        {
            full = Path.Combine(full, Descriptions.SimpleName(component));
            if ((File.Exists(full) || Directory.Exists(full)) && (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException($"Cannot write through a filesystem link: {relative}");
        }
        return full;
    }
}

public sealed class ProjectFiles
{
    private sealed record Backup(string Path, bool Existed);
    private readonly string root;
    private readonly Dictionary<string, string> baseline;
    public Dictionary<string, byte[]> Writes { get; } = new(StringComparer.Ordinal);
    public void CheckNames() => PortableNames.ProjectPaths(root, Writes.Keys);
    public ProjectFiles(string root)
    {
        this.root = root;
        baseline = PortableNames.ProjectPaths(root).ToDictionary(p => p, p => Hash(Path.Combine(root, p)), StringComparer.Ordinal);
    }
    static string Hash(string file) { using var f = File.OpenRead(file); return Convert.ToHexString(SHA256.HashData(f)); }
    private static FileStream Lock(string root) => new(Path.Combine(root, ".maestro-lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
    static string Journal(string root) => Path.Combine(root, ".maestro-transaction");
    public static void Recover(string root) { using var guard = Lock(root); Restore(root); }
    private static void Restore(string root)
    {
        var directory = Journal(root);
        if (!Directory.Exists(directory)) return;
        if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Unexpected linked journal.");
        var manifest = Path.Combine(directory, "prepared.json");
        if (File.Exists(manifest) && !File.Exists(Path.Combine(directory, "committed")))
        {
            var items = JsonSerializer.Deserialize<Backup[]>(File.ReadAllBytes(manifest))!;
            foreach (var item in items.Reverse())
            {
                var dest = SafeFiles.Target(root, item.Path);
                if (item.Existed) File.Copy(Path.Combine(directory, "before", item.Path), dest, true);
                else if (File.Exists(dest)) File.Delete(dest);
            }
        }
        Directory.Delete(directory, true);
    }
    private void Verify()
    {
        CheckNames();
        foreach (string name in Writes.Keys)
        {
            var path = SafeFiles.Target(root, name);
            if ((File.Exists(path) ? Hash(path) : null) != baseline.GetValueOrDefault(name))
                throw new IOException($"{name} changed outside Maestro. Reopen the project before saving.");
        }
    }
    public void Commit()
    {
        using var guard = Lock(root);
        Restore(root);
        Verify();
        var dir = Directory.CreateDirectory(Journal(root)).FullName;
        bool prepared = false;
        try
        {
            var items = new List<Backup>();
            foreach (var (name, bytes) in Writes)
            {
                var dest = SafeFiles.Target(root, name);
                var before = Path.Combine(dir, "before", name);
                var after = Path.Combine(dir, "after", name);
                Directory.CreateDirectory(Path.GetDirectoryName(after)!);
                File.WriteAllBytes(after, bytes);
                if (File.Exists(dest))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(before)!);
                    File.Copy(dest, before);
                }
                items.Add(new(name, File.Exists(dest)));
            }
            File.WriteAllBytes(Path.Combine(dir, "preparing"), JsonSerializer.SerializeToUtf8Bytes(items));
            Verify();
            File.Move(Path.Combine(dir, "preparing"), Path.Combine(dir, "prepared.json"));
            prepared = true;
            foreach (var item in items)
            {
                var dest = SafeFiles.Target(root, item.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Move(Path.Combine(dir, "after", item.Path), dest, true);
            }
            File.WriteAllText(Path.Combine(dir, "committed"), "");
        }
        finally
        {
            if (prepared) Restore(root);
            else if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
        Writes.Clear();
    }
    private static void CheckAssetReferences(string root, IReadOnlyCollection<string> paths, IEnumerable<Tool> tools)
    {
        var exact = paths.ToHashSet(StringComparer.Ordinal);
        foreach (var tool in tools)
        {
            var assets = tool.Action.Edits.Where(e => e.Str("op") == "asset").Select(e => e.Str("input")).ToHashSet();
            foreach (var edit in tool.Action.Edits.Where(e => e.Str("op") == "jsonEntry"))
            {
                var file = SafeFiles.Target(root, edit.Str("file"));
                if (!File.Exists(file) || assets.Count == 0) continue;
                var fields = edit["fields"]!.AsObject().Where(f => assets.Contains((string)f.Value!)).Select(f => f.Key).ToArray();
                if (JsonNode.Parse(File.ReadAllBytes(file))?[edit.Str("array")] is not JsonArray entries) continue;
                foreach (var entry in entries.OfType<JsonObject>())
                    foreach (var field in fields)
                        if (entry[field]?.GetValue<string>() is { Length: > 0 } path && !exact.Contains(path))
                            throw new InvalidDataException($"{Path.GetFileName(file)} references '{path}' in '{field}'. Check the asset and its exact filename.");
            }
        }
    }

    public static void Export(string root, string zip, IEnumerable<Tool>? tools = null)
    {
        var name = Path.GetFileName(root);
        zip = Path.GetFullPath(zip);
        if (Path.GetRelativePath(root, zip).Split(Path.DirectorySeparatorChar)[0] != ".." && !Path.IsPathRooted(Path.GetRelativePath(root, zip)))
            throw new InvalidDataException("Export outside the mod folder.");
        using var guard = Lock(root);
        Restore(root);
        if (!File.Exists(Path.Combine(root, name + ".mod"))) throw new InvalidDataException("Save the mod first.");
        var paths = PortableNames.ProjectPaths(root);
        if (tools != null) CheckAssetReferences(root, paths, tools);
        var tmp = zip + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var archive = ZipFile.Open(tmp, ZipArchiveMode.Create))
                foreach (var rel in paths.Order(StringComparer.Ordinal))
                {
                    archive.CreateEntryFromFile(Path.Combine(root, rel), name + "/" + rel, CompressionLevel.Optimal);
                }
            File.Move(tmp, zip, true);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }
}
