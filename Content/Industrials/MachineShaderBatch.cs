using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.Industrials
{
    /// <summary>
    /// PreTileDraw 层同着色器合批，屏内实体单次 Begin/End，差异走顶点色，共享 uniform 只设一次
    /// </summary>
    internal static class MachineShaderBatch
    {
        //复用缓冲；合批器顺序调用不嵌套
        private static readonly List<TileProcessor> buffer = [];

        /// <summary>收集屏内 match 的 TP，整批 shaderAsset 单次绘制；空着色器直接返回</summary>
        public static void DrawBatch(SpriteBatch spriteBatch, Asset<Effect> shaderAsset, SamplerState sampler,
            Func<TileProcessor, bool> match, Action<Effect> applyShared, Action<TileProcessor> drawOne) {
            if (Main.dedServ) {
                return;
            }
            Effect effect = shaderAsset?.Value;
            if (effect == null) {
                return;//着色器缺失，各机器自行回退
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
