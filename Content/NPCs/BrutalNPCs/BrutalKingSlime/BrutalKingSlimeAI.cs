using CalamityMod.Events;
using CalamityMod.World;
using CalamityOverhaul.Content.NPCs.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime
{
    internal class BrutalKingSlimeAI : NPCOverride
    {
        public override int TargetID => NPCID.KingSlime;

        private bool bossRush => BossRushEvent.BossRushActive;

        private bool masterMode => Main.masterMode || bossRush;

        private bool death => CalamityWorld.death || bossRush;

        private bool crystalAlive = true;
        private bool blueCrystalAlive = false;
        private bool greenCrystalAlive = true;
        private float lifeRatio;
        private float lifeRatio2;
        private float teleportScale = 1f;
        private float teleportScaleSpeed;
        private float teleportGateValue;
        private bool teleporting = false;
        private bool teleported = false;
        private bool phase2;
        private bool phase3;
        private Color dustColor;
        private int setDamage;
        private NPC NPC;

        public override bool CanLoad() {
            return false;
        }

        public override bool AI() {
            return false;
        }
    }
}
