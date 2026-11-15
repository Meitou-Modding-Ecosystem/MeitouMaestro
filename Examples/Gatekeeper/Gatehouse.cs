using System.Numerics;
using Meitou.Modding;
using Meitou.Simulation;
using Meitou.Simulation.Population;
using Meitou.Simulation.Dialogue;
using Meitou.Simulation.Items;

namespace Maestro.Examples;

public sealed class Gatehouse : IMod
{
    public void Initialize(ModContext context) => context.AddSystem("gatekeeper", _ => new Scene(), SystemAnchor.End);
    private sealed class Scene : ITickSystem
    {
        CharacterId player = CharacterId.None, keeper = CharacterId.None;
        readonly bool trial = Environment.GetEnvironmentVariable("MAESTRO_GATE_TRIAL") == "1";
        int stage;
        public void Inputs(World world, IReadOnlyList<SimCommand> commands)
        {
            if (world.Tick < 2 || stage == 5) return;
            var table = world.Characters;
            if (keeper.IsNone)
            {
                for (int i = 0; i < table.HighWater; i++)
                {
                    var cold = table.Cold(i);
                    if (cold?.RecordId == "2-Dune Gate.mod" && !cold.IsPlayer) keeper = table.IdOf(i);
                    if (cold is { IsPlayer: true, RecordId: "1-Dune Gate.mod" }) player = table.IdOf(i);
                }
                if (player.IsNone) return;
                if (keeper.IsNone)
                {
                    var at = table.Next[player.Slot].Position;
                    keeper = world.Systems.OfType<PopulationSystem>().First().SpawnSandbox(world, "2-Dune Gate.mod", new Vector2(at.X + 9, at.Z + 2), false, faction: world.Player.Faction);
                }
            }
            if (!trial || world.Tick < 20) return;
            var dialogue = world.Systems.OfType<DialogueSystem>().First();
            string? text = dialogue.PublishConversation()?.Text;
            if (stage == 0 || stage == 3)
            {
                world.Defer(new TalkOrder(player, keeper) { Tick = world.Tick + 1 }); stage++; return;
            }
            if (stage == 2 && text == null)
            {
                var cold = table.Cold(player.Slot)!; var inv = cold.Inventory!;
                int i = inv.Items.FindIndex(item => item.Section == "head" && item.Record == "2168-gamedata.base");
                var g = inv.Items[i];
                var token = new InventoryItemToken(i, g.Record, g.Section, g.X, g.Y, g.Quantity, g.Quality);
                if (!InventoryCommands.Move(inv, token, "main", 0, 0, cold) || g.Section != "main") throw new InvalidOperationException("Goggles could not move to inventory.");
                Console.WriteLine("MAESTRO GATE goggles now in main inventory"); stage = 3; return;
            }
            if ((stage == 1 || stage == 4) && text != null)
            {
                string expected = stage == 1 ? "Those square lenses will serve you well in the dunes." : "Mind the sand out there.";
                Console.WriteLine($"MAESTRO GATE {(stage == 1 ? "equipped" : "carried")} · keeper {keeper} · {text}");
                if (text != expected) { Console.WriteLine("MAESTRO GATE FAIL expected " + expected); stage = 5; return; }
                // The conversation window pauses simulation time. Use the same between-tick
                // command as the player's Close button so the next conversation can start.
                world.Commands.Enqueue(new EndConversation { Tick = world.Tick });
                if (stage == 4) { Console.WriteLine("MAESTRO GATE PASS same keeper and player; equipped and carried branches verified"); stage = 5; } else stage = 2;
            }
        }
    }
}
