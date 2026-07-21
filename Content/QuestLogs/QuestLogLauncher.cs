using CalamityOverhaul.Common;
using CalamityOverhaul.Content.QuestLogs.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.QuestLogs
{
    public class QuestLogLauncher
    {
        public Rectangle IconRect;
        public bool IsHovered;
        private float animTimer;
        private float pulseTimer;
        private float glowIntensity;

        public QuestLogLauncher() {
            animTimer = 0f;
            pulseTimer = 0f;
            glowIntensity = 0f;
        }

        public void Update(Vector2 position, bool isOpen) {
            int iconSize = 48;
            IconRect = new Rectangle((int)position.X, (int)position.Y, iconSize, iconSize);

            IsHovered = IconRect.Contains(Main.MouseScreen.ToPoint());

            animTimer += 0.05f;
            if (animTimer > MathHelper.TwoPi) {
                animTimer -= MathHelper.TwoPi;
            }

            pulseTimer += 0.04f;
            if (pulseTimer > MathHelper.TwoPi) {
                pulseTimer -= MathHelper.TwoPi;
            }

            float targetGlow = (isOpen && IsHovered) ? 1f : 0f;
            glowIntensity = MathHelper.Lerp(glowIntensity, targetGlow, 0.15f);
        }

        public void Draw(SpriteBatch spriteBatch, bool isOpen) {
            if (QuestLog.QuestLogStart == null || QuestLog.QuestLogStart.Value == null) {
                return;
            }

            Texture2D iconTexture = QuestLog.QuestLogStart.Value;

            //帧0关/1开/2开+悬停
            int frameIndex;
            if (!isOpen) {
                frameIndex = 0;
            }
            else if (IsHovered) {
                frameIndex = 2;
            }
            else {
                frameIndex = 1;
            }

            int frameHeight = iconTexture.Height / 3;
            Rectangle sourceRect = new Rectangle(0, frameHeight * frameIndex, iconTexture.Width, frameHeight);

            Vector2 shadowOffset = new Vector2(3, 3);
            Color shadowColor = Color.Black * 0.6f;
            spriteBatch.Draw(iconTexture, new Vector2(IconRect.X, IconRect.Y) + shadowOffset,
                sourceRect, shadowColor, 0f, Vector2.Zero,
                new Vector2((float)IconRect.Width / iconTexture.Width, (float)IconRect.Height / frameHeight),
                SpriteEffects.None, 0f);

            float scale = 1f;
            Color drawColor = Color.White;

            //悬停呼吸
            if (IsHovered) {
                float breathe = (float)Math.Sin(animTimer * 2f) * 0.05f + 1f;
                scale *= breathe;
            }

            Vector2 drawPos = new Vector2(
                IconRect.X + IconRect.Width / 2f,
                IconRect.Y + IconRect.Height / 2f
            );

            spriteBatch.Draw(iconTexture, drawPos, sourceRect, drawColor, 0f,
                new Vector2(iconTexture.Width / 2f, frameHeight / 2f),
                scale, SpriteEffects.None, 0f);

            //开且悬停时外发光
            if (glowIntensity > 0.01f) {
                DrawGlowEffect(spriteBatch, iconTexture, sourceRect, drawPos, scale);
            }

            DrawNotificationBadge(spriteBatch);
        }

        private void DrawNotificationBadge(SpriteBatch spriteBatch) {
            //未领奖励计数
            int unclaimedCount = 0;
            foreach (var quest in QuestNode.AllQuests) {
                if (quest.HasUnclaimedRewards) {
                    unclaimedCount++;
                }
            }

            if (unclaimedCount > 0) {
                string text = unclaimedCount > 99 ? "99+" : unclaimedCount.ToString();
                Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text) * 0.75f;
                float maxDim = Math.Max(textSize.X, textSize.Y);
                float bgSize = Math.Max(20, maxDim + 8);

                //红点右上角
                Vector2 badgeCenter = new Vector2(IconRect.Right - 4, IconRect.Top + 4);
                Rectangle badgeRect = new Rectangle(
                    (int)(badgeCenter.X - bgSize / 2),
                    (int)(badgeCenter.Y - bgSize / 2),
                    (int)bgSize, (int)bgSize);

                DrawShaderBadge(spriteBatch, badgeCenter, bgSize);

                Vector2 textPos = new Vector2(
                    badgeRect.X + badgeRect.Width / 2 - textSize.X / 2,
                    badgeRect.Y + badgeRect.Height / 2 - textSize.Y / 2);
                Utils.DrawBorderString(spriteBatch, text, textPos, Color.White, 0.75f);
            }
        }

        private void DrawShaderBadge(SpriteBatch spriteBatch, Vector2 center, float size) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Effect effect = EffectLoader.NotifBadge?.Value;

            if (effect != null) {
                float drawSize = size * 2f;
                Rectangle drawRect = new Rectangle(
                    (int)(center.X - drawSize / 2),
                    (int)(center.Y - drawSize / 2),
                    (int)drawSize, (int)drawSize);

                effect.Parameters["uTime"]?.SetValue(pulseTimer);
                effect.Parameters["uResolution"]?.SetValue(new Vector2(drawRect.Width, drawRect.Height));

                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                    SamplerState.AnisotropicClamp, DepthStencilState.None,
                    RasterizerState.CullNone, effect, Main.UIScaleMatrix);

                spriteBatch.Draw(px, drawRect, Color.White);

                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                    SamplerState.AnisotropicClamp, DepthStencilState.None,
                    RasterizerState.CullNone, null, Main.UIScaleMatrix);
            }
            else {
                //无着色器降级
                DrawFallbackBadge(spriteBatch, center, size);
            }
        }

        private void DrawFallbackBadge(SpriteBatch spriteBatch, Vector2 center, float size) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float pulse = MathF.Sin(pulseTimer * 2f) * 0.5f + 0.5f;

            float glowSize = size * 1.6f;
            Rectangle glowRect = new Rectangle(
                (int)(center.X - glowSize / 2),
                (int)(center.Y - glowSize / 2),
                (int)glowSize, (int)glowSize);
            spriteBatch.Draw(px, glowRect, new Color(200, 30, 20) * (0.2f + pulse * 0.1f));

            Rectangle mainRect = new Rectangle(
                (int)(center.X - size / 2),
                (int)(center.Y - size / 2),
                (int)size, (int)size);
            //分段渐变立体
            int segs = 6;
            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                float t2 = (i + 1f) / segs;
                int y1 = mainRect.Y + (int)(t * mainRect.Height);
                int y2 = mainRect.Y + (int)(t2 * mainRect.Height);
                float lightFactor = 1f - t * 0.5f;
                Color c = new Color(
                    (int)(220 * lightFactor),
                    (int)(40 * lightFactor),
                    (int)(30 * lightFactor));
                spriteBatch.Draw(px, new Rectangle(mainRect.X, y1, mainRect.Width, Math.Max(1, y2 - y1)), c);
            }

            spriteBatch.Draw(px,
                new Rectangle(mainRect.X + 3, mainRect.Y + 1, mainRect.Width - 6, 2),
                new Color(255, 180, 160) * 0.5f);
        }

        private void DrawGlowEffect(SpriteBatch spriteBatch, Texture2D texture, Rectangle sourceRect, Vector2 position, float baseScale) {
            int glowLayers = 3;
            for (int i = 0; i < glowLayers; i++) {
                float layerScale = baseScale * (1.2f + i * 0.15f);
                float layerAlpha = glowIntensity * (0.4f - i * 0.1f);

                float pulse = (float)Math.Sin(pulseTimer + i * 0.5f) * 0.5f + 0.5f;
                layerAlpha *= pulse;

                Color glowColor = new Color(255, 180, 100) * layerAlpha;

                spriteBatch.Draw(texture, position, sourceRect, glowColor, 0f,
                    new Vector2(texture.Width / 2f, sourceRect.Height / 2f),
                    layerScale, SpriteEffects.None, 0f);
            }
        }

        public void PlayClickSound(bool isOpening) {
            SoundEngine.PlaySound(isOpening ? CWRSound.ButtonZero with { Pitch = 0.1f, Volume = 0.6f } : CWRSound.ButtonZero with { Pitch = -0.1f, Volume = 0.6f });
        }
    }
}
