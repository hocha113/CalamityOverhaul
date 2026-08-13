using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Prefabs;
using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Rooms;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L1
{
    //====================================================================
    //L1房型库（ROOMS-L1 §1花名册 #3~#10）：纯算法构建规则+忏悔室prefab。
    //两类：教堂群落钉死房（前厅/圣器室/上廊，借教堂外壳单侧无壳）在脊层，
    //卫星散房（安全房/制烛间/回廊/井口房/忏悔室）为挂房，壳体2厚（Metrics），
    //口部/链边/落口由L1Content统一经CorridorRouter路由，本文件只管壳与内装。
    //每房型一句话身份见各方法头注释。
    //纪律：几何冻结后装修单向（§3.1-3）；家具合法锚定（F9）；
    //吊柱下方通行区净空4且零家具（P80包络洪泛可过）。
    //====================================================================
    internal static class L1Rooms
    {
        private const ushort Brick = L1Style.Brick;
        private const ushort Wall = L1Style.Wall;

        //==================== 通用几何 ====================

        //矩形壳体：周边2厚实心+内膛清空刷墙（跨脊房会把脊重新砌墙，口部随后开）
        internal static void StampShell(Rectangle bounds, ushort wall) {
            int shell = DungeonworldMetrics.RoomShellThick;
            for (int x = bounds.Left; x < bounds.Right; x++) {
                for (int y = bounds.Top; y < bounds.Bottom; y++) {
                    bool isShell = x < bounds.Left + shell || x >= bounds.Right - shell
                        || y < bounds.Top + shell || y >= bounds.Bottom - shell;
                    if (isShell) {
                        TileBrush.SetSolid(x, y, Brick);
                    }
                    else {
                        TileBrush.ClearCell(x, y, wall);
                    }
                }
            }
        }

        //地板级门插槽（底沿与室内地板齐平，§2.5接缝规则1）
        internal static DoorSocket FloorDoor(RoomNode room, SocketSide side)
            => new(side, room.FloorTop - 3 - room.Bounds.Top, SocketKind.Door, 3);

        //地板级拱洞插槽（默认4高，主干道感）
        internal static DoorSocket FloorArch(RoomNode room, SocketSide side, int height = 4)
            => new(side, room.FloorTop - height - room.Bounds.Top, SocketKind.Archway, height);

        //开口+可选门板：门板放在壳体内侧列（外侧留门洞框）
        internal static void OpenSide(RoomNode room, SocketSide side, bool withDoor) {
            DoorSocket socket = withDoor ? FloorDoor(room, side) : FloorArch(room, side);
            CorridorRouter.OpenWallSocket(room, socket, Wall);
            room.Sockets.Add(socket);
            if (withDoor) {
                int x = side == SocketSide.Left ? room.Bounds.Left + 1 : room.Bounds.Right - 2;
                L1Style.PlaceDoorPlate(x, room.FloorTop - 1);
            }
        }

        //吊柱（拱廊语法）：从内膛顶垂到地板上方4格，柱底两侧slope收拱角（F24）
        //柱下通行区保持零家具，P80包络可过
        private static void HangingPillar(int left, int interiorTop, int floorTop) {
            int bottom = floorTop - 5;
            for (int y = interiorTop; y <= bottom; y++) {
                TileBrush.SetSolid(left, y, Brick);
                TileBrush.SetSolid(left + 1, y, Brick);
            }
            TileBrush.SetSloped(left - 1, bottom, Brick, SlopeType.SlopeUpLeft);
            TileBrush.SetSloped(left + 2, bottom, Brick, SlopeType.SlopeUpRight);
        }

        //==================== #3 前厅 Narthex ====================

        /// <summary>
        /// 前厅：正门与层脊之间的缓冲框景——对称双柱+柱间挂画，玩家回望正门的构图。
        /// 钉死在教堂西墙外侧（东侧无壳，直接借教堂外壳），两端拱洞。
        /// </summary>
        internal static RoomNode BuildNarthex(int rightEdge, int floorRow) {
            int interiorH = 10;
            var bounds = new Rectangle(rightEdge - 24, floorRow - interiorH - 2, 24, interiorH + 4);
            //手工壳：只砌西/顶/底三面，东面是教堂自己的壳（不越界重凿，止步于教堂外壳前）
            for (int x = bounds.Left; x < bounds.Right - 2; x++) {
                for (int y = bounds.Top; y < bounds.Bottom; y++) {
                    bool shell = x < bounds.Left + 2
                        || y < bounds.Top + 2 || y >= floorRow;
                    if (shell) {
                        TileBrush.SetSolid(x, y, Brick);
                    }
                    else {
                        TileBrush.ClearCell(x, y, Wall);
                    }
                }
            }
            var room = new RoomNode { Bounds = bounds, Role = RoomRole.Entry };
            OpenSide(room, SocketSide.Left, withDoor: false);

            int interiorTop = bounds.Top + 2;
            int cx = bounds.Left + 12;
            HangingPillar(cx - 6, interiorTop, floorRow);
            HangingPillar(cx + 4, interiorTop, floorRow);
            //柱间挂画+柱基烛台对
            L1Style.PlacePainting(cx, interiorTop + 3);
            L1Style.PlaceStanding(cx - 8, floorRow - 1, TileID.Candelabras, L1Style.StyleCandelabra);
            L1Style.PlaceStanding(cx + 7, floorRow - 1, TileID.Candelabras, L1Style.StyleCandelabra);
            return room;
        }

        //==================== #5 回廊 Cloister ====================

        /// <summary>
        /// 回廊：连接教堂与散房的拱柱连廊——柱间壁龛轮换雕像/烛台/挂画（F30去重语义）。
        /// 两端拱洞；吊灯同类去重≥15；圆斑做旧集中在尽端（ROOMS-L1 §2.4）。
        /// </summary>
        internal static void FurnishCloister(RoomNode room, UnifiedRandom rand, bool slabAtLeftEnd) {
            int interiorTop = room.InteriorTop;
            int floor = room.FloorTop;
            int left = room.InteriorLeft;
            int right = room.InteriorRight;

            //拱柱阵，柱距10
            var pillars = new System.Collections.Generic.List<int>();
            for (int x = left + 6; x <= right - 8; x += 10) {
                HangingPillar(x, interiorTop, floor);
                pillars.Add(x);
            }
            //柱间内容轮换：雕像(花瓶)/烛台/挂画
            int rotation = 0;
            foreach (int px in pillars) {
                int bayX = px + 5;
                if (bayX >= right - 2) {
                    break;
                }
                switch (rotation % 3) {
                    case 0:
                        L1Style.PlaceStanding(bayX, floor - 1, TileID.Statues, L1Style.StyleVase);
                        break;
                    case 1:
                        L1Style.PlaceStanding(bayX, floor - 1, TileID.Candelabras, L1Style.StyleCandelabra);
                        break;
                    default:
                        L1Style.PlacePainting(bayX, interiorTop + 3);
                        break;
                }
                rotation++;
            }
            //吊灯去重15（F33），挂天花
            for (int x = left + 8; x < right - 4; x += 16) {
                WorldGen.PlaceObject(x, interiorTop, TileID.Chandeliers, mute: true, style: L1Style.StyleChandelier);
            }
            //地面蜡烛低档+做旧圆斑（尽端Slab，"向L2变旧"的预告方向）
            L1Style.ScatterFloorCandles(new Rectangle(left, floor - 2, right - left, 2), floor - 1, 2, 8, rand);
            int diskX = slabAtLeftEnd ? left + 4 : right - 5;
            L1Style.WallDisk(diskX, floor - 4, 4, L1Style.WallSlab);
        }

        //==================== #6 圣器室 Vestry ====================

        /// <summary>
        /// 圣器室：钟声门教学与第一件像样战利品——金箱台座+讲台（桌+圣书）+落地钟。
        /// 钉死在教堂东墙外（西壳=教堂外壳，东口拱洞+Slab过梁=钟声门门面；
        /// 门体本身【待定：帧改写or闸门TP】，运行时BellRiteSystem对接后落体，Wave-1不落门板）。
        /// </summary>
        internal static RoomNode BuildVestry(int leftEdge, int floorRow, UnifiedRandom rand) {
            int interiorH = 7;
            var bounds = new Rectangle(leftEdge, floorRow - interiorH - 2, 20, interiorH + 4);
            //西面借教堂外壳（东门洞在教堂字符画里），从Left+2起砌
            for (int x = bounds.Left + 2; x < bounds.Right; x++) {
                for (int y = bounds.Top; y < bounds.Bottom; y++) {
                    bool shell = x >= bounds.Right - 2
                        || y < bounds.Top + 2 || y >= floorRow;
                    if (shell) {
                        TileBrush.SetSolid(x, y, Brick);
                    }
                    else {
                        TileBrush.ClearCell(x, y, Wall);
                    }
                }
            }
            var room = new RoomNode { Bounds = bounds, Role = RoomRole.Treasure };
            OpenSide(room, SocketSide.Right, withDoor: false);
            //钟声门门面：拱洞上沿1格Slab过梁带（§2.5接缝-2，视觉框不改几何）
            for (int x = bounds.Right - 5; x < bounds.Right; x++) {
                if (WorldGen.InWorld(x, floorRow - 5) && Main.tile[x, floorRow - 5].WallType != 0) {
                    Main.tile[x, floorRow - 5].WallType = L1Style.WallSlab;
                }
            }

            int floor = floorRow;
            int cx = bounds.Left + 9;
            //金箱台座（2宽1高砖台+金箱，教学奖励；箱内容占位对位M4）
            TileBrush.SetSolid(cx, floor - 1, Brick);
            TileBrush.SetSolid(cx + 1, floor - 1, Brick);
            L1Style.PlaceChestWithLoot(cx, floor - 2, L1Style.StyleChestGold, gold: true);
            //讲台=桌+圣书（讲台原版无Lectern，D表fallback组合）
            if (L1Style.PlaceStanding(cx - 5, floor - 1, TileID.Tables, L1Style.StyleTable)) {
                L1Style.PlaceOnSurface(cx - 5, floor - 3, TileID.Books, rand.Next(L1Style.BookStyleCount));
            }
            //落地钟：钟声母题呼应（"钟声从这里起数"）
            L1Style.PlaceStanding(cx + 6, floor - 1, TileID.GrandfatherClocks, L1Style.StyleClock);
            //旗帜对+花瓶
            WorldGen.PlaceObject(cx - 3, room.InteriorTop, TileID.Banners, mute: true, style: L1Style.StyleBannerA);
            WorldGen.PlaceObject(cx + 4, room.InteriorTop, TileID.Banners, mute: true, style: L1Style.StyleBannerB);
            //粉Slab小圆斑：最靠近L2过渡带的房间做预告（密度≤L2的1/4，ROOMS-L1 §4）
            L1Style.WallDisk(bounds.Right - 5, floor - 5, 3, L1Style.WallPinkSlab);
            return room;
        }

        //==================== #4 侧廊安全房 SafeRoom ====================

        /// <summary>安全房落口列偏移（距Bounds.Left）：贴左壁，家具全在其右侧协同布置</summary>
        internal const int SafeRoomDropOffset = 2;

        /// <summary>
        /// 侧廊安全房：篝火/床/储物的新手港湾——真门抵住走廊，屋内自带下脊楼梯井。
        /// 只装修内膛；口部/门板/落口由L1Content统一路由。
        /// </summary>
        internal static void FurnishSafeRoom(RoomNode room, UnifiedRandom rand) {
            room.Role = RoomRole.Safe;
            int floor = room.FloorTop;
            //12格内膛精确排布：落口3列(左贴壁)|床4|篝火3|箱2；更宽内膛自然留缝
            L1Style.PlaceBed(room.InteriorLeft + 4, floor - 1, 1);
            L1Style.PlaceStanding(room.InteriorLeft + 8, floor - 1, TileID.Campfire, 0);
            L1Style.PlaceChestWithLoot(room.InteriorRight - 2, floor - 1, L1Style.StyleChestWood, gold: false);
            //旗帜一面（克制，居家不排场）
            WorldGen.PlaceObject(room.InteriorLeft + 6, room.InteriorTop, TileID.Banners, mute: true,
                style: rand.NextBool() ? L1Style.StyleBannerA : L1Style.StyleBannerB);
        }

        //==================== #7 制烛间 Chandlery ====================

        /// <summary>
        /// 制烛间：烛光身份的"来源"房，纯氛围——长桌+烛阵+罐；本层蜡泪做旧密度峰值点。
        /// 只装修内膛；口部由L1Content链边路由（工坊两侧拱洞不设门）。
        /// </summary>
        internal static void FurnishChandlery(RoomNode room, UnifiedRandom rand) {
            int floor = room.FloorTop;
            int left = room.InteriorLeft;
            //长桌=两张地牢桌拼接，桌面满铺蜡烛
            for (int i = 0; i < 2; i++) {
                int tx = left + 3 + i * 3;
                if (L1Style.PlaceStanding(tx, floor - 1, TileID.Tables, L1Style.StyleTable)) {
                    L1Style.PlaceOnSurface(tx - 1 + rand.Next(2), floor - 3, TileID.Candles, L1Style.StyleCandle);
                    L1Style.PlaceOnSurface(tx + 1, floor - 3, TileID.Candles, L1Style.StyleCandle);
                }
            }
            //工作台+椅（制烛台面）、储物梳妆台、罐两只
            L1Style.PlaceStanding(left + 10, floor - 1, TileID.WorkBenches, L1Style.StyleWorkbench);
            L1Style.PlaceStanding(left + 12, floor - 1, TileID.Chairs, L1Style.StyleChair);
            L1Style.PlaceStanding(room.InteriorRight - 3, floor - 1, TileID.Dressers, L1Style.StyleDresser);
            L1Style.PlacePot(room.InteriorRight - 6, floor - 1, rand);
            L1Style.PlacePot(room.InteriorRight - 5, floor - 1, rand);
            //地面蜡烛群（蜡烬母题保守解，密度全层峰值）
            L1Style.ScatterFloorCandles(new Rectangle(left, floor - 2, room.InteriorRight - left, 2), floor - 1, 4, 3, rand);
        }

        //==================== #10 后楼梯井口房 Stairhead ====================

        /// <summary>
        /// 后楼梯井口房：通L2次级通道的"进度保险丝"口部预留——
        /// 本房自带的下脊楼梯井（入口路由）即井口本体；告示牌"下行有怪"+石像鬼看门对。
        /// 向L2的井身与隔离带穿透归A路垂直连接清单（P20），本波只做口部房。
        /// </summary>
        internal static void FurnishStairhead(RoomNode room, UnifiedRandom rand) {
            int floor = room.FloorTop;
            int cx = (room.InteriorLeft + room.InteriorRight) / 2;
            //告示牌+石像鬼看门对（避开中央落口列±1）
            L1Style.PlaceSignWithText(cx - 4, floor - 1, "下行有怪。");
            L1Style.PlaceStanding(cx - 6, floor - 1, TileID.Statues, L1Style.StyleStatueGargoyle);
            L1Style.PlaceStanding(cx + 4, floor - 1, TileID.Statues, L1Style.StyleStatueGargoyle);
            //旗帜对+粉Slab预告斑（最接近L2过渡带的房间，密度≤L2的1/4）
            WorldGen.PlaceObject(cx - 2, room.InteriorTop, TileID.Banners, mute: true, style: L1Style.StyleBannerA);
            WorldGen.PlaceObject(cx + 2, room.InteriorTop, TileID.Banners, mute: true, style: L1Style.StyleBannerB);
            L1Style.WallDisk(cx, floor - 3, 3, L1Style.WallPinkSlab);
        }

        //==================== #8 忏悔室 Confessional（prefab，公共构件蓝皮）====================

        //跨层公共形制（INDEX §4：10x6级双隔间基准的蓝砖版，16x9含壳）：
        //两侧D口贯通层脊，中隔1宽墙+内门'+'，烛台座椅槽即检查点语义留位
        private static readonly string[] ConfessionalArt = [
            "################",
            "################",
            "##......#.....##",
            "##......#.....##",
            "DD............DD",
            "DD..b...+..h..DD",
            "DD.g........g.DD",
            "################",
            "################",
        ];

        private static Prefab _confessional;
        internal static Prefab Confessional => _confessional ??= Prefab.Parse("L1Confessional", ConfessionalArt, L1Style.Legend);

        /// <summary>
        /// 忏悔室：检查点（公共构件换皮：蓝基调）——运行时检查点系统对接前先落形制。
        /// 返回等效RoomNode供房间图登记。
        /// </summary>
        internal static RoomNode StampConfessional(int left, int floorRow) {
            Prefab prefab = Confessional;
            int top = floorRow + 2 - prefab.Height;
            prefab.StampGeometry(left, top, Brick, Wall, L1Style.PlatformFrameY);
            FurnishReport report = prefab.PlaceFurniture(left, top);
            CWRMod.Instance.Logger.Info(
                $"[L1] 忏悔室@({left},{top}) placed={report.Placed} rejected={report.Rejected}");
            return new RoomNode {
                Bounds = new Rectangle(left, top, prefab.Width, prefab.Height),
                Role = RoomRole.Safe
            };
        }

        //==================== 上廊 Gallery（回廊高处形态）====================

        /// <summary>
        /// 上廊：回望中殿的高处走廊（回廊花名册的上层形态）——
        /// 从教堂唱诗席夹层门进入，挂画与看门石像鬼，尽端封死。
        /// 东侧无壳（借教堂西墙），钉死在夹层门外。
        /// </summary>
        internal static RoomNode BuildGallery(int rightEdge, int mezzRow, UnifiedRandom rand) {
            int interiorH = 7;
            var bounds = new Rectangle(rightEdge - 48, mezzRow - interiorH - 2, 48, interiorH + 4);
            //东面借教堂西墙（夹层门在教堂字符画里）
            for (int x = bounds.Left; x < bounds.Right - 2; x++) {
                for (int y = bounds.Top; y < bounds.Bottom; y++) {
                    bool shell = x < bounds.Left + 2
                        || y < bounds.Top + 2 || y >= mezzRow;
                    if (shell) {
                        TileBrush.SetSolid(x, y, Brick);
                    }
                    else {
                        TileBrush.ClearCell(x, y, Wall);
                    }
                }
            }
            var room = new RoomNode { Bounds = new Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height) };
            //内容：挂画三幅+石像鬼+烛台对+长椅（高处静观）
            int floor = mezzRow;
            int left = bounds.Left + 2;
            L1Style.PlacePainting(left + 8, bounds.Top + 4);
            L1Style.PlacePainting(left + 22, bounds.Top + 4);
            L1Style.PlacePainting(left + 36, bounds.Top + 4);
            L1Style.PlaceStanding(left + 3, floor - 1, TileID.Statues, L1Style.StyleStatueGargoyle);
            L1Style.PlaceStanding(left + 14, floor - 1, TileID.Candelabras, L1Style.StyleCandelabra);
            L1Style.PlaceStanding(left + 28, floor - 1, TileID.Benches, L1Style.StyleSofa);
            L1Style.PlaceStanding(left + 38, floor - 1, TileID.Candelabras, L1Style.StyleCandelabra);
            L1Style.ScatterFloorCandles(new Rectangle(left, floor - 2, 44, 2), floor - 1, 2, 10, rand);
            return room;
        }
    }
}
