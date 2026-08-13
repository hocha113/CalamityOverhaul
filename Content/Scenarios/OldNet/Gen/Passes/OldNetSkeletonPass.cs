using CalamityOverhaul.Content.Scenarios.OldNet.NPCs;
using CalamityOverhaul.Content.Scenarios.OldNet.Tiles;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Gen.Passes
{
    //M0 骨架：清场+边界+黑墙体+横向地板带+浮空平台+锚点+数据节点撒布
    internal class OldNetSkeletonPass : GenPass
    {
        //出生点附近保持全平，保证终端与落点稳定（常量本体在 OldNetMetrics，ICE 撒布共用）
        private const int FlatCols = OldNetMetrics.SpawnFlatCols;
        //浮空平台参数
        private const int PlatformGapMin = 55;
        private const int PlatformGapMax = 95;

        private static long solidWrites;
        private static long clearWrites;

        public OldNetSkeletonPass() : base("OldNet Skeleton", 1f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            progress.Message = "重建旧网数据平原...";
            solidWrites = clearWrites = 0;

            int width = Main.maxTilesX;
            int height = Main.maxTilesY;
            int fadeLeft = OldNetMetrics.WallCols + OldNetMetrics.FootCols + OldNetMetrics.RuinCols;

            //逐列地板顶行：出生区全平，其余随机游走起伏
            int[] floorTop = new int[width];
            int wobble = 0;
            for (int x = 0; x < width; x++) {
                if (x < OldNetMetrics.WallCols + FlatCols) {
                    wobble = 0;
                }
                else {
                    wobble = System.Math.Clamp(wobble + WorldGen.genRand.Next(-1, 2),
                        -OldNetMetrics.FloorWobble, OldNetMetrics.FloorWobble);
                }
                floorTop[x] = OldNetMetrics.FloorRow + wobble;
            }

            for (int x = 0; x < width; x++) {
                progress.Set(x / (double)(width - 1) * 0.8);
                bool sideBorder = x < OldNetMetrics.BorderThick || x >= width - OldNetMetrics.BorderThick;
                bool wallBody = x < OldNetMetrics.WallCols;
                bool fadeBody = x >= fadeLeft;
                ushort brick = OldNetMetrics.BandForColumn(x)?.FloorBrick ?? TileID.ObsidianBrick;

                for (int y = 0; y < height; y++) {
                    bool topBottomBorder = y < OldNetMetrics.BorderThick || y >= height - OldNetMetrics.BorderThick;
                    bool solid = sideBorder || topBottomBorder
                        || wallBody || fadeBody
                        || y >= floorTop[x];
                    if (solid) {
                        //边界与墙体统一黑曜石砖，地板用带表砖色
                        ushort type = wallBody || fadeBody || sideBorder || topBottomBorder
                            ? TileID.ObsidianBrick : brick;
                        SetSolid(x, y, type);
                    }
                    else {
                        ClearCell(x, y);
                    }
                }
            }

            PlacePlatforms(floorTop, fadeLeft);
            PlaceAnchors(floorTop);
            //封锁区与中继站先于节点撒布，避免撒进砖盒内壁
            List<Rectangle> sealBoxes = PlaceSealBoxes(floorTop);
            PlaceRelays(floorTop);
            ScatterDataNodes(floorTop, fadeLeft, sealBoxes);
            ScatterEventNodes(floorTop, fadeLeft, sealBoxes);

            //本子世界任务表没有原版收尾帧修，必须自框
            progress.Message = "校准数据平原帧序...";
            WorldGen.RangeFrame(0, 0, width - 1, height - 1);
            progress.Set(1.0);

            CWRMod.Instance.Logger.Info(
                $"[OldNet] Skeleton solid={solidWrites} air={clearWrites} macroSeed={OldNetMetrics.MacroSeed}");
        }

        //浮空几何平台：赛博空间理应是直角的
        private static void PlacePlatforms(int[] floorTop, int fadeLeft) {
            int x = OldNetMetrics.WallCols + FlatCols + 30;
            while (x < fadeLeft - 20) {
                int slabWidth = WorldGen.genRand.Next(4, 10);
                int lift = WorldGen.genRand.Next(12, 43);
                int y = floorTop[x] - lift;
                ushort brick = OldNetMetrics.BandForColumn(x)?.FloorBrick ?? TileID.GrayBrick;
                for (int i = 0; i < slabWidth && x + i < fadeLeft - 8; i++) {
                    SetSolid(x + i, y, brick);
                    //双层厚度，读作悬浮板而不是细线
                    SetSolid(x + i, y + 1, brick);
                }
                x += slabWidth + WorldGen.genRand.Next(PlatformGapMin, PlatformGapMax + 1);
            }
        }

        //出生点与登出终端
        private static void PlaceAnchors(int[] floorTop) {
            Main.spawnTileX = OldNetMetrics.SpawnX;
            Main.spawnTileY = floorTop[OldNetMetrics.SpawnX];

            int terminalX = OldNetMetrics.LogoutX;
            int terminalY = floorTop[terminalX] - 1;
            Tile tile = Main.tile[terminalX, terminalY];
            tile.HasTile = true;
            tile.TileType = (ushort)ModContent.TileType<OldNetLogoutTerminalTile>();
            tile.TileFrameX = 0;
            tile.TileFrameY = 0;
        }

        //──────────── 封锁区：废墟带砖盒，双侧闸门封死，盒内高密度加密节点 ────────────

        //盒锚位避开中继站基准列（RelayCols 1000/1400 ± 抖动），错开放 820/1180
        private static readonly int[] sealBoxAnchorCols = [820, 1180];

        private static List<Rectangle> PlaceSealBoxes(int[] floorTop) {
            //每次重生成重登记（ShouldSave=false 回放制，残留登记=幽灵闸门）
            OldNetICEDirector.SealGates.Clear();
            List<Rectangle> boxes = [];
            int encryptType = ModContent.TileType<OldNetEncryptedNodeTile>();

            for (int b = 0; b < OldNetMetrics.SealBoxCount && b < sealBoxAnchorCols.Length; b++) {
                int cx = sealBoxAnchorCols[b] + WorldGen.genRand.Next(-30, 31);
                int x0 = cx - OldNetMetrics.SealBoxW / 2;
                int surface = floorTop[cx];
                int y0 = surface - OldNetMetrics.SealBoxH;
                Rectangle box = new(x0, y0, OldNetMetrics.SealBoxW, OldNetMetrics.SealBoxH);
                BuildSealBox(box, surface, floorTop, encryptType);
                boxes.Add(box);
            }
            CWRMod.Instance.Logger.Info(
                $"[OldNet] seal boxes={boxes.Count} gates={OldNetICEDirector.SealGates.Count}");
            return boxes;
        }

        private static void BuildSealBox(Rectangle box, int surface, int[] floorTop, int encryptType) {
            int gateType = ModContent.TileType<OldNetSealGateTile>();
            int right = box.Right - 1;

            //内腔清空 + 地台找平（起伏地形在盒内拉平到 surface）
            for (int x = box.X; x <= right; x++) {
                for (int y = box.Y + 1; y < surface; y++) {
                    ClearCell(x, y);
                }
                for (int y = surface; y < surface + 3; y++) {
                    SetSolid(x, y, TileID.ObsidianBrick);
                }
            }
            //顶盖
            for (int x = box.X; x <= right; x++) {
                SetSolid(x, box.Y, TileID.ObsidianBrick);
            }
            //侧壁：上段实心，底部 3 格开口用闸门封死
            int gateTop = surface - 3;
            for (int y = box.Y; y < surface; y++) {
                bool gateRow = y >= gateTop;
                for (int side = 0; side < 2; side++) {
                    int x = side == 0 ? box.X : right;
                    if (gateRow) {
                        Tile tile = Main.tile[x, y];
                        tile.HasTile = true;
                        tile.TileType = (ushort)gateType;
                        tile.TileFrameX = 0;
                        tile.TileFrameY = 0;
                        OldNetICEDirector.SealGates.Add(new Point(x, y));
                    }
                    else {
                        SetSolid(x, y, TileID.ObsidianBrick);
                    }
                }
            }

            //盒内高密度加密节点：自选风暴的糖
            int nodeCount = WorldGen.genRand.Next(OldNetMetrics.SealBoxNodeMin,
                OldNetMetrics.SealBoxNodeMax + 1);
            int placed = 0;
            int attempts = 0;
            while (placed < nodeCount && attempts++ < nodeCount * 30) {
                int x = WorldGen.genRand.Next(box.X + 2, right - 1);
                int y = WorldGen.genRand.Next(box.Y + 2, surface);
                Tile slot = Main.tile[x, y];
                if (slot.HasTile) {
                    continue;
                }
                //盒内允许更挤：左右 1 格去重即可
                bool crowded = false;
                for (int dx = -1; dx <= 1 && !crowded; dx++) {
                    Tile near = Main.tile[x + dx, y];
                    crowded = near.HasTile && near.TileType == encryptType;
                }
                if (crowded) {
                    continue;
                }
                slot.HasTile = true;
                slot.TileType = (ushort)encryptType;
                slot.TileFrameX = 0;
                slot.TileFrameY = 0;
                placed++;
            }
        }

        //──────────── 中继站：废墟带 2 座，基准列 ± 抖动 ────────────

        private static void PlaceRelays(int[] floorTop) {
            int relayType = ModContent.TileType<OldNetRelayTile>();
            int placed = 0;
            foreach (int baseCol in OldNetMetrics.RelayCols) {
                int anchor = baseCol + WorldGen.genRand.Next(
                    -OldNetMetrics.RelayColJitter, OldNetMetrics.RelayColJitter + 1);
                //锚位被占就近扫空位
                for (int dx = 0; dx < 16; dx++) {
                    int x = anchor + dx;
                    Tile slot = Main.tile[x, floorTop[x] - 1];
                    if (slot.HasTile) {
                        continue;
                    }
                    slot.HasTile = true;
                    slot.TileType = (ushort)relayType;
                    slot.TileFrameX = 0;
                    slot.TileFrameY = 0;
                    placed++;
                    break;
                }
            }
            CWRMod.Instance.Logger.Info($"[OldNet] relays placed={placed}");
        }

        //──────────── 节点撒布：分带加权 ────────────

        //从天空向下找第一块实心（地板或平台），返回其上方空位行；无效给 -1
        private static int FindNodeSlotY(int[] floorTop, int x) {
            int surfaceY = -1;
            for (int y = OldNetMetrics.BorderThick + 4; y < floorTop[x] + 2; y++) {
                Tile probe = Main.tile[x, y];
                if (probe.HasTile && Main.tileSolid[probe.TileType]) {
                    surfaceY = y;
                    break;
                }
            }
            if (surfaceY < 0 || Main.tile[x, surfaceY - 1].HasTile) {
                return -1;
            }
            return surfaceY - 1;
        }

        //同位置去重：左右 range 格内已有任何节点则拒绝
        private static bool IsCrowded(int x, int y, int range, int typeA, int typeB, int typeC) {
            for (int dx = -range; dx <= range; dx++) {
                Tile near = Main.tile[x + dx, y];
                if (near.HasTile && (near.TileType == typeA
                    || near.TileType == typeB || near.TileType == typeC)) {
                    return true;
                }
            }
            return false;
        }

        private static bool InsideSealBox(List<Rectangle> boxes, int x) {
            foreach (Rectangle box in boxes) {
                if (x >= box.X - 2 && x <= box.Right + 2) {
                    return true;
                }
            }
            return false;
        }

        //普通节点全域撒布 + 加密节点只落废墟带（墙脚带只出普通，废墟带自然混合）
        private static void ScatterDataNodes(int[] floorTop, int fadeLeft, List<Rectangle> sealBoxes) {
            int plainType = ModContent.TileType<OldNetDataNodeTile>();
            int encryptType = ModContent.TileType<OldNetEncryptedNodeTile>();
            int eventType = ModContent.TileType<OldNetEventNodeTile>();
            int ruinLeft = OldNetMetrics.WallCols + OldNetMetrics.FootCols;

            int PlaceBatch(int count, int minX, int type) {
                int placed = 0;
                int attempts = 0;
                while (placed < count && attempts++ < count * 40) {
                    int x = WorldGen.genRand.Next(minX, fadeLeft - 20);
                    if (InsideSealBox(sealBoxes, x)) {
                        continue;
                    }
                    int slotY = FindNodeSlotY(floorTop, x);
                    if (slotY < 0 || IsCrowded(x, slotY, 6, plainType, encryptType, eventType)) {
                        continue;
                    }
                    Tile slot = Main.tile[x, slotY];
                    slot.HasTile = true;
                    slot.TileType = (ushort)type;
                    slot.TileFrameX = 0;
                    slot.TileFrameY = 0;
                    placed++;
                }
                return placed;
            }

            int plain = PlaceBatch(OldNetMetrics.NodePlainCount,
                OldNetMetrics.WallCols + FlatCols, plainType);
            int encrypt = PlaceBatch(OldNetMetrics.NodeEncryptCount, ruinLeft, encryptType);
            CWRMod.Instance.Logger.Info($"[OldNet] data nodes plain={plain} encrypt={encrypt}");
        }

        //事件节点：只落废墟带，离封锁区 ≥ EventToSealMinCols（拉闸的人要跑一段才能吃到糖）
        private static void ScatterEventNodes(int[] floorTop, int fadeLeft, List<Rectangle> sealBoxes) {
            int plainType = ModContent.TileType<OldNetDataNodeTile>();
            int encryptType = ModContent.TileType<OldNetEncryptedNodeTile>();
            int eventType = ModContent.TileType<OldNetEventNodeTile>();
            int ruinLeft = OldNetMetrics.WallCols + OldNetMetrics.FootCols;
            List<int> placedX = [];
            int attempts = 0;

            while (placedX.Count < OldNetMetrics.NodeEventCount
                && attempts++ < OldNetMetrics.NodeEventCount * 60) {
                int x = WorldGen.genRand.Next(ruinLeft, fadeLeft - 20);
                bool tooClose = false;
                foreach (Rectangle box in sealBoxes) {
                    if (Math.Abs(x - (box.X + box.Width / 2)) < OldNetMetrics.EventToSealMinCols) {
                        tooClose = true;
                        break;
                    }
                }
                foreach (int prevX in placedX) {
                    if (Math.Abs(x - prevX) < 40) {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) {
                    continue;
                }
                int slotY = FindNodeSlotY(floorTop, x);
                if (slotY < 0 || IsCrowded(x, slotY, 6, plainType, encryptType, eventType)) {
                    continue;
                }
                Tile slot = Main.tile[x, slotY];
                slot.HasTile = true;
                slot.TileType = (ushort)eventType;
                slot.TileFrameX = 0;
                slot.TileFrameY = 0;
                placedX.Add(x);
            }
            CWRMod.Instance.Logger.Info($"[OldNet] event nodes placed={placedX.Count}");
        }

        private static void SetSolid(int x, int y, ushort type) {
            Tile tile = Main.tile[x, y];
            tile.HasTile = true;
            tile.TileType = type;
            tile.LiquidAmount = 0;
            solidWrites++;
        }

        private static void ClearCell(int x, int y) {
            Tile tile = Main.tile[x, y];
            tile.HasTile = false;
            tile.WallType = WallID.None;
            tile.LiquidAmount = 0;
            clearWrites++;
        }
    }
}
