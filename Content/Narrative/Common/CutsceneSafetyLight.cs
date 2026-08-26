using InnoVault.Cinematics;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative.Common
{
    /// <summary>
    /// 运镜保底光照：Boss 死亡/演出运镜把镜头拉离玩家时，天顶饥荒规则下
    /// 玩家原地入暗会触发黑暗受伤——演出期间在玩家身上点一盏保底灯，
    /// 运镜与种子规则都不动（反馈十三·#100，拍板：只保光照）
    /// </summary>
    internal class CutsceneSafetyLight : ModSystem
    {
        public override void PostUpdatePlayers() {
            if (Main.dedServ || !CutsceneDirector.IsPlaying) {
                return;
            }
            Player player = Main.LocalPlayer;
            if (player?.active == true && !player.dead) {
                //暖调保底灯：压过饥荒黑暗阈值，演出期间玩家自身也保持可见
                Lighting.AddLight(player.Center, 0.55f, 0.5f, 0.4f);
            }
        }
    }
}
