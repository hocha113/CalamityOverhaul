using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.Resurrections
{
    /// <summary>复苏死亡演出 RenderHandle，EndEntityDraw 调 DrawDeathEffects</summary>
    internal sealed class ResurrectionDeathRender : RenderHandle
    {
        /// <summary>权重 1.25，晚于普通弹幕层</summary>
        public override float Weight => 1.25f;

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main) {
            //先过滤是否有人处于死亡演出，避免空开批次
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

        /// <summary>是否有死亡演出玩家，<paramref name="firstIndex"/> 为首个命中下标</summary>
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
