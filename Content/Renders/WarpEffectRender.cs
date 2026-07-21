using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Renders
{
    /// <summary>EndCaptureDraw 屏幕扭曲后</summary>
    internal sealed class WarpEffectRender : RenderHandle
    {
        //预分配，每帧 Clear 复用
        private static readonly List<IWarpDrawable> _warpBuffer = new(16);
        private static readonly List<IWarpDrawable> _warpNoBlueshiftBuffer = new(16);

        /// <summary>权重 1.2，晚于热浪，采样含其他写入</summary>
        public override float Weight => 1.2f;

        public override void EndCaptureDraw(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            CollectDrawables();

            //无扭曲源则跳过
            if (_warpBuffer.Count == 0 && _warpNoBlueshiftBuffer.Count == 0) {
                return;
            }
            if (screenSwap == null || Main.screenTarget == null || Main.screenTargetSwap == null) {
                return;
            }
            if (EffectLoader.WarpShader == null || !EffectLoader.WarpShader.IsLoaded) {
                return;
            }

            if (_warpBuffer.Count > 0) {
                ProcessWarpSets(graphicsDevice, screenSwap, _warpBuffer, noBlueshift: false);
            }
            if (_warpNoBlueshiftBuffer.Count > 0) {
                ProcessWarpSets(graphicsDevice, screenSwap, _warpNoBlueshiftBuffer, noBlueshift: true);
            }
        }

        private static void CollectDrawables() {
            _warpBuffer.Clear();
            _warpNoBlueshiftBuffer.Clear();

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

                if (mp is IWarpDrawable warp) {
                    if (warp.DontUseBlueshiftEffect()) {
                        _warpNoBlueshiftBuffer.Add(warp);
                    }
                    else {
                        _warpBuffer.Add(warp);
                    }
                }
            }
        }

        /// <summary>拷屏→swap画源→WarpShader回写→自定义层</summary>
        private static void ProcessWarpSets(GraphicsDevice graphicsDevice, RenderTarget2D screen
            , List<IWarpDrawable> warpSets, bool noBlueshift) {
            SpriteBatch sb = Main.spriteBatch;

            //拷屏到临时 RT
            graphicsDevice.SetRenderTarget(screen);
            graphicsDevice.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            sb.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            sb.End();

            //swap 画扭曲源
            graphicsDevice.SetRenderTarget(Main.screenTargetSwap);
            graphicsDevice.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            int warpCount = warpSets.Count;
            for (int i = 0; i < warpCount; i++) {
                warpSets[i].Warp();
            }
            sb.End();

            //着色器回写主 RT
            graphicsDevice.SetRenderTarget(Main.screenTarget);
            graphicsDevice.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);

            Effect effect = EffectLoader.WarpShader.Value;
            effect.Parameters["tex0"]?.SetValue(Main.screenTargetSwap);
            effect.Parameters["noBlueshift"]?.SetValue(noBlueshift);
            effect.Parameters["i"]?.SetValue(0.035f);
            effect.CurrentTechnique.Passes[0].Apply();
            sb.Draw(screen, Vector2.Zero, Color.White);
            sb.End();

            //有自定义层才 Begin
            bool needCustomBatch = false;
            for (int i = 0; i < warpCount; i++) {
                if (warpSets[i].CanDrawCustom()) {
                    needCustomBatch = true;
                    break;
                }
            }
            if (!needCustomBatch) {
                return;
            }

            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            for (int i = 0; i < warpCount; i++) {
                IWarpDrawable warp = warpSets[i];
                if (warp.CanDrawCustom()) {
                    warp.DrawCustom(sb);
                }
            }
            sb.End();
        }
    }
}
