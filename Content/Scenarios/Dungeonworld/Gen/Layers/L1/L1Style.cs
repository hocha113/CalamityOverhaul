using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Prefabs;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L1
{
    //====================================================================
    //L1教堂区 样式与装修数据（ROOMS-L1 §0/§2，Wave-1一律原版fallback资产）
    //本文件三件事：
    //  1.对源核实的家具placeStyle常量表（逐条注TML行号，审计凭据）
    //  2.装修应用助手（合法锚定PlaceTile/PlaceObject、paint做旧、彩窗墙、撒布三段式）
    //  3.给A路撒布引擎的装饰条目声明表（自包含常量，引擎条目格式定稿后适配）
    //纪律：家具全走合法锚定放置，拒绝即跳过+记日志（F9/§3.2-1）；
    //碰撞几何零改动——本文件只动家具/wall/paint层（§3.1-3装修单向性）
    //====================================================================
    internal static class L1Style
    {
        //===材质基调（ROOMS-L1 §0：蓝砖41+蓝基础墙7，圆斑蓝Slab94约10%）===
        internal const ushort Brick = TileID.BlueDungeonBrick;
        internal const ushort Wall = WallID.BlueDungeonUnsafe;
        internal const ushort WallSlab = WallID.BlueDungeonSlabUnsafe;
        //L2预告用粉Slab小圆斑（ROOMS-L1 §4，密度≤L2的1/4）
        internal const ushort WallPinkSlab = WallID.PinkDungeonSlabUnsafe;
        //彩窗fallback：原版蓝彩玻璃墙（WallID.cs:242，INDEX§8"原版有无彩玻璃墙"已核实=有）
        internal const ushort WallStainedGlass = WallID.BlueStainedGlass;

        //===家具样式表（全部对TML源核实；"蓝地牢"列=MakeDungeon墙7默认分支）===
        //WorldGen.cs:29164-29177 样式变量初值 + :29349-29547 case体映射
        internal const int StyleChair = 13;        //椅 tile15（:29164）
        internal const int StyleTable = 10;        //桌 tile14（:29165）
        internal const int StyleWorkbench = 11;    //工作台 tile18（:29166）
        internal const int StyleCandle = 1;        //蜡烛 tile33（:29167）
        internal const int StyleVase = 46;         //蓝地牢花瓶 tile105（:29168；Item.cs:20520-20533 item1408）
        internal const int StyleBed = 5;           //床 tile79（:29170，Place4x2）
        internal const int StylePiano = 11;        //钢琴 tile87（:29171）
        internal const int StyleDresser = 5;       //梳妆台 tile88（:29172）
        internal const int StyleSofa = 6;          //沙发/长椅 tile89（:29173）
        internal const int StyleCandelabra = 22;   //烛台 tile100（:29175）
        internal const int StyleLamp = 24;         //落地灯 tile93（:29176）
        internal const int StyleClock = 30;        //落地钟 tile104（:29177）
        internal const int StyleChandelier = 27;   //吊灯 tile34（:28617-28621 墙7=27）
        internal const int StyleBannerA = 10;      //旗帜 tile91 基础墙组10/11（:28807-28817）
        internal const int StyleBannerB = 11;
        internal const int StyleDoor = 16;         //蓝地牢门 tile10（:27965-27981）
        internal const int StyleStatueAngel = 1;   //天使雕像 tile105（Item.cs:4184-4197 item52）
        internal const int StyleStatueGargoyle = 14;//石像鬼雕像 tile105（Item.cs:9077-9090 item450）
        internal const int StyleStatueCross = 22;  //十字雕像 tile105（Item.cs:9189-9202 item458）
        internal const int StyleChestWood = 0;     //普通箱 tile21（F35暗影钥匙分支同款）
        internal const int StyleChestGold = 1;     //金箱 tile21（Item.cs:7333-7346 item306）
        //地牢罐样式10~12（WorldGen.cs:13368-13374 wallDungeon分支）
        internal const int PotStyleMin = 10;
        internal const int PotStyleMax = 12;
        //书样式0~4安全；5=水矢法书（:28397-28400 frameX=90），深度彩蛋不属L1（F35）
        internal const int BookStyleCount = 5;
        //蓝地牢平台帧（RESEARCH §1.1d-6 墙7→108）
        internal const short PlatformFrameY = DungeonworldMetrics.PlatformFrameY;

        //===做旧签名=蜡泪+烟熏顶（INDEX §3裁决，全paint层【待签字】保守密度）===
        internal const byte PaintWax = PaintID.WhitePaint;   //蜡泪垂痕
        internal const byte PaintSoot = PaintID.GrayPaint;   //烟熏顶

        //杂物堆tile185样式号无法可靠对源（各生成上下文样式段含义不一），
        //按ROOMS-L1 §2.3【待工程确认】执行保守解：本波不撒185，
        //"蜡烬"由地面蜡烛群+蜡漆垂痕表达（资产波再议）

        //==================== 装修应用助手 ====================

        //落地家具：镜像原版MakeDungeon_GroundFurniture的PlaceTile(站立行)用法
        //（WorldGen.cs:29353等），成功以场上出现该tile为准
        internal static bool PlaceStanding(int x, int standRow, int type, int style) {
            if (!WorldGen.InWorld(x, standRow, 5)) {
                return false;
            }
            WorldGen.PlaceTile(x, standRow, type, mute: true, forced: false, -1, style);
            if (Main.tile[x, standRow].HasTile && Main.tile[x, standRow].TileType == type) {
                return true;
            }
            CWRMod.Instance.Logger.Warn($"[L1] 落地家具tile{type}样式{style}@({x},{standRow})放置失败,跳过");
            return false;
        }

        //床：4x2带方向，镜像原版Place4x2用法（WorldGen.cs:43548,:29513）
        internal static bool PlaceBed(int x, int standRow, int direction) {
            WorldGen.Place4x2(x, standRow, TileID.Beds, direction, StyleBed);
            bool ok = Main.tile[x, standRow].HasTile && Main.tile[x, standRow].TileType == TileID.Beds;
            if (!ok) {
                CWRMod.Instance.Logger.Warn($"[L1] 床@({x},{standRow})放置失败,跳过");
            }
            return ok;
        }

        //桌面小物（蜡烛/书/瓶）：先验证支承面，镜像原版桌面摆件分支（:29387-29413）
        internal static bool PlaceOnSurface(int x, int y, int type, int style) {
            if (Main.tile[x, y].HasTile || !Main.tile[x, y + 1].HasTile) {
                return false;
            }
            WorldGen.PlaceTile(x, y, type, mute: true, forced: false, -1, style);
            return Main.tile[x, y].HasTile && Main.tile[x, y].TileType == type;
        }

        //门板：1x3，D槽开洞后补门；机器验证上下实心（F4），拒绝记日志
        internal static bool PlaceDoorPlate(int x, int bottomRow) {
            WorldGen.PlaceObject(x, bottomRow, TileID.ClosedDoor, mute: true, style: StyleDoor);
            bool ok = Main.tile[x, bottomRow].HasTile && Main.tile[x, bottomRow].TileType == TileID.ClosedDoor;
            if (!ok) {
                CWRMod.Instance.Logger.Warn($"[L1] 门板@({x},{bottomRow})PlaceObject拒绝,跳过");
            }
            return ok;
        }

        //箱+基础补给（战利品表对位M4，本波占位少量通货/火把/小疗伤）
        internal static void PlaceChestWithLoot(int x, int standRow, int style, bool gold) {
            int index = WorldGen.PlaceChest(x, standRow, notNearOtherChests: false, style: style);
            if (index < 0) {
                CWRMod.Instance.Logger.Warn($"[L1] 箱样式{style}@({x},{standRow})放置失败,跳过");
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
            if (gold) {
                Add(ItemID.GoldCoin, 3);
                Add(ItemID.HealingPotion, 2);
                Add(ItemID.Torch, 12);
            }
            else {
                Add(ItemID.Torch, 15);
                Add(ItemID.LesserHealingPotion, 3);
                Add(ItemID.SilverCoin, 25);
            }
        }

        //告示牌+文本：PlaceSign（WorldGen.cs:35944）→ReadSign建条目→TextSign写文本
        internal static void PlaceSignWithText(int x, int standRow, string text) {
            if (!WorldGen.PlaceSign(x, standRow, TileID.Signs)) {
                CWRMod.Instance.Logger.Warn($"[L1] 告示牌@({x},{standRow})放置失败,跳过");
                return;
            }
            int sign = Sign.ReadSign(x, standRow);
            if (sign >= 0) {
                Sign.TextSign(sign, text);
            }
        }

        //定向挂画：原版随机画池RandPictureTile（:29845）+原版放置式样（:29014）
        internal static bool PlacePainting(int x, int y) {
            if (Main.tile[x, y].HasTile) {
                return false;
            }
            var entry = WorldGen.RandPictureTile();
            WorldGen.PlaceTile(x, y, entry.tileType, mute: true, forced: false, -1, entry.style);
            return Main.tile[x, y].HasTile;
        }

        //地牢罐（可破战利品点）
        internal static bool PlacePot(int x, int standRow, UnifiedRandom rand)
            => WorldGen.PlacePot(x, standRow, TileID.Pots, rand.Next(PotStyleMin, PotStyleMax + 1));

        //==================== wall/paint 层（§3.2-6 三层安全手段）====================

        //圆斑混墙：只替换既有地牢蓝墙，纯wall层（F32手法的局部版）
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
                    if (tile.WallType == Wall || tile.WallType == WallSlab) {
                        tile.WallType = newWall;
                    }
                }
            }
        }

        //彩窗圆盘：内盘彩玻璃墙+外缘1格Slab过梁（§2.5接缝语法迁移；图案【待签字】取素圆保守解）
        internal static void StainedGlassDisk(int cx, int cy, int radius) {
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
                    tile.WallType = d2 <= r2 ? WallStainedGlass : WallSlab;
                }
            }
        }

        //尖窗（lancet）：矩形彩玻璃墙条，祭坛背景/钟室侧窗用
        internal static void StainedGlassRect(int left, int top, int right, int bottom) {
            for (int x = left; x < right; x++) {
                for (int y = top; y < bottom; y++) {
                    if (WorldGen.InWorld(x, y) && Main.tile[x, y].WallType != 0) {
                        Main.tile[x, y].WallType = WallStainedGlass;
                    }
                }
            }
        }

        //蜡泪+烟熏做旧：扫描区内光源家具（蜡烛/烛台/吊灯），
        //正下墙面刷蜡色短垂线、正上刷烟色（全paint层，密度保守，观感【待签字】）
        internal static void AgeLightsInRect(Rectangle area) {
            for (int x = area.Left; x < area.Right; x++) {
                for (int y = area.Top; y < area.Bottom; y++) {
                    if (!WorldGen.InWorld(x, y) || !Main.tile[x, y].HasTile) {
                        continue;
                    }
                    ushort t = Main.tile[x, y].TileType;
                    //只认帧原点格，防止多格家具重复记账
                    if (Main.tile[x, y].TileFrameX % 18 != 0 && t != TileID.Chandeliers) {
                        continue;
                    }
                    if (t == TileID.Candles || t == TileID.Candelabras) {
                        PaintWallColumn(x, y + 1, 3, PaintWax);
                        PaintWallColumn(x, y - 2, 2, PaintSoot, upward: true);
                    }
                    else if (t == TileID.Chandeliers && Main.tile[x, y].TileFrameY == 0) {
                        PaintWallColumn(x, y - 1, 2, PaintSoot, upward: true);
                    }
                }
            }
        }

        private static void PaintWallColumn(int x, int yStart, int len, byte paint, bool upward = false) {
            for (int i = 0; i < len; i++) {
                int y = upward ? yStart - i : yStart + i;
                if (!WorldGen.InWorld(x, y)) {
                    return;
                }
                Tile tile = Main.tile[x, y];
                //只染地牢墙面且不穿实心（蜡泪停在障碍上，语义自然）
                if (tile.HasTile) {
                    return;
                }
                if (tile.WallType == Wall || tile.WallType == WallSlab) {
                    tile.WallColor = paint;
                }
            }
        }

        //==================== 撒布三段式（F30：撒点→验证→保底退出）====================

        //区内撒地面蜡烛（"蜡烬"母题保守解），同类去重距离dedupe
        internal static int ScatterFloorCandles(Rectangle interior, int floorRow, int count, int dedupe, UnifiedRandom rand) {
            int placed = 0, guard = 0;
            while (placed < count && guard++ < count * 12) {
                int x = rand.Next(interior.Left, interior.Right);
                if (Main.tile[x, floorRow].HasTile || !Main.tile[x, floorRow + 1].HasTile) {
                    continue;
                }
                if (HasSameTypeNearby(x, floorRow, dedupe, TileID.Candles)) {
                    continue;
                }
                if (PlaceStandingQuiet(x, floorRow, TileID.Candles, StyleCandle)) {
                    placed++;
                }
            }
            return placed;
        }

        //区内撒挂画（"标"档），失败保底退出
        internal static int ScatterPaintings(Rectangle interior, int count, UnifiedRandom rand) {
            int placed = 0, guard = 0;
            while (placed < count && guard++ < count * 16) {
                int x = rand.Next(interior.Left + 2, interior.Right - 2);
                int y = rand.Next(interior.Top + 1, interior.Bottom - 3);
                Tile tile = Main.tile[x, y];
                if (tile.HasTile || (tile.WallType != Wall && tile.WallType != WallSlab)) {
                    continue;
                }
                if (PlacePainting(x, y)) {
                    placed++;
                }
            }
            return placed;
        }

        private static bool PlaceStandingQuiet(int x, int standRow, int type, int style) {
            WorldGen.PlaceTile(x, standRow, type, mute: true, forced: false, -1, style);
            return Main.tile[x, standRow].HasTile && Main.tile[x, standRow].TileType == type;
        }

        private static bool HasSameTypeNearby(int cx, int cy, int dist, ushort type) {
            for (int x = cx - dist; x <= cx + dist; x++) {
                for (int y = cy - 4; y <= cy + 4; y++) {
                    if (WorldGen.InWorld(x, y) && Main.tile[x, y].HasTile && Main.tile[x, y].TileType == type) {
                        return true;
                    }
                }
            }
            return false;
        }

        //==================== L1层撒布装修声明（ctx.Scatter条目，P55 ScatterPass执行）====================

        //合法性预检共用件：撒布只落在"已开凿的蓝墙空间"（骨架实心区wall=0天然被排除），
        //且避开彩窗玻璃区（保窗面干净）
        private static bool InBlueInterior(int x, int y) {
            Tile t = Main.tile[x, y];
            return !t.HasTile && (t.WallType == Wall || t.WallType == WallSlab);
        }

        private static bool OnFloorCell(int x, int y) {
            Tile below = Main.tile[x, y + 1];
            return InBlueInterior(x, y) && below.HasTile
                && Main.tileSolid[below.TileType] && below.TileType != TileID.Platforms;
        }

        /// <summary>
        /// L1层撒布母题总表（INDEX §7矩阵L1列：灯=峰全亮/挂画=标/旗帜=标/杂物=低）。
        /// PlanAndBuild把本表灌进ctx.Scatter；水蜡烛/灯笼/书台/蛛网/骨堆为禁用或他层母题不入表；
        /// 杂物堆tile185样式对源不可靠（【待工程确认】），蜡烬用地面蜡烛保守解。
        /// </summary>
        internal static List<ScatterEntry> LayerScatter() => [
            new() {
                Name = "L1吊灯(峰,全亮)", Density = ScatterDensity.Peak,
                StandardPer100k = 2.5, DedupeDist = 15, MaxPlaced = 40,
                TryPlace = static (x, y) => {
                    if (!InBlueInterior(x, y) || !Main.tile[x, y - 1].HasTile) {
                        return false;
                    }
                    //吊挂净空声明：正下≥5空（§3.2-7）
                    for (int i = 0; i <= 5; i++) {
                        if (Main.tile[x, y + i].HasTile) {
                            return false;
                        }
                    }
                    WorldGen.PlaceObject(x, y, TileID.Chandeliers, mute: true, style: StyleChandelier);
                    return Main.tile[x, y].HasTile && Main.tile[x, y].TileType == TileID.Chandeliers;
                },
            },
            new() {
                Name = "L1烛台", Density = ScatterDensity.Standard,
                StandardPer100k = 4, DedupeDist = 10, MaxPlaced = 30,
                TryPlace = static (x, y) => {
                    if (!OnFloorCell(x, y)) {
                        return false;
                    }
                    WorldGen.PlaceTile(x, y, TileID.Candelabras, mute: true, forced: false, -1, StyleCandelabra);
                    return Main.tile[x, y].HasTile && Main.tile[x, y].TileType == TileID.Candelabras;
                },
            },
            new() {
                Name = "L1挂画", Density = ScatterDensity.Standard,
                StandardPer100k = 5, DedupeDist = 12, MaxPlaced = 30,
                TryPlace = static (x, y) => InBlueInterior(x, y) && PlacePainting(x, y),
            },
            new() {
                Name = "L1仪式旗帜", Density = ScatterDensity.Standard,
                StandardPer100k = 5, DedupeDist = 12, MaxPlaced = 30,
                TryPlace = static (x, y) => {
                    if (!InBlueInterior(x, y) || !Main.tile[x, y - 1].HasTile
                        || Main.tile[x, y + 1].HasTile || Main.tile[x, y + 2].HasTile) {
                        return false;
                    }
                    int style = WorldGen.genRand.NextBool() ? StyleBannerA : StyleBannerB;
                    WorldGen.PlaceObject(x, y, TileID.Banners, mute: true, style: style);
                    return Main.tile[x, y].HasTile && Main.tile[x, y].TileType == TileID.Banners;
                },
            },
            new() {
                Name = "L1地面蜡烛(蜡烬)", Density = ScatterDensity.Low,
                StandardPer100k = 6, DedupeDist = 8, MaxPlaced = 24,
                TryPlace = static (x, y) => {
                    if (!OnFloorCell(x, y)) {
                        return false;
                    }
                    WorldGen.PlaceTile(x, y, TileID.Candles, mute: true, forced: false, -1, StyleCandle);
                    return Main.tile[x, y].HasTile && Main.tile[x, y].TileType == TileID.Candles;
                },
            },
        ];

        //==================== 语义槽图例（prefab家族共用一套）====================

        //字符表：A祭坛桌 b长椅 h椅 c烛台 g蜡烛 L吊灯 F/f旗帜 P钢琴 m落地灯
        //        v花瓶 n天使像 X十字像 +门板 W玫瑰窗心(留位) B钟锚(留位) w钟室窗心(留位)
        //镜像对偶只为契约完整（L1不产倒吊变体，L7归他队）：c↔L互换，+/W自保留，其余镜像删除
        internal static PrefabLegend BuildLegend() => new PrefabLegend {
            HalfBrick = HalfBrickMirrorRule.ToPlatform
        }
            .Add(new PrefabSlotDef { Ch = 'A', Name = "祭坛桌", TileType = TileID.Tables, Style = StyleTable, MirrorCh = '\0' })
            .Add(new PrefabSlotDef { Ch = 'b', Name = "长椅", TileType = TileID.Benches, Style = StyleSofa, MirrorCh = '\0' })
            .Add(new PrefabSlotDef { Ch = 'h', Name = "椅", TileType = TileID.Chairs, Style = StyleChair, MirrorCh = '\0' })
            .Add(new PrefabSlotDef { Ch = 'c', Name = "烛台", TileType = TileID.Candelabras, Style = StyleCandelabra, MirrorCh = 'L' })
            .Add(new PrefabSlotDef { Ch = 'g', Name = "蜡烛", TileType = TileID.Candles, Style = StyleCandle, MirrorCh = '\0' })
            .Add(new PrefabSlotDef { Ch = 'L', Name = "吊灯", TileType = TileID.Chandeliers, Style = StyleChandelier, TopAnchor = true, ClearanceBelow = 5, MirrorCh = 'c' })
            .Add(new PrefabSlotDef { Ch = 'F', Name = "旗帜A", TileType = TileID.Banners, Style = StyleBannerA, TopAnchor = true, ClearanceBelow = 3, MirrorCh = '\0' })
            .Add(new PrefabSlotDef { Ch = 'f', Name = "旗帜B", TileType = TileID.Banners, Style = StyleBannerB, TopAnchor = true, ClearanceBelow = 3, MirrorCh = '\0' })
            .Add(new PrefabSlotDef { Ch = 'P', Name = "钢琴(充管风琴)", TileType = TileID.Pianos, Style = StylePiano, MirrorCh = '\0' })
            .Add(new PrefabSlotDef { Ch = 'm', Name = "落地灯", TileType = TileID.Lamps, Style = StyleLamp, MirrorCh = '\0' })
            .Add(new PrefabSlotDef { Ch = 'v', Name = "蓝地牢花瓶", TileType = TileID.Statues, Style = StyleVase, MirrorCh = '\0' })
            .Add(new PrefabSlotDef { Ch = 'n', Name = "天使雕像", TileType = TileID.Statues, Style = StyleStatueAngel, MirrorCh = '\0' })
            .Add(new PrefabSlotDef { Ch = 'X', Name = "十字雕像", TileType = TileID.Statues, Style = StyleStatueCross, MirrorCh = '\0' })
            .Add(new PrefabSlotDef { Ch = '+', Name = "门板", TileType = TileID.ClosedDoor, Style = StyleDoor, MirrorCh = '+' })
            .Add(new PrefabSlotDef { Ch = 'W', Name = "玫瑰窗心留位", MarkerOnly = true, MirrorCh = 'W' })
            .Add(new PrefabSlotDef { Ch = 'B', Name = "大钟锚留位", MarkerOnly = true, MirrorCh = '\0' })
            .Add(new PrefabSlotDef { Ch = 'w', Name = "钟室窗心留位", MarkerOnly = true, MirrorCh = '\0' });

        private static PrefabLegend _legend;
        internal static PrefabLegend Legend => _legend ??= BuildLegend();

        //共享的槽字典（供图例校验/统计）
        internal static IReadOnlyDictionary<char, PrefabSlotDef> LegendSlots => Legend.Slots;
    }
}
