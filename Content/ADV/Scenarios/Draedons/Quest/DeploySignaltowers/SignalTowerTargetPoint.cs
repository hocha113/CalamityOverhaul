using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.Quest.DeploySignaltowers
{
    /// <summary>
    /// 信号塔目标点数据
    /// </summary>
    public class SignalTowerTargetPoint
    {
        /// <summary>
        /// 目标点位置(图格坐标)
        /// </summary>
        public Point TilePosition { get; set; }

        /// <summary>
        /// 有效范围(图格单位)
        /// </summary>
        public int Range { get; set; }

        /// <summary>
        /// 是否已完成
        /// </summary>
        public bool IsCompleted { get; set; }

        /// <summary>
        /// 点位索引
        /// </summary>
        public int Index { get; set; }

        public SignalTowerTargetPoint(Point position, int range, int index) {
            TilePosition = position;
            Range = range;
            IsCompleted = false;
            Index = index;
        }

        /// <summary>
        /// 检查指定位置是否在范围内
        /// </summary>
        public bool IsInRange(Point tilePos) {
            float distance = Vector2.Distance(TilePosition.ToVector2(), tilePos.ToVector2());
            return distance <= Range;
        }

        /// <summary>
        /// 检查玩家是否在范围内
        /// </summary>
        public bool IsPlayerInRange(Player player) {
            Point playerTilePos = player.Center.ToTileCoordinates();
            return IsInRange(playerTilePos);
        }

        /// <summary>
        /// 获取世界坐标
        /// </summary>
        public Vector2 WorldPosition => TilePosition.ToVector2() * 16f;
    }

    /// <summary>
    /// 信号塔目标点管理器
    /// </summary>
    internal class SignalTowerTargetManager : ModSystem
    {
        /// <summary>
        /// 所有目标点
        /// </summary>
        public static List<SignalTowerTargetPoint> TargetPoints { get; private set; } = [];

        /// <summary>
        /// 目标点数量
        /// </summary>
        public const int TargetPointCount = 10;

        /// <summary>
        /// 每个点位的有效范围(图格)
        /// </summary>
        public const int PointRange = 50;

        /// <summary>
        /// 点位之间的最小距离(图格)
        /// </summary>
        public const int MinDistanceBetweenPoints = 200;

        /// <summary>
        /// 是否已生成目标点
        /// </summary>
        public static bool IsGenerated { get; private set; }

        /// <summary>
        /// 获取离玩家最近的未完成目标点
        /// </summary>
        public static SignalTowerTargetPoint GetNearestTarget(Player player) {
            SignalTowerTargetPoint nearest = null;
            float minDistance = float.MaxValue;

            foreach (SignalTowerTargetPoint point in TargetPoints) {
                if (point.IsCompleted) {
                    continue;
                }

                float distance = Vector2.Distance(player.Center, point.WorldPosition);
                if (distance < minDistance) {
                    minDistance = distance;
                    nearest = point;
                }
            }

            return nearest;
        }

        /// <summary>
        /// 生成目标点位
        /// </summary>
        public static void GenerateTargetPoints() {
            TargetPoints.Clear();

            //获取世界尺寸
            int worldWidth = Main.maxTilesX;
            int worldHeight = Main.maxTilesY;

            //定义可用区域(避开地狱和天空)
            int minY = (int)(worldHeight * 0.15f);//避开太高
            int maxY = (int)(worldHeight * 0.85f);//避开地狱
            int minX = (int)(worldWidth * 0.1f);
            int maxX = (int)(worldWidth * 0.9f);

            //已生成的点位
            List<Point> generatedPoints = [];

            int attempts = 0;
            int maxAttempts = 10000;

            while (generatedPoints.Count < TargetPointCount && attempts < maxAttempts) {
                attempts++;

                //随机生成候选点
                int x = Main.rand.Next(minX, maxX);
                int y = Main.rand.Next(minY, maxY);
                Point candidate = new(x, y);

                //检查是否在安全区域(不在液体中,有足够空间)
                if (!IsSafeLocation(candidate)) {
                    continue;
                }

                //检查与已有点位的距离
                bool tooClose = false;
                foreach (Point existingPoint in generatedPoints) {
                    float distance = Vector2.Distance(candidate.ToVector2(), existingPoint.ToVector2());
                    if (distance < MinDistanceBetweenPoints) {
                        tooClose = true;
                        break;
                    }
                }

                if (tooClose) {
                    continue;
                }

                //添加到列表
                generatedPoints.Add(candidate);
            }

            //如果生成失败,使用均匀分布
            if (generatedPoints.Count < TargetPointCount) {
                generatedPoints.Clear();
                int segmentWidth = (maxX - minX) / (TargetPointCount / 2);
                int segmentHeight = (maxY - minY) / 2;

                for (int i = 0; i < TargetPointCount; i++) {
                    int row = i / (TargetPointCount / 2);
                    int col = i % (TargetPointCount / 2);

                    int x = minX + col * segmentWidth + segmentWidth / 2;
                    int y = minY + row * segmentHeight + segmentHeight / 2;

                    generatedPoints.Add(new Point(x, y));
                }
            }

            //创建目标点对象
            for (int i = 0; i < generatedPoints.Count; i++) {
                TargetPoints.Add(new SignalTowerTargetPoint(generatedPoints[i], PointRange, i));
            }

            IsGenerated = true;
        }

        /// <summary>
        /// 检查位置是否安全
        /// </summary>
        private static bool IsSafeLocation(Point tilePos) {
            //检查是否有足够的空间(6x14区域)
            for (int x = -3; x < 3; x++) {
                for (int y = -7; y < 7; y++) {
                    int checkX = tilePos.X + x;
                    int checkY = tilePos.Y + y;

                    if (!WorldGen.InWorld(checkX, checkY)) {
                        return false;
                    }

                    Tile tile = Framing.GetTileSafely(checkX, checkY);

                    //避开液体
                    if (tile.LiquidAmount > 0) {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// 检查并标记点位完成
        /// </summary>
        public static bool CheckAndMarkCompletion(Point towerTilePos) {
            foreach (SignalTowerTargetPoint point in TargetPoints) {
                if (!point.IsCompleted && point.IsInRange(towerTilePos)) {
                    point.IsCompleted = true;

                    //播放完成效果
                    SignalTowerCompletionEffects.PlayCompletionEffect(point.WorldPosition, point.Index);

                    //检查是否全部完成
                    bool allCompleted = true;
                    foreach (SignalTowerTargetPoint p in TargetPoints) {
                        if (!p.IsCompleted) {
                            allCompleted = false;
                            break;
                        }
                    }

                    if (allCompleted) {
                        SignalTowerCompletionEffects.PlayAllCompletionEffect();
                    }

                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 重置所有目标点
        /// </summary>
        public static void Reset() {
            TargetPoints.Clear();
            IsGenerated = false;
        }

        public override void SaveWorldData(TagCompound tag) {
            if (!IsGenerated) {
                return;
            }

            List<Point> positions = [];
            List<bool> completions = [];

            foreach (SignalTowerTargetPoint point in TargetPoints) {
                positions.Add(point.TilePosition);
                completions.Add(point.IsCompleted);
            }

            tag["TargetPositions"] = positions;
            tag["TargetCompletions"] = completions;
            tag["IsGenerated"] = IsGenerated;
        }

        public override void LoadWorldData(TagCompound tag) {
            TargetPoints.Clear();

            if (!tag.TryGet("IsGenerated", out bool generated) || !generated) {
                IsGenerated = false;
                return;
            }

            List<Point> positions = tag.GetList<Point>("TargetPositions").ToList();
            List<bool> completions = tag.GetList<bool>("TargetCompletions").ToList();

            for (int i = 0; i < positions.Count && i < completions.Count; i++) {
                SignalTowerTargetPoint point = new(positions[i], PointRange, i) {
                    IsCompleted = completions[i]
                };
                TargetPoints.Add(point);
            }

            IsGenerated = true;
        }

        public override void ClearWorld() {
            Reset();
        }
    }

    /// <summary>
    /// 目标点位渲染器
    /// </summary>
    internal class SignalTowerTargetRenderer : ModSystem
    {
        private float animationTimer;
        private float pulseTimer;

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

            SpriteBatch spriteBatch = Main.spriteBatch;
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

            Player player = Main.LocalPlayer;

            foreach (SignalTowerTargetPoint point in SignalTowerTargetManager.TargetPoints) {
                if (point.IsCompleted) {
                    continue;
                }

                Vector2 screenPos = point.WorldPosition - Main.screenPosition;
                bool playerInRange = point.IsPlayerInRange(player);

                //绘制范围指示器
                DrawTargetRangeIndicator(spriteBatch, point, screenPos, playerInRange);
            }

            //绘制指向最近目标的箭头
            SignalTowerTargetPoint nearestTarget = SignalTowerTargetManager.GetNearestTarget(player);
            if (nearestTarget != null) {
                bool playerInRange = nearestTarget.IsPlayerInRange(player);
                DrawArrowToTarget(spriteBatch, player, nearestTarget, playerInRange);
            }

            spriteBatch.End();
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
                string inRangeText = "范围内";
                Vector2 textSize = FontAssets.MouseText.Value.MeasureString(inRangeText);
                Vector2 textPos = screenPos - textSize / 2f + new Vector2(0, -30);
                Utils.DrawBorderString(sb, inRangeText, textPos, Color.LimeGreen * alpha, 1.2f);
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
            string distanceText = playerInRange ? "范围内" : $"{(int)(distance / 16f)}m";
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
