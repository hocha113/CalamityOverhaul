using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Renders
{
    /// <summary>
    /// 弹幕扩展绘制层渲染句柄
    /// <br/>负责在 <see cref="RenderHandle.EndEntityDraw"/> 阶段：
    /// <list type="number">
    ///     <item>调用所有 <see cref="IPrimitiveDrawable"/> 弹幕在实体层之后的顶点绘制</item>
    ///     <item>使用 <see cref="BlendState.Additive"/> 批次绘制所有 <see cref="IAdditiveDrawable"/> 弹幕</item>
    /// </list>
    /// 单次扫描 <see cref="Main.projectile"/> 同时收集两类目标，并通过预分配缓冲消除每帧 list 分配
    /// </summary>
    internal sealed class ProjectileLayerRender : RenderHandle
    {
        private static readonly List<IPrimitiveDrawable> _primitiveBuffer = new(64);
        private static readonly List<IAdditiveDrawable> _additiveBuffer = new(64);

        /// <summary>
        /// 与原 <c>EffectLoader</c> 保持一致的权重，控制相对其他渲染句柄的执行次序
        /// </summary>
        public override float Weight => 1.2f;

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main) {
            CollectDrawables();

            //图元层：交由各自实现自行管理 SpriteBatch 状态
            int primitiveCount = _primitiveBuffer.Count;
            for (int i = 0; i < primitiveCount; i++) {
                _primitiveBuffer[i].DrawPrimitives();
            }

            //加色叠加层：仅在确实有内容时才开启批次，省去无意义的 GraphicsDevice 状态切换
            int additiveCount = _additiveBuffer.Count;
            if (additiveCount > 0) {
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointWrap
                    , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                for (int i = 0; i < additiveCount; i++) {
                    _additiveBuffer[i].DrawAdditiveAfterNon(spriteBatch);
                }

                spriteBatch.End();
            }
        }

        /// <summary>
        /// 一次遍历同时填充图元与加色绘制缓冲，避免多次穿过整个弹幕数组
        /// </summary>
        private static void CollectDrawables() {
            _primitiveBuffer.Clear();
            _additiveBuffer.Clear();

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
            }
        }
    }
}
