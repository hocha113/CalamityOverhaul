namespace CalamityOverhaul.Content.Items.Magic.Elysiums.Disciples
{
    /// <summary>单个门徒的身份参数(视觉与节奏；能力实现在各自类中)</summary>
    internal readonly struct DiscipleDef
    {
        /// <summary>袍体身份色</summary>
        public Color BodyColor { get; init; }
        /// <summary>亮饰色(缘光/光环/圣徽)</summary>
        public Color AccentColor { get; init; }
        /// <summary>胸前圣徽 SVG 路径(归一 -1~1 空间，M/L/H/V/C/Q/Z)</summary>
        public string EmblemPath { get; init; }
        /// <summary>主动能力冷却(帧)</summary>
        public int AbilityCooldown { get; init; }
        /// <summary>体型倍率</summary>
        public float SizeMul { get; init; }
        /// <summary>环绕速度倍率</summary>
        public float OrbitSpeedMul { get; init; }
    }

    /// <summary>
    /// 十二门徒目录。席位索引即身份：
    /// 0彼得 1安德鲁 2雅各 3约翰 4腓力 5巴多罗买 6多马 7马太 8小雅各 9达太 10奋锐党西门 11犹大
    /// </summary>
    internal static class DiscipleCatalog
    {
        public const int SeatCount = 12;
        /// <summary>约翰的席位(不殉道，启示录钥匙)</summary>
        public const int JohnSeat = 3;
        /// <summary>犹大的席位(背叛者)</summary>
        public const int JudasSeat = 11;

        private static readonly DiscipleDef[] defs = [
            //0 彼得·磐石：钥匙
            new DiscipleDef {
                BodyColor = new Color(181, 191, 224),
                AccentColor = new Color(235, 240, 255),
                EmblemPath = "M 0 -0.35 C 0.32 -0.35 0.32 -0.78 0 -0.78 C -0.32 -0.78 -0.32 -0.35 0 -0.35 M 0 -0.35 L 0 0.72 M 0 0.44 L 0.3 0.44 M 0 0.72 L 0.38 0.72",
                AbilityCooldown = 600,
                SizeMul = 1.08f,
                OrbitSpeedMul = 0.9f,
            },
            //1 安德鲁·渔夫：X形圣安德鲁十字
            new DiscipleDef {
                BodyColor = new Color(104, 178, 219),
                AccentColor = new Color(190, 232, 255),
                EmblemPath = "M -0.58 -0.58 L 0.58 0.58 M 0.58 -0.58 L -0.58 0.58",
                AbilityCooldown = 480,
                SizeMul = 1f,
                OrbitSpeedMul = 1.1f,
            },
            //2 雅各·雷霆之子：闪电
            new DiscipleDef {
                BodyColor = new Color(250, 220, 96),
                AccentColor = new Color(255, 246, 190),
                EmblemPath = "M 0.26 -0.78 L -0.2 0.06 L 0.12 0.06 L -0.26 0.78",
                AbilityCooldown = 150,
                SizeMul = 1.02f,
                OrbitSpeedMul = 1.25f,
            },
            //3 约翰·启示：启示之眼
            new DiscipleDef {
                BodyColor = new Color(203, 209, 252),
                AccentColor = new Color(240, 242, 255),
                EmblemPath = "M -0.7 0 Q 0 -0.56 0.7 0 Q 0 0.56 -0.7 0 Z M 0 -0.2 C 0.26 -0.2 0.26 0.2 0 0.2 C -0.26 0.2 -0.26 -0.2 0 -0.2",
                AbilityCooldown = 360,
                SizeMul = 1f,
                OrbitSpeedMul = 0.85f,
            },
            //4 腓力·引导：十字权杖
            new DiscipleDef {
                BodyColor = new Color(228, 210, 152),
                AccentColor = new Color(255, 244, 205),
                EmblemPath = "M 0 -0.78 L 0 0.78 M -0.34 -0.36 L 0.34 -0.36",
                AbilityCooldown = 300,
                SizeMul = 0.97f,
                OrbitSpeedMul = 1.05f,
            },
            //5 巴多罗买·真言：剥皮刀
            new DiscipleDef {
                BodyColor = new Color(168, 218, 174),
                AccentColor = new Color(220, 250, 224),
                EmblemPath = "M 0 0.75 L 0 0.02 Q 0 -0.68 0.46 -0.54 Q 0.2 -0.24 0 0.02",
                AbilityCooldown = 420,
                SizeMul = 0.98f,
                OrbitSpeedMul = 1f,
            },
            //6 多马·验证：矩尺与验痕
            new DiscipleDef {
                BodyColor = new Color(186, 160, 214),
                AccentColor = new Color(232, 214, 255),
                EmblemPath = "M -0.5 -0.6 L -0.5 0.6 L 0.62 0.6 M -0.08 0.06 L 0.14 0.32 L 0.56 -0.3",
                AbilityCooldown = 540,
                SizeMul = 1f,
                OrbitSpeedMul = 0.95f,
            },
            //7 马太·税吏：钱袋
            new DiscipleDef {
                BodyColor = new Color(233, 198, 96),
                AccentColor = new Color(255, 238, 170),
                EmblemPath = "M -0.34 -0.02 C -0.52 0.72 0.52 0.72 0.34 -0.02 C 0.28 -0.2 -0.28 -0.2 -0.34 -0.02 M -0.2 -0.14 Q 0 -0.6 0.2 -0.14",
                AbilityCooldown = 240,
                SizeMul = 1f,
                OrbitSpeedMul = 1f,
            },
            //8 小雅各·奉献：漂布之杖
            new DiscipleDef {
                BodyColor = new Color(214, 214, 216),
                AccentColor = new Color(248, 248, 250),
                EmblemPath = "M -0.26 0.76 L 0.2 -0.4 C 0.5 -0.78 0.02 -0.86 0.2 -0.4",
                AbilityCooldown = 480,
                SizeMul = 0.94f,
                OrbitSpeedMul = 1.05f,
            },
            //9 达太·奇迹：四芒星
            new DiscipleDef {
                BodyColor = new Color(196, 152, 231),
                AccentColor = new Color(238, 220, 255),
                EmblemPath = "M 0 -0.75 L 0.12 -0.12 L 0.75 0 L 0.12 0.12 L 0 0.75 L -0.12 0.12 L -0.75 0 L -0.12 -0.12 Z",
                AbilityCooldown = 660,
                SizeMul = 1f,
                OrbitSpeedMul = 1.1f,
            },
            //10 奋锐党西门·狂热：圣火
            new DiscipleDef {
                BodyColor = new Color(233, 122, 95),
                AccentColor = new Color(255, 196, 150),
                EmblemPath = "M 0 0.75 C -0.5 0.34 -0.26 -0.1 0 -0.75 C 0.1 -0.3 0.46 -0.2 0.36 0.2 C 0.3 0.46 0.16 0.6 0 0.75 Z",
                AbilityCooldown = 90,
                SizeMul = 1.02f,
                OrbitSpeedMul = 1.35f,
            },
            //11 犹大·背叛：三十银币
            new DiscipleDef {
                BodyColor = new Color(139, 72, 66),
                AccentColor = new Color(204, 204, 214),
                EmblemPath = "M 0 -0.58 C 0.56 -0.58 0.56 0.58 0 0.58 C -0.56 0.58 -0.56 -0.58 0 -0.58 M 0 -0.3 L 0 0.3",
                AbilityCooldown = 300,
                SizeMul = 1f,
                OrbitSpeedMul = 0.8f,
            },
        ];

        public static DiscipleDef Get(int seat) => defs[seat];
    }
}
