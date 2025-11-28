using InnoVault.GameSystem;

namespace CalamityOverhaul.Content.ADV.Scenarios.Helen.Sulfseas.OldDukes
{
    internal class ModifyOldDuke : NPCOverride
    {
        public override int TargetID => CWRID.NPC_OldDuke;

        public override bool FindFrame(int frameHeight) {
            return base.FindFrame(frameHeight);
        }

        public override bool AI() {
            return base.AI();
        }
    }
}
