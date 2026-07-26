using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFinaleSlashs
{
    /// <summary>终斩演出的锋利层图元：在全屏斩切/碎镜后效之上绘制。
    /// 世界是被切的对象，刀光是施刀者——刀不能被自己的斩击切碎</summary>
    internal interface IOniCrispDrawable
    {
        /// <summary>后效之上的绘制，实现者自管 SpriteBatch/设备状态</summary>
        void DrawCrisp();
    }

    /// <summary>锋利层渲染，权重 1.095：晚于 OniFinalePost 切片(1.09) 与碎镜折射(1.093)，
    /// 早于绯红 Bloom(1.10)——刀光不吃裂屏/切片/折射/径向模糊，但仍被 Bloom 拾亮。
    /// 伤口断面刻意不在此层（须与裂屏位移物理对位，留在世界里被劈开）</summary>
    internal sealed class OniFinaleCrispLayerRender : RenderHandle
    {
        private static readonly List<IOniCrispDrawable> buffer = new(24);

        public override float Weight => 1.095f;

        public override void EndCaptureDraw(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            buffer.Clear();
            Projectile[] projectiles = Main.projectile;
            for (int i = 0; i < projectiles.Length; i++) {
                Projectile p = projectiles[i];
                if (p.active && p.ModProjectile is IOniCrispDrawable crisp) {
                    buffer.Add(crisp);
                }
            }
            if (buffer.Count == 0) {
                return;
            }
            if (Main.screenTarget == null || Main.screenTarget.IsDisposed) {
                return;
            }

            //叠画在后效完成的画面之上（screenTarget 为 PreserveContents，绑定不丢内容）

            graphicsDevice.SetRenderTarget(Main.screenTarget);
            foreach (IOniCrispDrawable crisp in buffer) {
                crisp.DrawCrisp();
            }
        }
    }
}
