using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L2
{
    //L2牢狱层材质与样式表(ROOMS-L2 §0/§2,样式号全部对TML源逐符号核实,行号见各注释)
    //主题锚:粉砖囚区、死铁与锈;冷粉发光=深牢怨灵禁室独占(INDEX §3裁决2),本层只用不发光死铁
    internal static class L2Palette
    {
        //==================== 砖与墙(RESEARCH §1.1a:粉套件 44/9/483;变体墙 96/97) ====================

        internal const ushort Brick = TileID.PinkDungeonBrick;
        internal const ushort CrackedBrick = TileID.CrackedPinkDungeonBrick;
        internal const ushort WallBase = WallID.PinkDungeonUnsafe;
        internal const ushort WallSlab = WallID.PinkDungeonSlabUnsafe;
        //栅栏视觉墙:牢栅缝里的"铁栏"贴面(WallID.cs L352核实;非地牢墙,仅入1格缝隙玩家不可驻足)
        internal const ushort WallFence = WallID.IronFence;

        //==================== 平台/门(RESEARCH §1.1d:墙9→平台126、门style18) ====================

        /// <summary>粉地牢平台 frameY=126(style 7×18,WorldGen大平台段 墙9→126)</summary>
        internal const short PlatformFrameY = 7 * 18;
        /// <summary>粉地牢门 style 18(WorldGen.cs L27976-27978:墙9→18)</summary>
        internal const int DoorStyle = 18;

        //==================== 光源族=链灯笼(与L1吊灯族、禁室笼灯分家) ====================

        /// <summary>链灯笼 tile42 style0(Item 136 ChainLantern无placeStyle→0)</summary>
        internal const int LanternChainStyle = 0;
        /// <summary>笼灯 tile42 style2(Item 1391 CagedLantern placeStyle=2),看守室点缀用</summary>
        internal const int LanternCagedStyle = 2;

        //==================== 家具样式(WorldGen.cs L29196-29210 墙9列;烛台L29543 num15=24) ====================

        internal const int TableStyle = 12;        //粉地牢桌 tile14
        internal const int ChairStyle = 15;        //粉地牢椅 tile15
        internal const int WorkBenchStyle = 13;    //粉地牢工作台 tile18
        internal const int CandleStyle = 3;        //粉地牢蜡烛 tile33
        internal const int CandelabraStyle = 24;   //粉地牢烛台 tile100
        internal const int BookcaseStyle = 3;      //粉地牢书架 tile101(登记房单点豁免,INDEX §3)

        //==================== 容器/杂物 ====================

        internal const int ChestLockedGoldStyle = 2;   //锁金箱 tile21(F35房间箱)
        internal const int ChestBarrelStyle = 5;       //木桶 tile21(Item.cs L7714-7723)
        internal const int PotStyleMin = 10;           //地牢罐样式10~12(WorldGen.cs L13368-13371)
        internal const int PotStyleMax = 13;           //Next上界(不含)
        internal const int BannerStyleBase = 10;       //基础墙纹章旗 10/11(WorldGen.cs L28807-28817)

        //骨堆样式(WorldGen.cs L14337-14348地牢语境段/L14127-14130大堆段)
        internal const int SmallBone1x1Min = 12, SmallBone1x1Max = 23;  //tile185 第0行 X∈[12,22]
        internal const int SmallBone2x1Min = 6, SmallBone2x1Max = 11;   //tile185 第1行 X∈[6,10]
        internal const int LargeBoneMax = 7;                            //tile186 style∈[0,6]

        /// <summary>锈渍垂痕=棕漆(PaintID.cs L61),L2做旧签名(INDEX §3,与L1蜡泪同构不同色)</summary>
        internal const byte RustPaint = PaintID.BrownPaint;

        //==================== 死铁垂链(直写镜像 GaolBossRoom.SetChain 先例,顶锚由调用方构造保证) ====================

        /// <summary>
        /// 自(x, yTop)向下铺 length 节链(tile 214,可攀爬)。
        /// 调用方必须保证 yTop 上方是实心(顶锚构造保证);只在空格上落链,遇实心即停。
        /// 返回实际落链节数。
        /// </summary>
        internal static int HangChain(int x, int yTop, int length) {
            int placed = 0;
            for (int i = 0; i < length; i++) {
                int y = yTop + i;
                if (!WorldGen.InWorld(x, y, 5)) {
                    break;
                }
                Tile tile = Main.tile[x, y];
                if (tile.HasTile) {
                    break;
                }
                tile.HasTile = true;
                tile.TileType = TileID.Chain;
                tile.Slope = SlopeType.Solid;
                tile.IsHalfBlock = false;
                tile.LiquidAmount = 0;
                placed++;
            }
            return placed;
        }

        /// <summary>
        /// 锈渍垂痕:自锚点正下方起向下给墙面刷棕漆(paintWall只染有墙格,无墙自动跳过)。
        /// 用在链锚/栅根/铐挂点正下方,做旧签名不动碰撞几何(§3.2-6)。
        /// </summary>
        internal static void RustStreak(int x, int yTop, int length) {
            for (int i = 0; i < length; i++) {
                if (!WorldGen.InWorld(x, yTop + i, 5)) {
                    return;
                }
                WorldGen.paintWall(x, yTop + i, RustPaint);
            }
        }

        //==================== 挂件锚定放置(镜像 GaolBossRoom.TryPlaceObject:纵向试两格,以场上出现为准) ====================

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
    }
}
