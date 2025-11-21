using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.Quest.DeploySignaltowers.SignalTower
{
    /// <summary>
    /// 目标点位渲染器
    /// </summary>
    internal class SignalTowerTargetRenderer : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "UI";

        private float animationTimer;
        private float pulseTimer;

        //本地化文本
        public static LocalizedText InRangeText { get; private set; }
        public static LocalizedText TargetCompletedText { get; private set; }
        public static LocalizedText AllCompletedText { get; private set; }

        public override void SetStaticDefaults() {
            InRangeText = this.GetLocalization(nameof(InRangeText), () => "范围内");
            TargetCompletedText = this.GetLocalization(nameof(TargetCompletedText), () => "目标点[NUM]已完成!");
            AllCompletedText = this.GetLocalization(nameof(AllCompletedText), () => "量子纠缠网络已完成!");
        }

        public override void PostUpdateEverything() {
            if (!SignalTowerTargetManager.IsGenerated) {
                return;
            }

            animationTimer += 0.02f;
            pulseTimer += 0.05f;

            if (animationTimer > MathHelper.TwoPi) {
                animationTimer -= MathHelper.TwoPi;
            }
            if (pulseTimer > MathHelper.TwoPi) {
                pulseTimer -= MathHelper.TwoPi;
            }
        }

        public override void PostDrawTiles() {
            if (!SignalTowerTargetManager.IsGenerated) {
                return;
            }

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            Player player = Main.LocalPlayer;

            foreach (SignalTowerTargetPoint point in SignalTowerTargetManager.TargetPoints) {
                if (point.IsCompleted) {
                    continue;
                }

                Vector2 screenPos = point.WorldPosition - Main.screenPosition;
                bool playerInRange = point.IsPlayerInRange(player);

                //绘制范围指示器
                DrawTargetRangeIndicator(Main.spriteBatch, point, screenPos, playerInRange);
            }

            //绘制指向最近目标的箭头
            SignalTowerTargetPoint nearestTarget = SignalTowerTargetManager.GetNearestTarget(player);
            if (nearestTarget != null) {
                bool playerInRange = nearestTarget.IsPlayerInRange(player);
                DrawArrowToTarget(Main.spriteBatch, player, nearestTarget, playerInRange);
            }

            Main.spriteBatch.End();
        }

        /// <summary>
        /// 绘制目标范围指示器
        /// </summary>
        private void DrawTargetRangeIndicator(SpriteBatch sb, SignalTowerTargetPoint point, Vector2 screenPos, bool playerInRange) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //根据玩家是否在范围内调整透明度
            float baseAlpha = playerInRange ? 0.2f : 0.4f;
            float pulseIntensity = playerInRange ? 0.1f : 0.3f;
            float alpha = baseAlpha + pulseIntensity * (float)Math.Sin(pulseTimer);
            Color gridColor = new Color(100, 220, 255) * alpha;

            int rangePixels = point.Range * 16;

            //如果玩家在范围内只绘制网格线的淡化版本
            if (!playerInRange) {
                //绘制网格线
                int gridSpacing = 32;
                for (int x = -rangePixels; x <= rangePixels; x += gridSpacing) {
                    Vector2 lineStart = screenPos + new Vector2(x, -rangePixels);
                    Vector2 lineEnd = screenPos + new Vector2(x, rangePixels);
                    DrawDashedLine(sb, lineStart, lineEnd, gridColor * 0.3f, 2, 8);
                }

                for (int y = -rangePixels; y <= rangePixels; y += gridSpacing) {
                    Vector2 lineStart = screenPos + new Vector2(-rangePixels, y);
                    Vector2 lineEnd = screenPos + new Vector2(rangePixels, y);
                    DrawDashedLine(sb, lineStart, lineEnd, gridColor * 0.3f, 2, 8);
                }
            }

            //绘制边框(玩家在范围内时也保留)
            Color borderColor = new Color(100, 220, 255) * (alpha * (playerInRange ? 1.5f : 1.2f));
            if (playerInRange) {
                //在范围内时边框闪烁绿色
                borderColor = Color.Lerp(borderColor, Color.LimeGreen, 0.5f);
            }

            int borderThickness = 3;

            //上
            sb.Draw(pixel, screenPos + new Vector2(-rangePixels, -rangePixels), null, borderColor, 0f, Vector2.Zero, new Vector2(rangePixels * 2, borderThickness), SpriteEffects.None, 0f);
            //下
            sb.Draw(pixel, screenPos + new Vector2(-rangePixels, rangePixels - borderThickness), null, borderColor, 0f, Vector2.Zero, new Vector2(rangePixels * 2, borderThickness), SpriteEffects.None, 0f);
            //左
            sb.Draw(pixel, screenPos + new Vector2(-rangePixels, -rangePixels), null, borderColor, 0f, Vector2.Zero, new Vector2(borderThickness, rangePixels * 2), SpriteEffects.None, 0f);
            //右
            sb.Draw(pixel, screenPos + new Vector2(rangePixels - borderThickness, -rangePixels), null, borderColor, 0f, Vector2.Zero, new Vector2(borderThickness, rangePixels * 2), SpriteEffects.None, 0f);

            //绘制中心标记
            DrawCenterMarker(sb, screenPos, alpha, playerInRange);

            //绘制状态文本
            if (playerInRange) {
                Vector2 textSize = FontAssets.MouseText.Value.MeasureString(InRangeText.Value);
                Vector2 textPos = screenPos - textSize / 2f + new Vector2(0, -30);
                Utils.DrawBorderString(sb, InRangeText.Value, textPos, Color.LimeGreen * alpha, 1.2f);
            }
        }

        /// <summary>
        /// 绘制中心标记
        /// </summary>
        private void DrawCenterMarker(SpriteBatch sb, Vector2 centerPos, float alpha, bool playerInRange) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            float pulseScale = 1f + 0.3f * (float)Math.Sin(pulseTimer * 2f);
            Color markerColor = playerInRange
                ? Color.Lerp(new Color(100, 220, 255), Color.LimeGreen, 0.5f) * alpha
                : new Color(100, 220, 255) * alpha;

            int markerSize = (int)(20 * pulseScale);

            //十字标记
            sb.Draw(pixel, centerPos, null, markerColor, 0f, new Vector2(0.5f, 0.5f), new Vector2(markerSize, 3), SpriteEffects.None, 0f);
            sb.Draw(pixel, centerPos, null, markerColor, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(markerSize, 3), SpriteEffects.None, 0f);

            //圆圈
            int segments = 24;
            float radius = markerSize * 0.7f;
            for (int i = 0; i < segments; i++) {
                float angle1 = MathHelper.TwoPi * i / segments;
                float angle2 = MathHelper.TwoPi * (i + 1) / segments;

                Vector2 p1 = centerPos + angle1.ToRotationVector2() * radius;
                Vector2 p2 = centerPos + angle2.ToRotationVector2() * radius;

                DrawLine(sb, p1, p2, markerColor, 2);
            }
        }

        /// <summary>
        /// 绘制指向目标的箭头
        /// </summary>
        private void DrawArrowToTarget(SpriteBatch sb, Player player, SignalTowerTargetPoint target, bool playerInRange) {
            Vector2 direction = target.WorldPosition - player.Center;
            float distance = direction.Length();

            if (distance < 100f) {
                return;//太近不显示
            }

            direction.Normalize();

            //箭头起点(屏幕边缘)
            Vector2 screenCenter = new(Main.screenWidth / 2f, Main.screenHeight / 2f);
            float arrowDistance = Math.Min(distance / 2f, 200f);
            Vector2 arrowScreenPos = screenCenter + direction * arrowDistance;

            //根据是否在范围内调整颜色
            Color arrowColor = playerInRange
                ? Color.Lerp(new Color(100, 220, 255), Color.LimeGreen, 0.5f)
                : new Color(100, 220, 255);

            float dashAlpha = 0.6f + 0.4f * (float)Math.Sin(animationTimer * 2f);
            Color lineColor = arrowColor * dashAlpha;

            //绘制虚线
            Vector2 lineStart = screenCenter;
            Vector2 lineEnd = arrowScreenPos;
            DrawDashedLine(sb, lineStart, lineEnd, lineColor, 3, 15);

            //绘制箭头
            float arrowRotation = direction.ToRotation();
            DrawArrowHead(sb, arrowScreenPos, arrowRotation, lineColor, 20f);

            //绘制距离文本
            string distanceText = playerInRange ? InRangeText.Value : $"{(int)(distance / 16f)}m";
            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(distanceText);
            Vector2 textPos = arrowScreenPos + direction.RotatedBy(MathHelper.PiOver2) * 20f - textSize / 2f;
            Color textColor = playerInRange ? Color.LimeGreen : Color.White;
            Utils.DrawBorderString(sb, distanceText, textPos, textColor * dashAlpha, 0.9f);
        }

        /// <summary>
        /// 绘制虚线
        /// </summary>
        private void DrawDashedLine(SpriteBatch sb, Vector2 start, Vector2 end, Color color, float thickness, int dashLength) {
            Vector2 direction = end - start;
            float totalLength = direction.Length();
            direction.Normalize();

            float currentLength = 0f;
            bool draw = true;

            while (currentLength < totalLength) {
                float segmentLength = Math.Min(dashLength, totalLength - currentLength);
                if (draw) {
                    Vector2 segStart = start + direction * currentLength;
                    Vector2 segEnd = start + direction * (currentLength + segmentLength);
                    DrawLine(sb, segStart, segEnd, color, thickness);
                }

                currentLength += segmentLength;
                draw = !draw;
            }
        }

        /// <summary>
        /// 绘制直线
        /// </summary>
        private static void DrawLine(SpriteBatch sb, Vector2 start, Vector2 end, Color color, float thickness) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 edge = end - start;
            float length = edge.Length();

            if (length < 1f) {
                return;
            }

            float rotation = edge.ToRotation();
            sb.Draw(pixel, start, null, color, rotation, new Vector2(0, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0f);
        }

        /// <summary>
        /// 绘制箭头头部
        /// </summary>
        private static void DrawArrowHead(SpriteBatch sb, Vector2 position, float rotation, Color color, float size) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //箭头由三条线组成
            Vector2 forward = rotation.ToRotationVector2();
            Vector2 tip = position + forward * size;

            Vector2 left = position + (rotation + MathHelper.Pi * 0.75f).ToRotationVector2() * size * 0.6f;
            Vector2 right = position + (rotation - MathHelper.Pi * 0.75f).ToRotationVector2() * size * 0.6f;

            DrawLine(sb, left, tip, color, 3);
            DrawLine(sb, right, tip, color, 3);
            DrawLine(sb, position, tip, color, 3);
        }
    }
}
