using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Events
{
    internal class TungstenEffect : ModSceneEffect
    {
        public override int Music => MusicLoader.GetMusicSlot("CalamityOverhaul/Assets/Sounds/Music/Tungsten");
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;
        public override bool IsSceneEffectActive(Player player) => TungstenRiot.Instance.TungstenRiotIsOngoing;
        public override void SpecialVisuals(Player player, bool isActive) => player.ManageSpecialBiomeVisuals("CWRMod:TungstenSky", isActive);
    }
}
