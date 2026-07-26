using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Renders
{
    /// <summary>EndEntityDraw 弹幕扩展绘制层</summary>
    internal sealed class ProjectileLayerRender : RenderHandle
    {
        private static readonly List<IPrimitiveDrawable> _primitiveBuffer = new(64);
        private static readonly List<IAdditiveDrawable> _additiveBuffer = new(64);
        private static readonly List<IOverlayDrawable> _overlayBuffer = new(16);

        /// <summary>权重 1.2，与原 EffectLoader 挂载次序一致</summary>
        public override float Weight => 1.2f;

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main) {
            CollectDrawables();

            //图元层，自管 SpriteBatch
            int primitiveCount = _primitiveBuffer.Count;
            for (int i = 0; i < primitiveCount; i++) {
                _primitiveBuffer[i].DrawPrimitives();
            }

            //加色层，有内容才 Begin
            int additiveCount = _additiveBuffer.Count;
            if (additiveCount > 0) {
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointWrap
                    , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                for (int i = 0; i < additiveCount; i++) {
                    _additiveBuffer[i].DrawAdditiveAfterNon(spriteBatch);
                }

                spriteBatch.End();
            }

            //遮挡层，盖住特效的本体
            int overlayCount = _overlayBuffer.Count;
            if (overlayCount > 0) {
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp
                    , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                for (int i = 0; i < overlayCount; i++) {
                    _overlayBuffer[i].DrawOverlay(spriteBatch);
                }

                spriteBatch.End();
            }
        }

        private static void CollectDrawables() {
            _primitiveBuffer.Clear();
            _additiveBuffer.Clear();
            _overlayBuffer.Clear();

            Projectile[] projectiles = Main.projectile;
            int count = projectiles.Length;
            for (int i = 0; i < count; i++) {
                Projectile p = projectiles[i];
                if (!p.active) {
                    continue;
                }
                ModProjectile mp = p.ModProjectile;
                if (mp is null) {
                    continue;
                }

                if (mp is IPrimitiveDrawable primitive) {
                    _primitiveBuffer.Add(primitive);
                }
                if (mp is IAdditiveDrawable additive) {
                    _additiveBuffer.Add(additive);
                }
                if (mp is IOverlayDrawable overlay) {
                    _overlayBuffer.Add(overlay);
                }
            }
        }
    }
}
