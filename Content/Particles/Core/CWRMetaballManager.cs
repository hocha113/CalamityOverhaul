using CalamityMod;
using CalamityMod.Graphics.Metaballs;
using CalamityOverhaul.Common.Effects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Particles.Core
{
    internal class CWRMetaballManager : ModSystem
    {
        internal static readonly List<CWRMetaball> metaballs = new();

        public override void OnModLoad() {
            // 挂载更新钩子
            RenderTargetManager.RenderTargetUpdateLoopEvent += PrepareMetaballTargets;
            On_Main.DrawProjectiles += DrawMetaballsAfterProjectiles;
            On_Main.DrawNPCs += DrawMetaballsBeforeNPCs;
        }

        public override void OnModUnload() {
            // 在mod卸载时清除GPU上所有未管理的元球目标资源
            Main.QueueMainThreadAction(() => {
                foreach (CWRMetaball metaball in metaballs)
                    metaball?.Dispose();
            });
        }

        public override void OnWorldUnload() {
            foreach (CWRMetaball metaball in metaballs)
                metaball.ClearInstances();
        }

        private void PrepareMetaballTargets() {
            // 获取当前正在使用的所有元球的列表
            var activeMetaballs = metaballs.Where(m => m.AnythingToDraw);

            // 如果元球目前不使用，不要浪费资源
            if (!activeMetaballs.Any())
                return;

            // 为绘图准备纹理。元球可以通过PrepareSpriteBatch重新启动精灵批处理，如果它们的实现需要的话
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, Main.Rasterizer, null, Matrix.Identity);

            var gd = Main.instance.GraphicsDevice;
            foreach (CWRMetaball metaball in activeMetaballs) {
                // 更新元球集合
                if (!Main.gamePaused)
                    metaball.Update();

                // 根据元球实例的需要准备精灵批。默认情况下，这不会做任何事情
                metaball.PrepareSpriteBatch(Main.spriteBatch);

                // 将元球的原始内容绘制到它的每个呈现目标，填装到着色域中
                foreach (ManagedRenderTarget target in metaball.LayerTargets) {
                    gd.SetRenderTarget(target);
                    gd.Clear(Color.Transparent);
                    metaball.DrawInstances();

                    // 刷新元球内容到其渲染目标，并为下一次迭代重置精灵批处理
                    Main.spriteBatch.End();
                    Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, Main.Rasterizer, null, Matrix.Identity);
                }
            }

            // 返回backbuffer并结束精灵批处理
            gd.SetRenderTarget(null);
            Main.spriteBatch.End();
        }

        private void DrawMetaballsAfterProjectiles(On_Main.orig_DrawProjectiles orig, Main self) {
            if (AnyActiveMetaballsAtLayer(MetaballDrawLayer.BeforeProjectiles)) {
                Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointWrap, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                DrawMetaballs(MetaballDrawLayer.BeforeProjectiles);
                Main.spriteBatch.End();
            }

            orig(self);

            if (AnyActiveMetaballsAtLayer(MetaballDrawLayer.AfterProjectiles)) {
                Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointWrap, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                DrawMetaballs(MetaballDrawLayer.AfterProjectiles);
                Main.spriteBatch.End();
            }
        }

        private void DrawMetaballsBeforeNPCs(On_Main.orig_DrawNPCs orig, Main self, bool behindTiles) {
            if (!behindTiles && AnyActiveMetaballsAtLayer(MetaballDrawLayer.BeforeNPCs)) {
                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointWrap, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                DrawMetaballs(MetaballDrawLayer.BeforeNPCs);
                Main.spriteBatch.ExitShaderRegion();
            }
            orig(self, behindTiles);
        }

        /// <summary>
        /// 检查给定层次类型的元球当前是否正在使用。这用于在没有要绘制的内容时最小化不必要的<see cref="SpriteBatch"/>重新启动
        /// </summary>
        /// <param name="layerType">要检查的元球层次类型。</param>
        internal static bool AnyActiveMetaballsAtLayer(MetaballDrawLayer layerType) =>
            metaballs.Any(m => m.AnythingToDraw && m.DrawContext == layerType);

        /// <summary>
        /// 绘制给定<see cref="MetaballDrawLayer"/>的所有元球。用于图层排序的原因
        /// </summary>
        /// <param name="layerType">要绘制的层次类型。</param>
        public static void DrawMetaballs(MetaballDrawLayer layerType) {
            foreach (CWRMetaball metaball in metaballs.Where(m => m.DrawContext == layerType && m.AnythingToDraw)) {
                for (int i = 0; i < metaball.LayerTargets.Count; i++) {
                    // 为给定的图层目标准备着色器
                    metaball.PrepareShaderForTarget(i);

                    // 用着色器绘制元球的原始内容
                    Main.spriteBatch.Draw(metaball.LayerTargets[i], Main.screenLastPosition - Main.screenPosition, Color.White);
                }
            }
        }
    }
}
