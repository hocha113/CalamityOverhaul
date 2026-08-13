namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.UI
{
    /// <summary>
    /// 地牢加载屏色板与布局常量<br/>
    /// 色板与 DungeonworldLoading.fx / DungeonworldSky.fx 同源，改这里必须同步改 shader 内的 #define 常量<br/>
    /// 鎏金两档取 ChroniclePalette.Gold / GoldHi 同值——地牢的教团手工与任务书的远征纪要共用一种金
    /// </summary>
    internal static class DungeonworldLoadTheme
    {
        //恒定主色板
        public static readonly Color Abyss = new(5, 7, 14);          //井心/清屏底色
        public static readonly Color StoneDeep = new(14, 19, 29);    //石壁暗部/砖缝底
        public static readonly Color Stone = new(31, 39, 53);        //石壁中间调
        public static readonly Color StoneLit = new(58, 67, 86);     //受烛面/砖缝下唇
        public static readonly Color Candle = new(233, 185, 102);    //烛光/尘埃/吊笼灯
        public static readonly Color CandleHi = new(255, 233, 184);  //烛芯/顶光心
        public static readonly Color Gilt = new(186, 146, 76);       //深度计轨/铭牌(= ChroniclePalette.Gold)
        public static readonly Color GiltHi = new(242, 214, 148);    //过层闪亮(= ChroniclePalette.GoldHi)
        public static readonly Color Parchment = new(217, 205, 178); //文字主色
        public static readonly Color InkFaint = new(107, 100, 84);   //未达层铭牌/次级文字

        /// <summary>七层强调色：窗洞光斑/砖缝染色/铭牌点亮色（I..VII）</summary>
        public static readonly Color[] BandAccents = [
            new(62, 99, 176),   //I   教堂区·圣蓝
            new(163, 78, 104),  //II  牢狱层·囚粉
            new(138, 107, 63),  //III 大档案馆·纸墨褐
            new(63, 116, 88),   //IV  水牢·沼绿
            new(199, 185, 149), //V   万骨窖·骨白
            new(158, 85, 39),   //VI  铸造机关层·炉锈橙
            new(94, 85, 168),   //VII 倒吊教堂·冥紫
        ];

        public const int BandCount = 7;

        //进入节拍（秒）
        public const float FirstBellAt = 0.05f;    //第一响落点
        public const float BlackHoldEnd = 0.18f;   //纯黑保持结束
        public const float IntroFadeEnd = 0.65f;   //顶光/吊笼滑入结束
        public const float ScrollRampEnd = 1.0f;   //石壁滚动升至巡航

        //进度估计（真实进度缺席时的时间估计，钉 95%）
        public const float EnterEstSeconds = 14f;  //进入路径估时（M0 按世界规模实测重标）
        public const float ExitEstSeconds = 6f;    //退出路径估时（大地图读档更久，M0 实测重标）
        public const float EstPin = 0.95f;

        //石壁巡航速度（屏高/秒）与速率增益界
        public const float BaseScrollSpeed = 0.055f;
        public const float ScrollGainMin = 0.7f;
        public const float ScrollGainMax = 1.3f;

        //文案轮换：0.45s 淡入 / 3.8s 驻留 / 0.55s 淡出
        public const float TipFadeIn = 0.45f;
        public const float TipHold = 3.8f;
        public const float TipFadeOut = 0.55f;
        public const float TipPeriod = TipFadeIn + TipHold + TipFadeOut;

        //过层铭牌闪亮时长
        public const float PlaqueFlashTime = 0.3f;

        //深度计布局（屏宽/屏高的比例）
        public const float RailX = 0.918f;
        public const float RailTop = 0.175f;
        public const float RailBottom = 0.825f;

        //前景伪粒子（上飘尘埃与烛烬）数量
        public const int DustCount = 36;

        /// <summary>颜色转 shader 的 float3（0~1）</summary>
        public static Vector3 Vec3(Color color) => color.ToVector3();

        /// <summary>确定性散列（0~1），加载屏伪粒子与 flicker 用，不吃 Main.rand</summary>
        public static float Hash01(float seed) {
            float x = (float)System.Math.Sin(seed * 12.9898f + 78.233f) * 43758.5453f;
            return x - (float)System.Math.Floor(x);
        }
    }
}
