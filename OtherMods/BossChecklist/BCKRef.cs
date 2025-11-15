using BossChecklist;
using Terraria.ModLoader;

namespace CalamityOverhaul.OtherMods.BossChecklist
{
    [JITWhenModsEnabled("BossChecklist")]
    internal class BCKRef
    {
        public static bool Has => ModLoader.HasMod("BossChecklist");
        public static void SetActiveNPCEntryFlags(int index, int value) {
            if (!Has) {
                return;
            }
            if (index < 0 || index >= WorldAssist.ActiveNPCEntryFlags.Length) {
                return;
            }
            WorldAssist.ActiveNPCEntryFlags[index] = value;
        }
    }
}
