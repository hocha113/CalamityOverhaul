using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Kiame.Gen
{
    //鬼雨带：自西（入口台地）向东固定列数堆叠，区间半开 [Left,Right)
    //带内地板行由左右缘线性插值；起伏与洼地由骨架 pass 叠加
    internal readonly struct KiameBand
    {
        internal readonly string Name;
        internal readonly int Left;
        internal readonly int Right;
        internal readonly ushort GroundTile;
        internal readonly int FloorRowLeft;
        internal readonly int FloorRowRight;

        internal KiameBand(string name, int left, int cols, ushort groundTile, int floorLeft, int floorRight) {
            Name = name;
            Left = left;
            Right = left + cols;
            GroundTile = groundTile;
            FloorRowLeft = floorLeft;
            FloorRowRight = floorRight;
        }

        internal bool Contains(int x) => x >= Left && x < Right;

        /// <summary>带内该列的地板顶行（不含起伏扰动与洼地）</summary>
        internal float FloorAt(int x) {
            int span = Right - Left;
            float t = span <= 1 ? 0f : (x - Left) / (float)(span - 1);
            return MathHelper.Lerp(FloorRowLeft, FloorRowRight, MathHelper.Clamp(t, 0f, 1f));
        }
    }

    //洼地生成配置：每带各一份，骨架 pass 按此挖坑
    internal readonly struct KiamePoolProfile
    {
        internal readonly int CountMin;
        internal readonly int CountMax;      //含
        internal readonly int HalfWidthMin;
        internal readonly int HalfWidthMax;  //含
        internal readonly int DepthMin;
        internal readonly int DepthMax;      //含

        internal KiamePoolProfile(int countMin, int countMax, int halfWidthMin, int halfWidthMax, int depthMin, int depthMax) {
            CountMin = countMin;
            CountMax = countMax;
            HalfWidthMin = halfWidthMin;
            HalfWidthMax = halfWidthMax;
            DepthMin = depthMin;
            DepthMax = depthMax;
        }

        internal bool Any => CountMax > 0;
    }

    //生成与运行时常量集中声明；扩容/调参只动此处
    //蓝图 Doc/plans/Kiame/DESIGN.md，骨架镜像 KiyumeMetrics 惯例（姊妹世界不互引）
    internal static class KiameMetrics
    {
        internal const int Width = 2400;
        internal const int Height = 600;
        //世界四周实心边界厚度
        internal const int BorderThick = 12;
        internal const int PlayLeft = BorderThick;
        internal const int PlayRight = Width - BorderThick;

        //════════ 横向带表（列） ════════

        internal const int PlateauCols = 200;
        internal const int VillageWestCols = 580;
        internal const int FlatsCols = 380;
        internal const int VillageEastCols = 520;
        internal const int MarshCols = 440;
        internal const int ReserveCols = Width - PlateauCols - VillageWestCols - FlatsCols - VillageEastCols - MarshCols;

        internal const int VillageWestLeft = PlateauCols;
        internal const int FlatsLeft = VillageWestLeft + VillageWestCols;
        internal const int VillageEastLeft = FlatsLeft + FlatsCols;
        internal const int MarshLeft = VillageEastLeft + VillageEastCols;
        internal const int ReserveLeft = MarshLeft + MarshCols;

        //════════ 纵向剖面 ════════
        //
        //  [BorderThick,220)  天空带：乌云雷幡的主视区
        //  220..地板线        可玩空域
        //  地板线             台地 358 缓降到泽地 414，再抬回预留岭 368
        //  地板线以下         实心地体，直到世界底
        //
        //地板行关键值（带表成对声明，带内插值）：
        //  台地 358→364 / 西村 364→390 / 洼原 390→402 / 东村 402→398 / 泽地 398→414 / 预留岭 414→368

        //地板起伏振幅：村落最平（房子要放得下），泽地与预留岭最野
        internal const int FloorWobbleCalm = 2;
        internal const int FloorWobbleMid = 4;
        internal const int FloorWobbleRough = 7;

        //worldSurface 压到所有地板与洼底之下：玩法层判"地表"，天幕可见（同 Kiyume 方向）
        internal const int WorldSurfaceRow = 480;
        internal const int RockLayerRow = 540;
        //深处基底行：从这里到世界底全是石头
        internal const int DeepBaseRow = 500;

        //════════ 锚点 ════════

        //出生在台地东缘：西边是入口伞与回头路，东边是下坡的废村
        internal const int SpawnX = 120;
        //出生区全平列数
        internal const int SpawnFlatCols = 30;
        internal static int SpawnReserveLeft => SpawnX - SpawnFlatCols / 2 - 12;
        internal static int SpawnReserveRight => SpawnX + SpawnFlatCols / 2 + 12;

        internal static readonly KiameBand[] Bands;

        //逐带洼地配置，索引与 Bands 对齐
        internal static readonly KiamePoolProfile[] PoolProfiles;

        //宏观种子：主世界派生，同一存档的鬼雨布局固定
        internal static int MacroSeed { get; private set; }

        static KiameMetrics() {
            Bands = [
                new KiameBand("入口台地", 0, PlateauCols, TileID.Stone, 358, 364),
                new KiameBand("西村", VillageWestLeft, VillageWestCols, TileID.Dirt, 364, 390),
                new KiameBand("洼原", FlatsLeft, FlatsCols, TileID.Mud, 390, 402),
                new KiameBand("东村", VillageEastLeft, VillageEastCols, TileID.Dirt, 402, 398),
                new KiameBand("泽地", MarshLeft, MarshCols, TileID.Mud, 398, 414),
                new KiameBand("预留岭", ReserveLeft, ReserveCols, TileID.Stone, 414, 368),
            ];
            PoolProfiles = [
                new KiamePoolProfile(0, 0, 0, 0, 0, 0),
                new KiamePoolProfile(5, 8, 3, 8, 2, 3),
                new KiamePoolProfile(10, 14, 5, 14, 2, 5),
                new KiamePoolProfile(6, 9, 4, 10, 2, 4),
                new KiamePoolProfile(6, 8, 8, 18, 4, 8),
                new KiamePoolProfile(0, 0, 0, 0, 0, 0),
            ];
            int sum = 0;
            foreach (KiameBand band in Bands) {
                sum += band.Right - band.Left;
            }
            if (sum != Width) {
                throw new InvalidOperationException($"[Kiame] 带表列数总和{sum}与Width{Width}不符");
            }
        }

        internal static KiameBand? BandForColumn(int x) {
            foreach (KiameBand band in Bands) {
                if (band.Contains(x)) {
                    return band;
                }
            }
            return null;
        }

        /// <summary>带索引（0=台地..5=预留岭），越界给 -1</summary>
        internal static int BandIndexForColumn(int x) {
            for (int i = 0; i < Bands.Length; i++) {
                if (Bands[i].Contains(x)) {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>该列的基准地板行（不含起伏与洼地）；越界钳到端点带</summary>
        internal static float BaseFloorAt(int x) {
            if (x < 0) {
                return Bands[0].FloorRowLeft;
            }
            if (x >= Width) {
                return Bands[^1].FloorRowRight;
            }
            return BandForColumn(x)?.FloorAt(x) ?? Bands[^1].FloorRowRight;
        }

        /// <summary>该列的起伏振幅：村落带最平，泽地与预留岭最野</summary>
        internal static int WobbleAmpAt(int x) {
            int band = BandIndexForColumn(x);
            return band switch {
                0 => FloorWobbleCalm,
                1 or 3 => FloorWobbleCalm,
                2 => FloorWobbleMid,
                4 => FloorWobbleMid,
                _ => FloorWobbleRough,
            };
        }

        /// <summary>进入前在主世界缓存宏观种子</summary>
        internal static void CacheMacroSeed() {
            MacroSeed = Main.ActiveWorldFileData?.SeedText?.GetHashCode() ?? 0;
        }

        //════════ 村落结构常量 ════════

        //组团：2-4 栋共享窄巷成组，组团间留大间距保剪影呼吸
        internal const int ClusterHutMin = 2;
        internal const int ClusterHutMax = 4;      //含
        internal const int AlleyMin = 3;           //窄巷山墙间距（含）
        internal const int AlleyMax = 5;           //含
        internal const int ClusterGapMin = 22;     //组团间距（含）
        internal const int ClusterGapMax = 40;     //含
        //户型
        internal const int HutWidthMin = 10;
        internal const int HutWidthMax = 16;       //含
        internal const int HutWallHMin = 5;
        internal const int HutWallHMax = 7;        //含
        //残破抽签：整面塌顶 / 沉水户（东村专属加权）
        internal const float RoofCollapseChance = 0.30f;
        internal const float SunkenHutChanceEast = 0.35f;
        internal const float SunkenHutChanceWest = 0.10f;
        //村井：每组团至多一口
        internal const float WellChance = 0.45f;
        internal const int WellDepthMin = 6;
        internal const int WellDepthMax = 10;      //含
    }
}
