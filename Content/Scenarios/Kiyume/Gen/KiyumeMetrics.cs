using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.Gen
{
    //湖畔带：自西（血湖）向东固定列数堆叠，区间半开 [Left,Right)
    //带内地板行由左右缘线性插值——地面必须是斜的，雾线才有东西可淹
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

        //出生在村口：西边是滩涂与湖，东边是村子——一进来就面对那张剪影
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

        /// <summary>血湖右缘（世界px）——雾线倾斜与距离衰减的原点</summary>
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
    }
}
