using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Tiles.BloodAltars
{
    /// <summary>
    /// 定血月那一帧的屏幕血闪。<br/>
    /// 触发端已经按祭坛与本地玩家的距离做过闸（<see cref="BloodAltarRite"/>），
    /// 这里只负责包络与绘制：全屏暗红覆盖压住画面，落点再补一层加色晕，
    /// 两帧持峰后十四帧退干净，不做常驻暗角
    /// </summary>
    internal sealed class BloodAltarRender : RenderHandle
    {
        /// <summary>权重 1.26，晚于替死血臂(1.24)/亡者演出(1.25)，早于梵钟地表层(1.28)</summary>
        public override float Weight => 1.26f;

        private const int HoldFrames = 2;
        private const int FadeFrames = 14;
        private const float PeakAlpha = 0.34f;

        private static float flash;
        private static int age;
        private static Vector2 focusWorld;

        /// <summary>只应由本地可见的祭坛调用</summary>
        public static void Trigger(Vector2 worldPos) {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }
            focusWorld = worldPos;
            age = 0;
            flash = 1f;
        }

        public override void UpdateBySystem(int index) {
            if (Main.gameMenu) {
                flash = 0f;
                age = 0;
                return;
            }
            if (flash <= 0f) {
                return;
            }

            age++;
            flash = age <= HoldFrames
                ? 1f
                : Math.Max(0f, 1f - (age - HoldFrames) / (float)FadeFrames);
        }

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main
            , GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.dedServ || flash <= 0.004f) {
                return;
            }

            Texture2D pixel = VaultAsset.placeholder2?.Value;
            if (pixel != null) {
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp
                    , DepthStencilState.None, RasterizerState.CullNone);
                spriteBatch.Draw(pixel, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight)
                    , BloodAltarFx.ColWet * (flash * PeakAlpha));
                spriteBatch.End();
            }

            Texture2D ring = CWRAsset.DiffusionCircle?.Value;
            if (ring == null) {
                return;
            }

            //落点加色晕：告诉玩家这一下是从哪儿来的，不是无源的白闪
            Vector2 screen = focusWorld - Main.screenPosition;
            float grow = 1f + (1f - flash) * 1.6f;
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp
                , DepthStencilState.None, RasterizerState.CullNone, null
                , Main.GameViewMatrix.TransformationMatrix);
            spriteBatch.Draw(ring, screen, null, BloodAltarFx.ColDeep * (flash * 0.85f), 0f
                , ring.Size() * 0.5f, 2.1f * grow, SpriteEffects.None, 0f);
            spriteBatch.Draw(ring, screen, null, BloodAltarFx.ColWet * (flash * 0.55f), 0f
                , ring.Size() * 0.5f, 0.9f * grow, SpriteEffects.None, 0f);
            spriteBatch.End();
        }
    }
}
