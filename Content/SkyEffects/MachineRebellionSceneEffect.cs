using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.SkyEffects
{
    internal class MachineRebellionSceneEffect : ModSceneEffect
    {
        public override int Music => -1;// MusicLoader.GetMusicSlot("CalamityOverhaul/Assets/Sounds/Music/DEMSoulforge");//TODO:暂时不要使用
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;
        public override bool IsSceneEffectActive(Player player) => CWRWorld.MachineRebellion;
    }
}
