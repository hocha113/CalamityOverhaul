using CalamityOverhaul.Content.Scenarios.Hadalworld.Gen;

namespace CalamityOverhaul.Content.Scenarios.Hadalworld.UI
{
    /// <summary>
    /// 深渊海沟加载屏色板与布局常量(镜像 DungeonworldLoadTheme,第一期纯 CPU 无 shader)<br/>
    /// 主题=下潜:纵向海水渐变随深度整体变暗,五带铭牌深度计,气泡上飘
    /// </summary>
    internal static class HadalworldLoadTheme
    {
        //恒定主色板(海水五带渐变的取样点)
        public static readonly Color SurfaceCyan = new(56, 124, 148);   //海面浅青
        public static readonly Color SunlitBlue = new(22, 66, 108);     //日光带蓝
        public static readonly Color TwilightBlue = new(11, 32, 62);    //暮光带残蓝
        public static readonly Color MidnightBlue = new(5, 13, 30);     //午夜带深蓝
        public static readonly Color AbyssInk = new(2, 5, 12);          //深渊带墨色
        public static readonly Color HadalBlack = new(1, 2, 6);         //超深渊近黑/清屏底色
        public static readonly Color SeaFoam = new(172, 208, 214);      //文字主色·淡浪白
        public static readonly Color FoamDim = new(96, 128, 142);       //次级文字/未达带铭牌
        public static readonly Color GaugeTeal = new(88, 168, 176);     //深度计轨·蚀青
        public static readonly Color GaugeHi = new(196, 240, 240);      //过带闪亮/吊坠
        public static readonly Color Bubble = new(150, 196, 210);       //上飘气泡
        public static readonly Color SkyShaft = new(120, 190, 205);     //顶部天光柱心

        /// <summary>五带强调色(铭牌点亮/播报),自浅至深残光递减</summary>
        public static readonly Color[] BandAccents = [
            new(94, 186, 178),   //日光带·浅海青绿
            new(70, 122, 170),   //暮光带·残光蓝
            new(84, 92, 158),    //午夜带·蓝紫
            new(52, 96, 108),    //深渊带·暗蚀青
            new(158, 168, 176),  //超深渊带·无光灰白
        ];

        /// <summary>玩家可见带数(天空缓冲带不上深度计)</summary>
        public const int BandCount = 5;

        /// <summary>标称满深(米),超深渊带口径,深度计读数=travel*此值</summary>
        public const float TrenchDepthMeters = 10800f;

        //进入节拍(秒)
        public const float FirstPlungeAt = 0.35f;  //首声水涌落点
        public const float IntroFadeEnd = 0.65f;   //画面自压黑处起亮结束
        public const float UiRampEnd = 1.1f;       //前景文字/深度计淡入结束

        //进度估计(真实进度缺席时的时间估计,钉 95%)
        public const float EnterEstSeconds = 12f;  //进入路径估时(2200x5000 世界,待实测重标)
        public const float ExitEstSeconds = 6f;    //退出路径估时(大地图读档更久,待实测重标)
        public const float EstPin = 0.95f;

        //文案轮换:0.45s 淡入 / 3.8s 驻留 / 0.55s 淡出
        public const float TipFadeIn = 0.45f;
        public const float TipHold = 3.8f;
        public const float TipFadeOut = 0.55f;
        public const float TipPeriod = TipFadeIn + TipHold + TipFadeOut;

        //过带铭牌闪亮时长
        public const float PlaqueFlashTime = 0.3f;

        //深度计布局(屏宽/屏高的比例)
        public const float RailX = 0.918f;
        public const float RailTop = 0.175f;
        public const float RailBottom = 0.825f;

        //上飘气泡伪粒子数量
        public const int BubbleCount = 40;

        /// <summary>
        /// 五带的归一化深度断点(带底,海面=0 世界底=1),直接由 Metrics 常量换算,
        /// 与 HadalworldMetrics.DepthFraction 同一坐标系,不自造硬编码行数
        /// </summary>
        public static readonly float[] BandBottomFracs = BuildBandBottomFracs();

        private static float[] BuildBandBottomFracs() {
            float span = HadalworldMetrics.Height - HadalworldMetrics.SeaLevelRow;
            return [
                (HadalworldMetrics.SunlitBottom - HadalworldMetrics.SeaLevelRow) / span,
                (HadalworldMetrics.TwilightBottom - HadalworldMetrics.SeaLevelRow) / span,
                (HadalworldMetrics.MidnightBottom - HadalworldMetrics.SeaLevelRow) / span,
                (HadalworldMetrics.AbyssalBottom - HadalworldMetrics.SeaLevelRow) / span,
                1f,
            ];
        }

        /// <summary>归一化深度→带序(0..4),供铭牌/播报/配色取档</summary>
        public static int BandIndex(float frac) {
            for (int i = 0; i < BandCount - 1; i++) {
                if (frac < BandBottomFracs[i]) {
                    return i;
                }
            }
            return BandCount - 1;
        }

        //海水渐变取样点:海面青起点+五带带底,键与色一一对应
        private static readonly Color[] waterStops =
            [SurfaceCyan, SunlitBlue, TwilightBlue, MidnightBlue, AbyssInk, HadalBlack];
        private static readonly float[] waterKeys =
            [0f, BandBottomFracs[0], BandBottomFracs[1], BandBottomFracs[2], BandBottomFracs[3], 1f];

        /// <summary>归一化深度→海水底色,取样点线性插值,末端沉入近黑</summary>
        public static Color WaterAt(float frac) {
            if (frac <= 0f) {
                return SurfaceCyan;
            }
            for (int i = 1; i < waterKeys.Length; i++) {
                if (frac <= waterKeys[i]) {
                    float t = (frac - waterKeys[i - 1]) / MathHelper.Max(waterKeys[i] - waterKeys[i - 1], 0.0001f);
                    return Color.Lerp(waterStops[i - 1], waterStops[i], t);
                }
            }
            return HadalBlack;
        }

        /// <summary>确定性散列(0~1),加载屏伪粒子与微光用,不吃 Main.rand</summary>
        public static float Hash01(float seed) {
            float x = (float)System.Math.Sin(seed * 12.9898f + 78.233f) * 43758.5453f;
            return x - (float)System.Math.Floor(x);
        }
    }
}
