using System;
using Terraria;
using Terraria.GameInput;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.UI
{
    /// <summary>
    /// 伞下水鏡 HUD 色板与几何。HUD 不用图标表示领域形态——
    /// 整面鏡子随 RainBlend 在两套现成色板间浸染：
    /// 血湖态取 <see cref="KikasaVaults.KikasaVaultTheme"/> 族（与 KikasaGrade.fx 同源），
    /// 鬼雨态取 <see cref="KikasaStoryTheme"/> 族（与 KikasaSky.fx RAIN_* 同源，禁红禁暖）。
    /// </summary>
    internal static class KikasaHudTheme
    {
        #region UI空间坐标（与调用语境无关）
        //UIHandle 的 Update/Draw 运行在 UI 缩放空间，逻辑帧里是原始后台缓冲尺寸；
        //跨语境布局一律走这组换算，禁止直接读 Main.screenWidth/Height

        /// <summary>UI 空间屏幕宽</summary>
        public static float UIScreenW => PlayerInput.RealScreenWidth / Main.UIScale;

        /// <summary>UI 空间屏幕高</summary>
        public static float UIScreenH => PlayerInput.RealScreenHeight / Main.UIScale;

        /// <summary>UI 空间鼠标位置</summary>
        public static Vector2 UIMouse => new Vector2(PlayerInput.MouseX, PlayerInput.MouseY) / Main.UIScale;
        #endregion

        #region 双形态色板插值
        //角色色一律经 Blend 取用，血湖端与鬼雨端分别与两套领域着色器常量同源

        private static readonly Color BloodVoid = new(8, 2, 4);
        private static readonly Color RainVoid = new(7, 9, 11);
        private static readonly Color BloodDeep = new(18, 4, 7);
        private static readonly Color RainDeep = new(14, 18, 21);
        private static readonly Color BloodMid = new(41, 10, 14);
        private static readonly Color RainMid = new(28, 35, 38);
        private static readonly Color BloodAccent = new(198, 66, 60);
        private static readonly Color RainAccent = new(96, 120, 126);
        private static readonly Color BloodGlow = new(246, 133, 112);
        private static readonly Color RainGlow = new(196, 214, 218);
        private static readonly Color BloodText = new(240, 224, 219);
        private static readonly Color RainText = new(226, 234, 236);
        private static readonly Color BloodTextDim = new(172, 130, 126);
        private static readonly Color RainTextDim = new(150, 178, 186);

        /// <summary>近黑底色</summary>
        public static Color Void(float rain) => Color.Lerp(BloodVoid, RainVoid, rain);

        /// <summary>深水底色（CPU 回退面板底）</summary>
        public static Color Deep(float rain) => Color.Lerp(BloodDeep, RainDeep, rain);

        /// <summary>中间调（CPU 回退的天空带）</summary>
        public static Color Mid(float rain) => Color.Lerp(BloodMid, RainMid, rain);

        /// <summary>主强调色：血红 ⇄ 雨青</summary>
        public static Color Accent(float rain) => Color.Lerp(BloodAccent, RainAccent, rain);

        /// <summary>亮色：血沫暖光 ⇄ 溺月惨白</summary>
        public static Color Glow(float rain) => Color.Lerp(BloodGlow, RainGlow, rain);

        /// <summary>文字主色</summary>
        public static Color Text(float rain) => Color.Lerp(BloodText, RainText, rain);

        /// <summary>文字次色</summary>
        public static Color TextDim(float rain) => Color.Lerp(BloodTextDim, RainTextDim, rain);
        #endregion

        #region 掌中缩影几何
        /// <summary>缩影画片宽（横卷小片）</summary>
        public const int MiniW = 98;

        /// <summary>缩影画片高</summary>
        public const int MiniH = 56;

        /// <summary>HUD 锚点（缩影中心）距屏幕左下的偏移</summary>
        public static readonly Vector2 AnchorOffset = new(80f, -66f);
        #endregion

        /// <summary>异相位呼吸波，0~1 缓慢脉动</summary>
        public static float Breath(float time, float seed, float speed = 2f)
            => MathF.Sin(time * speed + seed * 17.39f) * 0.5f + 0.5f;
    }
}
