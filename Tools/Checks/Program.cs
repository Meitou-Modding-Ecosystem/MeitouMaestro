using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Maestro.Core;
using Meitou.Data.Fcs;

if (args is ["--compare-saddle", var outputFit, var referenceFit])
{
    var fit = FcsReader.ReadFile(outputFit);
    var reference = FcsReader.ReadFile(referenceFit);
    Test(fit.Dependencies.SequenceEqual(["Meitou Transport.mod"]) && fit.Dependencies.SequenceEqual(reference.Dependencies)
        && fit.References.SequenceEqual(reference.References) && fit.Records.Count == 0 && reference.Records.Count == 0,
        "saddle FCS dependencies and empty record set match the reference");
    return;
}
if (args is ["--fit-proof", var directory])
{
    static bool Near(Vector3 actual, Vector3 expected) => Vector3.Distance(actual, expected) < .0001f;
    var anchor = new Vector3(1, 2, 3);
    var identity = FitPose.At(anchor, new(.54f, 9.05f, .4f), default, new(0, 1.85f, 0), 0);
    Test(Near(identity.Pelvis, new(1.54f, 12.9f, 3.4f)), "reference fit aligns seat and bone in model units");
    var rotated = FitPose.At(anchor, new(.5f, 4, -2), new(0, 90, 0), new(1, 2, 3), 90);
    Test(Near(rotated.Pelvis, new(4.5f, 8, 0)) && Near(Vector3.TransformNormal(Vector3.UnitZ, rotated.Facing), -Vector3.UnitZ),
        "rotated saddle moves the local seat and composes rider yaw");
    Test(Near(Vector3.TransformNormal(Vector3.UnitY, FitPose.At(default, default, new(90,0,0), default, 0).Saddle), Vector3.UnitZ)
        && Near(Vector3.TransformNormal(Vector3.UnitX, FitPose.At(default, default, new(0,0,90), default, 0).Saddle), Vector3.UnitY),
        "positive pitch and roll agree with Transport");
    var testRoot = Directory.CreateDirectory(Path.Combine(directory, Guid.NewGuid().ToString("N"))).FullName;
    Directory.CreateDirectory(Path.Combine(testRoot, "Meshes"));
    File.WriteAllText(Path.Combine(testRoot, "Meshes", "Garru.mesh"), "");
    foreach (var conflict in new[] { "meshes/Other.mesh", "Meshes/garru.mesh", "Meshes/AUX.dds", "Meshes/a?.mesh" })
    {
        var trial = new ProjectFiles(testRoot);
        trial.Writes[conflict] = [];
        try { trial.CheckNames(); throw new Exception("Accepted " + conflict); }
        catch (InvalidDataException) { Test(true, "portable filename check rejected " + conflict); }
    }
    var jsonEdit = JsonNode.Parse("""{"op":"jsonEntry","file":"transport.json","array":"mounts","fields":{"saddleMesh":"mesh"}}""")!.AsObject();
    var assetEdit = JsonNode.Parse("""{"op":"asset","input":"mesh"}""")!.AsObject();
    var previewTool = new Tool("test", "Test", "Test.mod", new("fit","Fit","AnimalCharacter",[],[assetEdit,jsonEdit],null));
    File.WriteAllBytes(Path.Combine(testRoot, Path.GetFileName(testRoot) + ".mod"), []);
    string pack = Path.Combine(directory, Guid.NewGuid() + ".zip");
    foreach (var missing in new[] { "Meshes/garru.mesh", "Meshes/missing.mesh" })
    {
        File.WriteAllText(Path.Combine(testRoot,"transport.json"), JsonSerializer.Serialize(new { mounts = new[] { new { saddleMesh = missing } } }));
        try { ProjectFiles.Export(testRoot, pack, [previewTool]); throw new Exception("Exported a dangling reference."); }
        catch (InvalidDataException) { Test(!File.Exists(pack), "export refused missing or mis-cased reference " + missing); }
    }
    if (OperatingSystem.IsLinux())
    {
        File.WriteAllText(Path.Combine(testRoot, "Meshes", "garru.mesh"), "");
        File.WriteAllBytes(Path.Combine(testRoot, Path.GetFileName(testRoot) + ".mod"), []);
        string zip = Path.Combine(directory, Guid.NewGuid() + ".zip");
        try { ProjectFiles.Export(testRoot, zip); throw new Exception("Exported two case aliases."); }
        catch (InvalidDataException) { Test(!File.Exists(zip), "Linux case aliases rejected before publishing ZIP"); }
    }
    return;
}

