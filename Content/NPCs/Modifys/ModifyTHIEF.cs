using InnoVault.GameSystem;

namespace CalamityOverhaul.Content.NPCs.Modifys
{
    internal class ModifyTHIEF : NPCOverride
    {
        public override int TargetID => CWRID.NPC_THIEF;
        public override bool? CanGoToStatue(bool toKingStatue) => !toKingStatue;
    }
}
