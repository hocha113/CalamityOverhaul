using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.SkyEffects
{
    internal class BrutalSkeletronSceneEffect : ModSceneEffect
    {
        public override int Music => -1;
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;
        public override bool IsSceneEffectActive(Player player) => NPC.AnyNPCs(NPCID.SkeletronPrime);
        public override void SpecialVisuals(Player player, bool isActive) => player.ManageSpecialBiomeVisuals(BrutalSkeletronSky.Name, isActive);
    }
}
