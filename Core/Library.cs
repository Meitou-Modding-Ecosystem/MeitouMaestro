using System.Numerics;
using Meitou.Content;
using Meitou.Data;
using Meitou.Data.Fcs;
using Meitou.Data.Characters;
using Meitou.Data.Ogre;

namespace Maestro.Core;

public sealed record RecordItem(string Id, string Name, string Type, string Source)
{
    public override string ToString() => $"{Name}   {Id}";
}
public sealed record Surface(Vector3[] Positions, int[] Indices);
public sealed record Model(Surface[] Surfaces, IReadOnlyDictionary<string, Vector3> Bones);

public sealed class Library
{
    public GameInstall Install { get; }
    public GameDatabase Data { get; }
    public RecordItem[] Records { get; }
    public List<Tool> Actions { get; } = [];
    public List<string> Diagnostics { get; } = [];
    public string Project { get; }
    public string ModName { get; }
    public string FcsPath => Path.Combine(Project, ModName);
    public Dictionary<string, string> ModPaths { get; } = new(StringComparer.OrdinalIgnoreCase);
    private readonly CharacterRigs rigs;
    private readonly Dictionary<string, Model> models = [];
    private readonly Dictionary<string, string> projectArt = new(StringComparer.OrdinalIgnoreCase);

    public Library(string install, string project)
    {
        Install = GameInstall.Open(install);
        Project = Path.TrimEndingDirectorySeparator(Path.GetFullPath(project));
        if (!Directory.Exists(Project)) throw new DirectoryNotFoundException(Project);
        ModName = Descriptions.SimpleName(Path.GetFileName(Project)) + ".mod";
        if (Directory.EnumerateFiles(Project, "*.mod").Any(p => !p.Equals(FcsPath, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Choose a mod folder with a matching .mod filename.");
        ProjectFiles.Recover(Project);
        if (Environment.GetEnvironmentVariable("MEITOU_MODS_CFG") is { Length: > 0 } cfg) ModList.UseModsCfg(cfg);
        else ModList.ClearModsCfg();
        var order = LoadOrder.FromInstall(Install);
        Diagnostics.AddRange(order.Issues.Select(i => i.ToString()));
        var available = ModList.FindMods(Install);
        var current = File.Exists(FcsPath) ? FcsReader.ReadFile(FcsPath) : new FcsFile();
        // Include the opened project's dependency chain even if disabled in the player's profile.
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var completed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Include(string name)
        {
            if (name.Equals(ModName, StringComparison.OrdinalIgnoreCase) || !visiting.Add(name))
                throw new InvalidDataException($"Cyclic dependency on {name}.");
            if (!completed.Contains(name) && !order.Entries.Any(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                if (!available.TryGetValue(Path.GetFileNameWithoutExtension(name), out var found))
                    throw new InvalidDataException($"Missing project dependency {name}.");
                foreach (var dep in FcsReader.ReadFile(found).Dependencies) Include(dep);
                order.Entries.Add(new LoadOrderEntry(name, found));
            }
            visiting.Remove(name); completed.Add(name);
        }
        foreach (var dep in current.Dependencies) Include(dep);
        order.Entries.RemoveAll(e => e.Name.Equals(ModName, StringComparison.OrdinalIgnoreCase));
        if (File.Exists(FcsPath)) order.Entries.Add(new LoadOrderEntry(ModName, FcsPath));
        Data = GameDatabase.Load(order);
        foreach (var e in order.Entries) ModPaths[e.Name] = e.Path;
        foreach (var file in available.Values.Append(FcsPath).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var path = Path.Combine(Path.GetDirectoryName(file)!, "maestro.json");
            if (!File.Exists(path)) continue;
            try
            {
                var provider = Descriptions.Read(path);
                if (Actions.Any(a => a.ProviderId == provider.ProviderId)) throw new InvalidDataException($"Duplicate provider {provider.ProviderId}.");
                Actions.AddRange(provider.Actions.Select(a => new Tool(provider.ProviderId, provider.Name, Path.GetFileName(file), a)));
                ModPaths.TryAdd(Path.GetFileName(file), file);
            }
            catch (Exception ex) { Diagnostics.Add($"{path}: {ex.Message}"); }
        }
        Records = Data.Records.Values.Select(r => new RecordItem(r.StringId, r.Name, r.Type.ToString(), r.DefinedBy))
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        var assets = new AssetLocator(Install);
        foreach (var e in order.Entries.Where(e => !LoadOrder.BaseFiles.Contains(e.Name)))
            foreach (var file in SafeFiles.Walk(Path.GetDirectoryName(e.Path)!))
                if (Path.GetExtension(file).Equals(".mesh", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(file).Equals(".skeleton", StringComparison.OrdinalIgnoreCase))
                    projectArt[Path.GetFileName(file)] = file;
        string? Art(string path) => projectArt.GetValueOrDefault(Path.GetFileName(path.Replace('\\', '/')));
        rigs = new CharacterRigs(Data, p => Art(p) ?? assets.FindMesh(p), p => Art(p) ?? assets.Find(p));
    }
    public Model Animal(string id)
    {
        if (models.TryGetValue(id, out var v)) return v;
        var animal = Data.Find(id) ?? throw new InvalidDataException($"Unknown animal {id}.");
        var look = CharacterAppearance.BuildAnimal(Data, Install.Root, animal);
        var body = rigs.FindMesh(look.BodyMesh) ?? throw new FileNotFoundException(look.BodyMesh);
        var rig = rigs.Rig(rigs.BodySkeletonName(body) ?? throw new InvalidDataException("Body mesh has no skeleton.")) ?? throw new InvalidDataException("Skeleton unavailable.");
        return models[id] = new Model(Geometry(body), rig.Order.ToDictionary(h => rig.ByHandle[h]!.Name, h => rig.BindPosition[h]));
    }
    public static Surface[] Geometry(string path)
    {
        var mesh = OgreMeshReader.ReadFile(path);
        return mesh.SubMeshes.Select(s =>
        {
            var points = (s.UseSharedVertices ? mesh.SharedVertexData : s.VertexData)?.ReadPositions() ?? throw new InvalidDataException("Mesh has no positions.");
            if (s.Operation != OgreOperationType.TriangleList) throw new InvalidDataException("Preview supports triangle-list meshes.");
            return new Surface(points, Array.ConvertAll(s.Indices.Indices, i => checked((int)i)));
        }).ToArray();
    }
}
