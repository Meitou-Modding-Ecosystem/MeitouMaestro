using Meitou.Data.Fcs;
using Meitou.Data.Gameplay.Dialogue;

if (args.Length != 1) throw new ArgumentException("Specify an empty fixture output folder.");
string root = Path.GetFullPath(args[0]); Directory.CreateDirectory(root);
void Write(string mod, FcsFile file)
{
    var folder = Directory.CreateDirectory(Path.Combine(root, mod)).FullName;
    if (File.Exists(Path.Combine(folder, mod + ".mod"))) throw new IOException("Fixture already exists.");
    FcsWriter.WriteFile(file, Path.Combine(folder, mod + ".mod"));
}
FcsRecord Add(FcsFile file, string mod, FcsRecordType type, string name)
{
    var row = new FcsRecord { RecordType = type, StringId = $"{file.NextId++}-{mod}.mod", Name = name, ByteSize = 1 };
    file.Records.Add(row); return row;
}
var gear = new FcsFile { NextId = 1, Author = "Maestro example" };
var condition = Add(gear, "Gear Conditions", FcsRecordType.DialogAction, "Target wears Square Goggles");
condition.Ints["condition name"] = 18432; condition.Ints["compare by"] = 0; condition.Ints["who"] = (int)Talker.Target;
Write("Gear Conditions", gear);
const string mod = "Dune Gate";
var town = new FcsFile { NextId = 1, Author = "Maestro example" };
var player = Add(town, mod, FcsRecordType.Character, "Nara");
player.Ints["female chance"] = 100;
player.References["race"] = [new("18019-gamedata.base", 100, 0, 0)];
player.References["clothing"] = [new("2168-gamedata.base", 1, 100, 0), new("51663-Newwworld.mod", 1, 100, 0), new("51662-Newwworld.mod", 1, 100, 0)];
var guard = Add(town, mod, FcsRecordType.Character, "East Gate Keeper");
guard.References["race"] = [new("17-gamedata.quack", 100, 0, 0)];
var package = Add(town, mod, FcsRecordType.DialoguePackage, "East gate greetings");
guard.References["dialogue package"] = [new(package.StringId, 0, 0, 0)];
var opening = Add(town, mod, FcsRecordType.Dialogue, "Recognize the traveller");
var line = Add(town, mod, FcsRecordType.DialogueLine, "Greeting for square goggles");
line.Strings["text0"] = "Those square lenses will serve you well in the dunes.";
line.Ints["score bonus"] = 10;
var fallback = Add(town, mod, FcsRecordType.DialogueLine, "Ordinary greeting"); fallback.Strings["text0"] = "Mind the sand out there.";
opening.References["lines"] = [new(line.StringId, 0, 0, 0), new(fallback.StringId, 0, 0, 0)];
package.References["dialogs"] = [new(opening.StringId, 1, 0, 0)];
var squad = Add(town, mod, FcsRecordType.SquadTemplate, "Dune traveller"); squad.References["leader"] = [new(player.StringId, 1, 0, 0)];
var start = Add(town, mod, FcsRecordType.NewGameStartoff, "Dune Gate Greeting");
start.Bools["force start pos"] = true; start.Ints["start pos X"] = 34595; start.Ints["start pos Z"] = -70128;
start.References["squad"] = [new(squad.StringId, 1, 0, 0)];
Write(mod, town);
