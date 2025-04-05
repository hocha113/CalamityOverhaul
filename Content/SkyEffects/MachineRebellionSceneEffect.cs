using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.SkyEffects
{
    internal class MachineRebellionSceneEffect : ModSceneEffect
    {
        public override int Music => MusicLoader.GetMusicSlot("CalamityOverhaul/Assets/Sounds/Music/DEMSoulforge");
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;
        public override bool IsSceneEffectActive(Player player) => CWRWorld.MachineRebellion;
    }
}
