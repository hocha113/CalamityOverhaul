using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.Wraiths.Deaths
{
    /// <summary>
    /// 夺身死亡演出 RenderHandle：遍历所有玩家绘制演出，旁观者可见完整表现。<br/>
    /// 余韵在玩家死亡后继续，故不跳过死亡玩家。
    /// </summary>
    internal sealed class WraithDeathRender : RenderHandle
    {
        /// <summary>权重 1.25，晚于普通弹幕层</summary>
        public override float Weight => 1.25f;

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main) {
            if (!AnySeizureActive(out int firstIndex)) {
                return;
            }

            Player[] players = Main.player;
            //先走裸设备图元层（斩痕/血臂类 shader 三角带）
            GraphicsDevice device = Main.instance.GraphicsDevice;
            for (int i = firstIndex; i < players.Length; i++) {
                Player player = players[i];
                if (!player.active) {
                    continue;
                }
                if (player.TryGetModPlayer(out WraithRevivalDeathPlayer seizure)) {
                    seizure.DrawPerformancePrimitive(device);
                }
            }

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            for (int i = firstIndex; i < players.Length; i++) {
                Player player = players[i];
                if (!player.active) {
                    continue;
                }
                if (player.TryGetModPlayer(out WraithRevivalDeathPlayer seizure)) {
                    seizure.DrawPerformance(spriteBatch);
                }
            }

            spriteBatch.End();
        }

        private static bool AnySeizureActive(out int firstIndex) {
            Player[] players = Main.player;
            for (int i = 0; i < players.Length; i++) {
                Player player = players[i];
                if (!player.active) {
                    continue;
                }
                if (player.TryGetModPlayer(out WraithRevivalDeathPlayer seizure) && seizure.Active) {
                    firstIndex = i;
                    return true;
                }
            }
            firstIndex = -1;
            return false;
        }
    }
}
