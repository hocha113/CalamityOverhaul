using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L6
{
    //L6铸造机关层 材质与样式表(ROOMS-L6 §0/§2;样式号逐条对TML源核实,行号见注释)
    //主题锚:锈橙铸造场——齿轮没停过,每条走廊都上了膛
    //认领裁决(INDEX §3):做旧签名=焦油(焦痕+油渍,黑漆族,与L2棕锈/L1灰烟分家);
    //锁链/骨/书纸/水/蛛网均非本层母题,一律不用
    internal static class L6Palette
    {
        //==================== 砖与墙(RESEARCH §1.1a 蓝套件;墙基调 Tiled为主 §1.2) ====================

        internal const ushort Brick = TileID.BlueDungeonBrick;                 //41
        internal const ushort CrackedBrick = TileID.CrackedBlueDungeonBrick;   //481(F31假地板语言)
        internal const ushort WallBase = WallID.BlueDungeonUnsafe;             //7(约5%点缀)
        internal const ushort WallSlab = WallID.BlueDungeonSlabUnsafe;         //94(约20%)
        internal const ushort WallTiled = WallID.BlueDungeonTileUnsafe;        //95(主体~75%;派系2=狱甲/恶魔法师/烈焰轮,F28)

        //机械材质:原版Cog块(TileID.cs L1241核实=272),台座/轴承座/机件堆砌的fallback
        internal const ushort CogBlock = TileID.Cog;
        //落石巢的岩质补丁:placeTrap type1要求巢区含>=3格石/土/泥(对源WorldGen.cs L5697-5709)
        internal const ushort NestStone = TileID.Stone;

        //==================== 平台/门(RESEARCH §1.1d:墙7→平台108、门style16) ====================

        /// <summary>蓝地牢平台 frameY=108(WorldGen大平台段 墙7→108)</summary>
        internal const short PlatformFrameY = DungeonworldMetrics.PlatformFrameY;
        /// <summary>蓝地牢门 style 16(WorldGen.cs L27965-27981:墙7→16)</summary>
        internal const int DoorStyle = 16;

        //==================== 光源族=黄铜灯笼(炉光为主,灯"低"档;与L2链灯笼/L1吊灯族分家) ====================

        /// <summary>黄铜灯笼 tile42 style1(Item.cs L20265-20278:item1390 BrassLantern placeStyle=1)</summary>
        internal const int LanternBrassStyle = 1;
        /// <summary>蓝地牢吊灯 tile34 style27(WorldGen.cs L28617-28621 墙7=27),仅大厅定点</summary>
        internal const int ChandelierStyle = 27;

        //==================== 家具样式(WorldGen.cs L29164-29177 蓝地牢列,与L1Style同源) ====================

        internal const int TableStyle = 10;        //桌 tile14
        internal const int ChairStyle = 13;        //椅 tile15
        internal const int WorkBenchStyle = 11;    //工作台 tile18
        internal const int CandleStyle = 1;        //蜡烛 tile33
        internal const int CandelabraStyle = 22;   //烛台 tile100

        //旗帜 tile91 按墙变体两两分组(WorldGen.cs L28807-28817:基础10/11,Slab12/13,Tiled14/15)
        internal const int BannerBaseA = 10;
        internal const int BannerSlabA = 12;
        internal const int BannerTiledA = 14;

        //==================== 容器/机件/杂物 ====================

        internal const int ChestLockedGoldStyle = 2;   //锁金箱 tile21(F35房间箱)
        internal const int ChestBarrelStyle = 5;       //木桶 tile21(Item.cs L7714-7723)
        internal const int PotStyleMin = 10;           //地牢罐样式10~12(WorldGen.cs L13368-13374)
        internal const int PotStyleMax = 12;           //含上界

        //金属锭堆 tile239(MetalBars):铁=style2(Item.cs L3799-3813 item22)、铅=style3(L12320-12332 item704)
        internal const int BarIronStyle = 2;
        internal const int BarLeadStyle = 3;

        //传送带 tile421/422:纯碰撞机制(Collision.cs L3419读TileID.Sets.ConveyorDirection),
        //无需接线即运转,子世界UpdateMech停摆不影响(F17豁免);电线只做反向,本层不接
        internal const ushort BeltPushRight = TileID.ConveyorBeltLeft;   //421→方向+1
        internal const ushort BeltPushLeft = TileID.ConveyorBeltRight;   //422→方向-1

        //==================== 焦油做旧签名(INDEX §3:焦痕+油渍;全paint层 §3.2-6) ====================

        /// <summary>焦油/烟黑=黑漆(PaintID.cs L55);L2棕锈、L1灰烟均不借用</summary>
        internal const byte TarPaint = PaintID.BlackPaint;
        /// <summary>灰烬缘=灰漆(PaintID.cs L59),仅渣堆/炉缘少量点缀</summary>
        internal const byte AshPaint = PaintID.GrayPaint;

        /// <summary>
        /// 地面油渍条:自(x,floorRow)沿+dx方向给地板砖面刷黑漆(len格)。
        /// 机关段前的引导线——wire原版语义玩家不可见(INDEX §7),
        /// "看得见的上膛"由油渍+压板+箭垛表达。paintTile只染有物块格,自动跳空。
        /// </summary>
        internal static void OilStreakFloor(int x, int floorRow, int len, int dx = 1) {
            for (int i = 0; i < len; i++) {
                int px = x + i * dx;
                if (!WorldGen.InWorld(px, floorRow, 5)) {
                    return;
                }
                WorldGen.paintTile(px, floorRow, TarPaint);
            }
        }

        /// <summary>
        /// 焦痕放射斑:以(cx,cy)为心的半径r圆盘,墙面刷黑漆(熔炉/轴座背景)。
        /// 只染本层地牢墙族,不动物块层;圆缘1格按1/2掷灰烬漆过渡。
        /// </summary>
        internal static void ScorchDisk(int cx, int cy, int r) {
            int r2 = r * r;
            for (int x = cx - r; x <= cx + r; x++) {
                for (int y = cy - r; y <= cy + r; y++) {
                    if (!WorldGen.InWorld(x, y, 5)) {
                        continue;
                    }
                    int d2 = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                    if (d2 > r2) {
                        continue;
                    }
                    Tile tile = Main.tile[x, y];
                    if (tile.WallType != WallBase && tile.WallType != WallSlab && tile.WallType != WallTiled) {
                        continue;
                    }
                    bool rim = d2 > (r - 1) * (r - 1);
                    tile.WallColor = rim && WorldGen.genRand.NextBool(2) ? AshPaint : TarPaint;
                }
            }
        }

        /// <summary>焦油垂滴:自(x,yTop)向下给墙面刷黑漆len格(轴座/落石巢下缘),遇实心即停</summary>
        internal static void TarDrip(int x, int yTop, int len) {
            for (int i = 0; i < len; i++) {
                int y = yTop + i;
                if (!WorldGen.InWorld(x, y, 5) || Main.tile[x, y].HasTile) {
                    return;
                }
                WorldGen.paintWall(x, y, TarPaint);
            }
        }

        //==================== 放置助手(镜像 L2Palette/GaolBossRoom:以场上出现为准) ====================

        /// <summary>挂件锚定放置(纵向试两格),成功以场上出现该tile为准</summary>
        internal static bool TryPlaceObject(int x, int y, int type, int style) {
            for (int dy = 0; dy <= 1; dy++) {
                WorldGen.PlaceObject(x, y + dy, type, mute: true, style: style);
                if (Main.tile[x, y + dy].HasTile && Main.tile[x, y + dy].TileType == type) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>放置校验版PlaceTile:落地后核对类型,失败返回false交调用方记日志</summary>
        internal static bool TryPlaceTile(int x, int y, int type, int style = 0) {
            WorldGen.PlaceTile(x, y, type, mute: true, forced: false, -1, style);
            return Main.tile[x, y].HasTile && Main.tile[x, y].TileType == type;
        }

        /// <summary>告示牌+文本(PlaceSign对源WorldGen.cs L35944;ReadSign→TextSign先例=L1Style)</summary>
        internal static bool PlaceSignWithText(int x, int standRow, string text) {
            if (!WorldGen.PlaceSign(x, standRow, TileID.Signs)) {
                return false;
            }
            int sign = Sign.ReadSign(x, standRow);
            if (sign >= 0) {
                Sign.TextSign(sign, text);
            }
            return true;
        }

        /// <summary>按锚点所处墙变体取旗帜样式(基础10/11、Slab12/13、Tiled14/15,对源同上)</summary>
        internal static int BannerStyleFor(int x, int y) {
            ushort wall = Main.tile[x, y].WallType;
            int baseStyle = wall == WallSlab ? BannerSlabA : wall == WallBase ? BannerBaseA : BannerTiledA;
            return baseStyle + (WorldGen.genRand.NextBool(2) ? 1 : 0);
        }

        /// <summary>圆斑混墙(F32手法局部版):只把本层地牢墙族刷成newWall,纯wall层</summary>
        internal static void WallDisk(int cx, int cy, int radius, ushort newWall) {
            int r2 = radius * radius;
            for (int x = cx - radius; x <= cx + radius; x++) {
                for (int y = cy - radius; y <= cy + radius; y++) {
                    if (!WorldGen.InWorld(x, y, 5)) {
                        continue;
                    }
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) > r2) {
                        continue;
                    }
                    Tile tile = Main.tile[x, y];
                    if (tile.WallType == WallBase || tile.WallType == WallSlab || tile.WallType == WallTiled) {
                        tile.WallType = newWall;
                    }
                }
            }
        }
    }
}
