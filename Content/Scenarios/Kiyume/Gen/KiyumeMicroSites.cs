using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.Gen
{
    /// <summary>
    /// 微区撒布（P3-E，计划书 §3.S5 + 裁决17）：井×3（村2+社后1）/ 灯笼列道 / 墓地送葬道 / 旱田。<br/>
    /// 井位由 <see cref="PlanReservations"/> 在村落 Build 前占段（挂
    /// <see cref="KiyumeStructures.PlanReservations"/> 的 E 锚），本体在 StructurePass（P40）落地；
    /// 灯道不预留（W4 定案：3 列节点×柱距 26-34 会把组团截成恒 1 栋），P40 自扫落柱、灯让房；
    /// 微装饰（檐下罐/柴垛/晾物架/枯草丛/怪谈告示牌）由 ScatterPass（P55）调 <see cref="ScatterDecor"/>。<br/>
    /// 注册义务：WellMouths×3 / LanternPosts / GraveMain / ScarecrowPlot；
    /// 井口 3 列 FloorTop 语义破口与全部足印入 ScatterExclusions。
    /// 样式号全部对源 TML：墓碑 0-5（Item placeStyle）、木堆 22-25（WorldGen 洞穴木屋同款）、
    /// 素旗 0-3（Item 337-340）、铁链吊灯 0（Item 136）。
    /// </summary>
    internal static class KiyumeMicroSites
    {
        //════════ 材质表 ════════

        private const ushort StoneTile = TileID.GrayBrick;   //井壁/井沿/石灯柱
        private const ushort BeamTile = TileID.WoodenBeam;   //辘轳梁/灯柱/卒塔婆/田桩（非实心可穿行）
        private const ushort MoundTile = TileID.Dirt;        //坟堆土丘
        private const ushort TuftTile = TileID.SpookyWood;   //枯草丛细杆（与枯树同语汇）
        private const ushort StoneWall = WallID.GrayBrick;   //井筒衬墙

        //预留段标签：PlanReservations 写入 ReservedSpans，Build 按标签读回（Reset 随表自清）
        private const string WellTag = "井位";

        //告示牌文案池（zh-Hans 正典，硬编码沿 L4Palette 成规；池 5 条，洗牌后不重复取用）
        private static readonly string[] SignTexts = [
            "入夜不应门。叩三声者非客。",
            "雾没过门槛就闭窗，没过屋脊就别出声。",
            "井边孩童莫独行。",
            "灯灭了就停步，等它自己亮。",
            "送葬往东走，回来不许回头。",
        ];

        //本次生成的墓园窗（ScatterDecor 的道口告示定位用；gen 单线程，Build 头部复位）
        private static int graveWinL;
        private static int graveWinR;

        //════════ 预留（TerrainPass 村落 Build 前，经 KiyumeStructures.PlanReservations E 锚调入） ════════

        /// <summary>井位两段占段：村落组团让位后，井嵌进空当。灯道不预留（W4 定案）：
        /// 组团第 2 栋需要 27+ 连续列，3 列节点×柱距 26-34 的硬预留永远塞不下它，
        /// 村落会退化成恒 1 栋——灯道改 P40 自扫、灯让房（计划书 §3.S5 原案）</summary>
        internal static void PlanReservations() {
            ReserveWell(KiyumeMetrics.WellWestL, KiyumeMetrics.WellWestR);
            ReserveWell(KiyumeMetrics.WellEastL, KiyumeMetrics.WellEastR);
        }

        //井预留：窗内抽锚（井足印 7 列 + 两侧各 2 列呼吸）
        private static void ReserveWell(int winL, int winR) {
            int x = winL + WorldGen.genRand.Next(winR - winL + 1);
            KiyumeStructures.ReservedSpans.Add((x - 2, x + 9, WellTag));
        }

        private static bool SpanReserved(int left, int right) {
            foreach ((int l, int r, _) in KiyumeStructures.ReservedSpans) {
                if (left < r && right > l) {
                    return true;
                }
            }
            return false;
        }

        //════════ 入口（StructurePass 微区挂点，晚于信仰轴线/水缘） ════════

        internal static void Build(GenerationProgress progress) {
            KiyumePlans.Report(progress, "井绳还是湿的...");
            graveWinL = graveWinR = 0;
            int wells = BuildWells();
            int posts = BuildLanternRoad();
            (int graves, int stoneLanterns) = BuildGraveyard();
            BuildField();
            CWRMod.Instance.Logger.Info(
                $"[Kiyume] 微区 井={wells} 灯柱={posts} 坟={graves} 墓道石灯={stoneLanterns}"
                + $" 主坟={(KiyumeStructures.GraveMain.HasValue ? "有" : "无")}"
                + $" 旱田={(KiyumeStructures.ScarecrowPlot.HasValue ? "有" : "无")}");
        }

        //════════ 井 ════════

        //村井两口按预留段落位；社后井读村社 compound 矩形（宽 ≥20 的那条，路祠/山顶祠只有 4-6 宽）
        private static int BuildWells() {
            int built = 0;
            foreach ((int l, _, string tag) in KiyumeStructures.ReservedSpans) {
                if (tag == WellTag && BuildWell(l + 2)) {
                    built++;
                }
            }
            Rectangle? compound = null;
            foreach (Rectangle rect in KiyumeStructures.Shrines) {
                if (rect.Width >= 20) {
                    compound = rect;
                    break;
                }
            }
            int backyardL = compound.HasValue ? compound.Value.Right + 2 : KiyumeMetrics.ShrineSpanR - 9;
            backyardL = Math.Min(backyardL, KiyumeMetrics.ShrineSpanR - 8);
            if (BuildWell(backyardL)) {
                built++;
            }
            return built;
        }

        //井剖面（足印 7 列）：辘轳梁 5 宽 + 垂绳 + 井沿高出地面 1 行 + 3 宽井筒深 10-14 + 井底水 2 格
        //井口 3 列 FloorTop 停井沿行（语义破口），整井入禁区兜底
        private static bool BuildWell(int left) {
            int right = left + 7;
            if (SpanOccupied(left, right, 6)) {
                CWRMod.Instance.Logger.Warn($"[Kiyume] 井位@{left} 被占,跳过");
                return false;
            }
            int ground = HighestGround(left, right);
            FlattenSpan(left, right, ground);
            int rim = ground - 1;
            int depth = WorldGen.genRand.Next(KiyumeMetrics.WellDepthMin, KiyumeMetrics.WellDepthMax + 1);
            int bottom = ground + depth;   //封底实心行

            //井壁两侧 2 宽落到封底；井底封实
            KiyumeTileBrush.FillRect(left, rim, left + 2, bottom, StoneTile);
            KiyumeTileBrush.FillRect(left + 5, rim, left + 7, bottom, StoneTile);
            KiyumeTileBrush.FillRect(left + 2, bottom, left + 5, bottom + 1, StoneTile);
            //井口净空 + 井筒（衬石墙）+ 底水 2 格：净空行数 = depth-2 ≥ 8，满足 P4 井手位形
            KiyumeTileBrush.CarveRect(left + 2, rim - 4, left + 5, rim);
            KiyumeTileBrush.CarveRect(left + 2, rim, left + 5, bottom, StoneWall);
            for (int x = left + 2; x < left + 5; x++) {
                KiyumeTileBrush.SetWater(x, bottom - 2);
                KiyumeTileBrush.SetWater(x, bottom - 1);
            }
            //辘轳：立柱骑井沿，横梁 5 宽压顶，梁下垂绳到水面上一格
            int beamRow = rim - 4;
            KiyumeTileBrush.FillRect(left + 1, beamRow + 1, left + 2, rim, BeamTile);
            KiyumeTileBrush.FillRect(left + 5, beamRow + 1, left + 6, rim, BeamTile);
            KiyumeTileBrush.FillRect(left + 1, beamRow, left + 6, beamRow + 1, BeamTile);
            for (int y = beamRow + 1; y <= bottom - 3; y++) {
                KiyumeTileBrush.SetRope(left + 3, y);
            }

            //FloorTop 全部停井沿行：壁列是"结构即地面"，口 3 列是语义破口（靠禁区兜底）
            RewriteFloorTop(left, right, rim);
            KiyumeStructures.WellMouths.Add(new Point(left + 3, rim));
            KiyumeStructures.ScatterExclusions.Add(
                new Rectangle(left - 1, beamRow - 1, 9, bottom - beamRow + 3));
            return true;
        }

        //════════ 灯笼列道 ════════

        //P40 自扫落柱（计划书 §3.S5 原案）：柱距 26-34 抽签步进，
        //撞预留段（出生留白/村社段/井位）/禁区/既有结构即弃该节点，黑一段是节拍
        private static int BuildLanternRoad() {
            int posts = 0;
            int right = KiyumeMetrics.GroveLeft - 30;
            for (int x = KiyumeMetrics.VillageLeft + 30 + WorldGen.genRand.Next(0, 8); x < right;
                x += WorldGen.genRand.Next(KiyumeMetrics.LanternGapMin, KiyumeMetrics.LanternGapMax + 1)) {
                int ground = KiyumePlans.FloorTopAt(x);
                if (SpanReserved(x - 1, x + 2) || KiyumeStructures.InExclusion(x, ground - 1)
                    || SpanOccupied(x - 1, x + 2, 8)) {
                    continue;
                }
                BuildLanternPost(x, ground);
                posts++;
            }
            return posts;
        }

        //柱 WoodenBeam 1×5 + 顶横臂平台 + 臂下挂灯（铁链灯样式0）；双臂(成对)/单臂 6:4
        private static void BuildLanternPost(int x, int ground) {
            KiyumeTileBrush.FillRect(x, ground - 5, x + 1, ground, BeamTile);
            bool pair = WorldGen.genRand.NextFloat() < KiyumeMetrics.LanternPairChance;
            int dir = WorldGen.genRand.NextBool() ? 1 : -1;
            bool hung = HangArm(x, dir, ground);
            if (pair) {
                hung |= HangArm(x, -dir, ground);
            }
            if (!hung) {
                //全臂锚定拒绝：火把落臂平台上（灯位照登，P5 只认位置）。
                //禁落梁顶——CheckTorch 对源实锤梁 124 只进左右侧锚，梁顶火把收尾帧检即毁
                KiyumeTileBrush.SetTorch(x + dir, ground - 7);
            }
            KiyumeStructures.LanternPosts.Add(hung
                ? new Point(x + dir, ground - 5)
                : new Point(x + dir, ground - 7));
            KiyumeStructures.ScatterExclusions.Add(new Rectangle(x - 2, ground - 7, 5, 8));
        }

        private static bool HangArm(int x, int dir, int ground) {
            int ax = x + dir;
            KiyumeTileBrush.SetPlatform(ax, ground - 6, KiyumeMetrics.PlatformFrameY);
            //挂灯认平台顶锚（TileObjectData 42 的 Platform 替代锚，对源核实）
            return KiyumeTileBrush.TryPlaceObject(ax, ground - 5, TileID.HangingLanterns, 0);
        }

        //════════ 墓地送葬道 ════════

        private static (int graves, int stoneLanterns) BuildGraveyard() {
            int span = WorldGen.genRand.Next(KiyumeMetrics.GraveSpanMin, KiyumeMetrics.GraveSpanMax + 1);
            graveWinL = KiyumeMetrics.GraveWindowL
                + WorldGen.genRand.Next(KiyumeMetrics.GraveWindowR - KiyumeMetrics.GraveWindowL - span + 1);
            graveWinR = graveWinL + span;

            //道面：FloorTop 钳到基准曲线 ±1 并回写（wobble ±7 的碎台阶抹顺，送葬道走得顺脚）
            for (int x = graveWinL; x < graveWinR; x++) {
                int baseRow = (int)Math.Round(KiyumeMetrics.BaseFloorAt(x));
                int target = Math.Clamp(KiyumePlans.FloorTopAt(x), baseRow - 1, baseRow + 1);
                FlattenSpan(x, x + 1, target);
            }

            //沿道石灯 4-6 对：全窗均布，对内两座相距 4-5 列
            int pairs = WorldGen.genRand.Next(KiyumeMetrics.GraveLanternPairsMin, KiyumeMetrics.GraveLanternPairsMax + 1);
            int stoneLanterns = 0;
            for (int i = 0; i < pairs; i++) {
                int px = graveWinL + 3 + i * (span - 12) / Math.Max(pairs - 1, 1) + WorldGen.genRand.Next(-2, 3);
                stoneLanterns += StoneLantern(px) ? 1 : 0;
                stoneLanterns += StoneLantern(px + WorldGen.genRand.Next(4, 6)) ? 1 : 0;
            }

            //坟堆：窗东段约四分之三，8-14 座（挤上石灯就让位）
            var graves = new List<int>();
            int usableL = graveWinL + span / 4;
            int usableR = graveWinR - 6;
            int target2 = WorldGen.genRand.Next(KiyumeMetrics.GraveCountMin, KiyumeMetrics.GraveCountMax + 1);
            int step = Math.Max((usableR - usableL) / target2, 4);
            for (int i = 0, gx = usableL; i < target2 && gx + 5 <= usableR;
                i++, gx += step + WorldGen.genRand.Next(-1, 2)) {
                if (BuildGrave(gx)) {
                    graves.Add(gx);
                }
            }

            //主坟：中位那座配供烛常燃（P5 惊吓锚点 / 夜行列终点），登记 GraveMain
            if (graves.Count > 0) {
                int mainGx = graves[graves.Count / 2];
                int mg = KiyumePlans.FloorTopAt(mainGx + 1);
                int cx = mainGx - 2;
                if (WorldGen.InWorld(cx, mg - 2) && !Main.tile[cx, mg - 2].HasTile) {
                    //供烛要台面锚：小平台垫脚（实心柱顶过不了收尾帧检 CheckOnTable1x1）
                    KiyumeTileBrush.SetPlatform(cx, mg - 2, KiyumeMetrics.PlatformFrameY);
                    if (!KiyumeTileBrush.TryPlaceTile(cx, mg - 3, TileID.Candles, 0)) {
                        CWRMod.Instance.Logger.Warn("[Kiyume] 主坟供烛放置拒绝");
                    }
                }
                KiyumeStructures.GraveMain = new Point(mainGx + 1, mg - 2);
            }
            else {
                CWRMod.Instance.Logger.Warn("[Kiyume] 墓园一座坟都没落成");
            }

            //整窗入禁区：撒布树/草让路，道面与坟阵全罩
            int highest = int.MaxValue;
            int lowest = 0;
            for (int x = graveWinL; x < graveWinR; x++) {
                int t = KiyumePlans.FloorTopAt(x);
                highest = Math.Min(highest, t);
                lowest = Math.Max(lowest, t);
            }
            KiyumeStructures.ScatterExclusions.Add(
                new Rectangle(graveWinL, highest - 8, span, lowest - highest + 10));
            return (graves.Count, stoneLanterns);
        }

        //坟堆 4 宽土丘（两端斜切半拱、中 2 实心给碑锚底）+ 墓碑样式 0-5 + 三成卒塔婆
        private static bool BuildGrave(int gx) {
            int ground = KiyumePlans.FloorTopAt(gx + 1);
            if (SpanOccupied(gx - 1, gx + 5, 5)) {
                return false;
            }
            KiyumeTileBrush.SetSloped(gx, ground - 1, MoundTile, SlopeType.SlopeDownLeft);
            KiyumeTileBrush.SetSolid(gx + 1, ground - 1, MoundTile);
            KiyumeTileBrush.SetSolid(gx + 2, ground - 1, MoundTile);
            KiyumeTileBrush.SetSloped(gx + 3, ground - 1, MoundTile, SlopeType.SlopeDownRight);
            //墓碑骑丘顶（2×2 底左原点在 ground-2，对源 TileObjectData 85）；拒绝留素坟
            KiyumeTileBrush.TryPlaceObject(gx + 1, ground - 2, TileID.Tombstones, WorldGen.genRand.Next(0, 6));
            if (WorldGen.genRand.NextFloat() < KiyumeMetrics.SotobaChance
                && !Main.tile[gx + 4, ground - 2].HasTile) {
                //卒塔婆：坟后立牌 WoodenBeam 1×4 + 顶平台 1 格帽（读得出是"牌"不是"柱"）
                KiyumeTileBrush.FillRect(gx + 4, ground - 4, gx + 5, ground, BeamTile);
                KiyumeTileBrush.SetPlatform(gx + 4, ground - 5, KiyumeMetrics.PlatformFrameY);
            }
            return true;
        }

        //石灯：GrayBrick 2×3 柱 + 顶平台帽 + 蜡烛（烛要台面锚，帽用平台）
        private static bool StoneLantern(int px) {
            int ground = KiyumePlans.FloorTopAt(px);
            if (KiyumeStructures.InExclusion(px, ground - 1) || SpanOccupied(px, px + 2, 5)) {
                return false;
            }
            KiyumeTileBrush.FillRect(px, ground - 3, px + 2, ground, StoneTile);
            KiyumeTileBrush.SetPlatform(px, ground - 4, KiyumeMetrics.PlatformFrameY);
            KiyumeTileBrush.SetPlatform(px + 1, ground - 4, KiyumeMetrics.PlatformFrameY);
            return KiyumeTileBrush.TryPlaceTile(px, ground - 5, TileID.Candles, 0)
                || KiyumeTileBrush.TryPlaceTile(px + 1, ground - 5, TileID.Candles, 0);
        }

        //════════ 旱田（裁决17：ScarecrowPlot） ════════

        private static void BuildField() {
            int plotL = KiyumeMetrics.FieldWindowL + WorldGen.genRand.Next(
                KiyumeMetrics.FieldWindowR - KiyumeMetrics.FieldWindowL - KiyumeMetrics.FieldCols + 1);
            int plotR = plotL + KiyumeMetrics.FieldCols;
            int ground = HighestGround(plotL, plotR);
            FlattenSpan(plotL, plotR, ground);
            //五根装饰田桩均布 ±1 抖动：WoodenBeam 1×3 + 顶横杆（守田人的架子）
            for (int i = 0; i < KiyumeMetrics.FieldPostCount; i++) {
                int px = plotL + 3 + i * (KiyumeMetrics.FieldCols - 7) / (KiyumeMetrics.FieldPostCount - 1)
                    + WorldGen.genRand.Next(-1, 2);
                KiyumeTileBrush.FillRect(px, ground - 3, px + 1, ground, BeamTile);
                KiyumeTileBrush.SetSolid(px - 1, ground - 3, BeamTile);
                KiyumeTileBrush.SetSolid(px + 1, ground - 3, BeamTile);
            }
            KiyumeStructures.ScarecrowPlot = new Rectangle(plotL, ground - 4, KiyumeMetrics.FieldCols, 5);
            KiyumeStructures.ScatterExclusions.Add(
                new Rectangle(plotL - 1, ground - 5, KiyumeMetrics.FieldCols + 2, 7));
        }

        //════════ 微装饰撒布（ScatterPass P55 调，全走禁区过滤 + 落点校验） ════════

        internal static void ScatterDecor() {
            int pots = 0;
            int piles = 0;
            int racks = 0;
            //以门洞表为村落锚：檐下罐每栋 0-2、柴垛/晾物架按概率（计划书每组团 0-2 / 0-1 的近似口径）
            foreach (Point door in KiyumeStructures.DoorwayPoints) {
                int n = WorldGen.genRand.Next(0, 3);
                for (int i = 0; i < n; i++) {
                    int px = door.X + RandSide() * WorldGen.genRand.Next(3, 8);
                    pots += TryPot(px) ? 1 : 0;
                }
                if (WorldGen.genRand.NextFloat() < 0.35f) {
                    piles += TryWoodPile(door.X + RandSide() * WorldGen.genRand.Next(4, 11)) ? 1 : 0;
                }
                if (racks < 5 && WorldGen.genRand.NextFloat() < 0.22f) {
                    racks += TryRack(door.X + RandSide() * WorldGen.genRand.Next(5, 12)) ? 1 : 0;
                }
            }
            //枯草丛：枯林每 30-60 列一簇
            int tufts = 0;
            for (int x = KiyumeMetrics.GroveLeft + 8; x < KiyumeMetrics.RidgeLeft - 8;
                x += WorldGen.genRand.Next(30, 61)) {
                tufts += TryTuft(x) ? 1 : 0;
            }
            int signs = PlaceSigns();
            CWRMod.Instance.Logger.Info(
                $"[Kiyume] 微装饰 罐={pots} 柴垛={piles} 晾物架={racks} 枯草={tufts} 告示={signs}");
        }

        private static int RandSide() => WorldGen.genRand.NextBool() ? 1 : -1;

        //檐下罐：PlacePot 自带锚定校验（2×2）
        private static bool TryPot(int px) {
            int ground = KiyumePlans.FloorTopAt(px);
            if (!WorldGen.InWorld(px, ground, 8) || KiyumeStructures.InExclusion(px, ground - 1)
                || Main.tile[px, ground - 1].HasTile) {
                return false;
            }
            return WorldGen.PlacePot(px, ground - 1, TileID.Pots, WorldGen.genRand.Next(0, 4));
        }

        //柴垛：LargePiles 3×2 木堆样式 22-25（对源 WorldGen 洞穴木屋 genRand.Next(22,26) 同款）
        private static bool TryWoodPile(int px) {
            int ground = KiyumePlans.FloorTopAt(px);
            if (!WorldGen.InWorld(px, ground, 8) || KiyumeStructures.InExclusion(px, ground - 1)) {
                return false;
            }
            for (int dx = -1; dx <= 1; dx++) {
                if (KiyumePlans.FloorTopAt(px + dx) != ground
                    || Main.tile[px + dx, ground - 1].HasTile || Main.tile[px + dx, ground - 2].HasTile) {
                    return false;
                }
            }
            WorldGen.PlaceTile(px, ground - 1, TileID.LargePiles, mute: true, forced: false, -1,
                WorldGen.genRand.Next(22, 26));
            return Main.tile[px, ground - 1].HasTile
                && Main.tile[px, ground - 1].TileType == TileID.LargePiles;
        }

        //晾物架：双 WoodenBeam 柱 1×4 + 横杆平台 + 素色旗 1-2 面（样式 0-3，旗认平台顶锚）
        private static bool TryRack(int left) {
            int ground = KiyumePlans.FloorTopAt(left);
            if (!WorldGen.InWorld(left, ground, 12) || KiyumeStructures.InExclusion(left, ground - 1)
                || KiyumePlans.FloorTopAt(left + 4) != ground) {
                return false;
            }
            for (int x = left; x <= left + 4; x++) {
                for (int y = ground - 5; y < ground; y++) {
                    if (Main.tile[x, y].HasTile) {
                        return false;
                    }
                }
            }
            KiyumeTileBrush.FillRect(left, ground - 4, left + 1, ground, BeamTile);
            KiyumeTileBrush.FillRect(left + 4, ground - 4, left + 5, ground, BeamTile);
            for (int x = left; x <= left + 4; x++) {
                KiyumeTileBrush.SetPlatform(x, ground - 5, KiyumeMetrics.PlatformFrameY);
            }
            int flags = WorldGen.genRand.Next(1, 3);
            for (int i = 0; i < flags; i++) {
                KiyumeTileBrush.TryPlaceObject(left + 1 + i * 2, ground - 4,
                    TileID.Banners, WorldGen.genRand.Next(0, 4));
            }
            KiyumeStructures.ScatterExclusions.Add(new Rectangle(left - 1, ground - 6, 7, 7));
            return true;
        }

        //枯草丛：1 宽幽木短杆 2-3 根成簇（高 2-3），与枯树同语汇
        private static bool TryTuft(int left) {
            int stalks = WorldGen.genRand.Next(2, 4);
            bool any = false;
            for (int i = 0; i < stalks; i++) {
                int sx = left + i * 2;
                int ground = KiyumePlans.FloorTopAt(sx);
                if (!WorldGen.InWorld(sx, ground, 8) || KiyumeStructures.InExclusion(sx, ground - 1)
                    || Main.tile[sx, ground - 1].HasTile || Main.tile[sx, ground - 2].HasTile) {
                    continue;
                }
                int h = WorldGen.genRand.Next(2, 4);
                KiyumeTileBrush.FillRect(sx, ground - h, sx + 1, ground, TuftTile);
                any = true;
            }
            return any;
        }

        //怪谈告示牌：村口/井边/道口优先，文案池洗牌不重复
        private static int PlaceSigns() {
            //村口牌立在出生留白东缘内侧：留白外 +8 常撞第一组团（首栋多从留白右缘+2 起）
            var candidates = new List<int> { KiyumeMetrics.SpawnReserveRight - 6 };
            foreach (Point well in KiyumeStructures.WellMouths) {
                candidates.Add(well.X + 6);
            }
            if (graveWinR > graveWinL) {
                candidates.Add(graveWinL - 4);
            }
            candidates.Add(KiyumeMetrics.ToriiEastX - 10);

            string[] pool = (string[])SignTexts.Clone();
            for (int i = pool.Length - 1; i > 0; i--) {
                int j = WorldGen.genRand.Next(i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            int want = Math.Min(
                WorldGen.genRand.Next(KiyumeMetrics.SignCountMin, KiyumeMetrics.SignCountMax + 1),
                pool.Length);
            int placed = 0;
            foreach (int cx in candidates) {
                if (placed >= want) {
                    break;
                }
                if (TrySign(cx, pool[placed])) {
                    placed++;
                }
            }
            return placed;
        }

        //锚点两侧走位找平地（±6 列），PlaceSign 自带 2×2 锚定校验
        private static bool TrySign(int anchorX, string text) {
            for (int d = 0; d <= 6; d++) {
                foreach (int x in new[] { anchorX + d, anchorX - d }) {
                    int ground = KiyumePlans.FloorTopAt(x);
                    if (!WorldGen.InWorld(x, ground, 8) || KiyumeStructures.InExclusion(x, ground - 1)
                        || Main.tile[x, ground - 1].HasTile) {
                        continue;
                    }
                    if (KiyumeTileBrush.PlaceSignWithText(x, ground - 1, text)) {
                        return true;
                    }
                }
            }
            return false;
        }

        //════════ 公用小工（镜像 KiyumeShrine 手法，不共享） ════════

        //足印上方空域内已有实心即视为占用
        private static bool SpanOccupied(int left, int right, int probeUpRows) {
            for (int x = left; x < right; x++) {
                int top = KiyumePlans.FloorTopAt(x);
                for (int y = top - probeUpRows; y < top; y++) {
                    if (!WorldGen.InWorld(x, y)) {
                        continue;
                    }
                    Tile t = Main.tile[x, y];
                    if (t.HasTile && Main.tileSolid[t.TileType]) {
                        return true;
                    }
                }
            }
            return false;
        }

        //区间内最高地面行
        private static int HighestGround(int left, int right) {
            int best = int.MaxValue;
            for (int x = left; x < right; x++) {
                best = Math.Min(best, KiyumePlans.FloorTopAt(x));
            }
            return best == int.MaxValue ? (int)KiyumeMetrics.BaseFloorAt(left) : best;
        }

        //削高垫低到同一行并回写 FloorTop（Flatten 同款双动作）；垫料按带表取地砖
        private static void FlattenSpan(int left, int right, int row) {
            int[] top = KiyumePlans.FloorTop;
            for (int x = left; x < right; x++) {
                if (x < 0 || x >= Main.maxTilesX) {
                    continue;
                }
                int cur = KiyumePlans.FloorTopAt(x);
                if (cur > row) {
                    ushort fill = KiyumeMetrics.BandForColumn(x)?.GroundTile ?? TileID.Stone;
                    KiyumeTileBrush.FillRect(x, row, x + 1, cur, fill);
                }
                else if (cur < row) {
                    KiyumeTileBrush.CarveRect(x, cur, x + 1, row);
                }
                if (top != null && x < top.Length) {
                    top[x] = row;
                }
            }
        }

        //FloorTop 定向回写（不动地形，井沿"结构即地面"与井口语义破口共用井沿行）
        private static void RewriteFloorTop(int left, int right, int row) {
            int[] top = KiyumePlans.FloorTop;
            if (top == null) {
                return;
            }
            for (int x = left; x < right; x++) {
                if (x >= 0 && x < top.Length) {
                    top[x] = row;
                }
            }
        }
    }
}
