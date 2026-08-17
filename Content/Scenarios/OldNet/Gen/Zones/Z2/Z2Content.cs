using CalamityOverhaul.Content.Scenarios.OldNet.Gen.Prefabs;
using CalamityOverhaul.Content.Scenarios.OldNet.Gen.Rooms;
using CalamityOverhaul.Content.Scenarios.OldNet.NPCs;
using CalamityOverhaul.Content.Scenarios.OldNet.Tiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Gen.Zones.Z2
{
    //Z2 废墟带：中继站/封锁区（P30裁决锚位，这里建造）+ 机柜房prefab +
    //浅层/深层机房群 + 浮空板。遗址主产区
    internal static class Z2Content
    {
        internal static void PlanAndBuild(OldNetBuildContext ctx) {
            BuildRelays();
            BuildSealBoxes();
            //prefab先落位（优先级），挂房链随后填余量
            int prefabs = StampPrefabRooms(ctx);
            //地表目录（组数集中 Metrics）：墓地/断桥（M3）+ 方舟/冷却塔（本轮扩容）
            int graves = Z2Rooms.BuildServerGraveyards(ctx, OldNetMetrics.GraveyardCount);
            int bridges = Z2Rooms.BuildBrokenBridges(ctx, OldNetMetrics.BrokenBridgeCount);
            int arks = Z2Rooms.BuildDataArks(ctx, OldNetMetrics.DataArkCount);
            int stacks = Z2Rooms.BuildCoolantStacks(ctx, OldNetMetrics.CoolantStackCount);
            OldNetZoneCommon.PlaceFloatingSlabs(ctx.Area.Left + 20, ctx.Area.Right - 20,
                55, 95, Z2Style.FloorBrick);
            int rooms = OldNetZoneCommon.HangRoomsForBand(ctx, 2,
                Z2Style.RoomBrick, Z2Style.RoomWall, roomsPerLanding: 4, nodeChance: 0.5f);
            rooms += BuildDeepRooms(ctx);
            //带界立牌：西缘一块告示（引导语义，Dungeonworld PlaceSign 先例）
            OldNetZoneCommon.PlaceBoundarySign(ctx.Area.Left + 4, OldNetTexts.OldNetSignRuin.Value);
            CWRMod.Instance.Logger.Info(
                $"[OldNet] Z2 rooms={rooms} prefabs={prefabs} graves={graves} bridges={bridges}"
                + $" arks={arks} stacks={stacks} graphConnected={ctx.Graph.IsConnected()}");
        }

        //──────────── 中继站：P30 锚位落tile，锚位被占就近扫空位 ────────────

        private static void BuildRelays() {
            int relayType = ModContent.TileType<OldNetRelayTile>();
            int placed = 0;
            int[] floorTop = OldNetPlans.FloorTop;
            foreach (Point spot in OldNetPlans.RelaySpots) {
                for (int dx = 0; dx < 12; dx++) {
                    int x = spot.X + dx;
                    if (OldNetNodeBudget.WriteNodeTile(x, floorTop[x] - 1, relayType)) {
                        placed++;
                        break;
                    }
                }
            }
            CWRMod.Instance.Logger.Info($"[OldNet] relays placed={placed}/{OldNetPlans.RelaySpots.Count}");
        }

        //──────────── 封锁区：P30 裁决盒位，双侧闸门封死，盒内高密度加密节点 ────────────

        private static void BuildSealBoxes() {
            int encryptType = ModContent.TileType<OldNetEncryptedNodeTile>();
            foreach (Rectangle box in OldNetPlans.SealBoxes) {
                BuildSealBox(box, encryptType);
            }
            CWRMod.Instance.Logger.Info(
                $"[OldNet] seal boxes={OldNetPlans.SealBoxes.Count} gates={OldNetICEDirector.SealGates.Count}");
        }

        private static void BuildSealBox(Rectangle box, int encryptType) {
            int gateType = ModContent.TileType<OldNetSealGateTile>();
            int right = box.Right - 1;
            int surface = box.Bottom;

            //内腔清空 + 地台找平（起伏地形在盒内拉平到 surface）
            //内腔必须刷墙：密闭盒无墙时背后直接透出天幕（露天空 bug 成员）
            for (int x = box.X; x <= right; x++) {
                for (int y = box.Y + 1; y < surface; y++) {
                    OldNetTileBrush.ClearCell(x, y, Z2Style.RoomWall);
                }
                for (int y = surface; y < surface + 3; y++) {
                    OldNetTileBrush.SetSolid(x, y, TileID.ObsidianBrick);
                }
            }
            //顶盖
            for (int x = box.X; x <= right; x++) {
                OldNetTileBrush.SetSolid(x, box.Y, TileID.ObsidianBrick);
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
                        OldNetTileBrush.SetSolid(x, y, TileID.ObsidianBrick);
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
                if (Main.tile[x, y].HasTile) {
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
                if (OldNetNodeBudget.WriteNodeTile(x, y, encryptType)) {
                    placed++;
                }
            }
        }

        //──────────── prefab 房：浅层平台厅右接（首厅机柜房，次厅数据仓） ────────────

        private static int StampPrefabRooms(OldNetBuildContext ctx) {
            int stamped = 0;
            foreach (OldNetShaft shaft in OldNetPlans.Shafts) {
                if (shaft.Deep || OldNetMetrics.BandIndexForColumn(shaft.Col) != 2) {
                    continue;
                }
                OldNetPrefab prefab = stamped == 0 ? Z2Prefabs.RackRoom : Z2Prefabs.ArchiveRoom;
                if (stamped >= 2) {
                    break;
                }
                int gap = WorldGen.genRand.Next(6, 15);
                int left = shaft.Landing.Right + OldNetMetrics.RoomShellThick + gap;
                int top = shaft.Landing.Bottom - (prefab.Height - 1);
                Rectangle area = prefab.Area(left, top);
                if (!ctx.Grid.TryReserve(area, OldNetMetrics.RoomPadding)) {
                    continue;
                }
                prefab.StampGeometry(left, top, Z2Style.RoomBrick, Z2Style.RoomWall, Z2Style.PlatformFrameY);
                (int placedSlots, int rejected) = prefab.PlaceSlots(left, top);
                //走廊接厅：穿双方壳体
                var corridor = new Rectangle(shaft.Landing.Right - 3,
                    shaft.Landing.Bottom - 3, left + 1 - (shaft.Landing.Right - 3), 3);
                OldNetRoomBuilder.CarveCorridor(corridor.Left, corridor.Right,
                    shaft.Landing.Bottom, Z2Style.RoomWall);
                ctx.Grid.MarkUnchecked(corridor);
                stamped++;
                CWRMod.Instance.Logger.Info(
                    $"[OldNet] prefab {prefab.Name}@({left},{top}) slots={placedSlots} rejected={rejected}");
            }
            return stamped;
        }

        //──────────── 深层机房：深井落点挂大房 ────────────

        private static int BuildDeepRooms(OldNetBuildContext ctx) {
            int built = 0;
            foreach (OldNetShaft shaft in OldNetPlans.Shafts) {
                if (!shaft.Deep || OldNetMetrics.BandIndexForColumn(shaft.Col) != 2) {
                    continue;
                }
                built += OldNetZoneCommon.HangRoomsAt(ctx, shaft.Landing, 2,
                    Z2Style.RoomBrick, Z2Style.RoomWall,
                    new Point(10, 6), new Point(18, 8), nodeChance: 0.75f);
            }
            return built;
        }
    }
}
