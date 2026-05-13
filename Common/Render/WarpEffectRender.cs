using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Common.Render
{
    /// <summary>
    /// 屏幕扭曲后处理渲染句柄
    /// <br/>负责在 <see cref="RenderHandle.EndCaptureDraw"/> 阶段：
    /// <list type="number">
    ///     <item>调用所有处于活动状态的 <see cref="IPrimitiveDrawable"/> 弹幕绘制顶点图元</item>
    ///     <item>收集所有 <see cref="IWarpDrawable"/> 弹幕，统一使用 <see cref="EffectLoader.WarpShader"/> 进行屏幕扭曲合成</item>
    /// </list>
    /// 通过对 <see cref="Main.projectile"/> 进行单次线性扫描配合预分配缓冲，减少每帧的迭代次数与 GC 压力
    /// </summary>
    internal sealed class WarpEffectRender : RenderHandle
    {
        //预分配缓冲，每帧 Clear 后复用以避免 GC 压力
        private static readonly List<IWarpDrawable> _warpBuffer = new(16);
        private static readonly List<IWarpDrawable> _warpNoBlueshiftBuffer = new(16);

        /// <summary>
        /// 比常规热浪类后处理稍晚执行，确保扭曲采样的画面已包含其他渲染句柄写入的内容
        /// </summary>
        public override float Weight => 1.2f;

        public override void EndCaptureDraw(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            CollectDrawables();

            //仅在确实有扭曲源时才进入昂贵的全屏后处理路径
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

        /// <summary>
        /// 单次线性扫描 <see cref="Main.projectile"/> 同时收集图元绘制与扭曲源，
        /// 避免原实现中对弹幕数组的多次重复遍历
        /// </summary>
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

        /// <summary>
        /// 复制屏幕到临时 RT → 在 <see cref="Main.screenTargetSwap"/> 上绘制扭曲采样源 →
        /// 通过 <see cref="EffectLoader.WarpShader"/> 将采样源应用到主 RT → 在 AlphaBlend 层补绘自定义内容
        /// </summary>
        private static void ProcessWarpSets(GraphicsDevice graphicsDevice, RenderTarget2D screen
            , List<IWarpDrawable> warpSets, bool noBlueshift) {
            SpriteBatch sb = Main.spriteBatch;

            //把当前屏幕缓存到临时 RT
            graphicsDevice.SetRenderTarget(screen);
            graphicsDevice.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            sb.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            sb.End();

            //在 swap 缓冲上绘制扭曲源
            graphicsDevice.SetRenderTarget(Main.screenTargetSwap);
            graphicsDevice.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            int warpCount = warpSets.Count;
            for (int i = 0; i < warpCount; i++) {
                warpSets[i].Warp();
            }
            sb.End();

            //把扭曲采样源经着色器应用回主 RT
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

            //允许扭曲源在 AlphaBlend 层补绘不会被扭曲影响的自定义内容
            //先判断有无 CanDrawCustom 命中再 Begin，避免空批次造成多余的 GraphicsDevice 状态切换
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
