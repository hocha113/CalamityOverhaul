using CalamityMod;
using CalamityMod.Events;
using CalamityMod.NPCs;
using CalamityMod.World;
using CalamityOverhaul.Content.NPCs.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu
{
    internal class BrutalEyeOfCthulhuAI : NPCCoverage
    {
        private const float ProjectileOffset = 50f;
        public override int TargetID => NPCID.EyeofCthulhu;

        private CalamityGlobalNPC calNPC => npc.Calamity();

        private float lifeRatio => npc.life / (float)npc.lifeMax;

        private bool bossRush => BossRushEvent.BossRushActive;

        private bool masterMode => Main.masterMode || bossRush;

        private bool death => CalamityWorld.death || bossRush;

        private float phase2LifeRatio => masterMode ? 0.75f : 0.6f;

        private float phase3LifeRatio => masterMode ? 0.4f : 0.3f;

        private float finalPhaseRevLifeRatio => masterMode ? 0.2f : 0.15f;

        private float penultimatePhaseDeathLifeRatio => masterMode ? 0.3f : 0.2f;

        private float finalPhaseDeathLifeRatio => masterMode ? 0.15f : 0.1f;

        private bool phase2 => lifeRatio < phase2LifeRatio;

        private bool phase3 => lifeRatio < phase3LifeRatio;

        private bool finalPhaseRev => lifeRatio < finalPhaseRevLifeRatio;

        private bool penultimatePhaseDeath => lifeRatio < penultimatePhaseDeathLifeRatio;

        private bool finalPhaseDeath => lifeRatio < finalPhaseDeathLifeRatio;

        private float lineUpDist => death ? 15f : 20f;

        private float servantAndProjectileVelocity => (death ? 8f : 6f) + (masterMode ? 2f : 0f);

        private float enrageScale;

        public override void SetProperty() {
            enrageScale = bossRush ? 1f : masterMode ? 0.5f : 0f;
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
