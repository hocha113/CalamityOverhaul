using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Campsites
{
    /// <summary>
    /// 营地方向指示器
    /// </summary>
    internal class CampsiteDirectionIndicator : ModSystem
    {
        private static bool shouldShow;
        private static float indicatorAlpha;
        private static float pulseTimer;
        private static float wavePhase;
        private static float glowTimer;

        //动画参数
        private const float FadeSpeed = 0.08f;
        private const float MaxAlpha = 0.95f;

        public override void PostUpdatePlayers() {
            UpdateIndicatorState();
            UpdateAnimations();
        }

        /// <summary>
        /// 更新指示器状态
        /// </summary>
        private static void UpdateIndicatorState() {
            Player player = Main.LocalPlayer;
            if (player == null || !player.active) {
                shouldShow = false;
                return;
            }

            //检查玩家是否持有海洋碎片
            bool holdingFragment = false;
            if (player.HeldItem != null && !player.HeldItem.IsAir) {
                holdingFragment = player.HeldItem.type == ModContent.ItemType<Items.Oceanfragments>();
            }

            //检查营地是否已生成
            bool campsiteExists = OldDukeCampsite.IsGenerated;

            //只有持有碎片且营地已生成时才显示
            shouldShow = holdingFragment && campsiteExists;

            //更新透明度
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

        /// <summary>
        /// 更新动画计时器
        /// </summary>
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

        /// <summary>
        /// 绘制指示器
        /// </summary>
        public override void PostDrawInterface(SpriteBatch spriteBatch) {
            if (indicatorAlpha <= 0.01f || !OldDukeCampsite.IsGenerated) {
                return;
            }

            Player player = Main.LocalPlayer;
            if (player == null || !player.active) {
                return;
            }

            Vector2 playerScreenPos = new Vector2(Main.screenWidth / 2 + 300, Main.screenHeight / 2);
            Vector2 campsiteWorldPos = OldDukeCampsite.CampsitePosition;
            Vector2 campsiteScreenPos = campsiteWorldPos - Main.screenPosition;

            //计算方向
            Vector2 directionToCampsite = campsiteWorldPos - player.Center;
            float distance = directionToCampsite.Length();
            
            //如果玩家非常接近营地，不显示指示器
            if (distance < 300f) {
                return;
            }

            directionToCampsite.Normalize();

            //计算箭头起始位置（距离玩家一定距离）
            Vector2 arrowStartOffset = directionToCampsite * 80f;
            Vector2 arrowStartPos = playerScreenPos + arrowStartOffset;

            //绘制指示器
            DrawSulfurIndicator(spriteBatch, arrowStartPos, directionToCampsite, distance);
        }

        /// <summary>
        /// 绘制硫磺海风格的指示器
        /// </summary>
        private static void DrawSulfurIndicator(SpriteBatch spriteBatch, Vector2 position, Vector2 direction, float distance) {
            float rotation = direction.ToRotation();
            
            //脉冲效果
            float pulse = (float)Math.Sin(pulseTimer * 2.2f) * 0.5f + 0.5f;
            float glow = (float)Math.Sin(glowTimer * 1.8f) * 0.5f + 0.5f;

            //绘制外发光
            DrawGlowRing(spriteBatch, position, rotation, pulse, indicatorAlpha);

            //绘制虚线箭头
            DrawDashedArrow(spriteBatch, position, direction, rotation, pulse, indicatorAlpha);

            //绘制箭头头部
            DrawArrowHead(spriteBatch, position, direction, rotation, pulse, glow, indicatorAlpha);

            //绘制距离文字
            DrawDistanceText(spriteBatch, position, direction, distance, indicatorAlpha);

            //绘制装饰性粒子效果
            DrawToxicParticles(spriteBatch, position, rotation, indicatorAlpha);
        }

        #region 绘制组件

        /// <summary>
        /// 绘制外发光环
        /// </summary>
        private static void DrawGlowRing(SpriteBatch spriteBatch, Vector2 position, float rotation, float pulse, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            
            float glowSize = 35f + pulse * 12f;
            Color glowColor = new Color(100, 140, 50) * (alpha * 0.25f * (0.6f + pulse * 0.4f));

            spriteBatch.Draw(
                pixel,
                position,
                new Rectangle(0, 0, 1, 1),
                glowColor,
                0f,
                new Vector2(0.5f),
                new Vector2(glowSize),
                SpriteEffects.None,
                0f
            );
        }

        /// <summary>
        /// 绘制虚线箭头
        /// </summary>
        private static void DrawDashedArrow(SpriteBatch spriteBatch, Vector2 startPos, Vector2 direction, float rotation, float pulse, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            
            float arrowLength = 65f + pulse * 8f;
            int dashCount = 8;
            float dashLength = arrowLength / dashCount;
            float dashGap = dashLength * 0.45f;
            float dashWidth = 2.8f + pulse * 0.6f;

            //硫磺海配色：深绿到黄绿渐变
            Color dashColorStart = new Color(140, 180, 70);
            Color dashColorEnd = new Color(100, 140, 50);

            for (int i = 0; i < dashCount; i++) {
                //添加波动效果
                float waveOffset = (float)Math.Sin(wavePhase + i * 0.4f) * 2f;
                
                float t = i / (float)dashCount;
                float segmentStart = t * arrowLength;
                float actualDashLength = dashLength - dashGap;

                Vector2 dashPos = startPos + direction * segmentStart;
                Vector2 perpendicular = new Vector2(-direction.Y, direction.X);
                dashPos += perpendicular * waveOffset;

                Color dashColor = Color.Lerp(dashColorStart, dashColorEnd, t);
                dashColor *= alpha * (0.85f + pulse * 0.15f);

                //绘制虚线段
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

                //绘制虚线段的发光效果
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

        /// <summary>
        /// 绘制箭头头部
        /// </summary>
        private static void DrawArrowHead(SpriteBatch spriteBatch, Vector2 startPos, Vector2 direction, float rotation, float pulse, float glow, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            
            float arrowLength = 65f + pulse * 8f;
            Vector2 arrowTipPos = startPos + direction * arrowLength;

            //箭头头部尺寸
            float headLength = 16f + pulse * 3f;
            float headWidth = 11f + pulse * 2f;

            //主箭头颜色
            Color arrowColor = new Color(140, 180, 70) * (alpha * (0.9f + glow * 0.1f));
            Color arrowGlow = new Color(160, 190, 80) * (alpha * 0.5f * glow);

            //绘制箭头头部发光
            DrawTriangle(spriteBatch, arrowTipPos, rotation, headLength * 1.2f, headWidth * 1.3f, arrowGlow);

            //绘制箭头头部主体
            DrawTriangle(spriteBatch, arrowTipPos, rotation, headLength, headWidth, arrowColor);

            //绘制箭头头部内发光
            Color innerGlow = new Color(200, 220, 100) * (alpha * 0.6f * glow);
            DrawTriangle(spriteBatch, arrowTipPos, rotation, headLength * 0.6f, headWidth * 0.6f, innerGlow);
        }

        /// <summary>
        /// 绘制三角形（箭头头部）
        /// </summary>
        private static void DrawTriangle(SpriteBatch spriteBatch, Vector2 position, float rotation, float length, float width, Color color) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //绘制三角形的三条边
            Vector2 tip = position;
            Vector2 direction = rotation.ToRotationVector2();
            Vector2 perpendicular = new Vector2(-direction.Y, direction.X);

            Vector2 baseCenter = tip - direction * length;
            Vector2 baseLeft = baseCenter - perpendicular * (width * 0.5f);
            Vector2 baseRight = baseCenter + perpendicular * (width * 0.5f);

            //左边
            DrawLine(spriteBatch, tip, baseLeft, color, 2.5f);
            //右边
            DrawLine(spriteBatch, tip, baseRight, color, 2.5f);
            //底边
            DrawLine(spriteBatch, baseLeft, baseRight, color, 2.5f);

            //填充三角形内部
            int segments = 8;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                Vector2 leftPoint = Vector2.Lerp(tip, baseLeft, t);
                Vector2 rightPoint = Vector2.Lerp(tip, baseRight, t);
                DrawLine(spriteBatch, leftPoint, rightPoint, color * 0.6f, 1.5f);
            }
        }

        /// <summary>
        /// 绘制线段
        /// </summary>
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

        /// <summary>
        /// 绘制距离文字
        /// </summary>
        private static void DrawDistanceText(SpriteBatch spriteBatch, Vector2 position, Vector2 direction, float distance, float alpha) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            
            //计算文字位置（在箭头旁边）
            Vector2 perpendicular = new Vector2(-direction.Y, direction.X);
            Vector2 textPos = position + perpendicular * 28f;

            //格式化距离
            int distanceInTiles = (int)(distance / 16f);
            string distanceText = $"{distanceInTiles}m";
            string locationText = OldDukeCampsite.TitleText.Value;

            Vector2 distanceSize = font.MeasureString(distanceText) * 0.7f;
            Vector2 locationSize = font.MeasureString(locationText) * 0.75f;

            //绘制背景
            float padding = 8f;
            Rectangle distanceBg = new Rectangle(
                (int)(textPos.X - padding),
                (int)(textPos.Y - padding),
                (int)(distanceSize.X + padding * 2),
                (int)(distanceSize.Y + padding * 2)
            );
            Rectangle locationBg = new Rectangle(
                (int)(textPos.X - padding),
                (int)(textPos.Y - padding + distanceSize.Y + 4),
                (int)(locationSize.X + padding * 2),
                (int)(locationSize.Y + padding * 2)
            );

            Texture2D pixel = VaultAsset.placeholder2.Value;
            
            //硫磺海风格背景
            Color bgColor = new Color(12, 18, 8) * (alpha * 0.85f);
            Color borderColor = new Color(100, 140, 50) * (alpha * 0.75f);

            //距离背景
            spriteBatch.Draw(pixel, distanceBg, new Rectangle(0, 0, 1, 1), bgColor);
            DrawRectBorder(spriteBatch, distanceBg, borderColor, 2);

            //位置背景
            spriteBatch.Draw(pixel, locationBg, new Rectangle(0, 0, 1, 1), bgColor);
            DrawRectBorder(spriteBatch, locationBg, borderColor, 2);

            //绘制文字
            Color textColor = new Color(200, 220, 150) * alpha;
            Color glowColor = new Color(140, 180, 70) * (alpha * 0.6f);

            //距离文字
            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.TwoPi * i / 4f;
                Vector2 offset = angle.ToRotationVector2() * 1.5f;
                Utils.DrawBorderString(spriteBatch, distanceText, textPos + offset, glowColor * 0.5f, 0.7f);
            }
            Utils.DrawBorderString(spriteBatch, distanceText, textPos, textColor, 0.7f);

            //位置文字
            Vector2 locationTextPos = textPos + new Vector2(0, distanceSize.Y + 4);
            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.TwoPi * i / 4f;
                Vector2 offset = angle.ToRotationVector2() * 1.5f;
                Utils.DrawBorderString(spriteBatch, locationText, locationTextPos + offset, glowColor * 0.5f, 0.75f);
            }
            Utils.DrawBorderString(spriteBatch, locationText, locationTextPos, textColor, 0.75f);
        }

        /// <summary>
        /// 绘制矩形边框
        /// </summary>
        private static void DrawRectBorder(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //上
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), new Rectangle(0, 0, 1, 1), color);
            //下
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), new Rectangle(0, 0, 1, 1), color);
            //左
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), new Rectangle(0, 0, 1, 1), color);
            //右
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), new Rectangle(0, 0, 1, 1), color);
        }

        /// <summary>
        /// 绘制毒性粒子装饰效果
        /// </summary>
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

            //绘制围绕箭头的小粒子
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

            //绘制拖尾粒子效果
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
