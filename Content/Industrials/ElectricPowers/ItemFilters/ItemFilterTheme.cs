using System;
using Terraria;
using Terraria.GameInput;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.ItemFilters
{
    /// <summary>
    /// 过滤名单编辑器的色板与几何常量<br/>
    /// 废土工业锈色系，与收集器控制台(CollectorUI)同族；
    /// 青色为白名单强调、警示红为黑名单强调
    /// </summary>
    internal static class ItemFilterTheme
    {
        #region UI空间坐标（与调用语境无关）
        //UIHandle 的 Update/Draw 运行在 InterfaceScaleType.UI 层内，布局必须用这组换算，
        //禁止直接读 Main.screenWidth/Height，否则改变 UI 缩放时元素会漂移
        public static float UIScreenW => PlayerInput.RealScreenWidth / Main.UIScale;
        public static float UIScreenH => PlayerInput.RealScreenHeight / Main.UIScale;
        public static Vector2 UIMouse => new Vector2(PlayerInput.MouseX, PlayerInput.MouseY) / Main.UIScale;
        #endregion

        #region 色板（废土锈）
        //面板底色，由深到浅
        public static readonly Color Void = new(8, 6, 6);
        public static readonly Color PanelDark = new(14, 9, 8);
        public static readonly Color RustMid = new(24, 15, 11);
        public static readonly Color WarmEdge = new(38, 22, 15);
        //锈蚀描边，暗/亮两端做脉动插值
        public static readonly Color EdgeRust = new(130, 65, 35);
        public static readonly Color EdgeBright = new(200, 105, 55);
        //文字
        public static readonly Color TextWarm = new(230, 200, 170);
        public static readonly Color TextDim = new(150, 125, 105);
        public static readonly Color Label = new(200, 160, 130);
        //强调色
        public static readonly Color AccentWhitelist = new(120, 200, 255);
        public static readonly Color AccentBlacklist = new(255, 95, 70);
        public static readonly Color Gold = new(255, 200, 120);
        public static readonly Color Danger = new(220, 100, 70);
        #endregion

        #region 几何
        /// <summary>名单网格列数</summary>
        public const int GridCols = 8;
        /// <summary>格子边长</summary>
        public const int CellSize = 46;
        /// <summary>格子间距</summary>
        public const int CellGap = 5;
        /// <summary>一屏可见行数，超出滚动</summary>
        public const int VisibleRows = 5;
        /// <summary>面板内边距</summary>
        public const int Padding = 16;
        /// <summary>标题带高度(含模式芯片)</summary>
        public const int HeaderHeight = 58;
        /// <summary>底部按钮行高度</summary>
        public const int ButtonRowHeight = 34;
        /// <summary>底部提示行高度</summary>
        public const int FooterHeight = 24;

        public const int GridWidth = GridCols * CellSize + (GridCols - 1) * CellGap;
        public const int GridHeight = VisibleRows * CellSize + (VisibleRows - 1) * CellGap;
        public const int PanelWidth = GridWidth + Padding * 2 + 10; //右侧留滚动条
        public const int PanelHeight = HeaderHeight + GridHeight + ButtonRowHeight + FooterHeight + Padding * 2;

        /// <summary>编辑TP宿主时允许的最大距离(像素)，超出自动关闭</summary>
        public const float KeepDistance = 300f;
        #endregion

        /// <summary>当前模式的强调色</summary>
        public static Color ModeAccent(ItemFilterMode mode)
            => mode == ItemFilterMode.Whitelist ? AccentWhitelist : AccentBlacklist;

        /// <summary>异相位呼吸波，返回 0-1 的缓慢脉动</summary>
        public static float Breath(float time, float seed, float speed = 2f)
            => MathF.Sin(time * speed + seed * 17.39f) * 0.5f + 0.5f;
    }
}
