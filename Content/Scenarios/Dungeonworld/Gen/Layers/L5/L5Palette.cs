using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L5
{
    //L5万骨窖 材质与样式表(ROOMS-L5 §0/§2;样式号全部对TML源逐符号核实,行号见各注释)
    //主题锚:骨砌的墓城坑道，骨头咬得比灰浆紧,集市篝火是中途唯一的暖
    //母题纪律(INDEX §3):骨=本层全权;锁链只许"绷直承重悬链"(井链锚顶锚底/吊笼吊索),
    //禁松垂散链(L2形态)与铐;做旧签名=尘白+蛛网(白漆横向水洗,与L1蜡泪窄垂线两轴区分)
    internal static class L5Palette
    {
        //==================== 砖与墙(RESEARCH §1.1a粉套件44/9/483;变体墙96/97) ====================

        internal const ushort Brick = TileID.PinkDungeonBrick;
        //裂纹粉483:本层严格限定坑陷阱语言(预告+假地板),不作装饰过渡，保"裂=危险"可读性(F31)
        internal const ushort CrackedBrick = TileID.CrackedPinkDungeonBrick;
        //骨块=骨砌主材(TileID.cs L1085);群系计数靠粉砖收边与墙面维持(F12/F13)
        internal const ushort Bone = TileID.BoneBlock;

        internal const ushort WallBase = WallID.PinkDungeonUnsafe;      //9(WallID.cs L80),集市局部
        internal const ushort WallSlab = WallID.PinkDungeonSlabUnsafe;  //96(L254)主体，刷怪派系1+尖刺球白送(F28)
        internal const ushort WallTiled = WallID.PinkDungeonTileUnsafe; //97(L256)"更老的骨窖区":圣骨堂/骨井/深巷

        //==================== 平台/门 ====================

        /// <summary>骨平台 frameY=4x18(Item.cs L11425-11434 item634:tile19 placeStyle4)</summary>
        internal const short PlatformBone = 4 * 18;
        /// <summary>粉地牢平台 frameY=126(RESEARCH §1.1d-6墙9→126),脊接驳沿层带标准</summary>
        internal const short PlatformPink = 7 * 18;

        //==================== 骨家具(Bone Welder族,全部对源) ====================

        internal const int ChairBone = 7;        //骨椅 tile15(Item.cs L13614-13623 item808)
        internal const int TableBone = 4;        //骨桌 tile14(Item.cs L13855-13863 item827)
        internal const int WorkBenchBone = 4;    //骨工作台 tile18(Item.cs L13656-13664 item811)
        internal const int ChandelierBone = 21;  //骨吊灯 tile34(Item.cs L25470-25479 item2144:18+2144-2141=21)
        internal const int LanternBone = 25;     //骨灯笼 tile42(Item.cs L25487-25498 item2148:22+2148-2145=25)
        internal const int LanternCaged = 2;     //笼灯 tile42(L2Palette先例),吊笼组合的承重物
        internal const int CampfireBone = 7;     //骨篝火 tile215(Item.cs L37134-37143 item3724:6+3724-3723=7)
        internal const int CandlePink = 3;       //粉地牢蜡烛 tile33(L2Palette先例,WorldGen.cs L29167段墙9列)
        internal const int CandelabraPink = 24;  //粉地牢烛台 tile100(L2Palette先例,WorldGen.cs L29543)

        //==================== 容器/杂物 ====================

        internal const int ChestLockedGold = 2;  //锁金箱 tile21(F35房间箱轮换),圣骨堂大奖占位
        internal const int PotStyleMin = 10;     //地牢罐10~12(WorldGen.cs L13368地牢墙罐段),骨灰瓮fallback
        internal const int PotStyleMax = 13;     //Next上界(不含)
        internal const int BannerMarketA = 12;   //集市幡=变体墙组12/13(WorldGen.cs L28807-28817两两分组)
        internal const int BannerMarketB = 13;
        //墓碑 tile85:Style2x2底锚Origin(0,1)(TileObjectData.cs L3892-3897);
        //样式0~5=六件套(Item.cs L17375-17383 item1173 placeStyle1等)
        internal const int TombstoneStyles = 6;

        //骨堆样式段(对源WorldGen.cs地牢语境:L14337-14348修正段+L14406骨段判定):
        //tile185 1x1骨=X[12,23]、2x1骨=X[6,15];tile186大骨堆=style[0,6](L14127-14130)
        internal const int SmallBone1x1Min = 12, SmallBone1x1Max = 24;   //Next上界(不含)
        internal const int SmallBone2x1Min = 6, SmallBone2x1Max = 16;
        internal const int LargeBoneStyles = 7;

        /// <summary>尘白做旧=白漆26(PaintID.cs L57);paintTile/paintWall对源WorldGen.cs L36273/L36384</summary>
        internal const byte DustPaint = PaintID.WhitePaint;

        //==================== 绷直承重悬链(与L2死铁垂链的形态切割) ====================

        /// <summary>
        /// 绷紧链柱:自(x,yTop)向下逐格落链(tile214可攀爬)直到遇实心或铺满rows。
        /// 用于骨井"锚顶锚底"全跨链与吊笼吊索，调用方构造保证顶端贴实心/平台、
        /// 底端有承重物(平台篮/井底),不得用于无载荷的松垂散链(INDEX §3裁决)。
        /// 返回实际落链节数。直写镜像 L2Palette.HangChain 先例。
        /// </summary>
        internal static int TautChain(int x, int yTop, int rows) {
            int placed = 0;
            for (int i = 0; i < rows; i++) {
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
        /// 吊笼(链+笼灯组合,ROOMS-L5 §2.1 fallback):天花双绷链→3宽骨平台篮→篮下挂笼灯。
        /// 灯笼tile42有平台顶锚alternate(TileObjectData.cs L2641-2655),
        /// CanPlace自动遍历alternates(TileObject.cs L175-215),挂平台下合法。
        /// (cx,ceilingY)=天花下第一空行中列;须先保证下方净空。成功返回true。
        /// </summary>
        internal static bool HangingBasket(int cx, int ceilingY, UnifiedRandom rand) {
            int drop = rand.Next(4, 8);
            //净空预检:3宽x(链长+平台+灯2+缓冲2)全空
            for (int dx = -1; dx <= 1; dx++) {
                for (int dy = 0; dy < drop + 4; dy++) {
                    if (!WorldGen.InWorld(cx + dx, ceilingY + dy, 5) || Main.tile[cx + dx, ceilingY + dy].HasTile) {
                        return false;
                    }
                }
            }
            //两端锚点上方须实心(吊索有处可锚)
            if (!IsSolid(cx - 1, ceilingY - 1) || !IsSolid(cx + 1, ceilingY - 1)) {
                return false;
            }
            TautChain(cx - 1, ceilingY, drop);
            TautChain(cx + 1, ceilingY, drop);
            for (int dx = -1; dx <= 1; dx++) {
                TileBrush.SetPlatform(cx + dx, ceilingY + drop, PlatformBone);
            }
            //承重物:篮下笼灯(拒绝不回滚，链+空篮也是合法吊架形态)
            TryPlaceObject(cx, ceilingY + drop + 1, TileID.HangingLanterns, LanternCaged);
            return true;
        }

        //==================== 墙变体混斑(F32圆斑手法的确定性版) ====================

        //Tiled占比与块盐:成片而非逐格,否则墙面变成椒盐噪点
        private const int TiledCoverage = 18;
        private const int TiledSalt = 0x2D6F;

        /// <summary>
        /// 主体Slab里成片切出Tiled补丁,让整层墙面不再是单一变体。
        /// 只动Slab，Base是集市语义、Tiled是圣骨堂/骨井/深巷的"更老"语义,那两种都有出处不能乱铺。
        /// 零genRand消耗(块散列),不动R4随机流。
        /// </summary>
        internal static int MixWallVariants(Rectangle area) {
            int changed = 0;
            for (int x = area.Left; x < area.Right; x++) {
                for (int y = area.Top; y < area.Bottom; y++) {
                    if (!WorldGen.InWorld(x, y, 5)) {
                        continue;
                    }
                    Tile tile = Main.tile[x, y];
                    if (tile.HasTile || tile.WallType != WallSlab
                        || !LayerTint.BlockPatch(x, y, TiledCoverage, TiledSalt)) {
                        continue;
                    }
                    tile.WallType = WallTiled;
                    changed++;
                }
            }
            return changed;
        }

        //==================== 尘白做旧(全paint层,§3.2-6;签名与蛛网配对) ====================

        /// <summary>横向水洗:矩形内墙面刷白漆(paintWall无墙自动跳过),骨面拉平明度</summary>
        internal static void DustWallWash(int left, int top, int right, int bottom) {
            for (int x = left; x < right; x++) {
                for (int y = top; y < bottom; y++) {
                    if (WorldGen.InWorld(x, y, 5) && !Main.tile[x, y].HasTile) {
                        WorldGen.paintWall(x, y, DustPaint);
                    }
                }
            }
        }

        /// <summary>地表尘斑:自(x,y)向右沿地板面刷白漆len格(只染实心地表格)</summary>
        internal static int DustFloorRun(int x, int y, int len) {
            int painted = 0;
            for (int i = 0; i < len; i++) {
                int px = x + i;
                if (!WorldGen.InWorld(px, y, 5)) {
                    break;
                }
                //地表定义:本格实心且上格无物(尘落在能看见的面上)
                if (Main.tile[px, y].HasTile && !Main.tile[px, y - 1].HasTile
                    && WorldGen.paintTile(px, y, DustPaint)) {
                    painted++;
                }
            }
            return painted;
        }

        //==================== 放置校验助手(镜像L2Palette,拒绝即false交调用方记账) ====================

        /// <summary>锚定放置:纵向试两格,以场上出现为准(GaolBossRoom.TryPlaceObject先例)</summary>
        internal static bool TryPlaceObject(int x, int y, int type, int style) {
            for (int dy = 0; dy <= 1; dy++) {
                WorldGen.PlaceObject(x, y + dy, type, mute: true, style: style);
                if (Main.tile[x, y + dy].HasTile && Main.tile[x, y + dy].TileType == type) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>放置校验版PlaceTile:落地后核对类型</summary>
        internal static bool TryPlaceTile(int x, int y, int type, int style = 0) {
            WorldGen.PlaceTile(x, y, type, mute: true, forced: false, -1, style);
            return Main.tile[x, y].HasTile && Main.tile[x, y].TileType == type;
        }

        /// <summary>大骨堆tile186:样式[0,6](对源L14127-14130地牢分支),站立行放置</summary>
        internal static bool PlaceLargeBones(int x, int standRow, UnifiedRandom rand)
            => TryPlaceTile(x, standRow, TileID.LargePiles, rand.Next(LargeBoneStyles));

        /// <summary>小骨堆tile185:2/3掷2x1骨段否则1x1骨段(PlaceSmallPile自带锚定)</summary>
        internal static bool PlaceSmallBones(int x, int standRow, UnifiedRandom rand) {
            return rand.Next(3) != 0
                ? WorldGen.PlaceSmallPile(x, standRow, rand.Next(SmallBone2x1Min, SmallBone2x1Max), 1)
                : WorldGen.PlaceSmallPile(x, standRow, rand.Next(SmallBone1x1Min, SmallBone1x1Max), 0);
        }

        /// <summary>骨灰瓮fallback=地牢罐(样式10~12)</summary>
        internal static bool PlaceUrn(int x, int standRow, UnifiedRandom rand)
            => WorldGen.PlacePot(x, standRow, TileID.Pots, rand.Next(PotStyleMin, PotStyleMax));

        /// <summary>墓碑 tile85 六件套(样式0~5),上带散点用,去重距由撒布条目保证≥20</summary>
        internal static bool PlaceTombstone(int x, int standRow, UnifiedRandom rand)
            => TryPlaceObject(x, standRow, TileID.Tombstones, rand.Next(TombstoneStyles));

        internal static bool IsPinkDungeonWall(ushort wall)
            => wall == WallBase || wall == WallSlab || wall == WallTiled;

        /// <summary>巷口"最后的灯":自(x,y)向上探≤6行找实心天花,挂骨灯笼</summary>
        internal static bool MouthLantern(int x, int y) {
            for (int i = 0; i <= 6; i++) {
                int py = y - i;
                if (!WorldGen.InWorld(x, py - 1, 5)) {
                    return false;
                }
                if (Main.tile[x, py].HasTile) {
                    return false;
                }
                if (IsSolid(x, py - 1)) {
                    return TryPlaceObject(x, py, TileID.HangingLanterns, LanternBone);
                }
            }
            return false;
        }

        internal static bool IsSolid(int x, int y) {
            if (!WorldGen.InWorld(x, y, 5)) {
                return false;
            }
            Tile t = Main.tile[x, y];
            return t.HasTile && Main.tileSolid[t.TileType] && t.TileType != TileID.Platforms;
        }
    }
}
