using CalamityOverhaul.Content.NPCs.Core;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu
{
    internal class BrutalEyeOfCthulhuAI : NPCOverride
    {
        public override int TargetID => NPCID.EyeofCthulhu;
        public override void SetProperty() {

        }

        public override bool CanLoad() {
            return false;
        }

        //首先，重构这个东西便是一场彻彻底底的灾难
        public override bool AI() {
            return false;
        }
    }
}
