using CalamityOverhaul.Content.UIs.StorageUIs;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.Scenarios.OldDuke.OldDuchests.OldDuchestUIs
{
    /// <summary>
    /// 老箱子UI渲染器 - 木质主题
    /// </summary>
    internal class OldDuchestRenderer : BaseChestRenderer
    {
        private readonly OldDuchestAnimation themeAnimation;

        protected override int PanelWidth => 760;
        protected override int PanelHeight => 560;
        protected override int StorageStartX => 20;
        protected override int StorageStartY => 90;

        public OldDuchestRenderer(Player player, IChestStorage storage,
            OldDuchestAnimation animation, ChestInteraction interaction)
            : base(player, storage, animation, interaction) {
            themeAnimation = animation;
        }

        protected override string GetTitleText() => OldDuchestUI.TitleText.Value;
        protected override string GetStorageText() => OldDuchestUI.StorageText.Value;
        protected override Color GetFooterColor() => new Color(200, 150, 100);
        protected override Color GetSlotBackgroundColor() => new Color(40, 30, 20);
        protected override Color GetSlotHoverBackgroundColor() => new Color(80, 60, 40);
        protected override Color GetSlotBorderColor() => new Color(139, 87, 42);
        protected override Color GetSlotHoverBorderColor() => new Color(200, 150, 100);
        protected override Color GetCloseButtonHoverColor() => new Color(180, 80, 60);
        protected override Color GetCloseButtonNormalColor() => new Color(120, 80, 40);

        protected override void DrawMainPanel(SpriteBatch spriteBatch, Vector2 panelPosition) {
            Rectangle panelRect = new((int)panelPosition.X, (int)panelPosition.Y, PanelWidth, PanelHeight);
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //阴影
            Rectangle shadow = panelRect;
            shadow.Offset(4, 5);
            spriteBatch.Draw(pixel, shadow, new Rectangle(0, 0, 1, 1),
                Color.Black * (animation.UIAlpha * 0.45f));

            //深色木质底色
            spriteBatch.Draw(pixel, panelRect, new Rectangle(0, 0, 1, 1),
                new Color(20, 15, 10) * (animation.UIAlpha * 0.95f));

            //木纹纹路
            DrawWoodGrainLines(spriteBatch, panelRect, pixel);

            //暖光脉冲
            float glowPulse = (float)Math.Sin(themeAnimation.GlowTimer) * 0.5f + 0.5f;
            Rectangle innerGlow = panelRect;
            innerGlow.Inflate(-8, -8);
            spriteBatch.Draw(pixel, innerGlow, new Rectangle(0, 0, 1, 1),
                new Color(160, 100, 40) * (animation.UIAlpha * 0.04f * (0.4f + glowPulse * 0.6f)));

            //边框微光
            float borderPulse = (float)Math.Sin(themeAnimation.DustTimer * 0.8f) * 0.5f + 0.5f;
            Color borderColor = Color.Lerp(
                new Color(100, 62, 30),
                new Color(170, 107, 52),
                borderPulse * 0.4f
            ) * animation.UIAlpha;
            DrawPanelBorder(spriteBatch, panelRect, borderColor, 2);

            //四角微光
            DrawCornerGlow(spriteBatch, panelRect, glowPulse, pixel);
        }

        private void DrawWoodGrainLines(SpriteBatch spriteBatch, Rectangle panelRect, Texture2D pixel) {
            int lineCount = 14;
            for (int i = 0; i < lineCount; i++) {
                float t = (i + 0.5f) / lineCount;
                float y = panelRect.Y + t * panelRect.Height;
                float waveOffset = (float)Math.Sin(themeAnimation.WoodGrainPhase * 2f + i * 0.8f) * 3f;
                float alpha = 0.035f + (float)Math.Sin(themeAnimation.WoodGrainPhase + i * 0.5f) * 0.015f;

                Rectangle lineRect = new(
                    panelRect.X + 6,
                    (int)(y + waveOffset),
                    panelRect.Width - 12,
                    1
                );
                Color lineColor = new Color(139, 87, 42) * (animation.UIAlpha * alpha);
                spriteBatch.Draw(pixel, lineRect, new Rectangle(0, 0, 1, 1), lineColor);
            }
        }

        private void DrawCornerGlow(SpriteBatch spriteBatch, Rectangle rect, float pulse, Texture2D pixel) {
            float size = 5f + pulse * 3f;
            Color glowColor = new Color(200, 140, 60) * (animation.UIAlpha * (0.12f + pulse * 0.10f));

            Vector2[] corners = {
                new(rect.X + 8, rect.Y + 8),
                new(rect.Right - 8, rect.Y + 8),
                new(rect.X + 8, rect.Bottom - 8),
                new(rect.Right - 8, rect.Bottom - 8),
            };

            foreach (var pos in corners) {
                spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), glowColor,
                    0f, new Vector2(0.5f, 0.5f), size, SpriteEffects.None, 0f);
            }
        }

        protected override void DrawHeader(SpriteBatch spriteBatch, Vector2 panelPosition) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            string title = GetTitleText();
            Vector2 titleSize = font.MeasureString(title);
            Vector2 titlePos = panelPosition + new Vector2(PanelWidth / 2 - titleSize.X / 2 * 1.1f, 15);

            //暖色辉光
            float glow = (float)Math.Sin(themeAnimation.GlowTimer * 1.3f) * 0.5f + 0.5f;
            Color titleGlow = new Color(200, 140, 60) * (animation.UIAlpha * 0.5f * (0.5f + glow * 0.5f));
            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.TwoPi * i / 4f;
                Vector2 offset = angle.ToRotationVector2() * 1.5f;
                Utils.DrawBorderString(spriteBatch, title, titlePos + offset, titleGlow * 0.5f, 1.1f);
            }

            Utils.DrawBorderString(spriteBatch, title, titlePos, Color.White * animation.UIAlpha, 1.1f);
        }

        protected override void DrawHeaderDivider(SpriteBatch spriteBatch, Vector2 panelPosition) {
            Vector2 divStart = panelPosition + new Vector2(20, 55);
            Vector2 divEnd = divStart + new Vector2(PanelWidth - 40, 0);

            float shimmer = (float)Math.Sin(themeAnimation.DustTimer * 1.5f) * 0.5f + 0.5f;
            Color divColor = Color.Lerp(
                new Color(100, 62, 30),
                new Color(180, 120, 60),
                shimmer * 0.3f
            ) * animation.UIAlpha;
            DrawLine(spriteBatch, divStart, divEnd, divColor, 1.5f);
        }
    }
}
