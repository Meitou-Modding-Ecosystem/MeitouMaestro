using Meitou.Modding;
namespace Maestro.Gate;

public sealed class GearConditions : IMod
{
    public void Initialize(ModContext context)
    {
        context.AddDialogueCondition(18432, check =>
        {
            var table = check.World.Characters;
            bool equipped = table.TryResolveNext(check.Subject, out int slot)
                && table.Cold(slot)?.Inventory?.Items.Any(i => i.Section == "head" && i.Record == "2168-gamedata.base") == true;
            return check.Matches(equipped);
        });
    }
}
