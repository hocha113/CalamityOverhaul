﻿using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime
{
    internal class BrutalKingSlimeAI : CWRNPCOverride
    {
        public override int TargetID => NPCID.KingSlime;
        public override bool CanLoad() {
            return false;
        }

        public override bool AI() {
            return false;
        }
    }
}
