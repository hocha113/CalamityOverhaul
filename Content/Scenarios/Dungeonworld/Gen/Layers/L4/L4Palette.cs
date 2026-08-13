using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L4
{
    //L4水牢/下水道 材质与样式表(ROOMS-L4 §0/§2,样式号全部对TML源逐符号核实,行号见各注释)
    //主题锚:绿砖半淹管廊——水位是这层唯一的门;做旧签名=双水线痕+苔藓(INDEX §3,与L2锈渍垂痕分家:
    //本层水线是"横向paint带",L2是"纵向垂痕",形态两轴以上差异)
    internal static class L4Palette
    {
        //==================== 砖与墙(F12绿砖43/裂纹绿482;F13墙8/98) ====================

        internal const ushort Brick = TileID.GreenDungeonBrick;
        internal const ushort CrackedBrick = TileID.CrackedGreenDungeonBrick;
        internal const ushort WallBase = WallID.GreenDungeonUnsafe;      //墙8,水线上主调
        internal const ushort WallSlab = WallID.GreenDungeonSlabUnsafe;  //墙98,水线下主调("泡旧")
        //格栅块:排水口盖板视觉,Main.tileSolid[546]=true(Main.cs L10009对源),可安全当地板
        internal const ushort Grate = TileID.Grate;

        //==================== 平台/门(对源WorldGen.cs) ====================

        /// <summary>绿地牢平台 frameY=144(WorldGen.cs L28207-28208:墙8→144)</summary>
        internal const short PlatformFrameY = 144;
        /// <summary>绿地牢门 style 17(WorldGen.cs L27973-27974:墙8→17)</summary>
        internal const int DoorStyle = 17;

        //==================== 家具样式(WorldGen.cs L29180-29195 墙8列) ====================

        internal const int TableStyle = 11;        //绿地牢桌 tile14(style2)
        internal const int ChairStyle = 14;        //绿地牢椅 tile15(style)
        internal const int WorkBenchStyle = 12;    //绿地牢工作台 tile18(style3)
        internal const int CandleStyle = 2;        //绿地牢蜡烛 tile33(num8)
        internal const int CandelabraStyle = 23;   //绿地牢烛台 tile100(num15)
        internal const int LampStyle = 25;         //绿地牢落地灯 tile93(num16)

        //==================== 光源族=油布壁灯(与L1吊灯/L2链灯笼分家) ====================

        /// <summary>油布壁灯 tile42 style6(Item 1395 OilRagSconse placeStyle=6,Item.cs L20335-20347)</summary>
        internal const int LanternSconceStyle = 6;

        //==================== 容器/机件 ====================

        internal const int ChestWaterStyle = 17;   //水箱 tile21(Item 1298 placeStyle=17,Item.cs L19051-19060)
        internal const int ChestBarrelStyle = 5;   //木桶 tile21(Item.cs L7714-7723,沿L2成规)
        internal const int PotStyleMin = 10;       //地牢罐样式10~12(WorldGen.cs L13368-13371)
        internal const int PotStyleMax = 13;       //Next上界(不含)
        //拉杆tile132:2x2底锚(TileObjectData.cs L3762-3771),两态水位机的钩子占位+闸门电驱源
        //高闸门tile388/389:1x5上下实心锚(TileObjectData.cs L2457-2489),电驱开合
        //(Wiring.cs L1532-1538:HitWire→ShiftTallGate+NetMessage自带联机同步)

        //==================== paint层(PaintID.cs对源) ====================

        /// <summary>满水线痕=灰漆(PaintID.cs L59),双水线做旧签名的上线</summary>
        internal const byte HighLinePaint = PaintID.GrayPaint;
        /// <summary>排水线痕=黑漆(PaintID.cs L55),双水线做旧签名的下线</summary>
        internal const byte LowLinePaint = PaintID.BlackPaint;
        /// <summary>苔藓斑=深绿漆(PaintID.cs L39),水线下密、水线上稀</summary>
        internal const byte MossPaint = PaintID.DeepGreenPaint;
        /// <summary>干涸舱段龟裂感=棕漆点(仅L4→L5过渡预告用,非L2锈渍垂痕形态)</summary>
        internal const byte DryCrackPaint = PaintID.BrownPaint;

        //==================== 告示文案(硬编码中文沿L1 PlaceSignWithText成规;game-prose-voice已过) ====================

        //阀室告示池(genRand轮换)
        internal static readonly string[] ValveSignTexts = [
            "放水前敲管三下。回声断在哪一舱,哪一舱的堰坎就漏。",
            "灰线是满水位,黑线是排空位。水停在哪条线,路照哪条线走。",
            "排空后底泥没过脚踝,先探杆,再落脚。",
        ];
        //主泵房交接簿(呼应箴言8的"七代"传承,不用齿轮意象,ROOMS-L4 §3)
        internal const string PumpLogSignText = "泵房交接簿,末页:上油的人到我是第七代。泵没坏过,坏的一直是闸。";
        //落水缓冲厅
        internal const string SplashSignText = "上面掉下来的,都归这池子接。下去捞之前,先看水面还有没有别的东西在动。";
        //沉没囚室
        internal const string SunkenCellSignText = "水下的栅门早锈死了,栅条上的缺口够一个人侧身。";
        //最底一组阀室:L4→L5隔离带预告(骨/水互斥,INDEX §3)
        internal const string DryApproachSignText = "再往下就没有水了。骨头要干着搁,潮了会烂。";

        //==================== 放置助手(镜像L2Palette/GaolBossRoom成规:落地后核对,失败交调用方记日志) ====================

        internal static bool TryPlaceTile(int x, int y, int type, int style = 0) {
            WorldGen.PlaceTile(x, y, type, mute: true, forced: false, -1, style);
            return Main.tile[x, y].HasTile && Main.tile[x, y].TileType == type;
        }

        //挂件锚定放置:纵向试两格,以场上出现为准
        internal static bool TryPlaceObject(int x, int y, int type, int style) {
            for (int dy = 0; dy <= 1; dy++) {
                WorldGen.PlaceObject(x, y + dy, type, mute: true, style: style);
                if (Main.tile[x, y + dy].HasTile && Main.tile[x, y + dy].TileType == type) {
                    return true;
                }
            }
            return false;
        }

        //2x2拉杆:内部锚定可能微调落位,按2x2邻域验收(镜像L2 PlaceSignChecked思路)
        internal static bool TryPlaceLever(int x, int standRow) {
            WorldGen.PlaceTile(x, standRow, TileID.Lever, mute: true);
            for (int dx = -1; dx <= 1; dx++) {
                for (int dy = -1; dy <= 0; dy++) {
                    Tile t = Main.tile[x + dx, standRow + dy];
                    if (t.HasTile && t.TileType == TileID.Lever) {
                        return true;
                    }
                }
            }
            return false;
        }

        //1x5高闸门(388关闭态):槽顶格放置,按整槽验收
        internal static bool TryPlaceTallGate(int x, int slotTopY) {
            WorldGen.PlaceObject(x, slotTopY, TileID.TallGateClosed, mute: true);
            for (int dy = 0; dy < 5; dy++) {
                Tile t = Main.tile[x, slotTopY + dy];
                if (t.HasTile && (t.TileType == TileID.TallGateClosed || t.TileType == TileID.TallGateOpen)) {
                    return true;
                }
            }
            return false;
        }

        //告示牌+文本(镜像L1Style.PlaceSignWithText:PlaceSign→ReadSign→TextSign,Sign.cs L33/L75对源)
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

        //==================== 水下沉链(INDEX §3裁决:L4锁链唯一许可形态=地面横躺) ====================

        /// <summary>
        /// 自(x,y)向右横铺 length 节链(tile 214)。与L2垂链的差异化:横向、贴地、只落在水中。
        /// 直写不清液体(TileBrush会清LiquidAmount,故不走它);只占空格,遇实心即停。返回实际节数。
        /// </summary>
        internal static int LaySunkenChain(int x, int y, int length) {
            int placed = 0;
            for (int i = 0; i < length; i++) {
                if (!WorldGen.InWorld(x + i, y, 5)) {
                    break;
                }
                Tile tile = Main.tile[x + i, y];
                if (tile.HasTile || tile.LiquidAmount == 0) {
                    break;
                }
                tile.HasTile = true;
                tile.TileType = TileID.Chain;
                tile.Slope = SlopeType.Solid;
                tile.IsHalfBlock = false;
                placed++;
            }
            return placed;
        }

        //==================== paint助手(§3.2-6:做旧全走wall/paint层,不动碰撞几何) ====================

        /// <summary>水线痕:沿指定行给墙面刷横向漆带(paintWall只染有墙格,自动跳过实心与无墙格)</summary>
        internal static void PaintWaterlineRow(int left, int right, int y, byte paint) {
            for (int x = left; x < right; x++) {
                if (!WorldGen.InWorld(x, y, 5) || Main.tile[x, y].HasTile) {
                    continue;
                }
                ushort wall = Main.tile[x, y].WallType;
                if (wall == WallBase || wall == WallSlab) {
                    WorldGen.paintWall(x, y, paint);
                }
            }
        }

        /// <summary>苔藓斑:以(x,y)为心的十字小斑,只染绿砖面(paintTile只染实心格)</summary>
        internal static int MossDaub(int x, int y) {
            int painted = 0;
            for (int dx = -1; dx <= 1; dx++) {
                for (int dy = -1; dy <= 1; dy++) {
                    if (dx != 0 && dy != 0) {
                        continue;
                    }
                    int px = x + dx, py = y + dy;
                    if (!WorldGen.InWorld(px, py, 5)) {
                        continue;
                    }
                    Tile t = Main.tile[px, py];
                    if (t.HasTile && (t.TileType == Brick || t.TileType == CrackedBrick)
                        && WorldGen.paintTile(px, py, MossPaint)) {
                        painted++;
                    }
                }
            }
            return painted;
        }

        /// <summary>
        /// 墙面分带刷斑:水线下Slab为主(约2/3)、水线上基础墙为主(约1/6 Slab),
        /// "长年泡着"的分带做旧(ROOMS-L4 §2.4;F32圆斑手法的逐格简化版,纯wall层)。
        /// 只改写既有地牢墙,调用方保证区间已刷底墙。
        /// </summary>
        internal static void BandWalls(int left, int right, int top, int bottom, int waterlineRow) {
            for (int x = left; x < right; x++) {
                for (int y = top; y < bottom; y++) {
                    if (!WorldGen.InWorld(x, y, 5) || Main.tile[x, y].HasTile) {
                        continue;
                    }
                    Tile t = Main.tile[x, y];
                    if (t.WallType != WallBase && t.WallType != WallSlab) {
                        continue;
                    }
                    int slabChance = y >= waterlineRow ? 3 : 6;   //下带2/3,上带1/6
                    bool slab = y >= waterlineRow
                        ? !WorldGen.genRand.NextBool(slabChance)
                        : WorldGen.genRand.NextBool(slabChance);
                    t.WallType = slab ? WallSlab : WallBase;
                }
            }
        }
    }
}
