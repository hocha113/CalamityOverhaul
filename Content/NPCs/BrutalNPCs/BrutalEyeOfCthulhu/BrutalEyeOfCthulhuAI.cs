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
        CalamityGlobalNPC calNPC => npc.Calamity();
        float lifeRatio => npc.life / (float)npc.lifeMax;
        bool bossRush => BossRushEvent.BossRushActive;
        bool masterMode => Main.masterMode || bossRush;
        bool death => CalamityWorld.death || bossRush;
        float phase2LifeRatio => masterMode ? 0.75f : 0.6f;
        float phase3LifeRatio => masterMode ? 0.4f : 0.3f;
        float finalPhaseRevLifeRatio => masterMode ? 0.2f : 0.15f;
        float penultimatePhaseDeathLifeRatio => masterMode ? 0.3f : 0.2f;
        float finalPhaseDeathLifeRatio => masterMode ? 0.15f : 0.1f;
        bool phase2 => lifeRatio < phase2LifeRatio;
        bool phase3 => lifeRatio < phase3LifeRatio;
        bool finalPhaseRev => lifeRatio < finalPhaseRevLifeRatio;
        bool penultimatePhaseDeath => lifeRatio < penultimatePhaseDeathLifeRatio;
        bool finalPhaseDeath => lifeRatio < finalPhaseDeathLifeRatio;
        float lineUpDist => death ? 15f : 20f;
        float servantAndProjectileVelocity => (death ? 8f : 6f) + (masterMode ? 2f : 0f);
        float enrageScale;

        public override void SetProperty() {
            enrageScale = bossRush ? 1f : masterMode ? 0.5f : 0f;
        }

        public override bool CanLoad() {
            return false;
        }

        //首先，重构这个东西便是一场彻彻底底的灾难
        public override bool? AI() {
            return false;
        }
    }
}
