using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.Resurrections
{
    /// <summary>
    /// 深渊复苏死亡演出渲染句柄
    /// <br/>从原 <c>EffectLoader</c> 中抽离，专门负责在 <see cref="RenderHandle.EndEntityDraw"/> 阶段
    /// 遍历所有处于死亡演出中的玩家，调用其 <see cref="ResurrectionDeath.DrawDeathEffects"/>
    /// </summary>
    internal sealed class ResurrectionDeathRender : RenderHandle
    {
        /// <summary>
        /// 略晚于普通弹幕绘制层，保证死亡演出叠加在弹幕之上
        /// </summary>
        public override float Weight => 1.25f;

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main) {
            //先用便宜的 for 循环过滤一遍，避免在没有玩家处于死亡演出时还开启一次空批次
            if (!AnyPlayerInDeathSequence(out int firstIndex)) {
                return;
            }

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Player[] players = Main.player;
            for (int i = firstIndex; i < players.Length; i++) {
                Player player = players[i];
                if (!player.active || player.DeadOrGhost) {
                    continue;
                }
                if (player.TryGetModPlayer(out ResurrectionDeath deathSystem)) {
                    deathSystem.DrawDeathEffects(spriteBatch);
                }
            }

            spriteBatch.End();
        }

        /// <summary>
        /// 快速判断当前是否存在处于死亡演出的玩家，并输出第一个命中下标
        /// <br/>无命中时直接跳过开批次，避免每帧无意义的 GraphicsDevice 状态切换
        /// </summary>
        private static bool AnyPlayerInDeathSequence(out int firstIndex) {
            Player[] players = Main.player;
            for (int i = 0; i < players.Length; i++) {
                Player player = players[i];
                if (!player.active || player.DeadOrGhost) {
                    continue;
                }
                if (player.TryGetModPlayer(out ResurrectionDeath deathSystem) && deathSystem.IsInDeathSequence) {
                    firstIndex = i;
                    return true;
                }
            }
            firstIndex = -1;
            return false;
        }
    }
}
