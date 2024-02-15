using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.MurasamaProj
{
    internal class PowerSceneEffect : ModSceneEffect
    {
        public override int Music => MusicLoader.GetMusicSlot("CalamityOverhaul/Assets/Sounds/Music/BuryTheLight");
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;//我们让这个彩蛋音乐具有很高的优先性
        public override bool IsSceneEffectActive(Player player) => player.CWR().inFoodStallChair;
    }
}
