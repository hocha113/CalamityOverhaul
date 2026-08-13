using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;
using Terraria.UI;

namespace CalamityOverhaul.Content.Scenarios.OldNet.UI
{
    /// <summary>
    /// 弹出闪红转场：链路烧断的最简 CPU 全屏层（黑墙红 alpha 包络 + 横向撕裂线），
    /// 约 25 tick，红峰处交棒 LINK SEVERED 加载屏；残余帧落在主世界侧自然收尾。
    /// 刻意不用 shader——新 fx 有 FNA3D 无日志崩溃风险，此处挣不回成本
    /// </summary>
    internal class OldNetEjectFlash : ModSystem
    {
        internal const int TotalFrames = 25;
        /// <summary>红峰帧（ForceEject 倒数到此值时才真正 ExitWorld）</summary>
        internal const int PeakFrame = 10;

        private static int timer;
        private static readonly Color EmberRed = new(235, 64, 44);

        internal static void Begin() => timer = TotalFrames;

        public override void UpdateUI(GameTime gameTime) {
            if (timer > 0) {
                timer--;
            }
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
            if (timer <= 0) {
                return;
            }
            //末层盖住常规 UI（覆盖层先例：CybCourseEntryRevealLayer）
            layers.Add(new LegacyGameInterfaceLayer("CWRMod: OldNet Eject Flash",
                delegate {
                    DrawFlash(Main.spriteBatch);
                    return true;
                }, InterfaceScaleType.UI));
        }

        private static void DrawFlash(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return;
            }
            int w = (int)(PlayerInput.RealScreenWidth / Main.UIScale);
            int h = (int)(PlayerInput.RealScreenHeight / Main.UIScale);
            float progress = 1f - timer / (float)TotalFrames;
            //正弦包络 0→0.85→0，红峰约在 ExitWorld 交棒帧附近
            float alpha = 0.85f * MathF.Sin(MathF.PI * progress);

            sb.Draw(px, new Rectangle(0, 0, w, h), EmberRed * alpha);

            //横向撕裂线：逐帧随机 y 的闪烁横条，信号被硬扯断的样子
            for (int i = 0; i < 3; i++) {
                int y = Main.rand.Next(h);
                int thick = Main.rand.Next(2, 5 + i);
                sb.Draw(px, new Rectangle(0, y, w, thick), Color.White * (alpha * 0.45f));
                sb.Draw(px, new Rectangle(0, y + thick, w, 1), Color.Black * (alpha * 0.6f));
            }
        }
    }
}
