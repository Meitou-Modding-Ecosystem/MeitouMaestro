using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Meitou.Data;
using Meitou.Data.Fcs;

namespace Maestro.Core;

public sealed class Editing
{
    private readonly Library lib;
    private readonly ProjectFiles plan;
    private readonly byte[]? openedFcs;
    private readonly Dictionary<string, byte[]?> openedJson = new(StringComparer.OrdinalIgnoreCase);
    public bool Ready { get; private set; }
    public Editing(Library library)
    {
        lib = library; plan = new(lib.Project);
        openedFcs = File.Exists(lib.FcsPath) ? File.ReadAllBytes(lib.FcsPath) : null;
        foreach (var edit in lib.Actions.SelectMany(t => t.Action.Edits).Where(e => e.Str("op") == "jsonEntry"))
        {
            var name = edit.Str("file");
            var path = SafeFiles.Target(lib.Project, name);
            openedJson[name] = File.Exists(path) ? File.ReadAllBytes(path) : null;
        }
    }
    JsonObject Document(string file) => openedJson.GetValueOrDefault(file) is { } bytes
        ? JsonNode.Parse(bytes)!.AsObject() : new();
    public JsonObject? Existing(Tool tool, string record)
    {
        var edit = tool.Action.Edits.FirstOrDefault(e => e.Str("op") == "jsonEntry");
        if (edit == null) return null;
        var doc = Document(edit.Str("file"));
        var field = (string?)edit["uniqueSelection"];
        return (doc[edit.Str("array")] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(o => field != null &&
            o[field] is JsonArray ids && ids.Any(i => (string?)i == record));
    }
    public string? Owner(JsonObject e) => (string?)Document(e.Str("file"))[e.Str("ownerField")];
    public IEnumerable<JsonObject> Entries(JsonObject e) =>
        (Document(e.Str("file"))[e.Str("array")] as JsonArray)?.OfType<JsonObject>() ?? [];
    public bool? ExistingReference(Tool tool, string id)
    {
        var e = tool.Action.Edits.FirstOrDefault(e => e.Str("op") == "reference");
        if (e != null && openedFcs != null)
        {
            var project = FcsReader.Read(new MemoryStream(openedFcs));
            var record = project.Records.FirstOrDefault(r => r.StringId == id);
            if (record != null && record.References.TryGetValue(e.Str("list"), out var refs))
            {
                var local = refs.FirstOrDefault(r => r.TargetStringId == e.Str("target"));
                if (local.TargetStringId != null) return local.Value0 != 0;
            }
        }
        return e != null && lib.Data.Find(id)?.GetReferences(e.Str("list")).FirstOrDefault(r => r.TargetStringId == e.Str("target")) is { } link && link.TargetStringId != null
            ? link.Values.Value0 != 0 : null;
    }
    public void Invalidate() { Ready = false; plan.Writes.Clear(); }
    public string Review(Tool tool, IReadOnlyList<RecordItem> selection, Dictionary<string, string> values)
    {
        Invalidate();
        if (selection.Count == 0 || selection.Any(r => r.Type != tool.Action.RecordType)) throw new InvalidDataException("Select matching records.");
        if (selection.Count > 1 && !tool.Action.Inputs.Any(i => i.Kind == "selection")) throw new InvalidDataException("Select one record for this action.");
        var args = new Dictionary<string, JsonNode?>();
        var lines = new List<string>();
        foreach (var i in tool.Action.Inputs)
        {
            string value = values.GetValueOrDefault(i.Key, "").Trim();
            if (i.Kind == "selection") { args[i.Key] = JsonSerializer.SerializeToNode(selection.Select(r => r.Id)); continue; }
            if (value.Length == 0 && i.Optional) continue;
            if (value.Length == 0) throw new InvalidDataException($"Enter {i.Label}.");
            args[i.Key] = i.Kind switch
            {
                "vector" => Vector(value),
                "number" => JsonValue.Create(Number(value)),
                "boolean" => JsonValue.Create(bool.Parse(value)),
                _ => JsonValue.Create(value)
            };
            if (i.Kind == "bone") foreach (var animal in selection)
                if (!lib.Animal(animal.Id).Bones.ContainsKey(value)) throw new InvalidDataException($"{animal.Name} has no bone named {value}.");
        }
        FcsFile fcs = openedFcs == null ? new() : FcsReader.Read(new MemoryStream(openedFcs));
        void Dependency(string name)
        {
            if (name.Equals(lib.ModName, StringComparison.OrdinalIgnoreCase) || LoadOrder.BaseFiles.Contains(name, StringComparer.OrdinalIgnoreCase)) return;
            if (!lib.ModPaths.ContainsKey(name)) throw new InvalidDataException($"Dependency is not installed: {name}");
            if (!fcs.Dependencies.Contains(name, StringComparer.OrdinalIgnoreCase)) { fcs.Dependencies.Add(name); lines.Add("Require " + name); }
        }
        foreach (var row in selection) Dependency(row.Source);
        foreach (var e in tool.Action.Edits)
        {
            switch (e.Str("op"))
            {
                case "dependency": Dependency(e.Str("file")); break;
                case "asset":
                    var key = e.Str("input");
                    if (!args.ContainsKey(key)) break;
                    if (e["requires"]?.GetValue<string>() is string req && !args.ContainsKey(req)) throw new InvalidDataException("Choose the companion asset first.");
                    string raw = (string)args[key]!;
                    var file = Path.IsPathFullyQualified(raw) ? raw : SafeFiles.Target(lib.Project, raw);
                    if (!e["extensions"]!.AsArray().Any(ext => string.Equals((string?)ext, Path.GetExtension(file), StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidDataException($"Unsupported asset: {file}");
                    var rel = e.Str("directory") + "/" + Path.GetFileName(file);
                    var to = SafeFiles.Target(lib.Project, rel);
                    var data = File.ReadAllBytes(file);
                    bool same = File.Exists(to) && File.ReadAllBytes(to).AsSpan().SequenceEqual(data);
                    lines.Add(same ? "Reuse " + rel : File.Exists(to) ? "Replace conflicting asset " + rel : "Import " + rel);
                    plan.Writes[rel] = data;
                    args[key] = rel;
                    break;
                case "jsonEntry":
                    var name = e.Str("file"); var doc = Document(name);
                    int version = (int)e["version"]!;
                    var versionKey = e.Str("versionField"); var prior = (int?)doc[versionKey];
                    if (prior != null && !e["preserveVersions"]!.AsArray().Any(v => (int)v! == prior)) throw new InvalidDataException("Unsupported content schema.");
                    if (prior != null && prior != version) lines.Add($"Upgrade {name} from schema {prior} to {version}.");
                    // Transport schema 1 ignores placeOnLoad, even if an author wrote true in the file.
                    if (e["upgradeDrop"] is JsonObject migration && prior == (int?)migration["from"] && doc[migration.Str("array")] is JsonArray rows)
                        foreach (var row in rows.OfType<JsonObject>()) row.Remove(migration.Str("field"));
                    string owner = (string)args[e.Str("owner")]!, id = (string)args[e.Str("id")]!;
                    if (doc[e.Str("ownerField")]?.GetValue<string>() is string prev && prev != owner) throw new InvalidDataException("Keep the existing pack ID.");
                    if ((bool?)e["ownerPrefix"] == true && (!id.StartsWith(owner + ".", StringComparison.Ordinal) || id.Length == owner.Length + 1))
                        throw new InvalidDataException("Enter a fit ID such as pack-id.trail, with a name after the dot.");
                    doc[versionKey] = version; doc[e.Str("ownerField")] = owner;
                    var array = (doc[e.Str("array")] ??= new JsonArray()).AsArray();
                    var entry = array.OfType<JsonObject>().SingleOrDefault(n => (string?)n[e.Str("idField")] == id);
                    if (entry == null) { entry = new(); array.Add(entry); }
                    var before = entry.ToJsonString();
                    foreach (var (field, binding) in e["fields"]!.AsObject())
                        if (args.TryGetValue((string)binding!, out var val)) entry[field] = val?.DeepClone(); else entry.Remove(field);
                    if (e["uniqueSelection"]?.GetValue<string>() is string unique && array.OfType<JsonObject>().Where(n => n != entry)
                        .Any(n => n[unique] is JsonArray ids && ids.Any(v => selection.Any(r => r.Id == (string?)v))))
                        throw new InvalidDataException("A selected animal already belongs to another fit in this pack.");
                    var opts = new JsonSerializerOptions { WriteIndented = true };
                    plan.Writes[name] = Encoding.UTF8.GetBytes(doc.ToJsonString(opts) + "\n");
                    lines.Add($"{name} · {id}\nBefore: {before}\nAfter:\n{entry.ToJsonString(opts)}");
                    break;
                case "reference":
                    string target = e.Str("target"); var list = e.Str("list");
                    if (lib.Data.Find(target) == null && (!lib.ModPaths.TryGetValue(tool.Source, out var providerFile) ||
                        !FcsReader.ReadFile(providerFile).Records.Any(r => r.StringId == target && !r.IsRemoved)))
                        throw new InvalidDataException($"Missing provider reference {target}.");
                    foreach (var row in selection)
                    {
                        var rec = fcs.Records.SingleOrDefault(r => r.StringId == row.Id);
                        if (rec == null) { rec = new FcsRecord { RecordType = Enum.Parse<FcsRecordType>(row.Type), StringId = row.Id, Name = row.Name, Flags = 1, ByteSize = 1 }; fcs.Records.Add(rec); }
                        if (!rec.References.TryGetValue(list, out var refs)) rec.References[list] = refs = [];
                        int index = refs.FindIndex(r => r.TargetStringId == target);
                        var next = new FcsReference(target, (bool)args[e.Str("value")]! ? 1 : 0, 0, 0);
                        if (index >= 0) refs[index] = next; else refs.Add(next);
                        lines.Add($"{lib.ModName} · {row.Name} ({row.Id})\n{list} → {target} = {next.Value0}");
                    }
                    break;
            }
        }
        using var ms = new MemoryStream(); FcsWriter.Write(fcs, ms);
        plan.Writes[lib.ModName] = ms.ToArray();
        plan.CheckNames();
        Ready = true; return string.Join("\n\n", lines);
    }
    public static float Number(string s) => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) && float.IsFinite(v) ? v : throw new InvalidDataException("Expected a finite number.");
    public static JsonArray Vector(string s)
    {
        var v = s.Split([' ', ',', ';'], StringSplitOptions.RemoveEmptyEntries);
        if (v.Length != 3) throw new InvalidDataException("Enter three coordinates: X, Y, Z.");
        return new JsonArray(v.Select(x => (JsonNode?)JsonValue.Create(Number(x))).ToArray());
    }
    public void Save() { if (!Ready) throw new InvalidOperationException("Review changes first."); plan.Commit(); Ready = false; }
}
