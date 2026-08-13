using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L3
{
    //====================================================================
    //L3大档案馆 材质与样式表(ROOMS-L3 §0/§2,Wave-2一律原版fallback资产)
    //主题锚:纸墨褐的书架迷宫——灯三盏灭两盏,馆里名字比人多
    //蓝地牢族样式号沿用L1已审计常量(WorldGen.cs:29164-29177初值=墙7默认分支),
    //本层新增条目逐条对源核实,行号见注释;做旧签名=墨渍霉斑(INDEX §3,深灰/黑漆系)
    //纪律:家具全走合法锚定放置,拒绝即跳过+记日志(F9/§3.2-1);
    //碰撞几何只在各房型Build内一遍冻结,本文件只动家具/wall/paint层
    //====================================================================
    internal static class L3Palette
    {
        //===材质基调(ROOMS-L3 §0:蓝砖41为主+裂纹蓝481;墙=蓝基7约55%+蓝Slab94约45%)===
        internal const ushort Brick = TileID.BlueDungeonBrick;
        internal const ushort CrackedBrick = TileID.CrackedBlueDungeonBrick;
        internal const ushort WallBase = WallID.BlueDungeonUnsafe;
        internal const ushort WallSlab = WallID.BlueDungeonSlabUnsafe;

        //===平台/门(RESEARCH §1.1d-6:墙7→平台frameY=108;门style见WorldGen.cs:27965-27981)===
        internal const short PlatformFrameY = DungeonworldMetrics.PlatformFrameY;
        internal const int DoorStyle = 16;

        //===家具样式(蓝地牢列,WorldGen.cs:29164-29177初值+:29349-29547 case体)===
        internal const int StyleChair = 13;        //椅 tile15(:29164)
        internal const int StyleTable = 10;        //桌 tile14(:29165)
        internal const int StyleWorkbench = 11;    //工作台 tile18(:29166)
        internal const int StyleCandle = 1;        //蜡烛 tile33(:29167)
        internal const int StyleBookcase = 1;      //书架 tile101(:29169 style4=1+:29494 case3消费)
        internal const int StyleDresser = 5;       //梳妆台 tile88=目录柜(:29172+:29524 case7)
        internal const int StyleCandelabra = 22;   //烛台 tile100(:29175)
        internal const int StyleLamp = 24;         //落地灯 tile93(:29176)
        internal const int StyleClock = 30;        //落地钟 tile104(:29177+:29546 case12)
        internal const int StyleChandelier = 27;   //吊灯 tile34(:28617-28621 墙7=27)
        internal const int StyleBannerA = 10;      //旗帜 tile91 基础墙组(:28807-28817)
        internal const int StyleBannerB = 11;
        //灯笼 tile42:链灯笼style0(Item136无placeStyle→0)/笼灯style2(Item1391),L2已核先例
        internal const int StyleLanternChain = 0;
        internal const int StyleLanternCaged = 2;

        //===容器/杂物===
        internal const int ChestWoodStyle = 0;     //木箱 tile21(死端奖励)
        internal const int ChestGoldStyle = 1;     //金箱 tile21(禁书区大奖,Item.cs:7333-7346)
        internal const int PotStyleMin = 10;       //地牢罐10~12(WorldGen.cs:13368-13374)
        internal const int PotStyleMax = 12;
        //书样式0~4安全;5=水矢法书(WorldGen.cs:28397-28400 frameX=90),按L1裁决全层禁用
        internal const int BookStyleCount = 5;

        //===做旧签名=墨渍霉斑(INDEX §3:深灰/黑漆系,全paint层,观感【待签字】)===
        internal const byte PaintInk = PaintID.BlackPaint;    //墨渍
        internal const byte PaintMold = PaintID.GrayPaint;    //霉斑

        //==================== 放置助手(镜像L2Palette/L1Style先例,自包含不跨层引用) ====================

        /// <summary>挂件锚定放置:纵向试两格,以场上出现为准(镜像GaolBossRoom先例)</summary>
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

        //桌面/台面小物(书/瓶/蜡烛/杯):先验证支承面再放(镜像原版桌面摆件分支:29387-29413)
        internal static bool PlaceOnSurface(int x, int y, int type, int style = 0) {
            if (Main.tile[x, y].HasTile || !Main.tile[x, y + 1].HasTile) {
                return false;
            }
            return TryPlaceTile(x, y, type, style);
        }

        /// <summary>桌面书:样式0~4(5=水矢书禁用);成功返回true</summary>
        internal static bool PlaceBook(int x, int y, UnifiedRandom rand)
            => PlaceOnSurface(x, y, TileID.Books, rand.Next(BookStyleCount));

        /// <summary>墨瓶:tile13放置后按原版书台分支(:28425-28434)掷frameX=18/36变体</summary>
        internal static bool PlaceInkBottle(int x, int y, UnifiedRandom rand) {
            if (!PlaceOnSurface(x, y, TileID.Bottles)) {
                return false;
            }
            Main.tile[x, y].TileFrameX = (short)(rand.NextBool() ? 18 : 36);
            return true;
        }

        //门板:1x3,口部开洞后补门,PlaceObject自带F4上下实心校验
        internal static bool PlaceDoorPlate(int x, int bottomRow) {
            WorldGen.PlaceObject(x, bottomRow, TileID.ClosedDoor, mute: true, style: DoorStyle);
            return Main.tile[x, bottomRow].HasTile && Main.tile[x, bottomRow].TileType == TileID.ClosedDoor;
        }

        //箱+占位补给(战利品表对位M4;gold=禁书区/风险奖励档,否则死端小奖励档)
        internal static bool PlaceChestWithLoot(int x, int standRow, bool gold) {
            int index = WorldGen.PlaceChest(x, standRow, TileID.Containers,
                notNearOtherChests: false, gold ? ChestGoldStyle : ChestWoodStyle);
            if (index < 0) {
                return false;
            }
            Chest chest = Main.chest[index];
            int slot = 0;
            void Add(int itemId, int stack) {
                if (slot >= chest.item.Length) {
                    return;
                }
                chest.item[slot] = new Item();
                chest.item[slot].SetDefaults(itemId);
                chest.item[slot].stack = stack;
                slot++;
            }
            if (gold) {
                Add(ItemID.GoldCoin, 4);
                Add(ItemID.Book, 3);
                Add(ItemID.HealingPotion, 2);
            }
            else {
                Add(ItemID.SilverCoin, 30);
                Add(ItemID.Book, 2);
                Add(ItemID.LesserHealingPotion, 2);
            }
            return true;
        }

        //告示牌+文本:PlaceSign(WorldGen.cs:35944)→ReadSign建条目→TextSign写文本(L1先例)
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

        //定向挂画:原版随机画池RandPictureTile(:29845),预检空腔与墙面由调用方保证
        internal static bool PlacePainting(int x, int y) {
            if (Main.tile[x, y].HasTile) {
                return false;
            }
            var entry = WorldGen.RandPictureTile();
            WorldGen.PlaceTile(x, y, entry.tileType, mute: true, forced: false, -1, entry.style);
            return Main.tile[x, y].HasTile;
        }

        //==================== wall/paint层:混斑与墨霉(§3.2-6三层安全手段) ====================

        //圆斑混墙:只替换既有地牢蓝墙,纯wall层(F32手法;ROOMS-L3 §2.4半径取大)
        internal static void WallDisk(int cx, int cy, int radius, ushort newWall) {
            int r2 = radius * radius;
            for (int x = cx - radius; x <= cx + radius; x++) {
                for (int y = cy - radius; y <= cy + radius; y++) {
                    if (!WorldGen.InWorld(x, y)) {
                        continue;
                    }
                    int dx = x - cx, dy = y - cy;
                    if (dx * dx + dy * dy > r2) {
                        continue;
                    }
                    Tile tile = Main.tile[x, y];
                    if (tile.WallType == WallBase || tile.WallType == WallSlab) {
                        tile.WallType = newWall;
                    }
                }
            }
        }

        //墨渍垂痕:自锚点向下给墙面刷黑漆,遇实心即停(桌下墨渍/封条用)
        internal static void InkStreak(int x, int yTop, int length) {
            PaintColumn(x, yTop, length, PaintInk);
        }

        //霉斑点簇:阈值散点小盘,55%灰/30%黑/15%跳过(书架底部墙面做旧,密度保守【待签字】)
        internal static void MoldBlotch(int cx, int cy, int radius, UnifiedRandom rand) {
            int r2 = radius * radius;
            for (int x = cx - radius; x <= cx + radius; x++) {
                for (int y = cy - radius; y <= cy + radius; y++) {
                    if (!WorldGen.InWorld(x, y)) {
                        continue;
                    }
                    int dx = x - cx, dy = y - cy;
                    if (dx * dx + dy * dy > r2) {
                        continue;
                    }
                    int roll = rand.Next(100);
                    if (roll >= 85) {
                        continue;
                    }
                    Tile tile = Main.tile[x, y];
                    if (!tile.HasTile && (tile.WallType == WallBase || tile.WallType == WallSlab)) {
                        tile.WallColor = roll < 55 ? PaintMold : PaintInk;
                    }
                }
            }
        }

        /// <summary>
        /// 区内墨霉收尾:扫描书架(tile101)帧原点,沿架底墙面点霉斑;
        /// 扫描工作台/桌(抄写位),桌下刷墨渍。全paint层,不动碰撞几何。
        /// </summary>
        internal static void MoldUnderShelves(Rectangle area, UnifiedRandom rand) {
            for (int x = area.Left; x < area.Right; x++) {
                for (int y = area.Top; y < area.Bottom; y++) {
                    if (!WorldGen.InWorld(x, y) || !Main.tile[x, y].HasTile) {
                        continue;
                    }
                    Tile tile = Main.tile[x, y];
                    //书架3x4:帧原点=frameX%54==0且frameY==0(每样式54px宽)
                    if (tile.TileType == TileID.Bookcases
                        && tile.TileFrameX % 54 == 0 && tile.TileFrameY == 0) {
                        if (rand.NextBool(2)) {
                            MoldBlotch(x + 1, y + 4, 2, rand);
                        }
                    }
                    //抄写桌:工作台2x1帧原点,桌下墨渍
                    else if (tile.TileType == TileID.WorkBenches
                        && tile.TileFrameX % 36 == 0 && rand.NextBool(2)) {
                        InkStreak(x, y + 1, 2);
                        InkStreak(x + 1, y + 1, rand.Next(1, 3));
                    }
                }
            }
        }

        private static void PaintColumn(int x, int yStart, int len, byte paint) {
            for (int i = 0; i < len; i++) {
                int y = yStart + i;
                if (!WorldGen.InWorld(x, y)) {
                    return;
                }
                Tile tile = Main.tile[x, y];
                if (tile.HasTile) {
                    return;
                }
                if (tile.WallType == WallBase || tile.WallType == WallSlab) {
                    tile.WallColor = paint;
                }
            }
        }

        //==================== 共用几何谓词 ====================

        //落点空+脚下实心非平台(地面家具预检,镜像CommonScatter.OnFloor)
        internal static bool OnFloor(int x, int y) {
            if (Main.tile[x, y].HasTile) {
                return false;
            }
            Tile below = Main.tile[x, y + 1];
            return below.HasTile && Main.tileSolid[below.TileType] && below.TileType != TileID.Platforms;
        }

        //已开凿的蓝墙室内(骨架实心区wall=0天然排除)
        internal static bool InBlueInterior(int x, int y) {
            Tile t = Main.tile[x, y];
            return !t.HasTile && (t.WallType == WallBase || t.WallType == WallSlab);
        }
    }
}
