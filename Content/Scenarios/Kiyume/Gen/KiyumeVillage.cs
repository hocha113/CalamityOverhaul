using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.Gen
{
    /// <summary>
    /// 湖畔村纵深（P3-B）：两段式建村。先读 <see cref="KiyumeStructures.ReservedSpans"/> 避让
    /// （出生留白/村社段/井位由 PlanReservations 注入，灯道 P40 自扫灯让房），再按组团流式填充：
    /// 2-4 栋民居共享窄巷成组团，组团间大间距保剪影呼吸；望楼与空地保留旧节奏。<br/>
    /// 每栋民居壳建完即跑内饰（土间/板间/围炉/榻/箪笥/佛坛/桌椅槽序，按内膛实宽抽件），
    /// 户型修饰符：高床（抬柱脚、床下全通）与地窖（竖穴绳梯+蓝朝墙内膛）。<br/>
    /// 注册义务：门洞→DoorwayPoints、床下/柜/地窖→HideVolumes、足印→ScatterExclusions；
    /// 削垫回写 <see cref="KiyumePlans.FloorTop"/>（高床回写柱脚地面行）。
    /// 屋顶路线硬约束：组团内相邻檐口高差≤RoofStepMaxDh、山墙间距≤RoofGapMax、
    /// 组团两端檐口外挑平台踏台、巷内檐间晾台，从组团一端跳到另一端不落地。
    /// </summary>
    internal static class KiyumeVillage
    {
        //墙体与瓦：在血暮光照下压成暗红，别用亮木
        private const ushort WallTile = TileID.SpookyWood;
        private const ushort RoofTile = TileID.RedDynastyShingles;
        private const ushort FoundationTile = TileID.Ash;
        //板间/箪笥/棚板用暗朝木，和幽木壳分出层次
        private const ushort BoardTile = TileID.DynastyWood;
        private const ushort InnerWall = WallID.SpookyWood;
        //地窖墙：必须与 KiyumeStructures.CellarWall 常量同源（蓝朝墙=地窖签名，改一处必改两处）
        private const ushort CellarWall = WallID.BlueDynasty;
        //柜门墙：与 KiyumeStructures.CabinetAt 签名同源（围栏墙）
        private const ushort CabinetWall = WallID.WoodenFence;
        //佛坛衬墙：拼板墙。计划书写白朝墙，但"全村唯一白"是村社（C 包）的签名，这里让开
        private const ushort ButsudanWall = WallID.Planked;

        //家具样式（对源 Item.cs 王朝家具 placeStyle：2231床21/2259桌25/2228椅27/2236烛17）
        private const int BedStyle = 21;
        private const int TableStyle = 25;
        private const int ChairStyle = 27;
        private const int CandleStyle = 17;

        internal static int Huts;
        internal static int Towers;
        internal static int Torches;
        internal static int Clusters;
        internal static int Stilts;
        internal static int Cellars;
        internal static int Ruins;
        internal static int Irori;
        internal static int Butsudan;
        internal static int Tansu;
        internal static int BedCount;
        internal static int TableSets;

        //组团内一栋的外形回执，屋顶路线用
        private readonly struct HutShape(int left, int right, int eaveRow, int ground)
        {
            internal readonly int Left = left;       //山墙左列
            internal readonly int Right = right;     //山墙右列（含）
            internal readonly int EaveRow = eaveRow; //檐口行（首层屋面）
            internal readonly int Ground = ground;   //柱脚地面行
        }

        internal static void Reset() {
            Huts = Towers = Torches = Clusters = Stilts = Cellars = Ruins = 0;
            Irori = Butsudan = Tansu = BedCount = TableSets = 0;
        }

        internal static void Build(GenerationProgress progress = null) {
            Reset();
            const int startPad = 24;
            int x = KiyumeMetrics.VillageLeft + startPad;
            int right = KiyumeMetrics.GroveLeft - 30;
            int origin = x;
            int span = Math.Max(right - x, 1);

            while (x < right) {
                int prev = x;
                progress?.Set(0.72 + 0.28 * (x - origin) / (double)span);

                //预留段避让：出生留白与 C/E 包注入的段位全走注册表，一律不落建筑
                if (HitsReserved(x, x + 1, out int jump)) {
                    x = jump;
                }
                else {
                    float roll = WorldGen.genRand.NextFloat();
                    if (roll < 0.14f) {
                        //空地：巷口与空场，剪影要有呼吸
                        x += WorldGen.genRand.Next(22, 42);
                    }
                    else if (roll < 0.26f) {
                        x += BuildTower(x, right) + WorldGen.genRand.Next(16, 30);
                    }
                    else {
                        x = BuildCluster(x, right);
                    }
                }

                if (x <= prev) {
                    x = prev + 8;
                }
            }

            CWRMod.Instance.Logger.Info(
                $"[Kiyume] 村落纵深 组团={Clusters} 民居={Huts}(高床={Stilts} 地窖={Cellars} 残屋={Ruins})"
                + $" 望楼={Towers} 内饰[围炉={Irori} 佛坛={Butsudan} 箪笥={Tansu} 榻={BedCount} 桌椅={TableSets} 灯={Torches}]"
                + $" 门洞={KiyumeStructures.DoorwayPoints.Count} 藏身={KiyumeStructures.HideVolumes.Count}");
        }

        //候选足印撞上预留段：给出段外落点（右缘+2 列呼吸）
        private static bool HitsReserved(int left, int right, out int jumpTo) {
            jumpTo = left;
            bool hit = false;
            foreach ((int l, int r, _) in KiyumeStructures.ReservedSpans) {
                if (left < r && right > l) {
                    hit = true;
                    jumpTo = Math.Max(jumpTo, r);
                }
            }
            if (hit) {
                jumpTo += 2;
            }
            return hit;
        }

        //════════ 组团 ════════

        //2-4 栋共享窄巷；撞预留段/东界即截断。返回下一组团的起点列
        private static int BuildCluster(int start, int rightBound) {
            int want = WorldGen.genRand.Next(KiyumeMetrics.VillageClusterMin, KiyumeMetrics.VillageClusterMax + 1);
            List<HutShape> built = [];
            int x = start;
            int prevEave = int.MinValue;

            for (int i = 0; i < want; i++) {
                int w = WorldGen.genRand.Next(10, 18);
                if (x + w + 2 >= Math.Min(rightBound, KiyumeMetrics.PlayRight)) {
                    break;
                }
                if (HitsReserved(x - 2, x + w + 2, out int jump)) {
                    x = Math.Max(x, jump);
                    break;
                }
                HutShape hut = BuildHut(x, w, prevEave, edgeOfCluster: i == 0 || i == want - 1);
                built.Add(hut);
                prevEave = hut.EaveRow;
                x = hut.Right + 1;
                if (i < want - 1) {
                    x += WorldGen.genRand.Next(KiyumeMetrics.VillageAlleyMin, KiyumeMetrics.VillageAlleyMax + 1);
                }
            }

            if (built.Count > 0) {
                BuildRoofRoute(built);
                Clusters++;
            }
            return x + WorldGen.genRand.Next(KiyumeMetrics.VillageClusterGapMin, KiyumeMetrics.VillageClusterGapMax + 1);
        }

        //════════ 民居 ════════

        //身比檐窄，坡脊出檐，山墙真开门（人走得进去），檐口高差跟组团里前一栋对齐
        private static HutShape BuildHut(int left, int w, int prevEaveRow, bool edgeOfCluster) {
            const int eave = 2;
            int right = left + w - 1;
            int ground = HighestGround(left - eave, left + w + eave);
            Flatten(left - eave, left + w + eave, ground);

            //户型修饰符与檐口协调：eave行 = ground - lift - h - 1，组团内相邻高差 ≤ RoofStepMaxDh
            bool stilt = WorldGen.genRand.NextFloat() < KiyumeMetrics.StiltHutChance;
            int lift = stilt ? KiyumeMetrics.StiltLiftRows : 0;
            int h = PickBodyHeight(ground, ref lift, prevEaveRow);
            stilt = lift > 0;

            int floorRow = ground - lift;   //室内地板行（高床=楼板行）
            int bodyTop = floorRow - h;
            int roofH = WorldGen.genRand.Next(4, 7);

            if (stilt) {
                BuildStilts(left, w, floorRow, ground);
            }

            //外壳一格厚
            KiyumeTileBrush.FillRect(left, bodyTop, left + w, floorRow, WallTile);
            KiyumeTileBrush.CarveRect(left + 1, bodyTop + 1, left + w - 1, floorRow, InnerWall);
            if (stilt) {
                //楼板一格厚，床下签名（CeilIs 幽木）就认它
                KiyumeTileBrush.FillRect(left, floorRow, left + w, floorRow + 1, WallTile);
            }
            BuildRoof(left, left + w, bodyTop, roofH, eave);

            //门洞：真开在山墙上，1 宽 3 高，登记给 P4 导演
            bool doorLeft = WorldGen.genRand.NextBool();
            int doorCol = doorLeft ? left : right;
            KiyumeTileBrush.CarveRect(doorCol, floorRow - 3, doorCol + 1, floorRow, InnerWall);
            KiyumeStructures.DoorwayPoints.Add(new Point(doorCol, floorRow - 1));
            if (stilt) {
                BuildPorch(doorCol, doorLeft, ground);
            }

            //山墙窗开门对侧，火光从这里漏出去给雾吃
            int winY = bodyTop + Math.Max(h / 3, 1);
            int winX = doorLeft ? right : left;
            KiyumeTileBrush.CarveRect(winX, winY, winX + 1, winY + 2, InnerWall);

            //残屋只落组团边栋：组团中段塌屋会断屋顶路线
            bool ruined = edgeOfCluster && WorldGen.genRand.NextFloat() < 0.18f;
            if (ruined) {
                Ruin(left, bodyTop, w, h, roofH, eave);
                Ruins++;
            }

            //壳定形后立刻内饰（残屋也住过人）
            BuildInterior(left, w, floorRow, bodyTop, doorLeft, stilt, ground);

            //足印入撒布禁区：屋脊罩到地面
            int roofTop = bodyTop - roofH - 1;
            KiyumeStructures.ScatterExclusions.Add(
                new Rectangle(left - eave, roofTop, w + eave * 2, ground - roofTop + 1));

            Huts++;
            if (stilt) {
                Stilts++;
            }
            return new HutShape(left, right, bodyTop - 1, ground);
        }

        //身高抽签：夹进与前一栋檐口高差 ≤ RoofStepMaxDh 的窗口；高床夹不进就落回平房
        private static int PickBodyHeight(int ground, ref int lift, int prevEaveRow) {
            const int hMin = 6, hMax = 9;
            if (prevEaveRow == int.MinValue) {
                return WorldGen.genRand.Next(hMin, hMax + 1);
            }
            for (int pass = 0; pass < 2; pass++) {
                int lo = Math.Max(hMin, ground - lift - 1 - (prevEaveRow + KiyumeMetrics.RoofStepMaxDh));
                int hi = Math.Min(hMax, ground - lift - 1 - (prevEaveRow - KiyumeMetrics.RoofStepMaxDh));
                if (lo <= hi) {
                    return WorldGen.genRand.Next(lo, hi + 1);
                }
                if (lift == 0) {
                    break;
                }
                lift = 0;
            }
            //村带地面平缓，正常到不了这里；取最近可行值兜底
            return Math.Clamp(ground - lift - 1 - prevEaveRow, hMin, hMax);
        }

        //柱脚：左右内缩一列各一对，宽屋加中对；对间空档 ≤5 列，
        //保证床下签名（StiltGapAt ±5 列双侧见梁）在整段净空内都成立
        private static void BuildStilts(int left, int w, int slabRow, int ground) {
            BeamPair(left + 1);
            BeamPair(left + w - 3);
            if (w >= 12) {
                BeamPair(left + w / 2 - 1);
            }
            //床下藏身体积：楼板下净空整段（两端开放，全通）
            KiyumeStructures.HideVolumes.Add(
                (new Rectangle(left, slabRow + 1, w, ground - slabRow - 1), KiyumeStructures.KindStiltGap));

            void BeamPair(int col) => KiyumeTileBrush.FillRect(col, slabRow + 1, col + 2, ground, TileID.WoodenBeam);
        }

        //高床前廊：门外两级踏台，跳两次上门槛
        private static void BuildPorch(int doorCol, bool doorLeft, int ground) {
            int dir = doorLeft ? -1 : 1;
            PadIfEmpty(doorCol + dir * 2, ground - 2);
            PadIfEmpty(doorCol + dir * 3, ground - 2);
            PadIfEmpty(doorCol + dir, ground - 4);
        }

        //════════ 内饰 ════════

        //槽序从门侧向里：土间(3-4格烬灰落脚)→板间(垫1格朝木+踏步)→抽件（榻/箪笥/围炉/桌椅 2-4 件
        //+ 佛坛独立 34% 抽）→点灯合并（有围炉/佛坛烛就不放火把）→地窖（竖穴占最里两列）
        private static void BuildInterior(int left, int w, int floorRow, int bodyTop, bool doorLeft, bool stilt, int ground) {
            int inL = left + 1;
            int inR = left + w - 2;

            //地窖先抽：竖穴靠门洞对侧，占 2 列；高床时内移 2 列躲开柱脚对（梁占最里两列）
            bool cellar = WorldGen.genRand.NextFloat() < KiyumeMetrics.CellarChance;
            int shaftK = stilt ? 2 : 0;   //竖穴自最里数第几列起
            int shaftL = doorLeft ? inR - shaftK - 1 : inL + shaftK;

            //土间/板间
            int domaW = WorldGen.genRand.Next(3, 5);
            int itamaCols = inR - inL + 1 - domaW;
            bool hasItama = itamaCols >= 4;
            int itamaFrom = 0, itamaTo = -1;   //板间闭区间（无板间时保持空区间）
            if (hasItama) {
                itamaFrom = doorLeft ? inL + domaW : inL;
                itamaTo = doorLeft ? inR : inR - domaW;
                for (int cx = itamaFrom; cx <= itamaTo; cx++) {
                    KiyumeTileBrush.SetSolid(cx, floorRow - 1, BoardTile);
                }
                //界口踏步：斜面向土间落
                int stepCol = doorLeft ? itamaFrom : itamaTo;
                KiyumeTileBrush.SetSloped(stepCol, floorRow - 1, BoardTile,
                    doorLeft ? SlopeType.SlopeDownLeft : SlopeType.SlopeDownRight);
            }
            int itamaFloor = hasItama ? floorRow - 1 : floorRow;
            int standRow = itamaFloor - 1;   //板间家具的 bottom 行

            //地窖赶在摆件前挖好：洞口穿板间，后面的落灯落件才能看见破口避让
            if (cellar) {
                BuildCellar(shaftL, floorRow, bodyTop, ground, doorLeft);
            }

            //抽件：榻0/箪笥1/围炉2/桌椅3，抽 2-4 件后按"里→门"固定空间序打包
            Span<int> pool = [0, 1, 2, 3];
            for (int i = pool.Length - 1; i > 0; i--) {
                int j = WorldGen.genRand.Next(i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            int want = WorldGen.genRand.Next(KiyumeMetrics.InteriorPieceMin, KiyumeMetrics.InteriorPieceMax + 1);
            Span<bool> picked = stackalloc bool[4];
            for (int i = 0; i < want && i < pool.Length; i++) {
                picked[pool[i]] = true;
            }

            int cursor = cellar ? shaftK + 2 : 0;   //自最里列起已占列数（竖穴与柱脚让位）
            bool litByFire = false;
            if (hasItama) {
                if (picked[0]) {
                    TryBed(ref cursor);
                }
                if (picked[1]) {
                    TryTansu(ref cursor);
                }
                if (picked[2]) {
                    litByFire |= TryIrori(ref cursor);
                }
                if (picked[3]) {
                    TryTableChair(ref cursor);
                }
            }

            //佛坛独立抽签：钉在最里山墙高处（神龛高挂），有烛就算点灯
            if (WorldGen.genRand.NextFloat() < KiyumeMetrics.ButsudanChance) {
                litByFire |= TryButsudan();
            }

            //点灯率合并：围炉/佛坛烛已亮就不再放火把，否则沿旧 34% 落一盏
            if (!litByFire && WorldGen.genRand.NextFloat() < 0.34f) {
                TryTorch();
            }
            return;

            //k=0 是最里内膛列，向门侧递增
            int ColAt(int k) => doorLeft ? inR - k : inL + k;
            //板间可打包余量：留 1 列踏步口不占
            int Avail(int cur) => itamaCols - 1 - cur;

            bool TryBed(ref int cur) {
                if (Avail(cur) < 4) {
                    return false;
                }
                int lo = Math.Min(ColAt(cur), ColAt(cur + 3));
                if (!KiyumeTileBrush.TryPlaceObject(lo + 1, standRow, TileID.Beds, BedStyle)) {
                    return false;
                }
                cur += 4;
                BedCount++;
                return true;
            }

            bool TryTansu(ref int cur) {
                if (Avail(cur) < 3) {
                    return false;
                }
                //内膛 2 宽 3 高（站得进人），门侧立柱 + 顶盖，围栏墙当柜门
                //顶盖暗朝木是签名第二要素（KiyumeStructures.CabinetAt：围栏墙+3 行内朝木盖）
                int c0 = ColAt(cur);
                int c1 = ColAt(cur + 1);
                int pillar = ColAt(cur + 2);
                int cavL = Math.Min(c0, c1);
                for (int y = standRow - 2; y <= standRow; y++) {
                    KiyumeTileBrush.SetWall(c0, y, CabinetWall);
                    KiyumeTileBrush.SetWall(c1, y, CabinetWall);
                }
                KiyumeTileBrush.FillRect(pillar, standRow - 2, pillar + 1, standRow + 1, BoardTile);
                KiyumeTileBrush.FillRect(Math.Min(cavL, pillar), standRow - 3, Math.Max(cavL + 1, pillar) + 1, standRow - 2, BoardTile);
                KiyumeStructures.HideVolumes.Add(
                    (new Rectangle(cavL, standRow - 2, 2, 3), KiyumeStructures.KindCabinet));
                cur += 3;
                Tansu++;
                return true;
            }

            bool TryIrori(ref int cur) {
                if (Avail(cur) < 3) {
                    return false;
                }
                int lo = Math.Min(ColAt(cur), ColAt(cur + 2));
                if (!KiyumeTileBrush.TryPlaceObject(lo + 1, standRow, TileID.Campfire, 0)) {
                    return false;
                }
                cur += 3;
                Irori++;
                return true;
            }

            bool TryTableChair(ref int cur) {
                if (Avail(cur) < 4) {
                    return false;
                }
                int lo = Math.Min(ColAt(cur), ColAt(cur + 2));
                if (!KiyumeTileBrush.TryPlaceObject(lo + 1, standRow, TileID.Tables, TableStyle)) {
                    return false;
                }
                //椅子挤在门侧；桌上有时搁一只瓶
                KiyumeTileBrush.TryPlaceObject(ColAt(cur + 3), standRow, TileID.Chairs, ChairStyle);
                if (WorldGen.genRand.NextBool()) {
                    KiyumeTileBrush.TryPlaceTile(lo + 1, standRow - 2, TileID.Bottles);
                }
                cur += 4;
                TableSets++;
                return true;
            }

            bool TryButsudan() {
                //挂在最里山墙高处；竖穴不占最里两列时让位内移。
                //棚板用平台：烛/瓶要 tileTable 锚，实心棚板过不了收尾帧检（CheckOnTable1x1）
                int k0 = cellar && !stilt ? 2 : 0;
                if (inR - inL + 1 < k0 + 2) {
                    return false;
                }
                int c0 = ColAt(k0);
                int c1 = ColAt(k0 + 1);
                int shelfRow = bodyTop + 3;
                for (int y = bodyTop + 1; y <= bodyTop + 2; y++) {
                    KiyumeTileBrush.SetWall(c0, y, ButsudanWall);
                    KiyumeTileBrush.SetWall(c1, y, ButsudanWall);
                }
                PadIfEmpty(c0, shelfRow);
                PadIfEmpty(c1, shelfRow);
                bool lit = KiyumeTileBrush.TryPlaceTile(c0, bodyTop + 2, TileID.Candles, CandleStyle);
                KiyumeTileBrush.TryPlaceTile(c1, bodyTop + 2,
                    WorldGen.genRand.NextBool() ? TileID.Bottles : TileID.Books);
                Butsudan++;
                return lit;
            }

            void TryTorch() {
                int tx = WorldGen.genRand.Next(inL, inR + 1);
                int row = hasItama && tx >= itamaFrom && tx <= itamaTo ? standRow : floorRow - 1;
                //脚下要有实落点（竖穴口上不悬灯）
                if (WorldGen.InWorld(tx, row) && !Main.tile[tx, row].HasTile && Main.tile[tx, row + 1].HasTile) {
                    KiyumeTileBrush.SetTorch(tx, row);
                    Torches++;
                }
            }
        }

        //════════ 地窖 ════════

        //竖穴 2 宽穿地板（高床连楼板一起穿，床下敞空段只走绳），下接 6×4 内膛；
        //绳梯顶节贴天花实心正下方，一路垂到窖底
        private static void BuildCellar(int shaftL, int floorRow, int bodyTop, int ground, bool doorLeft) {
            int cavTop = ground + KiyumeMetrics.CellarShaftRows;
            int cavL = doorLeft ? shaftL + 1 - (KiyumeMetrics.CellarInnerW - 1) : shaftL;

            //洞口（板间+地板/楼板两行）与地下竖穴；蓝朝墙从洞口就开始，签名连续
            KiyumeTileBrush.CarveRect(shaftL, floorRow - 1, shaftL + 2, floorRow + 1, CellarWall);
            KiyumeTileBrush.CarveRect(shaftL, ground, shaftL + 2, cavTop, CellarWall);
            KiyumeTileBrush.CarveRect(cavL, cavTop, cavL + KiyumeMetrics.CellarInnerW,
                cavTop + KiyumeMetrics.CellarInnerH, CellarWall);

            int ropeCol = doorLeft ? shaftL : shaftL + 1;
            int cavFloor = cavTop + KiyumeMetrics.CellarInnerH;   //窖底实心行
            for (int y = bodyTop + 1; y < cavFloor; y++) {
                KiyumeTileBrush.SetRope(ropeCol, y);
            }

            //罐柜抽签 1-2 件，堆在离绳梯远的那半边
            int stand = cavFloor - 1;
            int itemBase = doorLeft ? cavL : cavL + KiyumeMetrics.CellarInnerW - 2;
            int items = WorldGen.genRand.Next(1, 3);
            for (int i = 0; i < items; i++) {
                int ix = doorLeft ? itemBase + i * 2 : itemBase - i * 2;
                if (WorldGen.genRand.NextBool()) {
                    WorldGen.PlacePot(ix, stand, TileID.Pots, WorldGen.genRand.Next(0, 4));
                }
                else {
                    KiyumeTileBrush.TryPlaceObject(ix, stand, TileID.Kegs, 0);
                }
            }

            //注册：内膛与地下竖穴入藏身表；整穴入撒布禁区兜底（井口同款语义破口纪律）
            KiyumeStructures.HideVolumes.Add(
                (new Rectangle(cavL, cavTop, KiyumeMetrics.CellarInnerW, KiyumeMetrics.CellarInnerH),
                 KiyumeStructures.KindCellar));
            KiyumeStructures.HideVolumes.Add(
                (new Rectangle(shaftL, ground, 2, KiyumeMetrics.CellarShaftRows), KiyumeStructures.KindCellar));
            KiyumeStructures.ScatterExclusions.Add(
                new Rectangle(cavL, ground, KiyumeMetrics.CellarInnerW,
                    KiyumeMetrics.CellarShaftRows + KiyumeMetrics.CellarInnerH + 1));
            Cellars++;
        }

        //════════ 望楼 ════════

        //窄高一柱，脊更陡，顶窗常明，雾涨上来时它是最后沉没的东西；
        //底门真开在山墙上，楼板缺口保持塔内可上
        private static int BuildTower(int left, int rightBound) {
            int w = WorldGen.genRand.Next(5, 8);
            int h = WorldGen.genRand.Next(14, 22);
            const int eave = 2;
            int roofH = WorldGen.genRand.Next(5, 8);
            if (left + w + eave >= Math.Min(rightBound, KiyumeMetrics.PlayRight)
                || HitsReserved(left - eave, left + w + eave, out _)) {
                return w;
            }

            int ground = HighestGround(left - eave, left + w + eave);
            Flatten(left - eave, left + w + eave, ground);

            int bodyTop = ground - h;
            KiyumeTileBrush.FillRect(left, bodyTop, left + w, ground, WallTile);
            KiyumeTileBrush.CarveRect(left + 1, bodyTop + 1, left + w - 1, ground, InnerWall);
            BuildRoof(left, left + w, bodyTop, roofH, eave);

            //底层门洞 + 登记
            bool doorLeft = WorldGen.genRand.NextBool();
            int doorCol = doorLeft ? left : left + w - 1;
            KiyumeTileBrush.CarveRect(doorCol, ground - 3, doorCol + 1, ground, InnerWall);
            KiyumeStructures.DoorwayPoints.Add(new Point(doorCol, ground - 1));

            //每隔几格一道楼板缺口，读得出是能上人的塔
            for (int y = bodyTop + 4; y < ground - 3; y += 5) {
                KiyumeTileBrush.FillRect(left + 1, y, left + w - 1, y + 1, WallTile);
                int gap = left + 1 + WorldGen.genRand.Next(Math.Max(w - 4, 1));
                KiyumeTileBrush.CarveRect(gap, y, gap + 2, y + 1, InnerWall);
            }

            //顶窗
            KiyumeTileBrush.CarveRect(left + w / 2, bodyTop + 1, left + w / 2 + 1, bodyTop + 3, InnerWall);
            LightInside(left + 1, bodyTop + 3, left + w - 2);

            int roofTop = bodyTop - roofH - 1;
            KiyumeStructures.ScatterExclusions.Add(
                new Rectangle(left - eave, roofTop, w + eave * 2, ground - roofTop + 1));

            Towers++;
            return w + eave * 2;
        }

        //════════ 屋顶路线 ════════

        //组团两端给上房入口，巷内给檐间落脚点；檐口高差已在建壳时夹紧
        private static void BuildRoofRoute(List<HutShape> built) {
            OuterPads(built[0], leftSide: true);
            OuterPads(built[^1], leftSide: false);
            for (int i = 1; i < built.Count; i++) {
                AlleyBridge(built[i - 1], built[i]);
            }
        }

        //檐口外挑 2 格平台 + 半高踏台；够不着再补一级（高床高身合计最多跳三次上檐）
        private static void OuterPads(HutShape hut, bool leftSide) {
            int dir = leftSide ? -1 : 1;
            int edge = leftSide ? hut.Left - 2 : hut.Right + 2;
            int c1 = edge + dir;
            int c2 = edge + dir * 2;
            PadIfEmpty(c1, hut.EaveRow);
            PadIfEmpty(c2, hut.EaveRow);
            PadIfEmpty(c1, hut.Ground - 4);
            PadIfEmpty(c2, hut.Ground - 4);
            if (hut.Ground - 4 - hut.EaveRow > 6) {
                PadIfEmpty(c1, hut.Ground - 8);
                PadIfEmpty(c2, hut.Ground - 8);
            }
        }

        //巷内晾台：吊在低檐下方 2 行，两边屋面都在一跳之内（高差 ≤RoofStepMaxDh 保证）
        private static void AlleyBridge(HutShape a, HutShape b) {
            int gapL = a.Right + 1;
            int gapR = b.Left - 1;
            if (gapR - gapL + 1 < 2) {
                return;
            }
            int mid = gapL + (gapR - gapL - 1) / 2;
            int row = Math.Max(a.EaveRow, b.EaveRow) + 2;
            PadIfEmpty(mid, row);
            PadIfEmpty(mid + 1, row);
        }

        private static void PadIfEmpty(int x, int y) {
            if (WorldGen.InWorld(x, y) && !Main.tile[x, y].HasTile) {
                KiyumeTileBrush.SetPlatform(x, y, KiyumeMetrics.PlatformFrameY);
            }
        }

        //════════ 共用低层 ════════

        //坡脊：檐口外挑，逐层收窄到脊头，屋顶下面那一层是檐板
        private static void BuildRoof(int left, int right, int bodyTop, int roofH, int eave) {
            int span = right - left + eave * 2;
            for (int i = 0; i < roofH; i++) {
                int inset = (int)MathF.Round(i * (span - 2) / (2f * roofH));
                int rl = left - eave + inset;
                int rr = right + eave - inset;
                if (rr - rl < 1) {
                    break;
                }
                KiyumeTileBrush.FillRect(rl, bodyTop - 1 - i, rr, bodyTop - i, RoofTile);
            }
        }

        //残屋：屋脊塌掉一段，墙上啃几个洞。村子不能整整齐齐，那不是记忆的样子
        private static void Ruin(int left, int bodyTop, int w, int h, int roofH, int eave) {
            int holeLeft = left + WorldGen.genRand.Next(Math.Max(w / 3, 1));
            int holeW = WorldGen.genRand.Next(3, Math.Max(w - 2, 4));
            KiyumeTileBrush.CarveRect(holeLeft, bodyTop - roofH - 1, holeLeft + holeW, bodyTop, WallID.None);
            for (int i = 0; i < 4; i++) {
                int hx = left + WorldGen.genRand.Next(w);
                int hy = bodyTop + WorldGen.genRand.Next(Math.Max(h - 1, 1));
                KiyumeTileBrush.CarveRect(hx, hy, hx + 1, hy + 1, InnerWall);
            }
        }

        //屋里点灯：火把要有实心落脚点，放不下就算了，不为一盏灯记日志
        private static void LightInside(int left, int floorRow, int right) {
            if (right <= left) {
                return;
            }
            int tx = left + WorldGen.genRand.Next(right - left + 1);
            if (WorldGen.InWorld(tx, floorRow) && !Main.tile[tx, floorRow].HasTile) {
                KiyumeTileBrush.SetTorch(tx, floorRow);
                Torches++;
            }
        }

        //取区间内最高的地面行：房子平放在最高点上，低处靠地基垫起来
        private static int HighestGround(int left, int right) {
            int best = int.MaxValue;
            for (int x = left; x < right; x++) {
                best = Math.Min(best, KiyumePlans.FloorTopAt(x));
            }
            return best == int.MaxValue ? (int)KiyumeMetrics.BaseFloorAt(left) : best;
        }

        //削高垫低到同一行，并回写规划态（高床回写的是柱脚地面行）
        private static void Flatten(int left, int right, int row) {
            int[] top = KiyumePlans.FloorTop;
            for (int x = left; x < right; x++) {
                if (x < 0 || x >= Main.maxTilesX) {
                    continue;
                }
                int cur = KiyumePlans.FloorTopAt(x);
                if (cur > row) {
                    KiyumeTileBrush.FillRect(x, row, x + 1, cur, FoundationTile);
                }
                else if (cur < row) {
                    KiyumeTileBrush.CarveRect(x, cur, x + 1, row);
                }
                if (top != null && x < top.Length) {
                    top[x] = row;
                }
            }
        }
    }
}
