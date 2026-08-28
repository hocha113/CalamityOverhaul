using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.Rendering
{
    /// <summary>水膜泡绘制参数,由弹幕逐帧给出</summary>
    internal struct SeaShrimpBubbleBodyParams
    {
        public Vector2 Center;
        public float Radius;
        public float Wobble;
        public float Arm;
        public float Burst;
        public float Fade;
        public float Seed;
    }

    /// <summary>泡类弹幕实现此接口即被统一绘制层接管;返回 false 表示本帧不画泡体</summary>
    internal interface ISeaShrimpBubbleBody
    {
        bool GetBubbleBody(out SeaShrimpBubbleBodyParams body);
    }

    /// <summary>
    /// 海虾水膜泡统一绘制:一次 Immediate 批画完全场泡体,逐泡换参 + Apply
    /// (泡幕一波 18+ 颗,逐弹拆合批次的开销不可接受,合同镜像 FishronBubbleRender)。
    /// DrawBeforePlayers 每帧被 BehindNPCs 与主玩家层各触发一次,
    /// 用 DrawAfterTiles 上膛 + 首次消费闩锁保证只画一次,首次触发落在 NPC 层之下。
    /// 着色器缺失时此层静默,弹幕 PreDraw 回退精灵绘制
    /// </summary>
    internal class SeaShrimpBubbleRender : RenderHandle
    {
        /// <summary>在场帧戳:泡类弹幕 AI 里盖,无泡时跳过全表扫描</summary>
        internal static ActivityStamp PresenceStamp;

        private static bool armed;

        public override void DrawAfterTiles(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap)
            => armed = true;

        public override void DrawBeforePlayers(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (!armed || Main.gameMenu || Main.dedServ || !SeaShrimpVFX.BubblePathReady) {
                return;
            }
            armed = false;
            if (!PresenceStamp.ActiveWithin()) {
                return;
            }

            //先探一遍有没有活泡,空场不开批
            bool any = false;
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.ModProjectile is ISeaShrimpBubbleBody) {
                    any = true;
                    break;
                }
            }
            if (!any) {
                return;
            }

            Effect fx = EffectLoader.FishronBubble.Value;
            fx.Parameters["uTint"]?.SetValue(SeaShrimpVFX.Film.ToVector3());
            fx.Parameters["uDeepColor"]?.SetValue(SeaShrimpVFX.Deep.ToVector3());

            graphicsDevice.Textures[1] = CWRAsset.PerlinNoise.Value;
            graphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;

            Texture2D pixel = VaultAsset.placeholder2.Value;
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);

            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.ModProjectile is not ISeaShrimpBubbleBody bubble
                    || !bubble.GetBubbleBody(out SeaShrimpBubbleBodyParams p)
                    || !OnScreen(p.Center, p.Radius + 160f)) {
                    continue;
                }
                SeaShrimpVFX.DrawBubbleInBatch(spriteBatch, fx, pixel, in p);
            }

            spriteBatch.End();
        }

        private static bool OnScreen(Vector2 worldPos, float pad) {
            Vector2 screen = worldPos - Main.screenPosition;
            return screen.X > -pad && screen.X < Main.screenWidth + pad
                && screen.Y > -pad && screen.Y < Main.screenHeight + pad;
        }
    }
}
