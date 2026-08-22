using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Rooms;
using System.Collections.Generic;
using Terraria;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L3
{
    //====================================================================
    //L3大档案馆内容入口(Wave-2接缝契约,契约全文见 LayerBuildContext 头注释)。
    //管线路/父级一行接线(LayerContentPass的L3槽位):
    //  Layers.L3.L3Content.PlanAndBuild(LayerPlans.L3);
    //
    //===夹楼堆叠方案(1348行弹性层的消化,ROOMS-L3 §0三区)===
    //层带纵向切成约21层"检索廊甲板":上区阅览(节距52,亮/家具密)→
    //中区迷宫(节距68,书架迷宫+书塔)→下区禁书区带(节距62,Slab暗区,贴层底)。
    //每层甲板=一条水平检索廊(净高5)+廊上方挂房(L2挂房制同构:地板=廊地板-10,
    //含壳+padding恰好贴住廊预留带);甲板间以楼梯井+书塔织成次级垂直循环
    //(塔顶接上层廊/塔底落本层廊,塔即带内容的竖向捷径);最底层廊落层脊
    //从脊走廊一路向上钻进迷宫深处(§1.4连通不变量:每廊段≥1条向下连接,归纳到脊)。
    //
    //===足印纪律===
    //检索廊逐列过ctx.Grid.CanReserve后才成段，主竖井与管线路预留的跨层垂直
    //连接足印天然把廊切段,构造性避开,不硬编码任何避让位置(brief §2.7);
    //房间全走RoomPlacer.TryPlace;链边只在同廊段内配对(不跨足印,契约纪律4)。
    //随机全走WorldGen.genRand(F22);撒布经ctx.Scatter声明(纪律5);fail loud(纪律6)。
    //====================================================================
    internal static class L3Content
    {
        //甲板节距(条带净高=节距-17,迷宫块/书塔按此掷高)
        private const int PitchReading = 52;
        private const int PitchMaze = 68;
        private const int PitchForbidden = 62;
        private const int MinPitch = 44;
        //节距抖动幅度:等距甲板一眼看穿是程序生成的,±5够打散又不撞MinPitch
        private const int PitchJitter = 5;
        //检索廊净高(主干道档,§2.5)
        private const int GalleryClearance = 5;
        //挂房地板=廊地板上收10行(含壳+padding贴住廊预留带,楼梯井落口刚好一跳程)
        private const int RoomHang = 10;
        //廊段最短可用长度
        private const int MinSegment = 44;
        //三区行数(ROOMS-L3 §0:上阅览约300/下禁书约300,中间全给迷宫)
        private const int ReadingRows = 300;
        private const int ForbiddenRows = 300;

        private enum DeckZone { Reading, Maze, Forbidden }

        private enum NodeKind { Hall, MazeBlock, Tower, Catalog, Scriptorium, LampRoom, Vault, Confessional, Falling, WellStation }

        private sealed class Deck
        {
            internal int Floor;
            internal DeckZone Zone;
            internal readonly List<(int L, int R)> Segments = [];
            internal int[] SegmentDownLinks;
        }

        private sealed class PlacedNode
        {
            internal RoomNode Room;
            internal NodeKind Kind;
            internal int GraphIndex;
            internal int SegIndex;
            //落口列偏移(距Bounds.Left),-1=不开(禁书区单入口)
            internal int DropOffset = -1;
            internal bool NoChain;
            //书塔:光井与塔顶甲板(上层廊楼梯井对接点)
            internal int TowerWellLeft = -1;
            internal int TowerTopDeck = -1;
        }

        //花名册数量档(ROOMS-L3 §1)上限,收尾对照下限告警
        private sealed class Caps
        {
            internal int Halls, Mazes, Towers, Catalogs, Scriptoria, LampRooms, Vaults;
            internal int Confessionals, Fallings, Stations;
        }

        /// <summary>层内容主入口:甲板规划→检索廊→挂房→链边→落口→垂直井网→混墙→撒布声明</summary>
        internal static void PlanAndBuild(LayerBuildContext ctx) {
            UnifiedRandom rand = WorldGen.genRand;
            LayerBand band = ctx.Band;
            L3Lights.ResetCounters();

            int xLeft = DungeonworldMetrics.PlayLeft + 6;
            int xRight = DungeonworldMetrics.PlayRight - 6;
            int usableTop = band.Top + 8;
            //最底廊的预留带(廊地板+3)不得触碰P30脊预留(SpineInteriorTop-1)
            int bottomLimit = band.SpineInteriorTop - 4;
            int readingBottom = band.Top + ReadingRows;
            int forbiddenTop = bottomLimit - ForbiddenRows;

            //1) 甲板行规划(纯数据):自上而下按区取节距,尾差并入最后一层
            var decks = new List<Deck>();
            int y = usableTop;
            while (bottomLimit - y >= MinPitch) {
                DeckZone zone = y + PitchMaze / 2 < readingBottom ? DeckZone.Reading
                    : y + PitchMaze / 2 >= forbiddenTop ? DeckZone.Forbidden : DeckZone.Maze;
                int pitch = zone switch {
                    DeckZone.Reading => PitchReading,
                    DeckZone.Forbidden => PitchForbidden,
                    _ => PitchMaze,
                };
                //节距抖动:等距甲板从竖井里看下去是一把标尺,层高不齐才像一座楼
                //(抖动量压在MinPitch之上:最小的阅览节距52-5=47仍高于下限44)
                pitch += rand.Next(-PitchJitter, PitchJitter + 1);
                int floor = System.Math.Min(y + pitch, bottomLimit);
                if (bottomLimit - floor < MinPitch) {
                    floor = bottomLimit;
                }
                decks.Add(new Deck { Floor = floor, Zone = ZoneOfFloor(floor, readingBottom, forbiddenTop) });
                y = floor;
            }
            if (decks.Count < 4) {
                throw new System.InvalidOperationException(
                    $"[L3Content] 层带{band.Top}~{band.Bottom}仅规划出{decks.Count}层甲板,层带行数预算被改动?");
            }

            //2) 检索廊:逐列CanReserve扫描成段(足印天然切段)→预留→刻画
            int segmentsTotal = 0;
            foreach (Deck deck in decks) {
                BuildGallery(ctx, deck, xLeft, xRight, rand);
                deck.SegmentDownLinks = new int[deck.Segments.Count];
                segmentsTotal += deck.Segments.Count;
            }

            //3) 挂房:逐甲板落房+刻画+装修
            var caps = new Caps();
            var placed = new List<PlacedNode>();
            int furnPlaced = 0, furnRejected = 0;
            for (int i = 0; i < decks.Count; i++) {
                PlaceDeckRooms(ctx, decks, i, caps, placed, rand, ref furnPlaced, ref furnRejected);
            }

            //4) 链边(同廊段内相邻配对,gap≤30)
            int chains = RouteChains(ctx, placed, rand);

            //5) 落口:挂房→本层廊(楼梯井,爬升10>坡道上限即井形态)
            int drops = RouteDrops(ctx, decks, placed);

            //6) 书塔顶井:上层廊沿光井轴直落塔顶甲板
            int towerLinks = RouteTowerTopLinks(decks, placed);

            //7) 廊际楼梯井:每廊段保≥1条向下连接(连通归纳到脊,§1.4)
            int wells = RouteGalleryWells(ctx, decks, rand);

            //8) 最底廊→层脊落井
            int spineWells = RouteSpineWells(decks[^1], band);

            //9) 圆斑混墙:蓝基/Slab/Tiled三变体(F32,半径取大;禁书带已Slab主调)
            //小盘Tiled压在大盘Slab之上,三种变体交叠出来的边界比两种碎得多
            int disks = 22;
            for (int d = 0; d < disks; d++) {
                L3Palette.WallDisk(rand.Next(xLeft, xRight),
                    rand.Next(band.Top + 40, forbiddenTop),
                    rand.Next(100, 161), L3Palette.WallSlab);
            }
            int tiledDisks = 14;
            for (int d = 0; d < tiledDisks; d++) {
                L3Palette.WallDisk(rand.Next(xLeft, xRight),
                    rand.Next(band.Top + 40, forbiddenTop),
                    rand.Next(55, 106), L3Palette.WallTiled);
            }

            //9b) 基调层染:纸墨褐洗到阅览区与迷宫区。禁书区留素蓝
            //暗区的身份是"看不清",染上反而把它洗亮了
            LayerTint.TintReport tint = L3Palette.PaperWash(
                new Rectangle(xLeft, band.Top, xRight - xLeft, forbiddenTop - band.Top));

            //10) 撒布声明(P55统一执行,契约纪律5)
            ctx.Scatter.AddRange(L3Scatter.Entries(new L3Zones(readingBottom, forbiddenTop)));

            //数量档下限对照(花名册纪律,fail loud)
            if (caps.Halls < 3 || caps.Vaults < 1 || caps.Towers < 4 || caps.Mazes < 8) {
                CWRMod.Instance.Logger.Warn(
                    $"[L3Content] 数量档低于花名册下限:厅{caps.Halls}/3 塔{caps.Towers}/4"
                    + $" 迷宫{caps.Mazes}/8 禁书{caps.Vaults}/1,查占用栅格拒绝量");
            }
            if (caps.Confessionals < 1 || caps.Fallings < 1 || caps.Stations < 1) {
                CWRMod.Instance.Logger.Warn(
                    $"[L3Content] 公共构件层内换皮缺席:忏{caps.Confessionals}/1 坠{caps.Fallings}/1"
                    + $" 井站{caps.Stations}/1(公共prefab波仍可覆盖)");
            }

            CWRMod.Instance.Logger.Info(
                $"[L3Content] 大档案馆落成 decks={decks.Count} segments={segmentsTotal}"
                + $" nodes={placed.Count}(厅{caps.Halls} 迷{caps.Mazes} 塔{caps.Towers} 录{caps.Catalogs}"
                + $" 抄{caps.Scriptoria} 灯房{caps.LampRooms} 禁{caps.Vaults}"
                + $" 忏{caps.Confessionals} 坠{caps.Fallings} 井站{caps.Stations})"
                + $" chains={chains} drops={drops} 塔顶井={towerLinks} 廊际井={wells} 落脊井={spineWells}"
                + $" 纸墨褐层染={tint} 灯=亮{L3Lights.LampsLit}/灭{L3Lights.LampsOff} 开关={L3Lights.SwitchesPlaced}"
                + $" 家具={furnPlaced}成/{furnRejected}拒 grid={ctx.Grid.ReserveOk}留/{ctx.Grid.ReserveReject}拒"
                + $" graphConnected={ctx.Graph.IsConnected()}(分量间由检索廊/层脊桥接,洪泛为准)");
        }

        private static DeckZone ZoneOfFloor(int floor, int readingBottom, int forbiddenTop)
            => floor <= readingBottom ? DeckZone.Reading
                : floor > forbiddenTop ? DeckZone.Forbidden : DeckZone.Maze;

        //==================== 检索廊(甲板动脉):扫描-预留-刻画 ====================

        //廊预留带:天花缓冲1+内膛5+地板2+落口缓冲1=9行,[Floor-6,Floor+3)
        private static void BuildGallery(LayerBuildContext ctx, Deck deck, int xLeft, int xRight,
            UnifiedRandom rand) {
            int top = deck.Floor - GalleryClearance - 1;
            int runStart = -1;
            for (int x = xLeft; x <= xRight; x++) {
                bool free = x < xRight
                    && ctx.Grid.CanReserve(new Rectangle(x, top, 1, 9), 0);
                if (free && runStart < 0) {
                    runStart = x;
                }
                else if (!free && runStart >= 0) {
                    if (x - runStart >= MinSegment) {
                        int segL = runStart + 1;
                        int segR = x - 1;
                        ctx.Grid.MarkUnchecked(new Rectangle(segL - 1, top, segR - segL + 2, 9));
                        CarveGallerySegment(deck, segL, segR, rand);
                        deck.Segments.Add((segL, segR));
                    }
                    runStart = -1;
                }
            }
            if (deck.Segments.Count == 0) {
                CWRMod.Instance.Logger.Error(
                    $"[L3Content] 甲板廊y={deck.Floor}零可用段,足印占满整幅?责任=P30预留量复核");
            }
        }

        //段内剖面节奏:直管走40~70列后换一段阅览湾或收窄,再回直管。
        //一条净高5的直管能横穿近1900列、还要在21层甲板上重复，这是全世界最大的一片
        //单调面;三种剖面全部压在P30已预留的9行带[Floor-6,Floor+3)内,不越界。
        //阅览湾:抬顶1+落地1=净高7,进湾自由下落、出湾1格自动登阶(F3),不断路
        //收窄  :压顶1=净高4,仍高于支线走廊底线3
        private static void CarveGallerySegment(Deck deck, int segL, int segR, UnifiedRandom rand) {
            ushort wall = SegmentWall(deck.Zone, rand);
            int x = segL;
            bool straight = true;
            while (x < segR) {
                bool bay = !straight && rand.Next(5) < 3;
                int run = straight ? rand.Next(40, 71)
                    : bay ? rand.Next(10, 21) : rand.Next(6, 13);
                int end = System.Math.Min(x + run, segR);
                //末尾不留短头:剩不到一个变化段就并进当前段走完
                if (segR - end < 8) {
                    end = segR;
                }
                int carveTop = deck.Floor - GalleryClearance;
                int carveBottom = deck.Floor;
                if (!straight && bay) {
                    carveTop--;
                    carveBottom++;
                }
                else if (!straight) {
                    carveTop++;
                }
                TileBrush.CarveRect(x, carveTop, end, carveBottom, wall);
                x = end;
                straight = !straight;
            }
        }

        //逐段换墙变体:蓝墙原版三种,以前整层只在基/Slab之间按区二选一
        private static ushort SegmentWall(DeckZone zone, UnifiedRandom rand) {
            if (zone == DeckZone.Forbidden) {
                //禁书带保Slab暗调,偶尔掺一段Tiled
                return rand.NextBool(5) ? L3Palette.WallTiled : L3Palette.WallSlab;
            }
            int roll = rand.Next(100);
            return roll < 55 ? L3Palette.WallBase
                : roll < 85 ? L3Palette.WallSlab
                : L3Palette.WallTiled;
        }

        //==================== 挂房布置 ====================

        private static void PlaceDeckRooms(LayerBuildContext ctx, List<Deck> decks, int deckIdx,
            Caps caps, List<PlacedNode> placed, UnifiedRandom rand,
            ref int furnPlaced, ref int furnRejected) {
            Deck deck = decks[deckIdx];
            if (deck.Segments.Count == 0) {
                return;
            }
            int floorRooms = deck.Floor - RoomHang;
            int stripTop = deckIdx == 0 ? decks[0].Floor - PitchReading + 3 : decks[deckIdx - 1].Floor + 3;
            int maxH = floorRooms - stripTop - 5;
            bool lastDeck = deckIdx == decks.Count - 1;
            bool secondLast = deckIdx == decks.Count - 2;

            //本甲板的心愿单(按区;错层感=奇偶甲板主房换段)
            var wish = new List<NodeKind>();
            switch (deck.Zone) {
                case DeckZone.Reading:
                    if (caps.Halls < 5) {
                        wish.Add(NodeKind.Hall);
                    }
                    if (caps.Catalogs < 1 && deckIdx >= 1) {
                        wish.Add(NodeKind.Catalog);
                    }
                    if (caps.Confessionals < 2 && deckIdx % 2 == 0) {
                        wish.Add(NodeKind.Confessional);
                    }
                    wish.Add(rand.NextBool(5) && caps.LampRooms < 3 ? NodeKind.LampRoom : NodeKind.Scriptorium);
                    break;
                case DeckZone.Maze:
                    if (caps.Mazes < 12) {
                        wish.Add(NodeKind.MazeBlock);
                    }
                    if (deckIdx % 2 == 1 && caps.Towers < 7 && maxH >= 38) {
                        wish.Add(NodeKind.Tower);
                    }
                    if (caps.Stations < 1 && maxH >= 10) {
                        wish.Add(NodeKind.WellStation);
                    }
                    if (rand.NextBool(3)) {
                        if (caps.Catalogs < 2 && deckIdx > decks.Count / 3) {
                            wish.Add(NodeKind.Catalog);
                        }
                        else if (caps.LampRooms < 3 && rand.NextBool()) {
                            wish.Add(NodeKind.LampRoom);
                        }
                        else if (caps.Scriptoria < 5) {
                            wish.Add(NodeKind.Scriptorium);
                        }
                    }
                    break;
                default:
                    if ((lastDeck || (secondLast && rand.NextBool())) && caps.Vaults < 2) {
                        wish.Add(NodeKind.Vault);
                    }
                    if (lastDeck && caps.Fallings < 1) {
                        wish.Add(NodeKind.Falling);
                    }
                    if (caps.Mazes < 14) {
                        wish.Add(NodeKind.MazeBlock);
                    }
                    break;
            }

            for (int w = 0; w < wish.Count; w++) {
                //主房与副房错段:奇偶甲板起始段互换,段不足时回绕
                int segIdx = (deckIdx % 2 + w) % deck.Segments.Count;
                if (wish[w] == NodeKind.WellStation) {
                    segIdx = NearestSegment(deck, DungeonworldMetrics.ShaftLeft);
                }
                else if (wish[w] == NodeKind.Falling && deck.Segments.Count > 1) {
                    //坠落房侧翼,避开段0(禁书区常占首段)
                    segIdx = deck.Segments.Count - 1;
                }
                PlacedNode node = TryBuildNode(ctx, deck, wish[w], segIdx, floorRooms, maxH,
                    lastDeck, caps, rand, ref furnPlaced, ref furnRejected);
                if (node != null) {
                    node.SegIndex = segIdx;
                    node.GraphIndex = ctx.Graph.Rooms.Count;
                    ctx.Graph.Rooms.Add(node.Room);
                    placed.Add(node);
                }
            }
        }

        private static PlacedNode TryBuildNode(LayerBuildContext ctx, Deck deck, NodeKind kind,
            int segIdx, int floorRooms, int maxH, bool lastDeck, Caps caps, UnifiedRandom rand,
            ref int furnPlaced, ref int furnRejected) {
            (int segL, int segR) = deck.Segments[segIdx];
            bool forbidden = deck.Zone == DeckZone.Forbidden;

            //尺寸先冻结再预留(L2先例)
            L3MazeBlock.MazePlan mazePlan = default;
            L3BookTower.TowerPlan towerPlan = default;
            Point size;
            switch (kind) {
                case NodeKind.Hall:
                    size = L3Rooms.ReadingHallInteriorSize(rand);
                    size.Y = System.Math.Min(size.Y, maxH);
                    break;
                case NodeKind.Catalog:
                    size = L3Rooms.CatalogInteriorSize(rand);
                    break;
                case NodeKind.Scriptorium:
                    size = L3Rooms.ScriptoriumInteriorSize(rand);
                    break;
                case NodeKind.LampRoom:
                    size = L3Rooms.LampGalleryInteriorSize(rand);
                    break;
                case NodeKind.Vault:
                    size = L3Rooms.VaultInteriorSize(rand);
                    size.Y = System.Math.Min(size.Y, maxH);
                    break;
                case NodeKind.Confessional:
                    size = L3Rooms.ConfessionalInteriorSize();
                    break;
                case NodeKind.Falling:
                    size = L3Rooms.FallingInteriorSize(rand);
                    size.Y = System.Math.Min(size.Y, maxH);
                    break;
                case NodeKind.WellStation:
                    size = L3Rooms.WellStationInteriorSize(rand);
                    size.Y = System.Math.Min(size.Y, maxH);
                    break;
                case NodeKind.Tower:
                    if (!L3BookTower.TryRoll(rand, maxH, out towerPlan)) {
                        return null;
                    }
                    size = L3BookTower.InteriorSize(towerPlan);
                    break;
                default:
                    mazePlan = L3MazeBlock.Roll(rand, maxH, forbidden,
                        soggy: lastDeck && forbidden);
                    size = L3MazeBlock.InteriorSize(mazePlan);
                    //段装不下就收窄区块宽度(弹性主体,不轻易弃)
                    int segSpan = segR - segL - 6;
                    if (size.X + 4 > segSpan && segSpan >= 40) {
                        mazePlan.Width = segSpan - 4;
                        size = L3MazeBlock.InteriorSize(mazePlan);
                    }
                    break;
            }

            RoomNode room = RoomPlacer.TryPlace(ctx.Grid, rand, segL + 1, segR - 1, floorRooms, size, size);
            if (room == null) {
                CWRMod.Instance.Logger.Warn(
                    $"[L3Content] {kind}在甲板y={deck.Floor}段[{segL},{segR})落位失败,弃(数量档内缺席)");
                return null;
            }

            var node = new PlacedNode { Room = room, Kind = kind };
            switch (kind) {
                case NodeKind.Hall: {
                    //层顶首厅挂"罪档入库"(L2→L3隔离带呼应)
                    bool intake = caps.Halls == 0 && deck.Zone == DeckZone.Reading;
                    Tally(L3Rooms.BuildReadingHall(room, rand, intake), ref furnPlaced, ref furnRejected);
                    node.DropOffset = DungeonworldMetrics.RoomShellThick;
                    caps.Halls++;
                    break;
                }
                case NodeKind.Catalog:
                    Tally(L3Rooms.BuildCatalog(room, rand), ref furnPlaced, ref furnRejected);
                    node.DropOffset = DungeonworldMetrics.RoomShellThick;
                    room.Role = RoomRole.Safe;
                    caps.Catalogs++;
                    break;
                case NodeKind.Scriptorium:
                    Tally(L3Rooms.BuildScriptorium(room, rand), ref furnPlaced, ref furnRejected);
                    node.DropOffset = DungeonworldMetrics.RoomShellThick;
                    caps.Scriptoria++;
                    break;
                case NodeKind.LampRoom:
                    Tally(L3Rooms.BuildLampGallery(room, rand), ref furnPlaced, ref furnRejected);
                    node.DropOffset = DungeonworldMetrics.RoomShellThick;
                    room.Role = RoomRole.Puzzle;
                    caps.LampRooms++;
                    break;
                case NodeKind.Vault: {
                    Tally(L3Rooms.BuildVault(room, rand), ref furnPlaced, ref furnRejected);
                    node.NoChain = true;
                    room.Role = RoomRole.Treasure;
                    //单入口:钟声门面通道(拱+过梁+封条),失败即fail loud
                    EnsureVaultApproach(ctx, deck, node, floorRooms);
                    caps.Vaults++;
                    break;
                }
                case NodeKind.Confessional:
                    Tally(L3Rooms.BuildConfessional(room, rand), ref furnPlaced, ref furnRejected);
                    node.DropOffset = DungeonworldMetrics.RoomShellThick;
                    room.Role = RoomRole.Safe;
                    caps.Confessionals++;
                    break;
                case NodeKind.Falling:
                    Tally(L3Rooms.BuildFallingHung(room, rand), ref furnPlaced, ref furnRejected);
                    node.DropOffset = DungeonworldMetrics.RoomShellThick;
                    caps.Fallings++;
                    break;
                case NodeKind.WellStation:
                    Tally(L3Rooms.BuildWellStation(room, rand), ref furnPlaced, ref furnRejected);
                    node.DropOffset = DungeonworldMetrics.RoomShellThick;
                    TryOpenShaftArch(room);
                    caps.Stations++;
                    break;
                case NodeKind.Tower: {
                    L3BookTower.TowerReport rep = L3BookTower.Build(room, towerPlan, rand);
                    furnPlaced += rep.ShelvesPlaced + rep.Rewards;
                    furnRejected += rep.ShelvesRejected;
                    node.TowerWellLeft = rep.WellLeft;
                    node.TowerTopDeck = rep.TopDeckRow;
                    node.DropOffset = rep.WellLeft - room.Bounds.Left;
                    caps.Towers++;
                    break;
                }
                default: {
                    L3MazeBlock.MazeReport rep = L3MazeBlock.Build(room, mazePlan, rand);
                    furnPlaced += rep.ShelvesPlaced + rep.Rewards + rep.WaterCandles;
                    furnRejected += rep.ShelvesRejected;
                    node.DropOffset = rep.DropOffset;
                    caps.Mazes++;
                    break;
                }
            }
            return node;
        }

        private static void Tally(L3Rooms.Tally t, ref int placed, ref int rejected) {
            placed += t.Placed;
            rejected += t.Rejected;
        }

        //==================== 链边与落口 ====================

        //同甲板同段相邻房门对门/拱对拱;禁书区不入链(单入口);跨段=跨足印,构造性不配对
        private static int RouteChains(LayerBuildContext ctx, List<PlacedNode> placed, UnifiedRandom rand) {
            int routed = 0;
            var bySeg = new Dictionary<(int floor, int seg), List<PlacedNode>>();
            foreach (PlacedNode node in placed) {
                if (node.NoChain) {
                    continue;
                }
                (int, int) key = (node.Room.FloorTop, node.SegIndex);
                if (!bySeg.TryGetValue(key, out List<PlacedNode> list)) {
                    bySeg[key] = list = [];
                }
                list.Add(node);
            }
            foreach (List<PlacedNode> list in bySeg.Values) {
                list.Sort((l, r) => l.Room.Bounds.Left.CompareTo(r.Room.Bounds.Left));
                for (int i = 0; i + 1 < list.Count; i++) {
                    PlacedNode a = list[i];
                    PlacedNode b = list[i + 1];
                    int gap = b.Room.Bounds.Left - a.Room.Bounds.Right;
                    if (gap > 30) {
                        continue;
                    }
                    bool archA = a.Kind is NodeKind.Hall or NodeKind.Catalog or NodeKind.MazeBlock;
                    bool archB = b.Kind is NodeKind.Hall or NodeKind.Catalog or NodeKind.MazeBlock;
                    DoorSocket sa = archA ? L3Rooms.FloorArch(a.Room, SocketSide.Right)
                        : L3Rooms.FloorDoor(a.Room, SocketSide.Right);
                    DoorSocket sb = archB ? L3Rooms.FloorArch(b.Room, SocketSide.Left)
                        : L3Rooms.FloorDoor(b.Room, SocketSide.Left);
                    a.Room.Sockets.Add(sa);
                    b.Room.Sockets.Add(sb);
                    if (!CorridorRouter.RouteDoorToDoor(a.Room, sa, b.Room, sb, L3Palette.WallBase)) {
                        continue;
                    }
                    //小房侧真门板(抄写室/灯房有"门后一间"的私密语义)
                    if (!archA && rand.NextBool()) {
                        L3Palette.PlaceDoorPlate(a.Room.Bounds.Right - 2, a.Room.FloorTop - 1);
                    }
                    if (!archB && rand.NextBool()) {
                        L3Palette.PlaceDoorPlate(b.Room.Bounds.Left + 1, b.Room.FloorTop - 1);
                    }
                    ctx.Graph.Edges.Add(new RoomEdge(a.GraphIndex, b.GraphIndex,
                        archA || archB ? SocketKind.Archway : SocketKind.Door, EdgeForm.Horizontal));
                    routed++;
                }
            }
            return routed;
        }

        //落口:每房PlatformGap楼梯井直落本甲板检索廊(爬升10>坡道上限,井形态)
        private static int RouteDrops(LayerBuildContext ctx, List<Deck> decks, List<PlacedNode> placed) {
            int drops = 0;
            foreach (PlacedNode node in placed) {
                if (node.DropOffset < 0) {
                    continue;
                }
                Deck deck = FindDeck(decks, node.Room.FloorTop);
                if (deck == null) {
                    continue;
                }
                var gap = new DoorSocket(SocketSide.Bottom, node.DropOffset,
                    SocketKind.PlatformGap, DungeonworldMetrics.StairWellWidth);
                node.Room.Sockets.Add(gap);
                CorridorRouter.RouteToFloorBelow(node.Room, gap, deck.Floor,
                    L3Palette.PlatformFrameY, L3Palette.WallBase);
                ctx.Graph.Edges.Add(new RoomEdge(node.GraphIndex, node.GraphIndex,
                    SocketKind.PlatformGap, EdgeForm.StairWell));
                drops++;
            }
            return drops;
        }

        private static Deck FindDeck(List<Deck> decks, int roomFloor) {
            foreach (Deck deck in decks) {
                if (deck.Floor - RoomHang == roomFloor) {
                    return deck;
                }
            }
            return null;
        }

        //==================== 垂直井网(次级循环) ====================

        //书塔顶井:上层廊(若其某段覆盖光井列)沿光井轴直落塔顶甲板
        private static int RouteTowerTopLinks(List<Deck> decks, List<PlacedNode> placed) {
            int links = 0;
            foreach (PlacedNode node in placed) {
                if (node.Kind != NodeKind.Tower) {
                    continue;
                }
                int deckIdx = decks.FindIndex(d => d.Floor - RoomHang == node.Room.FloorTop);
                if (deckIdx <= 0) {
                    continue;
                }
                Deck upper = decks[deckIdx - 1];
                int wx = node.TowerWellLeft;
                int segIdx = SegmentContaining(upper, wx, 3);
                if (segIdx < 0) {
                    //上层廊未覆盖光井列(段被足印切走),塔仍有底口,不算失败
                    CWRMod.Instance.Logger.Info(
                        $"[L3Content] 书塔顶井x={wx}未获上层廊覆盖,塔走底口单联");
                    continue;
                }
                CorridorRouter.CarveStairWell(wx, upper.Floor, node.TowerTopDeck,
                    L3Palette.PlatformFrameY, L3Palette.WallBase);
                TileBrush.PlatformRow(wx, wx + 3, upper.Floor, L3Palette.PlatformFrameY);
                upper.SegmentDownLinks[segIdx]++;
                links++;
            }
            return links;
        }

        //廊际楼梯井:每段≥1条向下(先1/3与2/3位,再全段步进兜底;i+1不通尝试到i+3)
        private static int RouteGalleryWells(LayerBuildContext ctx, List<Deck> decks, UnifiedRandom rand) {
            int wells = 0;
            for (int i = 0; i < decks.Count - 1; i++) {
                Deck upper = decks[i];
                for (int s = 0; s < upper.Segments.Count; s++) {
                    (int segL, int segR) = upper.Segments[s];
                    int want = segR - segL > 300 ? 2 : 1;
                    int got = upper.SegmentDownLinks[s];
                    for (int j = i + 1; j <= System.Math.Min(i + 3, decks.Count - 1) && got < want; j++) {
                        Deck lower = decks[j];
                        foreach ((int loL, int loR) in lower.Segments) {
                            if (got >= want) {
                                break;
                            }
                            int ovL = System.Math.Max(segL + 2, loL + 2);
                            int ovR = System.Math.Min(segR - 5, loR - 5);
                            if (ovR - ovL < 4) {
                                continue;
                            }
                            //候选:1/3与2/3位优先,失败全段步进(决定论扫描)
                            if (TryWellAt(ctx, upper, lower, ovL + (ovR - ovL) / 3, ref wells)
                                || TryWellAt(ctx, upper, lower, ovL + (ovR - ovL) * 2 / 3, ref wells)) {
                                got++;
                                upper.SegmentDownLinks[s]++;
                                continue;
                            }
                            for (int x = ovL; x <= ovR && got < want; x += 6) {
                                if (TryWellAt(ctx, upper, lower, x, ref wells)) {
                                    got++;
                                    upper.SegmentDownLinks[s]++;
                                }
                            }
                        }
                    }
                    if (got == 0) {
                        CWRMod.Instance.Logger.Error(
                            $"[L3Content] 甲板y={upper.Floor}段[{segL},{segR})零向下连接,"
                            + "预计洪泛不可达,责任=L3井网路由");
                    }
                }
            }
            return wells;
        }

        //井柱=3宽+侧壁裕量,只查两廊预留带之间的房区条带;成功即预留+刻画+盖口
        private static bool TryWellAt(LayerBuildContext ctx, Deck upper, Deck lower, int x, ref int wells) {
            int stripTop = upper.Floor + 3;
            int stripBottom = lower.Floor - 6;
            if (stripBottom > stripTop) {
                var strip = new Rectangle(x - 1, stripTop, DungeonworldMetrics.StairWellWidth + 2,
                    stripBottom - stripTop);
                if (!ctx.Grid.CanReserve(strip, 0)) {
                    return false;
                }
                ctx.Grid.MarkUnchecked(strip);
            }
            CorridorRouter.CarveStairWell(x, upper.Floor, lower.Floor,
                L3Palette.PlatformFrameY, L3Palette.WallBase);
            TileBrush.PlatformRow(x, x + DungeonworldMetrics.StairWellWidth, upper.Floor,
                L3Palette.PlatformFrameY);
            wells++;
            return true;
        }

        //最底廊→层脊:每段两口(层的总出入口,脊即穿越路径)
        private static int RouteSpineWells(Deck lowest, LayerBand band) {
            int wells = 0;
            foreach ((int segL, int segR) in lowest.Segments) {
                foreach (double t in new[] { 1.0 / 3, 2.0 / 3 }) {
                    int x = segL + (int)((segR - segL) * t);
                    CorridorRouter.CarveStairWell(x, lowest.Floor, band.SpineFloorTop,
                        L3Palette.PlatformFrameY, L3Palette.WallBase);
                    TileBrush.PlatformRow(x, x + DungeonworldMetrics.StairWellWidth, lowest.Floor,
                        L3Palette.PlatformFrameY);
                    wells++;
                }
            }
            return wells;
        }

        //==================== 禁书区通道(单入口构造保证) ====================

        //入口=唯一Archway→短廊→楼梯井上/下接本甲板廊;两侧都不可行才报错
        private static void EnsureVaultApproach(LayerBuildContext ctx, Deck deck,
            PlacedNode vault, int floorRooms) {
            RoomNode room = vault.Room;
            if (TryVaultSide(ctx, deck, room, SocketSide.Right, floorRooms)
                || TryVaultSide(ctx, deck, room, SocketSide.Left, floorRooms)) {
                return;
            }
            CWRMod.Instance.Logger.Error(
                $"[L3Content] 禁书区@{room.Bounds}两侧通道均落位失败,预计不可达,责任=禁书区选段");
        }

        private static bool TryVaultSide(LayerBuildContext ctx, Deck deck, RoomNode room,
            SocketSide side, int floorRooms) {
            const int landing = 5;
            int wellW = DungeonworldMetrics.StairWellWidth;
            int x0 = side == SocketSide.Right ? room.Bounds.Right : room.Bounds.Left - landing - wellW;
            //通道足印:短廊5+井柱,房区条带内先查后占
            var strip = new Rectangle(x0 - 1, floorRooms - 4, landing + wellW + 2, 4 + 4);
            if (!ctx.Grid.CanReserve(strip, 0)) {
                return false;
            }
            ctx.Grid.MarkUnchecked(strip);

            DoorSocket arch = L3Rooms.FloorArch(room, side);
            room.Sockets.Add(arch);
            CorridorRouter.OpenWallSocket(room, arch, L3Palette.WallSlab);
            //短廊(净高4)接到井口
            int corL = side == SocketSide.Right ? room.Bounds.Right : x0 + wellW;
            TileBrush.CarveRect(corL, floorRooms - 4, corL + landing, floorRooms, L3Palette.WallSlab);
            //井:短廊尽头直落本甲板廊
            int wellX = side == SocketSide.Right ? corL + landing : x0;
            CorridorRouter.CarveStairWell(wellX, floorRooms, deck.Floor,
                L3Palette.PlatformFrameY, L3Palette.WallSlab);
            TileBrush.PlatformRow(wellX, wellX + wellW, floorRooms, L3Palette.PlatformFrameY);
            //门面framing+封条+预告斑(门体=运行时BellRiteSystem)
            L3Rooms.SealVaultEntrance(room, arch);
            return true;
        }

        private static int SegmentContaining(Deck deck, int x, int width) {
            for (int s = 0; s < deck.Segments.Count; s++) {
                if (x - 1 >= deck.Segments[s].L && x + width + 1 <= deck.Segments[s].R) {
                    return s;
                }
            }
            return -1;
        }

        private static int NearestSegment(Deck deck, int x) {
            int best = 0;
            int bestDist = int.MaxValue;
            for (int s = 0; s < deck.Segments.Count; s++) {
                int mid = (deck.Segments[s].L + deck.Segments[s].R) / 2;
                int d = System.Math.Abs(mid - x);
                if (d < bestDist) {
                    bestDist = d;
                    best = s;
                }
            }
            return best;
        }

        //井站侧缘贴近主竖井时开拱通向井柱(井体由P20刻画,本层只做站台门面)
        private static void TryOpenShaftArch(RoomNode room) {
            int shaftL = DungeonworldMetrics.ShaftLeft;
            int shaftR = shaftL + DungeonworldMetrics.ShaftWidth;
            if (room.Bounds.Right + 8 < shaftL || room.Bounds.Left > shaftR + 8) {
                return;
            }
            SocketSide side = room.Bounds.Center.X < (shaftL + shaftR) / 2
                ? SocketSide.Right : SocketSide.Left;
            DoorSocket arch = L3Rooms.FloorArch(room, side);
            room.Sockets.Add(arch);
            CorridorRouter.OpenWallSocket(room, arch, L3Palette.WallBase);
        }
    }
}
