using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.Industrials
{
    /// <summary>
    /// 机器着色器合批工具：把同一着色器的所有屏内实体在墙后物块前(PreTileDraw 层)
    /// 合并为单次 <see cref="SpriteBatch.Begin"/>/<see cref="SpriteBatch.End"/>，
    /// 避免逐实体反复切批次、反复重载着色器。逐实体差异通过顶点色传入，整批共享 uniform 只设一次。
    /// 着色器缺失或屏内无目标时不切批次（由各机器自行 CPU 回退），属非破坏性优化。
    /// </summary>
    internal static class MachineShaderBatch
    {
        //复用缓冲，避免每帧分配；各合批器在 PreTileDrawEverything 中顺序调用，不会嵌套
        private static readonly List<TileProcessor> buffer = [];

        /// <summary>
        /// 收集屏内匹配 <paramref name="match"/> 的 TP，整批用 <paramref name="shaderAsset"/> 单次绘制。
        /// </summary>
        /// <param name="spriteBatch">当前 PreTileDraw 批次(以 Main.Transform 起始)</param>
        /// <param name="shaderAsset">合批着色器；为空则直接返回(交由各机器回退)</param>
        /// <param name="sampler">采样状态(像素美术用 Point，纯程序化用 Linear)</param>
        /// <param name="match">实体筛选(类型/精确类型判定)</param>
        /// <param name="applyShared">整批共享参数设置(uTime 等)，只调用一次</param>
        /// <param name="drawOne">单实体绘制(逐实体数据写入顶点色)</param>
        public static void DrawBatch(SpriteBatch spriteBatch, Asset<Effect> shaderAsset, SamplerState sampler,
            Func<TileProcessor, bool> match, Action<Effect> applyShared, Action<TileProcessor> drawOne) {
            if (Main.dedServ) {
                return;
            }
            Effect effect = shaderAsset?.Value;
            if (effect == null) {
                return;//着色器缺失：交由各机器自身 CPU 回退
            }

            buffer.Clear();
            foreach (var tp in TileProcessorLoader.TP_InWorld) {
                if (tp.Active && match(tp)
                    && VaultUtils.IsPointOnScreen(tp.PosInWorld - Main.screenPosition, tp.DrawExtendMode)) {
                    buffer.Add(tp);
                }
            }
            if (buffer.Count == 0) {
                return;
            }

            applyShared(effect);
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, sampler,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.Transform);
            foreach (var tp in buffer) {
                drawOne(tp);
            }
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
        }
    }
}
