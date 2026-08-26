using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.Gen
{
    //湖畔带：自西（血湖）向东固定列数堆叠，区间半开 [Left,Right)
    //带内地板行由左右缘线性插值，地面必须是斜的，雾线才有东西可淹
    internal readonly struct ShoreBand
    {
        internal readonly string Name;
        internal readonly int Left;
        internal readonly int Right;
        internal readonly ushort GroundTile;
        internal readonly int FloorRowLeft;
        internal readonly int FloorRowRight;

        internal ShoreBand(string name, int left, int cols, ushort groundTile, int floorLeft, int floorRight) {
            Name = name;
            Left = left;
            Right = left + cols;
            GroundTile = groundTile;
            FloorRowLeft = floorLeft;
            FloorRowRight = floorRight;
        }

        internal bool Contains(int x) => x >= Left && x < Right;

        /// <summary>带内该列的地板顶行（不含起伏扰动）</summary>
        internal float FloorAt(int x) {
            int span = Right - Left;
            float t = span <= 1 ? 0f : (x - Left) / (float)(span - 1);
            return MathHelper.Lerp(FloorRowLeft, FloorRowRight, MathHelper.Clamp(t, 0f, 1f));
        }
    }

    //生成与运行时常量集中声明；扩容/调参只动此处
    //蓝图 Doc/plans/Kiyume/DESIGN.md，镜像 OldNetMetrics 惯例
    internal static class KiyumeMetrics
    {
        internal const int Width = 3200;
        internal const int Height = 800;
        //世界四周实心边界厚度
        internal const int BorderThick = 12;
        internal const int PlayLeft = BorderThick;
        internal const int PlayRight = Width - BorderThick;

        //════════ 横向带表（列） ════════

        internal const int LakeCols = 320;
        internal const int ShoalCols = 300;
        internal const int VillageCols = 1080;
        internal const int GroveCols = 800;
        internal const int RidgeCols = Width - LakeCols - ShoalCols - VillageCols - GroveCols;

        internal const int ShoalLeft = LakeCols;
        internal const int VillageLeft = ShoalLeft + ShoalCols;
        internal const int GroveLeft = VillageLeft + VillageCols;
        internal const int RidgeLeft = GroveLeft + GroveCols;

        //════════ 纵向剖面 ════════
        //
        //  [BorderThick,240)  天空带：天幕主视区，村影与远山剪影落在这里
        //  240..地板线        可玩空域
        //  地板线             自西 585（湖底最深）一路抬到 296（远山脊）
        //  地板线以下         实心地体，直到世界底
        //
        //地板行关键值（带表里成对声明，带内插值）：
        //  湖底 585→466 / 滩涂 466→452 / 村落 452→420 / 枯林 420→380 / 远山 380→296

        //血湖水面行：湖床在它之下，滩涂在它之上，岸线由此浮出
        internal const int LakeSurfaceRow = 470;
        //地板起伏振幅（村落带最平，湖床与远山最野）
        internal const int FloorWobbleCalm = 2;
        internal const int FloorWobbleRough = 7;

        //worldSurface 压到所有地板之下：玩法层判"地表"，天幕可见（同 OldNet 方向，与深牢相反）
        internal const int WorldSurfaceRow = 620;
        internal const int RockLayerRow = 700;

        //天幕地平线基准行：相机中心停在这里时地平线落在屏幕 60% 处，上下走则反向轻移
        internal const int HorizonRefRow = 436;
        internal static float HorizonRefWorldY => HorizonRefRow * 16f;

        //════════ 锚点 ════════

        //出生在村口：西边是滩涂与湖，东边是村子，一进来就面对那张剪影
        internal const int SpawnX = VillageLeft + 40;
        //出生区全平列数
        internal const int SpawnFlatCols = 34;

        //════════ 雾线几何（浓度公式见 KiyumeFogSim，潮汐见 KiyumeFogTide） ════════

        //退潮：雾线贴着地面，只淹滩涂与洼地
        internal const int FogLineLowRow = 458;
        //涨潮：村子沉进雾海，只剩屋顶
        internal const int FogLineHighRow = 402;
        //湖侧雾面抬升（px）：雾是从湖里蒸上来的，源头那侧的面更高
        internal const float LakeTiltPx = 96f;
        //抬升衰减跨度（px）：从湖右缘算起 600 格后抬升归零，整片雾面缓缓向湖倾斜
        internal const float TiltSpanPx = 600f * 16f;
        //离湖越远雾越薄，远山那头剩多少
        internal const float FarFogMul = 0.35f;

        //════════ 水平线体系（血湖真水面是近景唯一锐利线，雾海面只在岸上） ════════

        /// <summary>血湖真水面（世界px，=LakeSurfaceRow，固定不随潮汐）</summary>
        internal static float LakeWaterYPx => LakeSurfaceRow * 16f;
        /// <summary>水面右缘（世界px）：灌水的东界（ShoalLeft+40，与 FillLake 同式），
        /// 也是雾面亮边渐入 / 水面反射带渐出的交接原点</summary>
        internal static float WaterRightPx => (ShoalLeft + 40) * 16f;
        //雾面亮边渐入跨度（px）：岸线以东这么远内 rim 从零长回全强，湖上不许有悬空液面线
        internal const float RimFadeSpanPx = 40f * 16f;
        //贴水蒸腾雾：水面以上这么高内有贴水雾墙（底部近满浓度、向上二次衰减）
        internal const float SteamHeightPx = 20f * 16f;
        //蒸腾雾底部浓度
        internal const float SteamBaseDensity = 0.88f;
        //蒸腾雾横向渐出跨度（px）：水面右缘以西这么远内从全强收敛到零，把岸线交给雾海
        internal const float SteamFadeSpanPx = 30f * 16f;

        /// <summary>血湖右缘（世界px），雾线倾斜与距离衰减的原点</summary>
        internal static float LakeRightPx => LakeCols * 16f;
        /// <summary>距离衰减跨度（世界px）：湖右缘到远山带中段</summary>
        internal static float FalloffSpanPx => (RidgeLeft + RidgeCols / 2 - LakeCols) * 16f;

        internal static readonly ShoreBand[] Bands;

        //宏观种子：主世界派生，同一存档的鬼梦布局固定
        internal static int MacroSeed { get; private set; }

        static KiyumeMetrics() {
            Bands = [
                new ShoreBand("深湖带", 0, LakeCols, TileID.Crimstone, 585, 466),
                new ShoreBand("滩涂带", ShoalLeft, ShoalCols, TileID.Mud, 466, 452),
                new ShoreBand("村落带", VillageLeft, VillageCols, TileID.Ash, 452, 420),
                new ShoreBand("枯林带", GroveLeft, GroveCols, TileID.Dirt, 420, 380),
                new ShoreBand("远山带", RidgeLeft, RidgeCols, TileID.Stone, 380, 296),
            ];
            int sum = 0;
            foreach (ShoreBand band in Bands) {
                sum += band.Right - band.Left;
            }
            if (sum != Width) {
                throw new InvalidOperationException($"[Kiyume] 带表列数总和{sum}与Width{Width}不符");
            }
        }

        internal static ShoreBand? BandForColumn(int x) {
            foreach (ShoreBand band in Bands) {
                if (band.Contains(x)) {
                    return band;
                }
            }
            return null;
        }

        /// <summary>带索引（0=深湖..4=远山），越界给 -1</summary>
        internal static int BandIndexForColumn(int x) {
            for (int i = 0; i < Bands.Length; i++) {
                if (Bands[i].Contains(x)) {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>该列的基准地板行（不含起伏）；越界钳到端点带</summary>
        internal static float BaseFloorAt(int x) {
            if (x < 0) {
                return Bands[0].FloorRowLeft;
            }
            if (x >= Width) {
                return Bands[^1].FloorRowRight;
            }
            return BandForColumn(x)?.FloorAt(x) ?? Bands[^1].FloorRowRight;
        }

        /// <summary>该列的起伏振幅：村落最平（房子要放得下），湖床与远山最野</summary>
        internal static int WobbleAmpAt(int x) {
            int band = BandIndexForColumn(x);
            return band switch {
                2 => FloorWobbleCalm,
                1 => FloorWobbleCalm + 1,
                _ => FloorWobbleRough,
            };
        }

        /// <summary>进入前在主世界缓存宏观种子</summary>
        internal static void CacheMacroSeed() {
            MacroSeed = Main.ActiveWorldFileData?.SeedText?.GetHashCode() ?? 0;
        }

        //════════ 结构常量（P3 各包调音位；本节骨架由 A 包立，B/C/D/E 按下方锚行追加各自小节） ════════

        //村西鸟居锚点（裁决7）：620±6，入画即见，不与出生平台（SpawnX±SpawnFlatCols/2）重叠
        internal const int ToriiWestX = 620;
        internal const int ToriiWestJitterCols = 6;
        //出生留白半开区间 [Left,Right)=[602,718)：与 KiyumeVillage 现行 spawnPad 判定同口径，
        //PlanReservations 以此登记 ReservedSpans（B 包重构村落后统一改读预留表）
        internal const int SpawnReservePadCols = 24;
        internal static int SpawnReserveLeft => SpawnX - SpawnFlatCols - SpawnReservePadCols;
        internal static int SpawnReserveRight => SpawnX + SpawnFlatCols + SpawnReservePadCols;
        /// <summary>村落木平台统一样式：幽木平台 frameY=16*18（Item.cs 1818 placeStyle=16 对源）</summary>
        internal const short PlatformFrameY = 288;

        //──结构常量锚：B 村落纵深（组团/户型/高床/地窖）──

        //组团：2-4 栋共享窄巷成组，组团间留大间距保剪影呼吸
        internal const int VillageClusterMin = 2;
        internal const int VillageClusterMax = 4;      //含
        internal const int VillageAlleyMin = 3;        //窄巷山墙间距（含）
        internal const int VillageAlleyMax = 5;        //含
        internal const int VillageClusterGapMin = 18;  //组团间距（含）
        internal const int VillageClusterGapMax = 34;  //含
        //高床户型：壳体连楼板整体抬起，床下全通
        //净空取 3：计划书写 2 格，但玩家碰撞盒 42px 进不去 32px，2 格即死规格（B 包报告有账）
        internal const float StiltHutChance = 0.25f;
        internal const int StiltClearRows = 3;                        //床下净空行数
        internal const int StiltLiftRows = StiltClearRows + 1;        //抬升=净空+楼板1厚
        //地窖户型：竖穴+绳梯+蓝朝墙内膛；墙种与 KiyumeStructures.CellarWall 同源，改一处必改两处
        internal const float CellarChance = 0.30f;
        internal const int CellarInnerW = 6;
        internal const int CellarInnerH = 4;
        internal const int CellarShaftRows = 3;        //地表行到内膛顶的竖穴行数
        //内饰：每户按内膛实宽抽件，放不下即跳过
        internal const int InteriorPieceMin = 2;
        internal const int InteriorPieceMax = 4;       //含
        internal const float ButsudanChance = 0.34f;   //佛坛入池率（与旧点灯率同源）
        //屋顶路线硬约束（B 包建壳时校验）
        internal const int RoofStepMaxDh = 4;          //组团内相邻檐口高差上限
        internal const int RoofGapMax = 6;             //山墙间距上限（巷宽 3-5 天然满足）

        //──结构常量锚：C 信仰轴线（鸟居/村社/路祠/石阶/山顶祠）──
        //东口鸟居 / 送葬道口素鸟居锚点（±抽签列；西口锚 ToriiWestX 见上）
        internal const int ToriiEastX = 1668;
        internal const int ToriiEastJitterCols = 4;
        internal const int ToriiFuneralX = 1712;
        internal const int ToriiFuneralJitterCols = 4;
        //村社预留段 [L,R)：compound 靠西落位，东端 ≥12 列后院空地留给 E 包社后井
        internal const int ShrineSpanL = 1150;
        internal const int ShrineSpanR = 1210;
        //村社台基：StoneSlab 44 列 ×3 行，拜殿 26 宽居中（两端各出 9）
        internal const int ShrineBaseCols = 44;
        internal const int ShrineBaseRows = 3;
        //路边祠座数（村缘 1 + 枯林 2-3，避开 E 包墓园窗 [1980,2240]）
        internal const int WaysideCountMin = 3;
        internal const int WaysideCountMax = 4;
        //山道石阶：起点 / 级宽 / 级差硬上限 / 歇脚节奏
        internal const int StairStartX = 2560;
        internal const int StairSegColsMin = 6;
        internal const int StairSegColsMax = 10;
        internal const int StairDropMax = 3;
        internal const int StairRestStepsMin = 5;
        internal const int StairRestStepsMax = 7;
        internal const int StairRestColsMin = 4;
        internal const int StairRestColsMax = 6;
        //山顶平台 [L,R)：行 ~310（基准曲线现值），雾线高潮 402 之上（雾上回望）
        internal const int SummitL = 3080;
        internal const int SummitR = 3094;
        //──结构常量锚：D 水缘（栈桥/船骸/苇塘）──

        //栈桥：东岸端锚点±抖动（列）、向西全长与断口列数；断口以西只剩歪桩残根
        //锚点 332（W4 定案，原 372）：实际水线在 x≈308（FillLake 右界 360 是灌水上限不是岸线，
        //湖带 wobble ±7 再摆 ±19 列），断口区间 root-len..root-len+break=[272,308] 才真悬湖；
        //锚点下限受滩涂带界 320 约束（-8 抖动后 root≥324，岸端永在滩上）
        internal const int JettyRootX = 332;
        internal const int JettyRootJitter = 8;
        internal const int JettyLenMin = 44;
        internal const int JettyLenMax = 60;
        internal const int JettyBreakMin = 8;
        internal const int JettyBreakMax = 12;
        //桥面行：水面 470 上 6 行、滩涂西缘地板 466 上 2 行；桥面列 FloorTop 不回写
        internal const int JettyDeckRow = 464;
        //船骸：滩上艘数（翻扣/侧倾抽签不重复），另水下龙骨肋固定 1 副
        internal const int WreckShoreMin = 2;
        internal const int WreckShoreMax = 3;
        //苇塘窗 [L,R)（裁决17：东缘退到 600，让出生留白 [602,718)）；
        //与 KiyumeStructures 苇丛签名 ReedWindowL/R 数值同源，改一处必改两处
        internal const int ReedPondLeft = 560;
        internal const int ReedPondRight = 600;
        internal const int ReedPondCellsMin = 2;
        internal const int ReedPondCellsMax = 4;
        //苇杆步距（列，含杆宽 1，即间距 1-3 列）
        internal const int ReedStepMin = 2;
        internal const int ReedStepMax = 4;
        //──结构常量锚：E 微区（井/灯道/墓地/旱田/告示）──

        //井：村 2 口（西/东段各一，窗口避开出生留白 [602,718) 与村社段 [1150,1210)）+ 社后 1 口；
        //筒 3 宽深 10-14、底水 2 格——竖井净空 ≥8 行满足 P4 井手位形，井沿中心入 WellMouths
        internal const int WellWestL = 740;
        internal const int WellWestR = 1120;
        internal const int WellEastL = 1240;
        internal const int WellEastR = 1590;
        internal const int WellDepthMin = 10;
        internal const int WellDepthMax = 14;
        //灯笼列道：VillageLeft+30 → GroveLeft-30，柱距 26-34 列；双臂(成对)/单臂 6:4
        internal const int LanternGapMin = 26;
        internal const int LanternGapMax = 34;
        internal const float LanternPairChance = 0.6f;
        //墓地送葬道：枯林 [1980,2240] 抽 60-90 列窗；坟 8-14、三成卒塔婆、沿道石灯 4-6 对
        internal const int GraveWindowL = 1980;
        internal const int GraveWindowR = 2240;
        internal const int GraveSpanMin = 60;
        internal const int GraveSpanMax = 90;
        internal const int GraveCountMin = 8;
        internal const int GraveCountMax = 14;
        internal const float SotobaChance = 0.3f;
        internal const int GraveLanternPairsMin = 4;
        internal const int GraveLanternPairsMax = 6;
        //旱田（裁决17）：[516,558] 窗内 34 列平整 + 5 桩，注册 ScarecrowPlot（守田人 null 则自探）
        internal const int FieldWindowL = 516;
        internal const int FieldWindowR = 558;
        internal const int FieldCols = 34;
        internal const int FieldPostCount = 5;
        //怪谈告示牌：4-5 面（文案池 5 条抽签不重复，计划书 4-6 的上限收到池深）
        internal const int SignCountMin = 4;
        internal const int SignCountMax = 5;
    }
}
