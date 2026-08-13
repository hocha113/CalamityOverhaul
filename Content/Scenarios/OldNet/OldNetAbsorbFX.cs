using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.OldNet
{
    /// <summary>
    /// 采集吸收动画：数据碎粒飞向玩家 + 白色核心闪（PRT 客户端限定）。
    /// 普通与加密节点共用，颜色随碎片类别
    /// </summary>
    internal static class OldNetAbsorbFX
    {
        internal static void Emit(Vector2 worldPos, Color color, int count) {
            if (Main.dedServ) {
                return;
            }
            Player player = Main.LocalPlayer;
            if (player?.active != true) {
                return;
            }
            Vector2 toPlayer = (player.Center - worldPos).SafeNormalize(-Vector2.UnitY);
            //粒数随产出微涨：像素方块正是"数据消散"的形
            int n = Math.Clamp(8 + count, 8, 14);
            for (int i = 0; i < n; i++) {
                Vector2 vel = toPlayer.RotatedBy(Main.rand.NextFloat(-0.5f, 0.5f))
                    * Main.rand.NextFloat(2f, 5f);
                PRTLoader.NewParticle<PRT_CyberSquare>(
                    worldPos + Main.rand.NextVector2Circular(6f, 6f), vel,
                    color, Main.rand.NextFloat(0.5f, 0.9f))
                    ?.Configure(Color.Lerp(color, Color.White, 0.35f), Main.rand.Next(18, 30));
            }
            //核心闪：一粒短命白光
            PRTLoader.NewParticle<PRT_Light>(worldPos, Vector2.Zero, Color.White,
                Main.rand.NextFloat(0.5f, 0.7f))?.Configure(12, opacity: 0.8f);
        }
    }
}
