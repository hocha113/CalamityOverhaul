using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L7
{
    //====================================================================
    //L7倒吊教堂 样式与材质表（ROOMS-L7 §0/§2，Wave-2一律原版fallback资产）
    //倒相材质方案（L1→L7替换表，ROOMS-L7 §0）：
    //  砖：蓝地牢砖41原样 + 深紫漆全面压暗（"蓝砖+深色paint"，paint不入群系计数F10/F12）
    //  墙：自定义冥紫墙属资产波（INDEX §8），本波保守解=蓝Tiled 95全覆盖
    //      （95∈wallDungeon表F13群系成立；F28派系2=终层杂怪，恰合ROOMS-L7 §0刷怪含义）
    //  彩窗：L1蓝彩玻璃90 → 紫彩玻璃88（同窗异相，LOADING-SCREEN §5-VII冥紫#5E55A8）
    //  平台/门/家具：沿用蓝地牢族样式号（同族变调），身份差异由深紫漆承担【待签字】
    //做旧签名=倒挂蜡泪+冥紫染圆斑（INDEX §3，全paint层不动碰撞几何§3.2-6）
    //纪律：本层锁链只许≥3格宽巨型结构链束（INDEX §3认领）；零陷阱；撒布全定点=零声明
    //====================================================================
    internal static class L7Style
    {
        //===材质基调===
        internal const ushort Brick = TileID.BlueDungeonBrick;
        //蓝Tiled墙（WallID.cs:252 BlueDungeonTileUnsafe=95，F13在wallDungeon表内）
        internal const ushort Wall = WallID.BlueDungeonTileUnsafe;
        //收边/过梁用蓝Slab（WallID.cs:250），与Tiled主体形成轮廓描边
        internal const ushort WallSlab = WallID.BlueDungeonSlabUnsafe;
        //倒置玫瑰窗=紫彩玻璃（WallID.cs:238 PurpleStainedGlass=88，与L1蓝彩玻璃90成对）
        internal const ushort WallGlass = WallID.PurpleStainedGlass;
        //蓝地牢平台帧（墙7族→frameY=108，RESEARCH §1.1d-6；Tiled无专属平台，沿用蓝族）
        internal const short PlatformFrameY = DungeonworldMetrics.PlatformFrameY;

        //===漆（PaintID.cs对源核实：DeepPurple=22/White=26）===
        //冥紫变调主漆：深紫漆刷砖（"明度压暗、偏紫"的保守解【待签字】）
        internal const byte PaintPurple = PaintID.DeepPurplePaint;
        //倒挂蜡泪：白漆（与L1蜡泪同料——同一教堂的蜡，倒吊后垂向深渊）
        internal const byte PaintWax = PaintID.WhitePaint;

        //===定点仪式光（ROOMS-L7 §0光照：候选骨火把/恶魔火把族紫相【待签字】）===
        //TorchID.cs对源核实：Demon=7（紫光）、Bone=13
        internal const int TorchDemon = TorchID.Demon;
        internal const int TorchBone = TorchID.Bone;

        //===家具样式（蓝地牢族，样式号与L1Style同源=WorldGen.cs墙7分支，行号见L1Style注）===
        internal const int StyleDoor = 16;         //蓝地牢门 tile10
        internal const int StyleBench = 6;         //长椅(沙发) tile89
        internal const int StyleCandle = 1;        //蜡烛 tile33
        internal const int StyleCandelabra = 22;   //烛台 tile100
        internal const int StyleChandelier = 27;   //吊灯 tile34
        internal const int StyleBannerA = 10;      //旗帜 tile91 基础墙组
        internal const int StyleBannerB = 11;
        //锁金箱 tile21 style2（F35房间箱；L2Palette同款已对源）——终点宝库顶位
        internal const int StyleChestLockedGold = 2;

        //===锁链（tile214，TileID.cs:1125；直写先例=L2Palette.HangChain/GaolBossRoom）===
        //INDEX §3裁决：L7只许≥3格宽巨型结构链束；本表不提供单列链接口
        internal const int BundleMinWidth = 3;

        //==================== 巨型链束（≥3宽，直写+顶锚构造保证）====================

        /// <summary>
        /// 自(xLeft..xLeft+width-1, yTop)向下铺length行链束。宽度断言≥3（INDEX §3）。
        /// 每列只在空格上落链、遇实心即停（调用方保证yTop上方为实心=顶锚）。
        /// 返回实际落链格数。
        /// </summary>
        internal static int ChainBundle(int xLeft, int width, int yTop, int length) {
            if (width < BundleMinWidth) {
                throw new System.InvalidOperationException(
                    $"[L7] 链束宽{width}<{BundleMinWidth}，违反INDEX §3巨型链束认领");
            }
            int placed = 0;
            for (int x = xLeft; x < xLeft + width; x++) {
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
            }
            return placed;
        }

        /// <summary>
        /// 断链垂端：每列自[searchTop,searchBottom)内最低实心格的下一格起垂length行。
        /// 建筑底面的"垂落链束末端"用（教堂下腹/深渊剪影），列内找不到锚即跳过该列。
        /// </summary>
        internal static int ChainBundleBelowSolid(int xLeft, int width, int searchTop, int searchBottom, int length) {
            if (width < BundleMinWidth) {
                throw new System.InvalidOperationException(
                    $"[L7] 链束宽{width}<{BundleMinWidth}，违反INDEX §3巨型链束认领");
            }
            int placed = 0;
            for (int x = xLeft; x < xLeft + width; x++) {
                int anchor = -1;
                for (int y = searchBottom - 1; y >= searchTop; y--) {
                    if (WorldGen.InWorld(x, y) && Main.tile[x, y].HasTile
                        && Main.tileSolid[Main.tile[x, y].TileType]) {
                        anchor = y;
                        break;
                    }
                }
                if (anchor < 0) {
                    continue;
                }
                for (int i = 1; i <= length; i++) {
                    int y = anchor + i;
                    if (!WorldGen.InWorld(x, y, 5) || Main.tile[x, y].HasTile) {
                        break;
                    }
                    Tile tile = Main.tile[x, y];
                    tile.HasTile = true;
                    tile.TileType = TileID.Chain;
                    tile.Slope = SlopeType.Solid;
                    tile.IsHalfBlock = false;
                    tile.LiquidAmount = 0;
                    placed++;
                }
            }
            return placed;
        }

        //==================== 放置助手（拒绝即false交调用方记日志，F9纪律）====================

        /// <summary>校验版PlaceTile：落地后核对类型（镜像L2Palette.TryPlaceTile）</summary>
        internal static bool TryPlaceTile(int x, int y, int type, int style = 0) {
            WorldGen.PlaceTile(x, y, type, mute: true, forced: false, -1, style);
            return Main.tile[x, y].HasTile && Main.tile[x, y].TileType == type;
        }

        /// <summary>挂件锚定放置：纵向试两格，以场上出现为准（镜像L2Palette.TryPlaceObject）</summary>
        internal static bool TryPlaceObject(int x, int y, int type, int style) {
            for (int dy = 0; dy <= 1; dy++) {
                WorldGen.PlaceObject(x, y + dy, type, mute: true, style: style);
                if (Main.tile[x, y + dy].HasTile && Main.tile[x, y + dy].TileType == type) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>仪式火把（定点光引线用；墙面锚定由室内墙保证，拒绝记日志）</summary>
        internal static bool PlaceTorch(int x, int y, int torchStyle) {
            if (TryPlaceTile(x, y, TileID.Torches, torchStyle)) {
                return true;
            }
            CWRMod.Instance.Logger.Warn($"[L7] 仪式火把style{torchStyle}@({x},{y})放置失败,跳过");
            return false;
        }

        /// <summary>门板：F4上下实心由调用方槽语法保证，拒绝记日志</summary>
        internal static bool PlaceDoorPlate(int x, int bottomRow) {
            WorldGen.PlaceObject(x, bottomRow, TileID.ClosedDoor, mute: true, style: StyleDoor);
            bool ok = Main.tile[x, bottomRow].HasTile && Main.tile[x, bottomRow].TileType == TileID.ClosedDoor;
            if (!ok) {
                CWRMod.Instance.Logger.Warn($"[L7] 门板@({x},{bottomRow})PlaceObject拒绝,跳过");
            }
            return ok;
        }

        /// <summary>定点挂画（ROOMS-L7 §2.2：定点2~3幅，正挂+深色paint；帧不可倒挂F事实）</summary>
        internal static bool PlacePainting(int x, int y) {
            if (Main.tile[x, y].HasTile) {
                return false;
            }
            var entry = WorldGen.RandPictureTile();
            WorldGen.PlaceTile(x, y, entry.tileType, mute: true, forced: false, -1, entry.style);
            if (!Main.tile[x, y].HasTile) {
                return false;
            }
            PaintTileArea(x - 3, y - 2, x + 3, y + 2, entry.tileType, PaintPurple);
            return true;
        }

        /// <summary>终点宝库锁金箱+占位战利品（M4轮换表顶位后补）</summary>
        internal static void PlaceVaultChest(int x, int standRow) {
            int index = WorldGen.PlaceChest(x, standRow, notNearOtherChests: false, style: StyleChestLockedGold);
            if (index < 0) {
                CWRMod.Instance.Logger.Warn($"[L7] 终库锁金箱@({x},{standRow})放置失败,跳过");
                return;
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
            Add(ItemID.GreaterHealingPotion, 3);
            Add(ItemID.GoldCoin, 5);
            Add(ItemID.BoneTorch, 20);
        }

        //==================== wall/paint层（§3.2-6三层安全手段）====================

        /// <summary>指定tile类型的区域刷漆（旗帜/挂画压暗用，只染目标类型）</summary>
        internal static void PaintTileArea(int left, int top, int right, int bottom, ushort type, byte paint) {
            for (int x = left; x <= right; x++) {
                for (int y = top; y <= bottom; y++) {
                    if (!WorldGen.InWorld(x, y)) {
                        continue;
                    }
                    Tile tile = Main.tile[x, y];
                    if (tile.HasTile && tile.TileType == type) {
                        tile.TileColor = paint;
                    }
                }
            }
        }

        /// <summary>
        /// 冥紫变调主漆：区内所有蓝地牢砖实心刷深紫漆（含斜切/半砖）。
        /// 家具/平台/链为其他tile类型天然跳过；paint不影响群系计数（F10/F12）。
        /// </summary>
        internal static long PurpleSweep(Rectangle area) {
            long painted = 0;
            for (int x = area.Left; x < area.Right; x++) {
                for (int y = area.Top; y < area.Bottom; y++) {
                    if (!WorldGen.InWorld(x, y)) {
                        continue;
                    }
                    Tile tile = Main.tile[x, y];
                    if (tile.HasTile && tile.TileType == Brick) {
                        tile.TileColor = PaintPurple;
                        painted++;
                    }
                }
            }
            return painted;
        }

        /// <summary>冥紫染圆斑：墙面深紫漆圆盘（做旧签名的染色半边，INDEX §3【待签字】）</summary>
        internal static void PurpleWallDisk(int cx, int cy, int radius) {
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
                    if (tile.WallType == Wall || tile.WallType == WallSlab) {
                        tile.WallColor = PaintPurple;
                    }
                }
            }
        }

        /// <summary>
        /// 倒挂蜡泪：自(x,yTop)向下给墙面刷白漆len行（蜡凝在"天花板"=原地板上，
        /// 垂向深渊方向；遇实心即停——蜡泪挂在下垂物末端的语义）。
        /// </summary>
        internal static void WaxDrip(int x, int yTop, int len) {
            for (int i = 0; i < len; i++) {
                int y = yTop + i;
                if (!WorldGen.InWorld(x, y)) {
                    return;
                }
                Tile tile = Main.tile[x, y];
                if (tile.HasTile) {
                    return;
                }
                if (tile.WallType == Wall || tile.WallType == WallSlab) {
                    tile.WallColor = PaintWax;
                }
            }
        }

        /// <summary>
        /// 做旧收尾：区内扫描顶锚光源（吊灯），其正下方墙面倒挂蜡泪+锚点冥紫小圆斑。
        /// 与L1Style.AgeLightsInRect成对（L1蜡泪垂在地板上方，L7蜡自天花垂向深渊）。
        /// </summary>
        internal static void AgeInvertedInRect(Rectangle area) {
            for (int x = area.Left; x < area.Right; x++) {
                for (int y = area.Top; y < area.Bottom; y++) {
                    if (!WorldGen.InWorld(x, y) || !Main.tile[x, y].HasTile) {
                        continue;
                    }
                    Tile tile = Main.tile[x, y];
                    if (tile.TileType == TileID.Chandeliers && tile.TileFrameY == 0
                        && tile.TileFrameX % 54 == 0) {
                        //吊灯3宽，帧原点列=左列，蜡泪挂中列正下
                        WaxDrip(x + 1, y + 3, 3);
                        PurpleWallDisk(x + 1, y + 1, 2);
                    }
                }
            }
        }

        //==================== 彩玻璃（同窗异相：L1蓝→L7紫）====================

        /// <summary>倒置玫瑰窗：内盘紫彩玻璃+外缘1格Slab过梁圈（镜像L1Style.StainedGlassDisk）</summary>
        internal static void RoseWindowDisk(int cx, int cy, int radius) {
            int rim = radius + 1;
            int rim2 = rim * rim, r2 = radius * radius;
            for (int x = cx - rim; x <= cx + rim; x++) {
                for (int y = cy - rim; y <= cy + rim; y++) {
                    if (!WorldGen.InWorld(x, y)) {
                        continue;
                    }
                    int d2 = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                    if (d2 > rim2) {
                        continue;
                    }
                    Tile tile = Main.tile[x, y];
                    if (tile.WallType == 0) {
                        continue;
                    }
                    tile.WallType = d2 <= r2 ? WallGlass : WallSlab;
                }
            }
        }

        /// <summary>紫彩玻璃矩形条（尖窗/龛窗），只替换已有墙面</summary>
        internal static void GlassRect(int left, int top, int right, int bottom) {
            for (int x = left; x < right; x++) {
                for (int y = top; y < bottom; y++) {
                    if (WorldGen.InWorld(x, y) && Main.tile[x, y].WallType != 0) {
                        Main.tile[x, y].WallType = WallGlass;
                    }
                }
            }
        }

        //==================== L7撒布声明（契约纪律5的空声明）====================

        /// <summary>
        /// L7撒布母题表=空。ROOMS-L7量产brief禁用清单："一切散布陷阱与撒布装饰pass
        /// （本层全定点）"；INDEX §7矩阵L7列全部为零或定点。灯/挂画/旗帜由构建代码
        /// 定点落位，杂物/蛛网/骨堆/书台=零——P55对本层应无内置条目（矩阵归管线路配置）。
        /// </summary>
        internal static List<ScatterEntry> LayerScatter() => [];
    }
}
