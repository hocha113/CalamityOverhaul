using System.Collections.Generic;
using System.Linq;
using Terraria;
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
}
