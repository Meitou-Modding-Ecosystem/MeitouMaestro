using System.Text.Json;
using System.Text.Json.Nodes;
using Meitou.Data.Fcs;

namespace Maestro.Core;

public sealed record Input(string Key, string Label, string Kind, bool Optional, JsonNode? Default);
public sealed record ToolAction(string Id, string Label, string RecordType, Input[] Inputs, JsonObject[] Edits, JsonObject? Preview);
public sealed record Provider(int SchemaVersion, string ProviderId, string Name, ToolAction[] Actions);
public sealed record Tool(string ProviderId, string Name, string Source, ToolAction Action)
{
    public override string ToString() => $"{Action.Label}  ·  {Name}";
}

public static class Descriptions
{
    public static string Str(this JsonObject obj, string key) =>
        obj[key]?.GetValue<string>() ?? throw new InvalidDataException($"Missing descriptor field '{key}'.");
    public static string SimpleName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim() || value is "." or ".." ||
            value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || value.Contains('/') || value.Contains('\\') ||
            value.Contains(':') || value.EndsWith('.') || value.StartsWith(".maestro", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Expected a file name or ID, got '{value}'.");
        return value;
    }
    public static Provider Read(string path)
    {
        var raw = JsonNode.Parse(File.ReadAllBytes(path))!.AsObject();
        if ((int?)raw["schemaVersion"] != 1) throw new InvalidDataException("Unsupported descriptor version.");
        var opt = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var actions = raw["actions"]!.Deserialize<ToolAction[]>(opt) ?? [];
        if (actions.Length == 0 || actions.Select(a => a.Id).Distinct().Count() != actions.Length)
            throw new InvalidDataException("Missing or repeated action.");
        foreach (var a in actions)
        {
            SimpleName(a.Id);
            if (!Enum.TryParse<FcsRecordType>(a.RecordType, false, out var type) || !Enum.IsDefined(type) || string.IsNullOrWhiteSpace(a.Label))
                throw new InvalidDataException("Unknown record type or empty label.");
            var inputs = a.Inputs.ToDictionary(i => SimpleName(i.Key));
            foreach (var i in a.Inputs)
                if (i.Kind is not ("text" or "selection" or "vector" or "number" or "asset" or "bone" or "boolean"))
                    throw new InvalidDataException($"Unknown input '{i.Kind}'.");
            void Needs(string key, string? kind = null)
            {
                if (!inputs.TryGetValue(key, out var field) || kind != null && field.Kind != kind)
                    throw new InvalidDataException($"Invalid input binding '{key}'.");
            }
            foreach (var e in a.Edits)
                switch (e.Str("op"))
                {
                    case "dependency": SimpleName(e.Str("file")); break;
                    case "asset":
                        Needs(e.Str("input"), "asset"); SimpleName(e.Str("directory"));
                        if (e["requires"] != null) Needs(e.Str("requires"), "asset");
                        if (e["extensions"] is not JsonArray { Count: > 0 }) throw new InvalidDataException("Missing asset extensions.");
                        break;
                    case "jsonEntry":
                        if (!SimpleName(e.Str("file")).EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                            e.Str("file").Equals("maestro.json", StringComparison.OrdinalIgnoreCase) ||
                            e.Str("file").Equals("meitou.json", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Expected a content JSON file.");
                        Needs(e.Str("id"), "text"); Needs(e.Str("owner"), "text");
                        foreach (var pair in e["fields"]!.AsObject()) Needs((string)pair.Value!);
                        break;
                    case "reference": Needs(e.Str("value"), "boolean"); e.Str("list"); e.Str("target"); break;
                    default: throw new InvalidDataException($"Unsupported operation '{e.Str("op")}'.");
                }
            if (a.Preview is { } p)
                foreach (var (key, kind) in new[] { ("selection", "selection"), ("anchor", "bone"), ("mesh", "asset"), ("offset", "vector"), ("rotation", "vector"), ("seat", "vector"), ("facing", "number") })
                    Needs(p.Str(key), kind);
        }
        return new(1, SimpleName(raw.Str("provider")), raw.Str("name"), actions);
    }
}
