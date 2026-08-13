using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Rooms;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L1
{
    //====================================================================
    //L1教堂区内容入口（Wave-1接缝契约，契约全文见LayerBuildContext头注释）。
    //布局分两制：
    //  ·教堂群落（主教堂+前厅+圣器室+上廊+钟楼）=层的演出型唯一建筑，
    //    内联跨脊落位（沿用GaolBossRoom"内联跨脊/后写方"先例），
    //    足印以MarkUnchecked登记为既成事实，正门/后殿口即层脊穿越路径；
    //  ·卫星散房=挂房制（同L2）：地板=SpineInteriorTop-5，含壳+padding贴住
    //    P30脊缓冲带，经ctx.Grid正常预留；链边门对门/拱对拱，落口楼梯井下探层脊。
    //撒布装修按契约纪律5声明进ctx.Scatter，由P55统一执行。
    //====================================================================
    internal static class L1Content
    {
        //===布局表（全部相对教堂左缘=主竖井对齐推导，预览可整体搬移）===
        //教堂群落含扩建占x∈[-24,+160]，卫星窗口避开其膨胀足印与上廊足印
        private const int SafeAOffMin = -292, SafeAOffMax = -236;
        private const int ConfAOffMin = -226, ConfAOffMax = -176;
        private const int ChandleryOffMin = -166, ChandleryOffMax = -112;
        private const int CloisterWOffMin = -110, CloisterWOffMax = -50;
        private const int CloisterEOffMin = 166, CloisterEOffMax = 236;
        private const int ConfBOffMin = 244, ConfBOffMax = 292;
        private const int StairheadOffMin = 300, StairheadOffMax = 348;
        private const int SafeBOffMin = 356, SafeBOffMax = 416;

        private enum NodeKind { SafeRoom, Confessional, Chandlery, Cloister, Stairhead }

        private sealed class PlacedNode
        {
            internal RoomNode Room;
            internal NodeKind Kind;
            //真门房：链边到它的口部放门板（安全房语义）
            internal bool WantsDoorPlate;
            //落口楼梯井（下探层脊）
            internal bool WantsSpineDrop;
        }

        /// <summary>
        /// L1教堂区一条龙构建：教堂群落内联盖章→卫星挂房落位→装修→链边→脊落口→撒布声明。
        /// <para/>A路一行接线（替换LayerContentPass的L1 TODO段）：
        /// <code>Layers.L1.L1Content.PlanAndBuild(LayerPlans.L1);</code>
        /// <para/>前置依赖：P10骨架+P20（脊/主竖井/出生点）+P30（ctx就绪）。
        /// 随机全走WorldGen.genRand（F22）；不注册GenPass、不改A路文件。
        /// 教堂位置由主竖井对齐推导（后殿竖井口=ShaftLeft），出生点落于中殿尖塔正下通行区，
        /// M0教堂占位安全房足印被中殿内膛整体吸收。
        /// </summary>
        internal static void PlanAndBuild(LayerBuildContext ctx) {
            LayerBand band = ctx.Band;
            int cathLeft = DungeonworldMetrics.ShaftLeft - L1CathedralPrefab.ShaftArtLeft;

            //对齐断言（fail loud）：出生列必须落在教堂尖塔通行区；层带装不下教堂即硬错误
            int spawnRel = DungeonworldMetrics.SpawnX - cathLeft;
            if (spawnRel < L1CathedralPrefab.SpireInnerLeft || spawnRel >= L1CathedralPrefab.SpireInnerRight) {
                throw new System.InvalidOperationException(
                    $"[L1] SpawnX相对教堂列{spawnRel}不在尖塔通行区[{L1CathedralPrefab.SpireInnerLeft},{L1CathedralPrefab.SpireInnerRight})，Metrics布局被改动，请同步L1对齐常量");
            }
            if (band.SpineFloorTop - L1CathedralPrefab.FloorArtRow < band.Top) {
                throw new System.InvalidOperationException("[L1] 教堂高度超出L1层带，检查层带行数预算");
            }

            BuildLayer(ctx.Grid, ctx.Graph, band.SpineFloorTop, band.SpineInteriorTop,
                DungeonworldMetrics.ShaftLeft, fullTower: true, WorldGen.genRand);

            //层撒布装修声明，P55统一执行（契约纪律5）
            ctx.Scatter.AddRange(L1Style.LayerScatter());
        }

        //==================== 主构建（gen与预览共用，坐标全参数化）====================

        private static void BuildLayer(OccupancyGrid grid, RoomGraph graph, int spineFloor,
            int spineInteriorTop, int shaftLeft, bool fullTower, UnifiedRandom rand) {
            int cathLeft = shaftLeft - L1CathedralPrefab.ShaftArtLeft;
            int cathTop = spineFloor - L1CathedralPrefab.FloorArtRow;
            int cathRight = cathLeft + L1CathedralPrefab.ArtWidth;
            int mezzRow = cathTop + 41;
            //挂房地板：脊内膛顶上收5行，含壳+padding贴住P30脊缓冲带（同L2公式）
            int floorA = spineInteriorTop - 5;

            //1) 教堂群落（内联跨脊既成事实）：主教堂+前厅+圣器室+上廊，足印统一登记
            L1CathedralPrefab.Build(spineFloor, shaftLeft, fullTower);
            RoomNode narthex = L1Rooms.BuildNarthex(cathLeft + 2, spineFloor);
            RoomNode vestry = L1Rooms.BuildVestry(cathRight - 2, spineFloor, rand);
            RoomNode gallery = L1Rooms.BuildGallery(cathLeft + 2, mezzRow, rand);
            graph.Rooms.Add(narthex);
            graph.Rooms.Add(vestry);
            graph.Rooms.Add(gallery);
            grid.MarkUnchecked(Inflate(new Rectangle(narthex.Bounds.Left,
                cathTop, cathRight + 20 - narthex.Bounds.Left, spineFloor + 3 - cathTop), 2));
            grid.MarkUnchecked(Inflate(gallery.Bounds, 2));

            //2) 卫星挂房：ctx.Grid正常预留，窗口西群/东群绕开群落足印
            var placed = new List<PlacedNode>();
            void Try(NodeKind kind, int offMin, int offMax, Point min, Point max) {
                RoomNode room = RoomPlacer.TryPlace(grid, rand, cathLeft + offMin, cathLeft + offMax,
                    floorA, min, max);
                if (room == null) {
                    CWRMod.Instance.Logger.Warn(
                        $"[L1] {kind}窗口[{cathLeft + offMin},{cathLeft + offMax})落位失败,跳过(数量档内缺席)");
                    return;
                }
                var node = new PlacedNode { Room = room, Kind = kind };
                switch (kind) {
                    case NodeKind.SafeRoom:
                        L1Rooms.StampShell(room.Bounds, L1Style.Wall);
                        L1Rooms.FurnishSafeRoom(room, rand);
                        node.WantsDoorPlate = true;
                        node.WantsSpineDrop = true;
                        break;
                    case NodeKind.Confessional: {
                        //忏悔室为prefab：以TryPlace结果定位后重新按字符画盖章（几何一致，16x9）
                        RoomNode stamped = L1Rooms.StampConfessional(room.Bounds.Left, room.FloorTop);
                        room.Role = stamped.Role;
                        break;
                    }
                    case NodeKind.Chandlery:
                        L1Rooms.StampShell(room.Bounds, L1Style.Wall);
                        L1Rooms.FurnishChandlery(room, rand);
                        break;
                    case NodeKind.Cloister:
                        L1Rooms.StampShell(room.Bounds, L1Style.Wall);
                        L1Rooms.FurnishCloister(room, rand, slabAtLeftEnd: offMin < 0);
                        node.WantsSpineDrop = true;
                        break;
                    default:
                        L1Rooms.StampShell(room.Bounds, L1Style.Wall);
                        L1Rooms.FurnishStairhead(room, rand);
                        node.WantsSpineDrop = true;
                        break;
                }
                placed.Add(node);
                graph.Rooms.Add(room);
            }

            //西群（远→近）：安全房A/忏悔室A/制烛间/回廊W；东群：回廊E/忏悔室B/井口房/安全房B
            Try(NodeKind.SafeRoom, SafeAOffMin, SafeAOffMax, new(12, 8), new(14, 9));
            Try(NodeKind.Confessional, ConfAOffMin, ConfAOffMax,
                new(L1Rooms.Confessional.Width - 4, L1Rooms.Confessional.Height - 4),
                new(L1Rooms.Confessional.Width - 4, L1Rooms.Confessional.Height - 4));
            Try(NodeKind.Chandlery, ChandleryOffMin, ChandleryOffMax, new(14, 6), new(16, 7));
            Try(NodeKind.Cloister, CloisterWOffMin, CloisterWOffMax, new(46, 10), new(50, 11));
            Try(NodeKind.Cloister, CloisterEOffMin, CloisterEOffMax, new(46, 10), new(50, 11));
            Try(NodeKind.Confessional, ConfBOffMin, ConfBOffMax,
                new(L1Rooms.Confessional.Width - 4, L1Rooms.Confessional.Height - 4),
                new(L1Rooms.Confessional.Width - 4, L1Rooms.Confessional.Height - 4));
            Try(NodeKind.Stairhead, StairheadOffMin, StairheadOffMax, new(14, 7), new(16, 8));
            Try(NodeKind.SafeRoom, SafeBOffMin, SafeBOffMax, new(12, 8), new(14, 9));

            //3) 链边：同群相邻房门对门/拱对拱（不跨群落/竖井），真门房补门板
            int edges = RouteChainEdges(graph, placed, cathLeft);
            //4) 落口：楼梯井下探层脊（爬升11>坡道上限，PlatformGap即井形态）
            int drops = RouteSpineDrops(graph, placed, spineFloor);
            //孤立分量兜底：无链边且无落口=预计洪泛不可达，fail loud交P80复核
            foreach (PlacedNode node in placed) {
                int idx = graph.Rooms.IndexOf(node.Room);
                if (!node.WantsSpineDrop && !HasAnyEdge(graph, idx)) {
                    CWRMod.Instance.Logger.Error(
                        $"[L1Content] 节点{node.Kind}@{node.Room.Bounds}无链边且无落口,预计洪泛不可达,责任=L1布局表");
                }
            }

            //5) 做旧收尾：全部房间蜡泪+烟熏（扫光源家具，paint层，观感【待签字】）
            foreach (RoomNode room in graph.Rooms) {
                L1Style.AgeLightsInRect(room.Bounds);
            }

            CWRMod.Instance.Logger.Info(
                $"[L1Content] 教堂区落成 nodes={graph.Rooms.Count}(群落3+散房{placed.Count})"
                + $" edges={edges} drops={drops} graphConnected={graph.IsConnected()}(分量间由脊桥接,洪泛为准)"
                + $" grid={grid.ReserveOk}留/{grid.ReserveReject}拒 教堂origin={L1CathedralPrefab.LastOrigin}");
        }

        //相邻散房链边：gap≤64且不跨群落/竖井足印；安全房侧上真门板，其余拱洞
        private static int RouteChainEdges(RoomGraph graph, List<PlacedNode> placed, int cathLeft) {
            int routed = 0;
            placed.Sort((l, r) => l.Room.Bounds.Left.CompareTo(r.Room.Bounds.Left));
            for (int i = 0; i + 1 < placed.Count; i++) {
                PlacedNode a = placed[i];
                PlacedNode b = placed[i + 1];
                int gapL = a.Room.Bounds.Right;
                int gapR = b.Room.Bounds.Left;
                //跨教堂群落（含上廊）或主竖井列带的间隙不走长廊（契约纪律4），脊即穿越路径
                if (gapR - gapL > 64 || (gapL < cathLeft + 162 && gapR > cathLeft - 50)) {
                    continue;
                }
                DoorSocket sa = a.WantsDoorPlate
                    ? L1Rooms.FloorDoor(a.Room, SocketSide.Right)
                    : L1Rooms.FloorArch(a.Room, SocketSide.Right);
                DoorSocket sb = b.WantsDoorPlate
                    ? L1Rooms.FloorDoor(b.Room, SocketSide.Left)
                    : L1Rooms.FloorArch(b.Room, SocketSide.Left);
                a.Room.Sockets.Add(sa);
                b.Room.Sockets.Add(sb);
                if (!CorridorRouter.RouteDoorToDoor(a.Room, sa, b.Room, sb, L1Style.Wall)) {
                    continue;
                }
                //真门房的门板落在自壳内侧列（F4上下实心由壳厚构造满足）
                if (a.WantsDoorPlate) {
                    L1Style.PlaceDoorPlate(a.Room.Bounds.Right - 2, a.Room.FloorTop - 1);
                }
                if (b.WantsDoorPlate) {
                    L1Style.PlaceDoorPlate(b.Room.Bounds.Left + 1, b.Room.FloorTop - 1);
                }
                graph.Edges.Add(new RoomEdge(graph.Rooms.IndexOf(a.Room), graph.Rooms.IndexOf(b.Room),
                    a.WantsDoorPlate || b.WantsDoorPlate ? SocketKind.Door : SocketKind.Archway,
                    EdgeForm.Horizontal));
                routed++;
            }
            return routed;
        }

        //落口：PlatformGap→楼梯井直落层脊地板；安全房贴左壁（家具让位），其余居中
        private static int RouteSpineDrops(RoomGraph graph, List<PlacedNode> placed, int spineFloor) {
            int drops = 0;
            foreach (PlacedNode node in placed) {
                if (!node.WantsSpineDrop) {
                    continue;
                }
                int offset = node.Kind == NodeKind.SafeRoom
                    ? L1Rooms.SafeRoomDropOffset
                    : (node.Room.Bounds.Width - DungeonworldMetrics.StairWellWidth) / 2;
                var gap = new DoorSocket(SocketSide.Bottom, offset,
                    SocketKind.PlatformGap, DungeonworldMetrics.StairWellWidth);
                node.Room.Sockets.Add(gap);
                CorridorRouter.RouteToFloorBelow(node.Room, gap, spineFloor,
                    L1Style.PlatformFrameY, L1Style.Wall);
                graph.Edges.Add(new RoomEdge(graph.Rooms.IndexOf(node.Room),
                    graph.Rooms.IndexOf(node.Room), SocketKind.PlatformGap, EdgeForm.StairWell));
                drops++;
            }
            return drops;
        }

        private static bool HasAnyEdge(RoomGraph graph, int index) {
            foreach (RoomEdge e in graph.Edges) {
                if (e.A == index || e.B == index) {
                    return true;
                }
            }
            return false;
        }

        private static Rectangle Inflate(Rectangle r, int pad)
            => new(r.X - pad, r.Y - pad, r.Width + pad * 2, r.Height + pad * 2);

        //==================== 免接线看样入口（镜像DungeonworldPreview惯例）====================

        //整块浇实模拟gen前提；仅单人调试，联机不发tile同步
        private static void SolidifyStrip(Rectangle rect) {
            if (Main.netMode != NetmodeID.SinglePlayer) {
                CWRMod.Instance.Logger.Warn("[L1Preview] 看样入口仅单人调试用,联机不发tile同步");
            }
            for (int x = rect.Left; x < rect.Right; x++) {
                for (int y = rect.Top; y < rect.Bottom; y++) {
                    TileBrush.SetSolid(x, y, L1Style.Brick);
                }
            }
        }

        //本地伪层脊+伪主竖井stub+伪占用栅格预留（复刻P20几何与P30登记，预览与gen同构）
        private static OccupancyGrid FakeContext(Rectangle strip, int floorRow, int shaftLeft) {
            TileBrush.CarveRect(strip.Left + 2, floorRow - DungeonworldMetrics.SpineClearance,
                strip.Right - 2, floorRow, L1Style.Wall);
            int shaftRight = shaftLeft + DungeonworldMetrics.ShaftWidth;
            for (int y = floorRow - DungeonworldMetrics.SpineClearance; y < floorRow + 10; y++) {
                for (int x = shaftLeft; x < shaftRight; x++) {
                    TileBrush.ClearCell(x, y, L1Style.Wall);
                }
            }
            TileBrush.PlatformRow(shaftLeft, shaftRight, floorRow, L1Style.PlatformFrameY);

            var grid = new OccupancyGrid(strip);
            grid.MarkUnchecked(new Rectangle(strip.Left,
                floorRow - DungeonworldMetrics.SpineClearance - 1,
                strip.Width, strip.Bottom - (floorRow - DungeonworldMetrics.SpineClearance - 1)));
            grid.MarkUnchecked(new Rectangle(shaftLeft - 2, strip.Top,
                DungeonworldMetrics.ShaftWidth + 4, strip.Height));
            return grid;
        }

        /// <summary>
        /// 看样1：脚下盖出L1完整代表房型带（教堂群落+钟楼短塔+全部卫星挂房+链边落口）。
        /// centerX对应出生列语义（教堂尖塔正下），floorRow=玩家脚下地板行。
        /// 占地约[centerX-380, centerX+480]×[floorRow-100, floorRow+12]，请在平坦测试世界使用。
        /// 层撒布(吊灯/挂画/旗帜氛围铺撒)由P55执行，本看样不含。
        /// </summary>
        internal static void PreviewShowcase(int centerX, int floorRow) {
            int shaftLeft = centerX + (DungeonworldMetrics.ShaftLeft - DungeonworldMetrics.SpawnX);
            int cathLeft = shaftLeft - L1CathedralPrefab.ShaftArtLeft;
            var strip = new Rectangle(cathLeft - 320, floorRow - 100, 320 + 440 + L1CathedralPrefab.ArtWidth, 112);
            SolidifyStrip(strip);
            OccupancyGrid grid = FakeContext(strip, floorRow, shaftLeft);
            BuildLayer(grid, new RoomGraph(), floorRow, floorRow - DungeonworldMetrics.SpineClearance,
                shaftLeft, fullTower: false, WorldGen.genRand);
            WorldGen.RangeFrame(strip.Left - 1, strip.Top - 1, strip.Right + 1, strip.Bottom + 1);
            CWRMod.Instance.Logger.Info($"[L1Preview] Showcase落成 center={centerX} floor={floorRow}");
        }

        /// <summary>
        /// 看样2：只盖主教堂+钟楼（短塔）+前厅/圣器室/上廊，快速看穿衣效果。占地约220宽×112高。
        /// </summary>
        internal static void PreviewCathedral(int centerX, int floorRow) {
            int shaftLeft = centerX + (DungeonworldMetrics.ShaftLeft - DungeonworldMetrics.SpawnX);
            int cathLeft = shaftLeft - L1CathedralPrefab.ShaftArtLeft;
            //条带须盖住上廊西端(cathLeft-46起)与圣器室东壳(cathRight+18止)
            var strip = new Rectangle(cathLeft - 56, floorRow - 100, L1CathedralPrefab.ArtWidth + 86, 112);
            SolidifyStrip(strip);
            FakeContext(strip, floorRow, shaftLeft);
            L1CathedralPrefab.Build(floorRow, shaftLeft, fullTower: false);
            RoomNode narthex = L1Rooms.BuildNarthex(cathLeft + 2, floorRow);
            RoomNode vestry = L1Rooms.BuildVestry(cathLeft + L1CathedralPrefab.ArtWidth - 2, floorRow, WorldGen.genRand);
            RoomNode gallery = L1Rooms.BuildGallery(cathLeft + 2, floorRow - L1CathedralPrefab.FloorArtRow + 41, WorldGen.genRand);
            L1Style.AgeLightsInRect(narthex.Bounds);
            L1Style.AgeLightsInRect(vestry.Bounds);
            L1Style.AgeLightsInRect(gallery.Bounds);
            WorldGen.RangeFrame(strip.Left - 1, strip.Top - 1, strip.Right + 1, strip.Bottom + 1);
            CWRMod.Instance.Logger.Info($"[L1Preview] Cathedral落成 center={centerX} floor={floorRow}");
        }
    }
}