if (args is ["--compare", var generated, var original, var townFile, var seedFile])
{
    var fit = FcsReader.ReadFile(generated);
    var released = FcsReader.ReadFile(original);
    Test(fit.Dependencies.SequenceEqual(released.Dependencies) && fit.Dependencies.SequenceEqual(["Meitou Transport.mod"])
        && fit.References.SequenceEqual(released.References) && fit.Records.Count == released.Records.Count,
        "exported saddle FCS records and dependencies match the released sample");
    var town = FcsReader.ReadFile(townFile);
    var seed = FcsReader.ReadFile(seedFile);
    var authored = town.Records.Single(r => r.StringId == "5-Dune Gate.mod");
    Test(authored.References["conditions"].Single() == new FcsReference("1-Gear Conditions.mod", 1, 0, 0)
        && town.Dependencies.Contains("Gear Conditions.mod") && town.Records.Count == seed.Records.Count
        && !seed.Records.Single(r => r.StringId == authored.StringId).References.ContainsKey("conditions"),
        "editor authored the condition and framework dependency on the original town line");
    return;
}
if (args.Length != 4) throw new ArgumentException("Supply isolated Kenshi profile, art folder, parent for trial folders, and gate fixture file.");
var profile = args[0]; var art = Path.GetFullPath(args[1]); var parent = Directory.CreateDirectory(Path.Combine(args[2], Guid.NewGuid().ToString("N"))).FullName;
void Test(bool pass, string text) { if (!pass) throw new Exception(text); Console.WriteLine("PASS " + text); }
var project = Directory.CreateDirectory(Path.Combine(parent, "Young Hounds")).FullName;
var fcs = new FcsFile { Author = "Maestro test", NextId = 2 };
var pup = new FcsRecord { ByteSize = 1, StringId = "1-Young Hounds.mod", RecordType = FcsRecordType.AnimalCharacter, Name = "Artist's hound" };
var db = Meitou.Data.GameDatabase.Load(Meitou.Data.LoadOrder.FromInstall(Meitou.Content.GameInstall.Open(profile)));
var race = db.Find("42201-rebirth.mod")!.GetReferences("race")[0];
pup.References["race"] = [new(race.TargetStringId, 1, 0, 0)]; fcs.Records.Add(pup);
FcsWriter.WriteFile(fcs, Path.Combine(project, "Young Hounds.mod"));
var old = jsonFrom("""{"schemaVersion":1,"packId":"hound","vessels":[{"id":"hound.boat"}],"placements":[{"id":"hound.legacy","placeOnLoad":true}]}""");
var jsonPath = Path.Combine(project, "transport.json"); File.WriteAllText(jsonPath, old.ToJsonString());
var lib = new Library(profile, project);
var action = lib.Actions.Single(t => t.Action.Id == "riding-fit");
var selected = new[] { lib.Records.Single(r => r.Id == pup.StringId) };
Test(selected[0].Source == "Young Hounds.mod", "new animal from the writable project, outside mods.cfg");
var v = new Dictionary<string, string> { { "pack", "hound" }, { "fit", "hound.plain" }, { "label", "Hound trail seat" }, { "bone", "Bip01 Spine1" }, { "saddle", "0 0 0" }, { "rotation", "0 0 0" }, { "seat", "0 1 0" }, { "yaw", "0" } };
var edit = new Editing(lib);
v["bone"] = "missing bone";
try { edit.Review(action, selected, v); throw new Exception("Missing bone accepted"); } catch (InvalidDataException) { Test(!edit.Ready, "invalid bone refused"); }
v["bone"] = "Bip01 Spine1"; edit.Review(action, selected, v);
var initial = File.ReadAllBytes(lib.FcsPath);
File.AppendAllText(jsonPath, " ");
try { edit.Save(); throw new Exception("External edit accepted"); } catch (IOException) { Test(File.ReadAllBytes(lib.FcsPath).SequenceEqual(initial), "external edit preserved before committing other files"); }
edit = new Editing(lib); edit.Review(action, selected, v); edit.Save();
var doc = jsonFrom(File.ReadAllText(jsonPath));
Test((int?)doc["schemaVersion"] == 3 && doc["placements"]![0]!["placeOnLoad"] == null && (string?)doc["vessels"]![0]!["id"] == "hound.boat", "schema upgrade preserved vessel, placement and manual launch");
edit = new Editing(lib); v["seat"] = "0 2 0"; edit.Review(action, selected, v); edit.Save();
Test(jsonFrom(File.ReadAllText(jsonPath))["mounts"]!.AsArray().Count == 1, "editing stable fit ID updates in place");
var mesh = Path.Combine(art, "meshes", "mvv_garru_saddle.mesh"); var dest = Path.Combine(project, "meshes", Path.GetFileName(mesh));
Directory.CreateDirectory(Path.GetDirectoryName(dest)!); File.WriteAllText(dest, "prior asset");
edit = new Editing(lib); v["mesh"] = mesh;
Test(edit.Review(action, selected, v).Contains("Replace conflicting asset"), "asset replacement shown in review");
var j = Path.Combine(project, ".maestro-transaction"); Directory.CreateDirectory(Path.Combine(j, "before"));
File.Copy(jsonPath, Path.Combine(j, "before", "transport.json"));
File.WriteAllText(Path.Combine(j, "prepared.json"), """[{"Path":"transport.json","Existed":true},{"Path":"new.json","Existed":false}]""");
File.WriteAllText(jsonPath, "interrupted"); File.WriteAllText(Path.Combine(project, "new.json"), "new");
ProjectFiles.Recover(project);
Test(jsonFrom(File.ReadAllText(jsonPath))["mounts"]!.AsArray().Count == 1 && !File.Exists(Path.Combine(project, "new.json")), "interrupted transaction rolls back old and new files");
var broken = Path.Combine(parent, "bad.json"); File.WriteAllText(broken, """{"schemaVersion":22}""");
try { Descriptions.Read(broken); throw new Exception("Bad descriptor accepted"); } catch (InvalidDataException) { Test(true, "unsupported provider version rejected"); }
var brokenMod = "Maestro Checks " + Guid.NewGuid().ToString("N");
var brokenFolder = Directory.CreateDirectory(Path.Combine(profile, "mods", brokenMod)).FullName;
try
{
    FcsWriter.WriteFile(new FcsFile(), Path.Combine(brokenFolder, brokenMod + ".mod"));
    var invalid = Path.Combine(brokenFolder, "maestro.json");
    File.WriteAllText(invalid, """{"schemaVersion":1,"provider":"unknown","name":"Unknown","actions":[{"id":"x","label":"x","recordType":"AnimalCharacter","inputs":[],"edits":[{"op":"notSupported"}]}]}""");
    var withBroken = new Library(profile, project);
    Test(withBroken.Diagnostics.Any(d => d.Contains(invalid) && d.Contains("notSupported")) &&
        withBroken.Actions.Any(t => t.Action.Id == "worn-goggles") && withBroken.Actions.Any(t => t.Action.Id == "riding-fit"),
        "unsupported operation identifies its provider file and leaves both good providers available");
}
finally
{
    File.Delete(Path.Combine(brokenFolder, "maestro.json")); File.Delete(Path.Combine(brokenFolder, brokenMod + ".mod"));
    Directory.Delete(brokenFolder);
}
var gate = Directory.CreateDirectory(Path.Combine(parent, "Gate Addon")).FullName;
var baseTown = Directory.CreateDirectory(Path.Combine(profile, "mods", "Dune Gate")).FullName;
var native = Path.Combine(baseTown, "Dune Gate.mod"); if (File.Exists(native)) throw new IOException("Use an isolated profile without Dune Gate installed.");
File.Copy(args[3], native);
try
{
    var addon = new FcsFile(); addon.Dependencies.Add("Dune Gate.mod"); FcsWriter.WriteFile(addon, Path.Combine(gate, "Gate Addon.mod"));
    var scene = new Library(profile, gate); var act = scene.Actions.Single(t => t.Action.Id == "worn-goggles");
    var line = new[] { scene.Records.Single(r => r.Id == "5-Dune Gate.mod") };
    foreach (var value in new[] { "True", "False" }) { var e = new Editing(scene); e.Review(act, line, new() { { "wearing", value } }); e.Save(); }
    var inherited = FcsReader.ReadFile(scene.FcsPath);
    Test(inherited.Records.Single().IsModified && inherited.Records[0].References["conditions"].Single().Value0 == 0 && File.ReadAllBytes(native).SequenceEqual(File.ReadAllBytes(args[3])), "inherited line override, idempotent value update and untouched source");
}
finally { File.Delete(native); }
static JsonObject jsonFrom(string value) => JsonNode.Parse(value)!.AsObject();
