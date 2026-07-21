using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.Content.Scenarios.OldDuke.Quest;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldDuke.Campsites
{
    /// <summary>营地方向指示</summary>
    internal class CampsiteDirectionIndicator : ModSystem
    {
        private static bool shouldShow;
        private static float indicatorAlpha;
        private static float pulseTimer;
        private static float wavePhase;
        private static float glowTimer;

        private const float FadeSpeed = 0.08f;
        private const float MaxAlpha = 0.95f;

        public override void PostUpdatePlayers() {
            UpdateIndicatorState();
            UpdateAnimations();
        }

        private static void UpdateIndicatorState() {
            Player player = Main.LocalPlayer;
            if (player == null || !player.active) {
                shouldShow = false;
                return;
            }

            var campsiteEntry = QuestManagerUI.Instance?.GetEntry(AbyssQuestLine.CampsiteKey);
            bool questTracked = campsiteEntry != null
                && campsiteEntry.Status == QuestEntryStatus.Tracked;

            bool campsiteExists = OldDukeCampsite.IsGenerated;

            shouldShow = questTracked && campsiteExists;

            if (shouldShow) {
                if (indicatorAlpha < MaxAlpha) {
                    indicatorAlpha += FadeSpeed;
                }
            }
            else {
                if (indicatorAlpha > 0f) {
                    indicatorAlpha -= FadeSpeed * 1.5f;
                }
            }

            indicatorAlpha = MathHelper.Clamp(indicatorAlpha, 0f, MaxAlpha);
        }

        private static void UpdateAnimations() {
            if (indicatorAlpha > 0.01f) {
                pulseTimer += 0.045f;
                wavePhase += 0.038f;
                glowTimer += 0.052f;

                if (pulseTimer > MathHelper.TwoPi) pulseTimer -= MathHelper.TwoPi;
                if (wavePhase > MathHelper.TwoPi) wavePhase -= MathHelper.TwoPi;
                if (glowTimer > MathHelper.TwoPi) glowTimer -= MathHelper.TwoPi;
            }
        }

        public override void PostDrawInterface(SpriteBatch spriteBatch) {
            if (indicatorAlpha <= 0.01f || !OldDukeCampsite.IsGenerated) {
                return;
            }

            Player player = Main.LocalPlayer;
            if (player == null || !player.active) {
                return;
            }

            Vector2 playerScreenPos = new Vector2(Main.screenWidth / 2, Main.screenHeight / 2);
            Vector2 campsiteWorldPos = OldDukeCampsite.CampsitePosition;

            Vector2 directionToCampsite = campsiteWorldPos - player.Center;
            float distance = directionToCampsite.Length();

            //近处不显示
            if (distance < 300f) {
                return;
            }

            directionToCampsite.Normalize();

            Vector2 arrowStartOffset = directionToCampsite * 180f;
            Vector2 arrowStartPos = playerScreenPos + arrowStartOffset;

            DrawSulfurIndicator(spriteBatch, arrowStartPos, directionToCampsite, distance);
        }

        private static void DrawSulfurIndicator(SpriteBatch spriteBatch, Vector2 position, Vector2 direction, float distance) {
            float rotation = direction.ToRotation();

            float pulse = (float)Math.Sin(pulseTimer * 2.2f) * 0.5f + 0.5f;
            float glow = (float)Math.Sin(glowTimer * 1.8f) * 0.5f + 0.5f;

            DrawGlowRing(spriteBatch, position, rotation, pulse, indicatorAlpha);

            DrawDistanceText(spriteBatch, position, distance, indicatorAlpha);

            DrawDashedArrow(spriteBatch, position, direction, rotation, pulse, indicatorAlpha);

            DrawArrowHead(spriteBatch, position, direction, rotation, pulse, glow, indicatorAlpha);

            DrawToxicParticles(spriteBatch, position, rotation, indicatorAlpha);
        }

        #region 绘制组件

        private static void DrawGlowRing(SpriteBatch spriteBatch, Vector2 position, float rotation, float pulse, float alpha) {
            Texture2D pixel = CWRAsset.SoftGlow.Value;

            float glowSize = 5.5f + pulse * 1.2f;
            Color glowColor = new Color(100, 140, 50, 0) * (alpha * 0.25f * (0.6f + pulse * 0.4f));

            spriteBatch.Draw(
                pixel,
                position,
                null,
                glowColor,
                0f,
                pixel.Size() / 2,
                glowSize,
                SpriteEffects.None,
                0f
            );
        }

        private static void DrawDashedArrow(SpriteBatch spriteBatch, Vector2 startPos, Vector2 direction, float rotation, float pulse, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            float arrowLength = 65f + pulse * 8f;
            int dashCount = 8;
            float dashLength = arrowLength / dashCount;
            float dashGap = dashLength * 0.45f;
            float dashWidth = 2.8f + pulse * 0.6f;

            //深绿→黄绿
            Color dashColorStart = new Color(140, 180, 70);
            Color dashColorEnd = new Color(100, 140, 50);

            for (int i = 0; i < dashCount; i++) {
                float waveOffset = (float)Math.Sin(wavePhase + i * 0.4f) * 2f;

                float t = i / (float)dashCount;
                float segmentStart = t * arrowLength;
                float actualDashLength = dashLength - dashGap;

                Vector2 dashPos = startPos + direction * segmentStart;
                Vector2 perpendicular = new Vector2(-direction.Y, direction.X);
                dashPos += perpendicular * waveOffset;

                Color dashColor = Color.Lerp(dashColorStart, dashColorEnd, t);
                dashColor *= alpha * (0.85f + pulse * 0.15f);

                spriteBatch.Draw(
                    pixel,
                    dashPos,
                    new Rectangle(0, 0, 1, 1),
                    dashColor,
                    rotation,
                    new Vector2(0f, 0.5f),
                    new Vector2(actualDashLength, dashWidth),
                    SpriteEffects.None,
                    0f
                );

                Color glowColor = new Color(160, 190, 80) * (alpha * 0.35f * pulse);
                spriteBatch.Draw(
                    pixel,
                    dashPos,
                    new Rectangle(0, 0, 1, 1),
                    glowColor,
                    rotation,
                    new Vector2(0f, 0.5f),
                    new Vector2(actualDashLength, dashWidth + 2f),
                    SpriteEffects.None,
                    0f
                );
            }
        }

        private static void DrawArrowHead(SpriteBatch spriteBatch, Vector2 startPos, Vector2 direction, float rotation, float pulse, float glow, float alpha) {
            float arrowLength = 65f + pulse * 8f;
            Vector2 arrowTipPos = startPos + direction * arrowLength;

            float headLength = 16f + pulse * 3f;
            float headWidth = 11f + pulse * 2f;

            Color arrowColor = new Color(140, 180, 70) * (alpha * (0.9f + glow * 0.1f));
            Color arrowGlow = new Color(160, 190, 80) * (alpha * 0.5f * glow);

            DrawTriangle(spriteBatch, arrowTipPos, rotation, headLength * 1.2f, headWidth * 1.3f, arrowGlow);

            DrawTriangle(spriteBatch, arrowTipPos, rotation, headLength, headWidth, arrowColor);

            Color innerGlow = new Color(200, 220, 100) * (alpha * 0.6f * glow);
            DrawTriangle(spriteBatch, arrowTipPos, rotation, headLength * 0.6f, headWidth * 0.6f, innerGlow);
        }

        private static void DrawTriangle(SpriteBatch spriteBatch, Vector2 position, float rotation, float length, float width, Color color) {
            Vector2 tip = position;
            Vector2 direction = rotation.ToRotationVector2();
            Vector2 perpendicular = new Vector2(-direction.Y, direction.X);

            Vector2 baseCenter = tip - direction * length;
            Vector2 baseLeft = baseCenter - perpendicular * (width * 0.5f);
            Vector2 baseRight = baseCenter + perpendicular * (width * 0.5f);

            DrawLine(spriteBatch, tip, baseLeft, color, 2.5f);
            DrawLine(spriteBatch, tip, baseRight, color, 2.5f);
            DrawLine(spriteBatch, baseLeft, baseRight, color, 2.5f);

            int segments = 8;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                Vector2 leftPoint = Vector2.Lerp(tip, baseLeft, t);
                Vector2 rightPoint = Vector2.Lerp(tip, baseRight, t);
                DrawLine(spriteBatch, leftPoint, rightPoint, color * 0.6f, 1.5f);
            }
        }

        private static void DrawLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color, float thickness) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            Vector2 edge = end - start;
            float length = edge.Length();
            if (length < 0.1f) return;

            float rotation = edge.ToRotation();

            spriteBatch.Draw(
                pixel,
                start,
                new Rectangle(0, 0, 1, 1),
                color,
                rotation,
                new Vector2(0f, 0.5f),
                new Vector2(length, thickness),
                SpriteEffects.None,
                0f
            );
        }

        private static void DrawDistanceText(SpriteBatch spriteBatch, Vector2 position, float distance, float alpha) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;

            Vector2 textPos = position + new Vector2(0, 28);

            int distanceInTiles = (int)(distance / 16f);
            string distanceText = $"{distanceInTiles}m";
            string locationText = OldDukeCampsite.TitleText.Value;

            Vector2 distanceSize = font.MeasureString(distanceText) * 0.7f;
            Vector2 locationSize = font.MeasureString(locationText) * 0.75f;
            Vector2 locationTextPos = textPos + new Vector2(0, distanceSize.Y + 4);

            //柔光衬底，无方框
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float glowPulse = MathF.Sin(Main.GlobalTimeWrappedHourly * 2.2f) * 0.15f + 0.85f;
            Color backingColor = new Color(70, 110, 40) with { A = 0 } * (alpha * 0.45f * glowPulse);

            DrawGlowBacking(spriteBatch, glow, textPos, distanceSize, backingColor);
            DrawGlowBacking(spriteBatch, glow, locationTextPos, locationSize, backingColor);

            Color textColor = new Color(200, 220, 150) * alpha;
            Color glowColor = new Color(140, 180, 70) * (alpha * 0.6f);

            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.TwoPi * i / 4f;
                Vector2 offset = angle.ToRotationVector2() * 1.5f;
                Utils.DrawBorderString(spriteBatch, distanceText, textPos + offset, glowColor * 0.5f, 0.7f);
            }
            Utils.DrawBorderString(spriteBatch, distanceText, textPos, textColor, 0.7f);

            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.TwoPi * i / 4f;
                Vector2 offset = angle.ToRotationVector2() * 1.5f;
                Utils.DrawBorderString(spriteBatch, locationText, locationTextPos + offset, glowColor * 0.5f, 0.75f);
            }
            Utils.DrawBorderString(spriteBatch, locationText, locationTextPos, textColor, 0.75f);
        }

        private static void DrawGlowBacking(SpriteBatch spriteBatch, Texture2D glow, Vector2 textTopLeft, Vector2 textSize, Color color) {
            Vector2 center = textTopLeft + textSize / 2f;
            Vector2 scale = new Vector2((textSize.X + 36f) / glow.Width, (textSize.Y + 20f) / glow.Height);
            spriteBatch.Draw(glow, center, null, color, 0f, glow.Size() / 2f, scale, SpriteEffects.None, 0f);
        }

        private static void DrawToxicParticles(SpriteBatch spriteBatch, Vector2 position, float rotation, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            spriteBatch.Draw(
                    OldDukeCampsite.OldDuke_Head_Boss,
                    position,
                    null,
                    Color.White,
                    0f,
                    OldDukeCampsite.OldDuke_Head_Boss.Size() / 2,
                    1f,
                    SpriteEffects.None,
                    0f
                );

            int particleCount = 5;
            for (int i = 0; i < particleCount; i++) {
                float angle = wavePhase + i * MathHelper.TwoPi / particleCount;
                float distance = 22f + (float)Math.Sin(pulseTimer * 1.5f + i) * 4f;

                Vector2 particlePos = position + angle.ToRotationVector2() * distance;
                float particleSize = 2f + (float)Math.Sin(glowTimer * 2f + i) * 1f;

                Color particleColor = new Color(140, 180, 70) * (alpha * 0.5f);

                spriteBatch.Draw(
                    pixel,
                    particlePos,
                    new Rectangle(0, 0, 1, 1),
                    particleColor,
                    0f,
                    new Vector2(0.5f),
                    new Vector2(particleSize),
                    SpriteEffects.None,
                    0f
                );
            }

            for (int i = 0; i < 3; i++) {
                float trailOffset = -15f - i * 8f;
                Vector2 trailPos = position + rotation.ToRotationVector2() * trailOffset;

                float trailSize = 3f - i * 0.8f;
                float trailAlpha = alpha * (0.4f - i * 0.1f);

                Color trailColor = new Color(100, 140, 50) * trailAlpha;

                spriteBatch.Draw(
                    pixel,
                    trailPos,
                    new Rectangle(0, 0, 1, 1),
                    trailColor,
                    0f,
                    new Vector2(0.5f),
                    new Vector2(trailSize),
                    SpriteEffects.None,
                    0f
                );
            }
        }

        #endregion

        public override void Unload() {
            indicatorAlpha = 0f;
            shouldShow = false;
            pulseTimer = 0f;
            wavePhase = 0f;
            glowTimer = 0f;
        }
    }
}
