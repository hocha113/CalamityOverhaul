using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.SkyEffects
{
    internal class BrutalSkeletronSceneEffect : ModSceneEffect
    {
        public override int Music => MusicID.Boss3;
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;
        public override bool IsSceneEffectActive(Player player) => false;
    }
}
