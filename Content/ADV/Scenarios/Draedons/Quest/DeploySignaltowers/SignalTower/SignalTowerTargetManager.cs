using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.Quest.DeploySignaltowers.SignalTower
{
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
        /// 存档格式版本号，发生不兼容变更时递增
        /// </summary>
        private const int SaveDataVersion = 2;

        /// <summary>
        /// 单次存档允许保存的最大点位数，作为越界保护
        /// </summary>
        private const int MaxSaveablePoints = 256;

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

            //发送生成的数据点集
            SendGeneratedPoints(generatedPoints);

            //创建目标点对象
            for (int i = 0; i < generatedPoints.Count; i++) {
                TargetPoints.Add(new SignalTowerTargetPoint(generatedPoints[i], PointRange, i));
            }

            SetIsGenerated();
        }

        /// <summary>
        /// 设置为已生成状态
        /// </summary>
        internal static void SetIsGenerated() {
            IsGenerated = true;
            //标记接受任务
            if (Main.LocalPlayer.TryGetADVSave(out var save)) {
                save.Get<DraedonADVData>().DeploySignaltowerQuestAccepted = true;
            }
        }

        #region NetWork
        internal static void SendGeneratedPoints(List<Point> points) {
            if (VaultUtils.isSinglePlayer) {
                return;
            }
            ModPacket modPacket = CWRMod.Instance.GetPacket();
            modPacket.Write((byte)CWRMessageType.SignalTowerTargetManager);
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

        internal static void NetHandle(CWRMessageType type, BinaryReader reader, int whoAmI) {
            if (type == CWRMessageType.SignalTowerTargetManager) {
                List<Point> points = ReceiveGeneratedPoints(reader);
                if (VaultUtils.isServer) {
                    ModPacket modPacket = CWRMod.Instance.GetPacket();
                    modPacket.Write((byte)CWRMessageType.SignalTowerTargetManager);
                    modPacket.Write(points.Count);
                    for (int i = 0; i < points.Count; i++) {
                        modPacket.Write(points[i].X);
                        modPacket.Write(points[i].Y);
                    }
                    modPacket.Send(-1, whoAmI);
                }
            }
        }
        #endregion

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
        /// 检查并标记点位完成，返回完成的目标点索引
        /// </summary>
        public static int CheckAndMarkCompletionWithIndex(Point towerTilePos) {
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

                    return point.Index;
                }
            }
            return -1;
        }

        /// <summary>
        /// 取消指定位置的目标点完成状态（当信号塔被移除时调用）
        /// </summary>
        public static bool UnmarkCompletion(Point towerTilePos) {
            foreach (SignalTowerTargetPoint point in TargetPoints) {
                if (point.IsCompleted && point.IsInRange(towerTilePos)) {
                    point.IsCompleted = false;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 根据索引取消目标点完成状态
        /// </summary>
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

        /// <summary>
        /// 重置所有目标点
        /// </summary>
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
                    //当未生成时显式写入 false，避免旧存档残留的 true 被错误读取
                    tag["IsGenerated"] = false;
                    return;
                }

                //每个点用单独的 TagCompound 持久化，绑定坐标与完成状态，避免两个并行列表错位
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
            //先把状态彻底归零，任何加载分支异常都不会留下残缺数据
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

                //只有真正读到了点位才算"已生成"，否则保持未生成状态等待重新生成
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

        /// <summary>
        /// 加载新版(V2)存档格式，每个点位为独立 TagCompound
        /// </summary>
        private static bool TryLoadFromV2(TagCompound tag) {
            if (!tag.ContainsKey("TargetPointsV2")) {
                return false;
            }

            IList<TagCompound> pointTags = tag.GetList<TagCompound>("TargetPointsV2");
            if (pointTags == null || pointTags.Count == 0) {
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
                    //坐标非法直接跳过该点位，避免后续越界
                    continue;
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

        /// <summary>
        /// 兼容旧版双列表格式的加载逻辑
        /// </summary>
        private static bool TryLoadFromLegacy(TagCompound tag) {
            if (!tag.ContainsKey("TargetPositions") || !tag.ContainsKey("TargetCompletions")) {
                return false;
            }

            IList<Point> positions = tag.GetList<Point>("TargetPositions");
            IList<bool> completions = tag.GetList<bool>("TargetCompletions");

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

        /// <summary>
        /// 检查图格坐标是否落在当前世界范围内
        /// </summary>
        private static bool IsValidTilePosition(int x, int y) {
            //世界尚未初始化时 maxTilesX/Y 可能为 0，此时仅放行明显合法的非负坐标
            if (Main.maxTilesX <= 0 || Main.maxTilesY <= 0) {
                return x >= 0 && y >= 0;
            }

            return x >= 0 && y >= 0 && x < Main.maxTilesX && y < Main.maxTilesY;
        }

        public override void ClearWorld() => Reset();
    }
}
