namespace CalamityOverhaul.Content.Items.Magic.Elysiums.Revelations
{
    /// <summary>单个骑士的身份参数(轨道运动学与视觉；被动接线在 <see cref="ElysiumPlayer"/>)</summary>
    internal readonly struct HorsemanDef
    {
        public Color BodyColor { get; init; }
        public Color AccentColor { get; init; }
        /// <summary>随行圣徽 SVG(骑影上方悬浮的标记)</summary>
        public string SigilPath { get; init; }
        /// <summary>基础轨道半径</summary>
        public float OrbitRadius { get; init; }
        /// <summary>角速度</summary>
        public float OrbitSpeed { get; init; }
        /// <summary>体型倍率</summary>
        public float SizeMul { get; init; }
    }

    /// <summary>
    /// 天启四骑士目录：0瘟疫 1战争 2饥荒 3死亡。
    /// 轨道形态各异：瘟疫漩涡收放、战争冲刺扩张、饥荒沉缓低回、死亡八字巡游
    /// </summary>
    internal static class HorsemanCatalog
    {
        public const int Count = 4;

        private static readonly HorsemanDef[] defs = [
            //0 瘟疫：病绿
            new HorsemanDef {
                BodyColor = new Color(122, 168, 92),
                AccentColor = new Color(190, 235, 130),
                SigilPath = "M 0 -0.7 L 0 0.7 M -0.5 -0.3 L 0.5 -0.3 M -0.35 0.25 Q 0 0.55 0.35 0.25",
                OrbitRadius = 210f,
                OrbitSpeed = 0.021f,
                SizeMul = 1f,
            },
            //1 战争：赤红
            new HorsemanDef {
                BodyColor = new Color(196, 74, 60),
                AccentColor = new Color(255, 150, 110),
                SigilPath = "M -0.45 0.6 L 0.35 -0.55 M 0.35 -0.55 L 0.12 -0.62 M 0.35 -0.55 L 0.44 -0.32 M -0.28 0.18 L 0.02 0.48",
                OrbitRadius = 250f,
                OrbitSpeed = 0.03f,
                SizeMul = 1.05f,
            },
            //2 饥荒：枯黑
            new HorsemanDef {
                BodyColor = new Color(96, 84, 62),
                AccentColor = new Color(214, 186, 120),
                SigilPath = "M 0 -0.65 L 0 0.3 M -0.5 -0.3 L 0.5 -0.3 M -0.5 -0.3 L -0.5 0 C -0.5 0.3 -0.2 0.3 -0.2 0 L -0.2 -0.3 M 0.5 -0.3 L 0.5 0 C 0.5 0.3 0.2 0.3 0.2 0 L 0.2 -0.3",
                OrbitRadius = 185f,
                OrbitSpeed = 0.014f,
                SizeMul = 0.98f,
            },
            //3 死亡：苍白
            new HorsemanDef {
                BodyColor = new Color(168, 172, 176),
                AccentColor = new Color(235, 240, 245),
                SigilPath = "M 0.1 -0.72 C -0.55 -0.6 -0.55 -0.05 0.05 -0.18 M 0.05 -0.18 L -0.3 0.72",
                OrbitRadius = 230f,
                OrbitSpeed = 0.018f,
                SizeMul = 1.1f,
            },
        ];

        public static HorsemanDef Get(int index) => defs[index];
    }
}
