using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Scenarios.Draedon.Quest.DeploySignaltowers.SignalTower
{
    internal class SignalTowerTargetManager : ModSystem
    {
        public static List<SignalTowerTargetPoint> TargetPoints { get; private set; } = [];

        public const int TargetPointCount = 10;
        public const int PointRange = 50;//图格
        public const int MinDistanceBetweenPoints = 200;//图格

        public static bool IsGenerated { get; private set; }

        /// <summary>存档格式版本,不兼容时递增</summary>
        private const int SaveDataVersion = 2;

        /// <summary>单次存档最大点位,越界保护</summary>
        private const int MaxSaveablePoints = 256;

        /// <summary>最近未完成目标点</summary>
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

        public static void GenerateTargetPoints() {
            TargetPoints.Clear();

            int worldWidth = Main.maxTilesX;
            int worldHeight = Main.maxTilesY;

            //避开地狱/天空
            int minY = (int)(worldHeight * 0.15f);
            int maxY = (int)(worldHeight * 0.85f);
            int minX = (int)(worldWidth * 0.1f);
            int maxX = (int)(worldWidth * 0.9f);

            List<Point> generatedPoints = [];

            int attempts = 0;
            int maxAttempts = 10000;

            while (generatedPoints.Count < TargetPointCount && attempts < maxAttempts) {
                attempts++;

                int x = Main.rand.Next(minX, maxX);
                int y = Main.rand.Next(minY, maxY);
                Point candidate = new(x, y);

                if (!IsSafeLocation(candidate)) {
                    continue;
                }

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

                generatedPoints.Add(candidate);
            }

            //均匀分布兜底
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

            SendGeneratedPoints(generatedPoints);

            for (int i = 0; i < generatedPoints.Count; i++) {
                TargetPoints.Add(new SignalTowerTargetPoint(generatedPoints[i], PointRange, i));
            }

            SetIsGenerated();
        }

        internal static void SetIsGenerated() {
            IsGenerated = true;
            if (Main.LocalPlayer.Alives()) {
                DraedonStorySync.WriteDraedon(
                    d => d.DeploySignaltowerQuestAccepted = true,
                    d => d.DeploySignaltowerQuestAccepted = true);
            }
        }

        #region NetWork
        internal static void SendGeneratedPoints(List<Point> points) {
            if (VaultUtils.isSinglePlayer) {
                return;
            }
            ModPacket modPacket = CWRNetWork.GetPacket<SignalTowerTargetNet>();
            modPacket.Write(points.Count);
            for (int i = 0; i < points.Count; i++) {
                modPacket.Write(points[i].X);
                modPacket.Write(points[i].Y);
            }
            modPacket.Send();
        }

        internal static List<Point> ReceiveGeneratedPoints(BinaryReader reader) {
            List<Point> points = [];
            int count = reader.ReadInt32();
            TargetPoints.Clear();
            for (int i = 0; i < count; i++) {
                int x = reader.ReadInt32();
                int y = reader.ReadInt32();
                TargetPoints.Add(new SignalTowerTargetPoint(new Point(x, y), PointRange, i));
                points.Add(new Point(x, y));
            }
            SetIsGenerated();
            return points;
        }

        internal static void HandleNet(BinaryReader reader, int whoAmI) {
            List<Point> points = ReceiveGeneratedPoints(reader);
            if (VaultUtils.isServer) {
                ModPacket modPacket = CWRNetWork.GetPacket<SignalTowerTargetNet>();
                modPacket.Write(points.Count);
                for (int i = 0; i < points.Count; i++) {
                    modPacket.Write(points[i].X);
                    modPacket.Write(points[i].Y);
                }
                modPacket.Send(-1, whoAmI);
            }
        }
        #endregion

        private static bool IsSafeLocation(Point tilePos) {
            //6×14空间
            for (int x = -3; x < 3; x++) {
                for (int y = -7; y < 7; y++) {
                    int checkX = tilePos.X + x;
                    int checkY = tilePos.Y + y;

                    if (!WorldGen.InWorld(checkX, checkY)) {
                        return false;
                    }

                    Tile tile = Framing.GetTileSafely(checkX, checkY);

                    if (tile.LiquidAmount > 0) {
                        return false;
                    }
                }
            }

            return true;
        }

        public static bool CheckAndMarkCompletion(Point towerTilePos) {
            foreach (SignalTowerTargetPoint point in TargetPoints) {
                if (!point.IsCompleted && point.IsInRange(towerTilePos)) {
                    point.IsCompleted = true;

                    SignalTowerCompletionEffects.PlayCompletionEffect(point.WorldPosition, point.Index);

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

        /// <summary>标记完成并返回索引</summary>
        public static int CheckAndMarkCompletionWithIndex(Point towerTilePos) {
            foreach (SignalTowerTargetPoint point in TargetPoints) {
                if (!point.IsCompleted && point.IsInRange(towerTilePos)) {
                    point.IsCompleted = true;

                    SignalTowerCompletionEffects.PlayCompletionEffect(point.WorldPosition, point.Index);

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

                    return point.Index;
                }
            }
            return -1;
        }

        public static bool UnmarkCompletion(Point towerTilePos) {
            foreach (SignalTowerTargetPoint point in TargetPoints) {
                if (point.IsCompleted && point.IsInRange(towerTilePos)) {
                    point.IsCompleted = false;
                    return true;
                }
            }
            return false;
        }

        public static bool UnmarkCompletionByIndex(int index) {
            if (index < 0 || index >= TargetPoints.Count) {
                return false;
            }

            if (TargetPoints[index].IsCompleted) {
                TargetPoints[index].IsCompleted = false;
                return true;
            }

            return false;
        }

        public static void Reset() {
            TargetPoints.Clear();
            IsGenerated = false;
        }

        public override void NetSend(BinaryWriter writer) {
            writer.Write(IsGenerated);
            writer.Write(TargetPoints.Count);
            for (int i = 0; i < TargetPoints.Count; i++) {
                writer.Write(TargetPoints[i].TilePosition.X);
                writer.Write(TargetPoints[i].TilePosition.Y);
                writer.Write(TargetPoints[i].IsCompleted);
            }
        }

        public override void NetReceive(BinaryReader reader) {
            IsGenerated = reader.ReadBoolean();
            TargetPoints.Clear();
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++) {
                int x = reader.ReadInt32();
                int y = reader.ReadInt32();
                bool isCompleted = reader.ReadBoolean();
                SignalTowerTargetPoint point = new(new Point(x, y), PointRange, i) {
                    IsCompleted = isCompleted
                };
                TargetPoints.Add(point);
            }
        }

        public override void SaveWorldData(TagCompound tag) {
            try {
                if (tag == null) {
                    return;
                }

                if (!IsGenerated || TargetPoints == null || TargetPoints.Count == 0) {
                    //未生成写false,防旧档true残留
                    tag["IsGenerated"] = false;
                    return;
                }

                //每点独立TagCompound,防双列表错位
                List<TagCompound> pointTags = new List<TagCompound>(TargetPoints.Count);
                foreach (SignalTowerTargetPoint point in TargetPoints) {
                    if (point == null) {
                        continue;
                    }

                    Point tilePos = point.TilePosition;
                    pointTags.Add(new TagCompound {
                        ["X"] = tilePos.X,
                        ["Y"] = tilePos.Y,
                        ["IsCompleted"] = point.IsCompleted,
                        ["Index"] = point.Index,
                    });

                    if (pointTags.Count >= MaxSaveablePoints) {
                        break;
                    }
                }

                tag["SaveDataVersion"] = SaveDataVersion;
                tag["TargetPointsV2"] = pointTags;
                tag["IsGenerated"] = true;
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error($"[SignalTowerTargetManager:SaveWorldData] an error has occurred:{ex.Message}");
            }
        }

        public override void LoadWorldData(TagCompound tag) {
            //Load前先清零,防残缺数据
            TargetPoints.Clear();
            IsGenerated = false;

            if (tag == null) {
                return;
            }

            try {
                if (!tag.TryGet("IsGenerated", out bool generated) || !generated) {
                    return;
                }

                bool loaded = TryLoadFromV2(tag) || TryLoadFromLegacy(tag);

                //读到点位才算已生成
                if (loaded && TargetPoints.Count > 0) {
                    IsGenerated = true;
                }
                else {
                    TargetPoints.Clear();
                    IsGenerated = false;
                }
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error($"[SignalTowerTargetManager:LoadWorldData] an error has occurred:{ex.Message}");
                TargetPoints.Clear();
                IsGenerated = false;
            }
        }

        /// <summary>V2每点TagCompound</summary>
        private static bool TryLoadFromV2(TagCompound tag) {
            if (!tag.TryGet("TargetPointsV2", out List<TagCompound> pointTags)
                || pointTags == null || pointTags.Count == 0) {
                return false;
            }

            int fallbackIndex = 0;
            foreach (TagCompound pt in pointTags) {
                if (pt == null) {
                    continue;
                }

                if (TargetPoints.Count >= MaxSaveablePoints) {
                    break;
                }

                int x = pt.GetAsInt("X");
                int y = pt.GetAsInt("Y");

                if (!IsValidTilePosition(x, y)) {
                    continue;//坐标非法跳过
                }

                bool isCompleted = pt.GetBool("IsCompleted");
                int savedIndex = pt.ContainsKey("Index") ? pt.GetAsInt("Index") : fallbackIndex;

                TargetPoints.Add(new SignalTowerTargetPoint(new Point(x, y), PointRange, savedIndex) {
                    IsCompleted = isCompleted
                });

                fallbackIndex++;
            }

            return TargetPoints.Count > 0;
        }

        /// <summary>legacy双列表</summary>
        private static bool TryLoadFromLegacy(TagCompound tag) {
            if (!tag.TryGet("TargetPositions", out List<Point> positions)
                || !tag.TryGet("TargetCompletions", out List<bool> completions)) {
                return false;
            }

            if (positions == null || completions == null) {
                return false;
            }

            int count = Math.Min(positions.Count, completions.Count);
            for (int i = 0; i < count && TargetPoints.Count < MaxSaveablePoints; i++) {
                Point pos = positions[i];
                if (!IsValidTilePosition(pos.X, pos.Y)) {
                    continue;
                }

                TargetPoints.Add(new SignalTowerTargetPoint(pos, PointRange, i) {
                    IsCompleted = completions[i]
                });
            }

            return TargetPoints.Count > 0;
        }

        private static bool IsValidTilePosition(int x, int y) {
            //世界未初始化时maxTiles=0,仅放行非负坐标
            if (Main.maxTilesX <= 0 || Main.maxTilesY <= 0) {
                return x >= 0 && y >= 0;
            }

            return x >= 0 && y >= 0 && x < Main.maxTilesX && y < Main.maxTilesY;
        }

        public override void ClearWorld() => Reset();
    }

    /// <summary>信号塔目标点位同步信道</summary>
    internal sealed class SignalTowerTargetNet : CWRNetChannel
    {
        public override void Receive(BinaryReader reader, int whoAmI) => SignalTowerTargetManager.HandleNet(reader, whoAmI);
    }
}
