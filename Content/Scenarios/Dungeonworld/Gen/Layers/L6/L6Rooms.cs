using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Rooms;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L6
{
    //L6房型构建器(ROOMS-L6 §1花名册;井站段/忏悔室=跨层公共构件归公共波,本层不做)
    //全部纯算法;写入只走TileBrush+原版放置函数,拒绝记日志跳过(§3.2-1)
    //告示文本遵循game-prose-voice:具体物件+动词扛戏,机制警示平铺直叙,末句平落
    internal static class L6Rooms
    {
        internal struct Tally
        {
            internal int Placed;
            internal int Rejected;

            internal void Add(bool ok, string what, int x, int y) {
                if (ok) {
                    Placed++;
                }
                else {
                    Rejected++;
                    CWRMod.Instance.Logger.Warn($"[L6Rooms] {what}放置失败 at ({x},{y})");
                }
            }
        }

        //====(告示文案:工头体的车间安全话,具体名词平落地,与L2教学语气呼应)====
        internal const string SignTrapHall = "廊里上了膛。看脚下的板，龛里能躲人。";
        internal const string SignBoulder = "顶上那块石头没焊死。踩了板就跑。";
        internal const string SignSpikeSpan = "裂砖下面埋着刺。沿着裂纹跳，别赌。";
        internal const string SignEpitaph = "装镖机的师傅们葬在上一层。手上的活计还在走。";
        internal const string SignBellBlank = "第七口钟铸了三回。前两回的坯子埋在这堆渣底下。";
        internal const string SignThreshold = "过了这道门，机关全停。下面是倒吊的教堂，第七口钟挂在深渊上头。";

        /// <summary>整包络重盖蓝砖+开内膛(清看样残余),所有房型共用第一遍</summary>
        internal static void StampAndCarve(RoomNode room, ushort wall) {
            for (int x = room.Bounds.Left; x < room.Bounds.Right; x++) {
                for (int y = room.Bounds.Top; y < room.Bounds.Bottom; y++) {
                    TileBrush.SetSolid(x, y, L6Palette.Brick);
                }
            }
            TileBrush.CarveRect(room.InteriorLeft, room.InteriorTop, room.InteriorRight, room.FloorTop, wall);
        }

        //==================== 机关走廊A型(#1主体:一段一母题的机关串) ====================

        internal struct CorridorPlanA
        {
            internal L6Traps.Motif[] Motifs;
            internal int[] Lens;
            internal int Tier;
        }

        /// <summary>掷A型走廊计划:段数按威胁层级(t0:2/t1:2~3/t2:3/t3:4),尺寸即冻结</summary>
        internal static CorridorPlanA RollCorridorA(UnifiedRandom rand, int tier) {
            int segs = tier switch { 0 => 2, 1 => rand.Next(2, 4), 2 => 3, _ => 4 };
            var plan = new CorridorPlanA {
                Motifs = new L6Traps.Motif[segs],
                Lens = new int[segs],
                Tier = tier,
            };
            L6Traps.Motif prev = (L6Traps.Motif)(-1);
            for (int i = 0; i < segs; i++) {
                plan.Motifs[i] = L6Traps.RollMotif(rand, tier, i == 0, prev);
                plan.Lens[i] = L6Traps.RollSegLen(rand, plan.Motifs[i]);
                prev = plan.Motifs[i];
            }
            return plan;
        }

        internal static Point CorridorAInteriorSize(CorridorPlanA plan) {
            int w = 4;   //两端各2列净缓冲(门后无杀招,§2.4-⑥节奏)
            for (int i = 0; i < plan.Lens.Length; i++) {
                w += plan.Lens[i] + (i > 0 ? 2 : 0);   //段间2格实心缓冲带
            }
            return new Point(w, L6Traps.AInteriorH);
        }

        internal static Tally BuildCorridorA(RoomNode room, CorridorPlanA plan, UnifiedRandom rand) {
            var roomTally = new Tally();
            var trapTally = new L6Traps.Tally();
            StampAndCarve(room, L6Palette.WallTiled);

            //回填龛带4行(躲避龛/活塞槽/落石巢的实心底子),行走面净高5
            for (int x = room.InteriorLeft; x < room.InteriorRight; x++) {
                for (int y = room.InteriorTop; y < room.InteriorTop + L6Traps.NicheBand; y++) {
                    TileBrush.SetSolid(x, y, L6Palette.Brick);
                }
            }

            int cursor = room.InteriorLeft + 2;
            for (int i = 0; i < plan.Motifs.Length; i++) {
                int segR = cursor + plan.Lens[i];
                switch (plan.Motifs[i]) {
                    case L6Traps.Motif.Conveyor:
                        L6Traps.SegConveyor(room, cursor, segR, rand, ref trapTally);
                        break;
                    case L6Traps.Motif.Dart:
                        L6Traps.SegDart(room, cursor, segR, rand, ref trapTally);
                        break;
                    case L6Traps.Motif.DartNet:
                        L6Traps.SegDartNet(room, cursor, segR, rand, ref trapTally);
                        break;
                    case L6Traps.Motif.Boulder:
                        L6Traps.SegBoulder(room, cursor, segR, rand, ref trapTally);
                        break;
                    default:
                        L6Traps.SegPistonSlot(room, cursor, segR, rand, ref trapTally);
                        break;
                }
                cursor = segR + 2;
            }

            //走廊入口照明(黄铜灯笼,炉光替代档但禁无光)+致命母题警示牌
            int walkTop = room.InteriorTop + L6Traps.NicheBand;
            roomTally.Add(L6Palette.TryPlaceObject(room.InteriorLeft + 1, walkTop,
                TileID.HangingLanterns, L6Palette.LanternBrassStyle),
                "廊口灯笼", room.InteriorLeft + 1, walkTop);
            string sign = null;
            if (trapTally.WantsSign) {
                sign = SignBoulder;
            }
            else if (plan.Tier >= 3) {
                sign = SignTrapHall;
            }
            if (sign != null) {
                roomTally.Add(L6Palette.PlaceSignWithText(room.InteriorLeft + 1, room.FloorTop - 1, sign),
                    "机关警示牌", room.InteriorLeft + 1, room.FloorTop - 1);
            }

            MergeTrap(ref roomTally, trapTally);
            return roomTally;
        }

        //==================== 机关走廊B型(#1裂地形态:裂砖假地板+下厅双层) ====================

        internal struct CorridorPlanB
        {
            internal int[] SpanWidths;
            internal bool Spikes;
            internal bool DartOver;
        }

        /// <summary>掷B型走廊计划:跨段1~2,t2起解禁刺坑,t3必带追身镖组合</summary>
        internal static CorridorPlanB RollCorridorB(UnifiedRandom rand, int tier) {
            int spans = tier >= 2 && rand.NextBool(2) ? 2 : 1;
            var plan = new CorridorPlanB {
                SpanWidths = new int[spans],
                Spikes = tier >= 2 && (tier >= 3 || rand.NextBool(2)),
                DartOver = tier >= 3 || tier >= 2 && rand.NextBool(2),
            };
            for (int i = 0; i < spans; i++) {
                plan.SpanWidths[i] = rand.Next(6, 10);
            }
            return plan;
        }

        internal static Point CorridorBInteriorSize(CorridorPlanB plan) {
            //两端梯口区各6+每跨段模块(4approach+span+4exit)
            int w = 12;
            foreach (int span in plan.SpanWidths) {
                w += span + 8;
            }
            return new Point(w, L6Traps.BInteriorH);
        }

        internal static Tally BuildCorridorB(RoomNode room, CorridorPlanB plan, UnifiedRandom rand) {
            var roomTally = new Tally();
            var trapTally = new L6Traps.Tally();
            StampAndCarve(room, L6Palette.WallTiled);

            int midFloor = room.FloorTop - 6;   //机关道地板行(下厅净高5+此行)
            //回填龛带与中层地板
            for (int x = room.InteriorLeft; x < room.InteriorRight; x++) {
                for (int y = room.InteriorTop; y < room.InteriorTop + L6Traps.NicheBand; y++) {
                    TileBrush.SetSolid(x, y, L6Palette.Brick);
                }
                TileBrush.SetSolid(x, midFloor, L6Palette.Brick);
            }

            //两端梯口:下厅(对外接驳层)↔机关道可往返
            L6Traps.DeckLadder(room, midFloor, room.InteriorLeft);
            L6Traps.DeckLadder(room, midFloor, room.InteriorRight - 3);

            int cursor = room.InteriorLeft + 6;
            foreach (int span in plan.SpanWidths) {
                int spanL = cursor + 4;
                L6Traps.CrackedSpan(room, midFloor, spanL, spanL + span,
                    plan.Spikes, plan.DartOver, rand, ref trapTally);
                cursor = spanL + span + 4;
            }

            //机关道尽头的躲避龛+走完奖励(罐,战利品表归M4)
            L6Traps.CarvePocket(room, room.InteriorRight - 6, ref trapTally, lantern: true);
            roomTally.Add(WorldGen.PlacePot(room.InteriorRight - 5, midFloor - 1, TileID.Pots,
                rand.Next(L6Palette.PotStyleMin, L6Palette.PotStyleMax + 1)),
                "机关道奖励罐", room.InteriorRight - 5, midFloor - 1);

            //下厅照明:梯口侧实心天花挂灯(裂砖段下方悬灯会随碎裂掉锚,镜像L2教训)
            roomTally.Add(L6Palette.TryPlaceObject(room.InteriorLeft + 4, midFloor + 1,
                TileID.HangingLanterns, L6Palette.LanternBrassStyle),
                "下厅灯笼", room.InteriorLeft + 4, midFloor + 1);
            roomTally.Add(L6Palette.TryPlaceObject(room.InteriorRight - 5, midFloor + 1,
                TileID.HangingLanterns, L6Palette.LanternBrassStyle),
                "下厅灯笼", room.InteriorRight - 5, midFloor + 1);

            if (trapTally.WantsSign) {
                roomTally.Add(L6Palette.PlaceSignWithText(room.InteriorLeft + 4, room.FloorTop - 1,
                    SignSpikeSpan), "刺坑警示牌", room.InteriorLeft + 4, room.FloorTop - 1);
            }

            MergeTrap(ref roomTally, trapTally);
            return roomTally;
        }

        //==================== 铸造大厅(#2:熔炉阵大房,本层门面与光源核心,零机关) ====================

        internal static Point HallInteriorSize(UnifiedRandom rand)
            => new(rand.Next(46, 63), rand.Next(20, 25));

        internal static Tally BuildFoundryHall(RoomNode room, UnifiedRandom rand) {
            var tally = new Tally();
            StampAndCarve(room, L6Palette.WallTiled);
            int floor = room.FloorTop;
            int left = room.InteriorLeft;
            int right = room.InteriorRight;
            int mid = (left + right) / 2;

            //抬高工作面2行(熔渣池沉回真地板),两端1格台阶收边(F3自动登)
            int workFloor = floor - 2;
            for (int x = left; x < right; x++) {
                int h = x == left || x == right - 1 ? 1 : 2;
                for (int dy = 1; dy <= h; dy++) {
                    TileBrush.SetSolid(x, floor - dy, L6Palette.Brick);
                }
            }

            //熔渣池:工作面上开1~2口沉池,池底=真地板,焦油漆池体+地狱熔炉池心
            int basins = rand.NextBool(2) ? 2 : 1;
            var basinRanges = new (int l, int r)[basins];
            for (int b = 0; b < basins; b++) {
                int bw = rand.Next(8, 11);
                int bl = b == 0 ? mid - bw - 2 : mid + 4;
                basinRanges[b] = (bl, bl + bw);
                TileBrush.CarveRect(bl, workFloor, bl + bw, floor, L6Palette.WallTiled);
                for (int x = bl; x < bl + bw; x++) {
                    WorldGen.paintTile(x, floor, L6Palette.TarPaint);
                }
                int bc = bl + bw / 2;
                tally.Add(L6Palette.TryPlaceTile(bc, floor - 1, TileID.Hellforge, 0), "池心地狱熔炉", bc, floor - 1);
                L6Palette.ScorchDisk(bc, floor - 3, 3);
            }

            //炉膛线:工作面上熔炉阵(避开沉池),炉后焦痕放射斑=本层光源核心
            for (int x = left + 4; x < right - 4; x += rand.Next(8, 11)) {
                if (InAnyBasin(x, basinRanges, margin: 3)) {
                    continue;
                }
                bool hell = rand.NextBool(3);
                tally.Add(L6Palette.TryPlaceTile(x, workFloor - 1, hell ? TileID.Hellforge : TileID.Furnaces, 0),
                    hell ? "地狱熔炉" : "熔炉", x, workFloor - 1);
                L6Palette.ScorchDisk(x, workFloor - 3, rand.Next(2, 4));
            }

            //行车梁:通高平台横贯(中央3格断开留吊灯位),梁上双Cog=行车小车剪影
            int beamY = room.InteriorTop + 2;
            TileBrush.PlatformRow(left + 2, mid - 2, beamY, L6Palette.PlatformFrameY);
            TileBrush.PlatformRow(mid + 2, right - 2, beamY, L6Palette.PlatformFrameY);
            TileBrush.SetSolid(mid + 4, beamY, L6Palette.CogBlock);
            TileBrush.SetSolid(mid + 5, beamY, L6Palette.CogBlock);

            //高位输送平台环:两侧廊台+端头之字梯上行(§2.5竖距≤5)
            int galleryY = room.InteriorTop + 6;
            int third = (right - left) / 3;
            TileBrush.PlatformRow(left + 2, left + third, galleryY, L6Palette.PlatformFrameY);
            TileBrush.PlatformRow(right - third, right - 2, galleryY, L6Palette.PlatformFrameY);
            for (int y = workFloor - 4; y > galleryY; y -= 4) {
                TileBrush.PlatformRow(left + 2, left + 5, y, L6Palette.PlatformFrameY);
                TileBrush.PlatformRow(right - 5, right - 2, y, L6Palette.PlatformFrameY);
            }

            //小齿轮留位:高位背景墙,轴承座2x2 Cog+焦痕盘,帧包络6x6登记
            int gearX = mid + 6 + rand.Next(0, 4);
            int gearY = room.InteriorTop + 8;
            L6Palette.ScorchDisk(gearX, gearY, 3);
            TileBrush.SetSolid(gearX - 1, gearY - 1, L6Palette.CogBlock);
            TileBrush.SetSolid(gearX, gearY - 1, L6Palette.CogBlock);
            TileBrush.SetSolid(gearX - 1, gearY, L6Palette.CogBlock);
            TileBrush.SetSolid(gearX, gearY, L6Palette.CogBlock);
            L6Palette.TarDrip(gearX, gearY + 1, 3);
            L6MachineSlots.Register(L6SlotKind.GearSmall,
                new Rectangle(gearX - 3, gearY - 3, 6, 6), "铸造大厅背景小齿轮");

            //铁砧+铁锭码放(车间语汇借一角)+吊灯与旗帜(Tiled样式组)
            tally.Add(L6Palette.TryPlaceTile(left + 6, workFloor - 1, TileID.Anvils, 0), "铁砧", left + 6, workFloor - 1);
            L6Palette.OilStreakFloor(left + 5, workFloor, 4);
            tally.Add(L6Palette.TryPlaceTile(left + 9, workFloor - 1, TileID.MetalBars, L6Palette.BarIronStyle),
                "铁锭堆", left + 9, workFloor - 1);
            tally.Add(L6Palette.TryPlaceTile(left + 10, workFloor - 1, TileID.MetalBars, L6Palette.BarLeadStyle),
                "铅锭堆", left + 10, workFloor - 1);
            tally.Add(L6Palette.TryPlaceObject(mid, room.InteriorTop, TileID.Chandeliers, L6Palette.ChandelierStyle),
                "蓝地牢吊灯", mid, room.InteriorTop);
            tally.Add(L6Palette.TryPlaceTile(left + 5, room.InteriorTop, TileID.Banners,
                L6Palette.BannerStyleFor(left + 5, room.InteriorTop + 1)), "旗帜", left + 5, room.InteriorTop);
            tally.Add(L6Palette.TryPlaceTile(right - 6, room.InteriorTop, TileID.Banners,
                L6Palette.BannerStyleFor(right - 6, room.InteriorTop + 1)), "旗帜", right - 6, room.InteriorTop);

            //Slab圆斑1~2处(墙面配比~20%)
            L6Palette.WallDisk(left + rand.Next(6, 14), workFloor - rand.Next(4, 8), rand.Next(3, 6), L6Palette.WallSlab);
            if (rand.NextBool(2)) {
                L6Palette.WallDisk(right - rand.Next(6, 14), room.InteriorTop + rand.Next(4, 8), rand.Next(3, 5), L6Palette.WallSlab);
            }
            return tally;
        }

        private static bool InAnyBasin(int x, (int l, int r)[] basins, int margin) {
            foreach ((int l, int r) in basins) {
                if (x >= l - margin && x < r + margin) {
                    return true;
                }
            }
            return false;
        }

        //==================== 车间排(#4:工具与工作台的喘息小房) ====================

        internal static Point WorkshopInteriorSize(UnifiedRandom rand)
            => new(rand.Next(13, 18), rand.Next(7, 9));

        internal static Tally BuildWorkshop(RoomNode room, UnifiedRandom rand, string signText = null) {
            var tally = new Tally();
            //车间=Slab基调(墙面配比的20%集中在车间/龛,机加工面语义)
            StampAndCarve(room, L6Palette.WallSlab);
            int floor = room.FloorTop;
            int left = room.InteriorLeft;
            int right = room.InteriorRight;

            //炉+砧+台:一条工序线;砧下油渍=本层做旧签名的地面形态
            tally.Add(L6Palette.TryPlaceTile(left + 2, floor - 1, TileID.Furnaces, 0), "熔炉", left + 2, floor - 1);
            L6Palette.ScorchDisk(left + 2, floor - 3, 2);
            tally.Add(L6Palette.TryPlaceTile(left + 6, floor - 1, TileID.Anvils, 0), "铁砧", left + 6, floor - 1);
            L6Palette.OilStreakFloor(left + 5, floor, 3);
            tally.Add(L6Palette.TryPlaceTile(left + 9, floor - 1, TileID.WorkBenches, L6Palette.WorkBenchStyle),
                "工作台", left + 9, floor - 1);
            tally.Add(L6Palette.TryPlaceTile(left + 9, floor - 2, TileID.Candles, L6Palette.CandleStyle),
                "台面蜡烛", left + 9, floor - 2);
            //铁锭码放在墙根
            tally.Add(L6Palette.TryPlaceTile(right - 2, floor - 1, TileID.MetalBars, L6Palette.BarIronStyle),
                "铁锭堆", right - 2, floor - 1);

            //黄铜灯笼+旗帜(Slab样式组自动匹配)
            tally.Add(L6Palette.TryPlaceObject((left + right) / 2, room.InteriorTop,
                TileID.HangingLanterns, L6Palette.LanternBrassStyle),
                "黄铜灯笼", (left + right) / 2, room.InteriorTop);
            if (floor - room.InteriorTop >= 6) {
                tally.Add(L6Palette.TryPlaceTile(left + 4, room.InteriorTop, TileID.Banners,
                    L6Palette.BannerStyleFor(left + 4, room.InteriorTop + 1)), "旗帜", left + 4, room.InteriorTop);
            }

            //告示位(顶折车间=工匠墓志,L5铁件混葬龛的回答,ROOMS-L6 §4)
            if (signText != null) {
                tally.Add(L6Palette.PlaceSignWithText(right - 4, floor - 1, signText),
                    "车间告示", right - 4, floor - 1);
            }
            return tally;
        }

        //==================== 试炼库(#5:≥3段机关串之后的风险对账,零机关) ====================

        internal static Point VaultInteriorSize(UnifiedRandom rand)
            => new(rand.Next(12, 16), rand.Next(8, 10));

        internal static Tally BuildTrialVault(RoomNode room, UnifiedRandom rand) {
            var tally = new Tally();
            StampAndCarve(room, L6Palette.WallSlab);
            int floor = room.FloorTop;
            int mid = (room.InteriorLeft + room.InteriorRight) / 2;

            //锁金箱=风险对账主奖(F35房间箱语言,正式战利品轮换表归M4,先落基础补给)
            int chest = WorldGen.PlaceChest(mid, floor - 1, TileID.Containers,
                notNearOtherChests: false, L6Palette.ChestLockedGoldStyle);
            tally.Add(chest >= 0, "锁金箱", mid, floor - 1);
            if (chest >= 0) {
                FillVaultChest(Main.chest[chest], rand);
            }

            tally.Add(WorldGen.PlacePot(room.InteriorLeft + 1, floor - 1, TileID.Pots,
                rand.Next(L6Palette.PotStyleMin, L6Palette.PotStyleMax + 1)),
                "罐", room.InteriorLeft + 1, floor - 1);
            tally.Add(WorldGen.PlacePot(room.InteriorRight - 3, floor - 1, TileID.Pots,
                rand.Next(L6Palette.PotStyleMin, L6Palette.PotStyleMax + 1)),
                "罐", room.InteriorRight - 3, floor - 1);
            tally.Add(L6Palette.TryPlaceTile(mid - 3, floor - 1, TileID.Candelabras, L6Palette.CandelabraStyle),
                "烛台", mid - 3, floor - 1);
            tally.Add(L6Palette.TryPlaceObject(mid, room.InteriorTop, TileID.HangingLanterns,
                L6Palette.LanternBrassStyle), "黄铜灯笼", mid, room.InteriorTop);
            tally.Add(L6Palette.TryPlaceTile(mid + 3, room.InteriorTop, TileID.Banners,
                L6Palette.BannerStyleFor(mid + 3, room.InteriorTop + 1)), "旗帜", mid + 3, room.InteriorTop);
            return tally;
        }

        //库藏:基础补给+铸造场味的哑弹(正式轮换表归M4)
        private static void FillVaultChest(Chest chest, UnifiedRandom rand) {
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
            Add(ItemID.GoldCoin, rand.Next(4, 7));
            Add(ItemID.HealingPotion, rand.Next(2, 4));
            Add(ItemID.Torch, rand.Next(10, 16));
            if (rand.NextBool(2)) {
                Add(ItemID.Grenade, rand.Next(6, 11));
            }
        }

        //==================== 主控室(#6:通往L7的门房,钟声门留位+决战前检查点,零机关) ====================

        internal static Point ControlInteriorSize(UnifiedRandom rand)
            => new(rand.Next(23, 29), rand.Next(10, 13));

        /// <summary>
        /// 构建主控室并返回钟声门落口的插槽偏移(距Bounds.Left,3宽PlatformGap用);
        /// 门禁TP本体归资产波(BellRiteSystem对位),本波留Cog门柱+过梁的帧精确框
        /// </summary>
        internal static Tally BuildControlRoom(RoomNode room, UnifiedRandom rand, out int gateOffset) {
            var tally = new Tally();
            StampAndCarve(room, L6Palette.WallTiled);
            int floor = room.FloorTop;
            int left = room.InteriorLeft;
            int right = room.InteriorRight;

            //钟声门框:落口(3宽)两侧Cog门柱3高+Cog过梁,立在地板舱口上方
            int gapL = right - 6;
            gateOffset = gapL - room.Bounds.Left;
            for (int dy = 1; dy <= 3; dy++) {
                TileBrush.SetSolid(gapL - 1, floor - dy, L6Palette.CogBlock);
                TileBrush.SetSolid(gapL + 3, floor - dy, L6Palette.CogBlock);
            }
            for (int x = gapL - 1; x <= gapL + 3; x++) {
                TileBrush.SetSolid(x, floor - 4, L6Palette.CogBlock);
            }
            L6Palette.TarDrip(gapL + 1, floor - 3, 2);
            L6MachineSlots.Register(L6SlotKind.BellGate,
                new Rectangle(gapL - 1, floor - 4, 5, 4), "主控室→L7静默通路钟声门(BellRite对位)");

            //控制台:拉杆排(不接线,联动归资产波)+桌椅+桌面蜡烛
            for (int x = left + 2; x <= left + 6; x += 2) {
                tally.Add(L6Palette.TryPlaceTile(x, floor - 3, TileID.Lever, 0), "拉杆", x, floor - 3);
            }
            int deskX = left + 10;
            tally.Add(L6Palette.TryPlaceTile(deskX, floor - 1, TileID.Tables, L6Palette.TableStyle),
                "控制台桌", deskX, floor - 1);
            tally.Add(L6Palette.TryPlaceTile(deskX - 2, floor - 1, TileID.Chairs, L6Palette.ChairStyle),
                "椅", deskX - 2, floor - 1);
            tally.Add(L6Palette.TryPlaceTile(deskX, floor - 3, TileID.Candles, L6Palette.CandleStyle),
                "桌面蜡烛", deskX, floor - 3);

            //终局阈值告示(ROOMS-L6:预告"下方=倒吊教堂")+烛台+吊灯+旗帜
            tally.Add(L6Palette.PlaceSignWithText(gapL - 4, floor - 1, SignThreshold),
                "阈值告示", gapL - 4, floor - 1);
            tally.Add(L6Palette.TryPlaceTile(left + 8, floor - 1, TileID.Candelabras, L6Palette.CandelabraStyle),
                "烛台", left + 8, floor - 1);
            tally.Add(L6Palette.TryPlaceObject((left + right) / 2, room.InteriorTop,
                TileID.Chandeliers, L6Palette.ChandelierStyle), "吊灯", (left + right) / 2, room.InteriorTop);
            tally.Add(L6Palette.TryPlaceTile(left + 4, room.InteriorTop, TileID.Banners,
                L6Palette.BannerStyleFor(left + 4, room.InteriorTop + 1)), "旗帜", left + 4, room.InteriorTop);
            return tally;
        }

        //==================== 渣堆厅(#7:坠落房间B的落点大房,渣山+钟坯彩蛋) ====================

        internal static Point SlagInteriorSize(UnifiedRandom rand)
            => new(rand.Next(30, 39), rand.Next(13, 17));

        internal static Tally BuildSlagHall(RoomNode room, UnifiedRandom rand) {
            var tally = new Tally();
            StampAndCarve(room, L6Palette.WallTiled);
            int floor = room.FloorTop;
            int left = room.InteriorLeft;
            int right = room.InteriorRight;

            //渣山3座:阶梯砖堆(逐列1格收分,F3可攀)+焦油漆通体、灰烬漆点缘
            int mounds = 3;
            int tallestX = 0, tallestH = 0;
            for (int m = 0; m < mounds; m++) {
                int mw = rand.Next(5, 9);
                int mx = left + 3 + m * ((right - left - 6) / mounds) + rand.Next(0, 3);
                int mh = rand.Next(2, 4);
                for (int dx = 0; dx < mw; dx++) {
                    int h = System.Math.Min(System.Math.Min(dx + 1, mw - dx), mh);
                    for (int dy = 1; dy <= h; dy++) {
                        TileBrush.SetSolid(mx + dx, floor - dy, L6Palette.Brick);
                        WorldGen.paintTile(mx + dx, floor - dy,
                            rand.NextBool(5) ? L6Palette.AshPaint : L6Palette.TarPaint);
                    }
                }
                if (mh > tallestH) {
                    tallestH = mh;
                    tallestX = mx + mw / 2;
                }
            }

            //铸废的钟坯:最高渣山顶2x2 Cog(L1大钟与L7终钟的叙事中点,ROOMS-L6 §3)
            TileBrush.SetSolid(tallestX, floor - tallestH - 1, L6Palette.CogBlock);
            TileBrush.SetSolid(tallestX + 1, floor - tallestH - 1, L6Palette.CogBlock);
            TileBrush.SetSolid(tallestX, floor - tallestH - 2, L6Palette.CogBlock);
            TileBrush.SetSolid(tallestX + 1, floor - tallestH - 2, L6Palette.CogBlock);
            tally.Add(L6Palette.PlaceSignWithText(left + 1, floor - 1, SignBellBlank),
                "钟坯铭牌", left + 1, floor - 1);

            //坠落房间B落点包络:生成期在此登记候选(prefab两态归公共构件波,R2)
            L6MachineSlots.Register(L6SlotKind.ElevatorStation,
                new Rectangle(left, room.InteriorTop, right - left, 4),
                "渣堆厅顶部=坠落房间B落点候选包络(公共构件波对位,非电梯)");

            //罐+灯+油渍池
            tally.Add(WorldGen.PlacePot(left + 2, floor - 1, TileID.Pots,
                rand.Next(L6Palette.PotStyleMin, L6Palette.PotStyleMax + 1)), "罐", left + 2, floor - 1);
            tally.Add(WorldGen.PlacePot(right - 3, floor - 1, TileID.Pots,
                rand.Next(L6Palette.PotStyleMin, L6Palette.PotStyleMax + 1)), "罐", right - 3, floor - 1);
            tally.Add(L6Palette.TryPlaceObject(left + 5, room.InteriorTop, TileID.HangingLanterns,
                L6Palette.LanternBrassStyle), "黄铜灯笼", left + 5, room.InteriorTop);
            tally.Add(L6Palette.TryPlaceObject(right - 5, room.InteriorTop, TileID.HangingLanterns,
                L6Palette.LanternBrassStyle), "黄铜灯笼", right - 5, room.InteriorTop);
            L6Palette.OilStreakFloor(left + 8, floor, 5);
            L6Palette.WallDisk((left + right) / 2, floor - 5, rand.Next(4, 7), L6Palette.WallSlab);
            return tally;
        }

        //==================== 齿轮井/检修梯井(#3/#10:折间竖向下降的演出井) ====================

        internal static Point WellInteriorSize(bool gear, int drop)
            => new(gear ? 12 : 8, drop + 11);

        /// <summary>
        /// 构建折间井:全宽交错平台(竖距4,自顶折入口行锚定),齿轮井带1~2处
        /// 大齿轮留位(该跨度平台改3宽侧错,轮缘与通行净距≥2)+检修龛+平台蜡烛照明。
        /// topFloor=上折地板行(入口门在此行,由编排器开洞)。
        /// </summary>
        internal static Tally BuildWell(RoomNode room, int topFloor, bool gear, UnifiedRandom rand) {
            var tally = new Tally();
            StampAndCarve(room, L6Palette.WallTiled);
            int left = room.InteriorLeft;
            int right = room.InteriorRight;
            int mid = (left + right) / 2;
            int floor = room.FloorTop;

            //齿轮演出跨度(先定,平台绕轮布置)
            var gearZones = new System.Collections.Generic.List<int>();
            if (gear) {
                int firstZone = topFloor + 30 + rand.Next(0, 12);
                gearZones.Add(firstZone);
                if (floor - firstZone > 130) {
                    gearZones.Add(firstZone + 80 + rand.Next(0, 20));
                }
            }

            //交错平台:自入口行向下每4行一档(F2满跳6.6,上下双向可通);
            //齿轮跨度内改3宽侧错平台,轮缘净距≥2(ROOMS-L6 §1齿轮井语法)
            for (int y = topFloor; y < floor; y += 4) {
                bool inGearZone = false;
                foreach (int zc in gearZones) {
                    if (y >= zc - 5 && y <= zc + 5) {
                        inGearZone = true;
                        break;
                    }
                }
                if (inGearZone) {
                    bool leftSide = ((y / 4) & 1) == 0;
                    int platLeft = leftSide ? left : right - 3;
                    TileBrush.PlatformRow(platLeft, platLeft + 3, y, L6Palette.PlatformFrameY);
                }
                else {
                    TileBrush.PlatformRow(left, right, y, L6Palette.PlatformFrameY);
                }
            }

            //大齿轮留位:轴承座2x2 Cog+焦痕盘+垂滴,帧包络8x8登记(留2格通行净距)
            foreach (int zc in gearZones) {
                L6Palette.ScorchDisk(mid, zc, 4);
                TileBrush.SetSolid(mid - 1, zc - 1, L6Palette.CogBlock);
                TileBrush.SetSolid(mid, zc - 1, L6Palette.CogBlock);
                TileBrush.SetSolid(mid - 1, zc, L6Palette.CogBlock);
                TileBrush.SetSolid(mid, zc, L6Palette.CogBlock);
                L6Palette.TarDrip(mid - 1, zc + 1, 4);
                L6Palette.TarDrip(mid, zc + 1, 4);
                L6MachineSlots.Register(L6SlotKind.GearLarge,
                    new Rectangle(mid - 4, zc - 4, 8, 8), "齿轮井大齿轮(纯演出旋转,不载人不碰撞)");
                tally.Placed++;
            }

            //检修龛(1深浅龛,竖井壁的视觉调剂,§2.5)+平台蜡烛照明(禁无光)
            for (int y = topFloor + 8; y < floor - 6; y += 14) {
                bool leftSide = ((y / 14) & 1) == 0;
                int nx = leftSide ? left - 1 : right;
                TileBrush.CarveRect(nx, y, nx + 1, y + 3, L6Palette.WallSlab);
                if (((y / 14) & 3) == 1) {
                    L6Palette.TarDrip(nx, y + 3, 2);
                }
            }
            for (int y = topFloor + 8; y < floor; y += 16) {
                int cx = ((y / 16) & 1) == 0 ? left + 1 : right - 2;
                tally.Add(L6Palette.TryPlaceTile(cx, y - 1, TileID.Candles, L6Palette.CandleStyle),
                    "井壁蜡烛", cx, y - 1);
            }
            return tally;
        }

        private static void MergeTrap(ref Tally roomTally, L6Traps.Tally trap) {
            roomTally.Placed += trap.TrapsPlaced + trap.FurnPlaced;
            roomTally.Rejected += trap.TrapsFailed + trap.FurnRejected;
        }
    }
}
