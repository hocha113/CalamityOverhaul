using System;
using Terraria;
using Terraria.GameInput;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaVaults
{
    /// <summary>
    /// 湖窗 UI 色板与几何常量。血系配色与 KikasaGrade.fx 血湖常量同源：
    /// 深绯暗底 + 血红流层 + 血沫暖光，避免整屏冷黑。
    /// </summary>
    internal static class KikasaVaultTheme
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

        //基底，由深到浅——带红壳的近黑，不是纯黑
        public static readonly Color Void = new(8, 2, 4);
        public static readonly Color Deep = new(18, 4, 7);
        public static readonly Color Mid = new(41, 10, 14);
        //血红主色（湖水流层）与血沫暖光（缝线/强调）
        public static readonly Color Blood = new(198, 66, 60);
        public static readonly Color Foam = new(246, 133, 112);
        //文字主次色，暖白与灰绯
        public static readonly Color Text = new(240, 224, 219);
        public static readonly Color TextDim = new(172, 130, 126);
        //面板 CPU 回退底色
        public static readonly Color PanelBg = new(14, 4, 6);

        #region 湖窗几何
        /// <summary>每行沉物数</summary>
        public const int SlotsPerRow = 8;
        /// <summary>可视行数，超出走滚轮</summary>
        public const int VisibleRows = 2;
        /// <summary>槽位间距（中心距）</summary>
        public const float SlotSpacingX = 74f;
        public const float SlotSpacingY = 78f;
        /// <summary>物品图适配盒边长</summary>
        public const float SlotFit = 44f;
        /// <summary>面板尺寸</summary>
        public const float PanelW = 680f;
        public const float PanelH = 356f;
        /// <summary>水面线在面板内的 uv 高度（开窗动画的终值）</summary>
        public const float WaterLineY = 0.30f;
        /// <summary>面板中心的屏高占比——放上一点，玩家脚边的湖面演出别被窗子挡住</summary>
        public const float PanelCenterYRatio = 0.40f;
        #endregion

        /// <summary>异相位呼吸波，0~1 缓慢脉动</summary>
        public static float Breath(float time, float seed, float speed = 2f)
            => MathF.Sin(time * speed + seed * 17.39f) * 0.5f + 0.5f;
    }
}
