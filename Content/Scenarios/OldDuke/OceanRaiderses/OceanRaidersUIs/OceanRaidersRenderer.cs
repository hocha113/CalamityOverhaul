using CalamityOverhaul.Content.UIs.StorageUIs;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.OldDuke.OceanRaiderses.OceanRaidersUIs
{
    /// <summary>海洋吞噬者UI渲染</summary>
    internal class OceanRaidersRenderer : BaseChestRenderer
    {
        private readonly OceanRaidersAnimation themeAnimation;
        private OceanRaidersTP machine;

        protected override int PanelWidth => 760;
        protected override int PanelHeight => 780;
        protected override int StorageStartX => 20;
        protected override int StorageStartY => 90;

        public OceanRaidersRenderer(Player player, IChestStorage storage, OceanRaidersTP machine,
            OceanRaidersAnimation animation, ChestInteraction interaction)
            : base(player, storage, animation, interaction) {
            themeAnimation = animation;
            this.machine = machine;
        }

        public void UpdateMachine(OceanRaidersTP newMachine) {
            machine = newMachine;
        }

        protected override string GetTitleText() => OceanRaidersUI.TitleText.Value;
        protected override string GetStorageText() => OceanRaidersUI.StorageText.Value;
        protected override Color GetFooterColor() => new Color(140, 170, 75);
        protected override Color GetSlotBackgroundColor() => new Color(20, 28, 15);
        protected override Color GetSlotHoverBackgroundColor() => new Color(60, 80, 40);
        protected override Color GetSlotBorderColor() => new Color(80, 100, 45);
        protected override Color GetSlotHoverBorderColor() => new Color(140, 170, 70);
        protected override Color GetCloseButtonHoverColor() => new Color(180, 80, 60);
        protected override Color GetCloseButtonNormalColor() => new Color(120, 140, 60);

        protected override void DrawBackgroundEffects(SpriteBatch spriteBatch, IChestEffects effects) {
            effects.DrawEffects(spriteBatch, animation.UIAlpha * 0.6f);
        }

        protected override void DrawForegroundEffects(SpriteBatch spriteBatch, IChestEffects effects) {
            effects.DrawEffects(spriteBatch, animation.UIAlpha * 0.4f);
        }

        protected override void DrawMainPanel(SpriteBatch spriteBatch, Vector2 panelPosition) {
            Rectangle panelRect = new Rectangle(
                (int)panelPosition.X, (int)panelPosition.Y, PanelWidth, PanelHeight);

            Texture2D pixel = VaultAsset.placeholder2.Value;

            Rectangle shadow = panelRect;
            shadow.Offset(6, 8);
            spriteBatch.Draw(pixel, shadow, new Rectangle(0, 0, 1, 1), Color.Black * (animation.UIAlpha * 0.60f));

            //渐变背景
            DrawGradientBackground(spriteBatch, panelRect, pixel);
            DrawToxicWaveOverlay(spriteBatch, panelRect, pixel);

            //内部发光
            float pulse = (float)Math.Sin(themeAnimation.SulfurPulse * 1.4f) * 0.5f + 0.5f;
            float miasmaGlow = (float)Math.Sin(themeAnimation.MiasmaTimer * 0.8f) * 0.5f + 0.5f;
            Rectangle inner = panelRect;
            inner.Inflate(-6, -6);
            spriteBatch.Draw(pixel, inner, new Rectangle(0, 0, 1, 1),
                new Color(80, 100, 35) * (animation.UIAlpha * 0.12f * (0.35f + pulse * 0.4f + miasmaGlow * 0.25f)));

            DrawSulfseaFrame(spriteBatch, panelRect, pulse, pixel);
        }

        protected override void DrawHeader(SpriteBatch spriteBatch, Vector2 panelPosition) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            string title = GetTitleText();
            Vector2 titleSize = font.MeasureString(title);
            Vector2 titlePos = panelPosition + new Vector2(PanelWidth / 2 - titleSize.X / 2 * 1.1f, 15);

            //发光效果
            Color titleGlow = new Color(160, 190, 80) * (animation.UIAlpha * 0.75f);
            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.TwoPi * i / 4f;
                Vector2 offset = angle.ToRotationVector2() * 2f;
                Utils.DrawBorderString(spriteBatch, title, titlePos + offset, titleGlow * 0.6f, 1.1f);
            }

            Utils.DrawBorderString(spriteBatch, title, titlePos, Color.White * animation.UIAlpha, 1.1f);
        }

        protected override void DrawHeaderDivider(SpriteBatch spriteBatch, Vector2 panelPosition) {
            Vector2 divStart = panelPosition + new Vector2(20, 55);
            Vector2 divEnd = divStart + new Vector2(PanelWidth - 40, 0);
            DrawGradientLine(spriteBatch, divStart, divEnd,
                new Color(100, 140, 50) * (animation.UIAlpha * 0.9f),
                new Color(100, 140, 50) * (animation.UIAlpha * 0.08f), 1.3f);
        }

        protected override void DrawStorageSlot(SpriteBatch spriteBatch, Vector2 position, int index,
            bool isHovered, float hoverProgress, DynamicSpriteFont font) {
            base.DrawStorageSlot(spriteBatch, position, index, isHovered, hoverProgress, font);

            //额外的悬停tooltip
            Item item = storage.GetItem(index);
            if (isHovered && hoverProgress > 0.5f && item != null && item.type > ItemID.None && item.stack > 0) {
                DrawItemTooltip(spriteBatch, item, position + new Vector2(SlotSize / 2, -20));
            }
        }

        private void DrawItemTooltip(SpriteBatch spriteBatch, Item item, Vector2 position) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            string itemName = item.Name;
            Vector2 textSize = font.MeasureString(itemName) * 0.7f;
            Vector2 tooltipPos = position - new Vector2(textSize.X / 2, 0);

            //背景
            Rectangle bgRect = new Rectangle(
                (int)(tooltipPos.X - 4),
                (int)(tooltipPos.Y - 2),
                (int)(textSize.X + 8),
                (int)(textSize.Y + 4)
            );
            Texture2D pixel = VaultAsset.placeholder2.Value;
            spriteBatch.Draw(pixel, bgRect, new Rectangle(0, 0, 1, 1),
                new Color(15, 20, 10) * (animation.UIAlpha * 0.9f));

            //文本
            Utils.DrawBorderString(spriteBatch, itemName, tooltipPos,
                Color.White * animation.UIAlpha, 0.7f);
        }

        private void DrawGradientBackground(SpriteBatch spriteBatch, Rectangle panelRect, Texture2D pixel) {
            int segments = 30;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = panelRect.Y + (int)(t * panelRect.Height);
                int y2 = panelRect.Y + (int)(t2 * panelRect.Height);
                Rectangle r = new(panelRect.X, y1, panelRect.Width, Math.Max(1, y2 - y1));

                Color sulfurDeep = new Color(12, 18, 8);
                Color toxicMid = new Color(28, 38, 15);
                Color acidEdge = new Color(65, 85, 30);
                float breathing = (float)Math.Sin(themeAnimation.SulfurPulse) * 0.5f + 0.5f;
                Color blendBase = Color.Lerp(sulfurDeep, toxicMid,
                    (float)Math.Sin(themeAnimation.SulfurPulse * 0.5f + t * 1.4f) * 0.5f + 0.5f);
                Color c = Color.Lerp(blendBase, acidEdge, t * 0.7f * (0.3f + breathing * 0.7f));
                c *= animation.UIAlpha * 0.92f;
                spriteBatch.Draw(pixel, r, new Rectangle(0, 0, 1, 1), c);
            }

            float miasmaEffect = (float)Math.Sin(themeAnimation.MiasmaTimer * 1.1f) * 0.5f + 0.5f;
            Color miasmaTint = new Color(45, 55, 20) * (animation.UIAlpha * 0.4f * miasmaEffect);
            spriteBatch.Draw(pixel, panelRect, new Rectangle(0, 0, 1, 1), miasmaTint);
        }

        private void DrawToxicWaveOverlay(SpriteBatch spriteBatch, Rectangle rect, Texture2D pixel) {
            int bands = 10;
            for (int i = 0; i < bands; i++) {
                float t = i / (float)bands;
                float y = rect.Y + 18 + t * (rect.Height - 36);
                float amp = 9f + (float)Math.Sin((themeAnimation.ToxicWavePhase + t) * 2.5f) * 6.5f;
                float thickness = 1.6f + (float)Math.Sin(themeAnimation.AcidFlowTimer + i * 0.7f) * 1.4f;
                int segs = 50;
                Vector2 prev = Vector2.Zero;
                for (int s = 0; s <= segs; s++) {
                    float p = s / (float)segs;
                    float localY = y + (float)Math.Sin(themeAnimation.ToxicWavePhase * 2.5f + p * MathHelper.TwoPi * 1.5f + t * 1.2f) * amp;
                    localY += (float)Math.Sin(themeAnimation.AcidFlowTimer * 1.8f + p * 3f + i) * 2.5f;
                    Vector2 point = new(rect.X + 8 + p * (rect.Width - 16), localY);
                    if (s > 0) {
                        Vector2 diff = point - prev;
                        float len = diff.Length();
                        if (len > 0.01f) {
                            float rot = diff.ToRotation();
                            float bandAlpha = 0.10f + (float)Math.Sin(themeAnimation.MiasmaTimer + i * 0.4f) * 0.045f;
                            Color c = new Color(55, 85, 28) * (animation.UIAlpha * bandAlpha);
                            spriteBatch.Draw(pixel, prev, new Rectangle(0, 0, 1, 1), c, rot,
                                Vector2.Zero, new Vector2(len, thickness), SpriteEffects.None, 0f);
                        }
                    }
                    prev = point;
                }
            }
        }

        private static void DrawSulfseaFrame(SpriteBatch spriteBatch, Rectangle rect, float pulse, Texture2D pixel) {
            float alpha = 0.85f;
            Color edge = Color.Lerp(new Color(70, 100, 35), new Color(130, 160, 65), pulse) * alpha;

            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), new Rectangle(0, 0, 1, 1), edge);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), new Rectangle(0, 0, 1, 1), edge * 0.75f);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.88f);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.88f);

            Rectangle inner = rect;
            inner.Inflate(-5, -5);
            Color innerC = new Color(140, 170, 70) * (alpha * 0.22f * pulse);
            spriteBatch.Draw(pixel, new Rectangle(inner.X, inner.Y, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerC);
            spriteBatch.Draw(pixel, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerC * 0.7f);
            spriteBatch.Draw(pixel, new Rectangle(inner.X, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerC * 0.88f);
            spriteBatch.Draw(pixel, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerC * 0.88f);

            float starPulse = 0.55f + pulse * 0.45f;
            DrawCornerStar(spriteBatch, new Vector2(rect.X + 10, rect.Y + 10), alpha * 0.9f * starPulse);
            DrawCornerStar(spriteBatch, new Vector2(rect.Right - 10, rect.Y + 10), alpha * 0.9f * starPulse);
            DrawCornerStar(spriteBatch, new Vector2(rect.X + 10, rect.Bottom - 10), alpha * 0.65f * starPulse);
            DrawCornerStar(spriteBatch, new Vector2(rect.Right - 10, rect.Bottom - 10), alpha * 0.65f * starPulse);
        }

        private static void DrawCornerStar(SpriteBatch spriteBatch, Vector2 pos, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float size = 6.5f;
            Color c = new Color(160, 190, 80) * alpha;
            spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), c, 0f,
                new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.26f), SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), c * 0.8f, MathHelper.PiOver2,
                new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.26f), SpriteEffects.None, 0f);
        }

        private static void DrawGradientLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end,
            Color startColor, Color endColor, float thickness) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 edge = end - start;
            float length = edge.Length();
            if (length < 1f) return;
            edge.Normalize();
            float rotation = (float)Math.Atan2(edge.Y, edge.X);
            int segs = Math.Max(1, (int)(length / 11f));
            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                Vector2 segPos = start + edge * (length * t);
                float segLength = length / segs;
                Color color = Color.Lerp(startColor, endColor, t);
                spriteBatch.Draw(pixel, segPos, new Rectangle(0, 0, 1, 1), color, rotation,
                    new Vector2(0, 0.5f), new Vector2(segLength, thickness), SpriteEffects.None, 0);
            }
        }
    }
}
