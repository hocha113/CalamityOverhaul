using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.Scenarios.Himayo.ToriiShrines
{
    /// <summary>
    /// 鸟居拔刀提示。玩家层后画，压过 AfterTiles 的 3D 鸟居合成。<br/>
    /// 刀与模型仍走 Actor AfterTiles，提示单独抬层。
    /// </summary>
    internal sealed class ToriiShrinePromptRender : RenderHandle
    {
        public override void DrawAfterPlayers(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.gameMenu || Main.dedServ || !ShouldDraw()) {
                return;
            }

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            try {
                DrawPrompt(spriteBatch);
            }
            finally {
                spriteBatch.End();
            }
        }

        private static bool ShouldDraw() {
            if (!ToriiShrine.SwordPresentForLocalPlayer() || ToriiShrineActor.PullRiteHolding) {
                return false;
            }
            return ToriiShrine.GetInteractPromptAlpha() > 0.01f;
        }

        /// <summary>柔光衬底+描边文字，锚在刀心上方</summary>
        private static void DrawPrompt(SpriteBatch sb) {
            float alpha = ToriiShrine.GetInteractPromptAlpha();
            Vector2 swordAnchor = ToriiShrine.ShrinePosition + new Vector2(0f, -ToriiShrineActor.SwordCenterHeight);
            Vector2 textPos = swordAnchor - Main.screenPosition + new Vector2(0f, -96f);

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            string hintText = ToriiShrine.GetPromptText();
            Vector2 textSize = font.MeasureString(hintText) * 0.9f;

            Texture2D glow = CWRAsset.SoftGlow.Value;
            float pulse = MathF.Sin(Main.GlobalTimeWrappedHourly * 3f) * 0.5f + 0.5f;

            Vector2 backingScale = new Vector2((textSize.X + 50f) / glow.Width, (textSize.Y + 30f) / glow.Height);
            Color backingColor = new Color(190, 55, 80) with { A = 0 } * (alpha * (0.3f + pulse * 0.12f));
            sb.Draw(glow, textPos, null, backingColor, 0f, glow.Size() / 2f, backingScale, SpriteEffects.None, 0f);

            Color textColor = new Color(255, 228, 232) * alpha;
            Utils.DrawBorderString(sb, hintText, textPos - textSize / 2f, textColor, 0.9f);

            float lineWidth = textSize.X * (0.7f + pulse * 0.25f);
            Vector2 linePos = textPos + new Vector2(0f, textSize.Y / 2f + 6f);
            Color lineColor = new Color(235, 95, 118) with { A = 0 } * (alpha * 0.6f);
            sb.Draw(glow, linePos, null, lineColor, 0f, glow.Size() / 2f
                , new Vector2(lineWidth / glow.Width, 4f / glow.Height), SpriteEffects.None, 0f);
        }
    }
}
