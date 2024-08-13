using CalamityMod.Events;
using CalamityMod.World;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.SkyEffects
{
    internal class BrutalSkeletronSceneEffect : ModSceneEffect
    {
        public override int Music => MusicLoader.GetMusicSlot("CalamityOverhaul/Assets/Sounds/Music/TheGomsDead");
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;
        public override bool IsSceneEffectActive(Player player) => NPC.AnyNPCs(NPCID.SkeletronPrime);
        public override void SpecialVisuals(Player player, bool isActive) => player.ManageSpecialBiomeVisuals(BrutalSkeletronSky.name, isActive);
    }
}
