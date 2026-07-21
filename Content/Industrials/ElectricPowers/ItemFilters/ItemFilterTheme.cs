using System;
using Terraria;
using Terraria.GameInput;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.ItemFilters
{
    /// <summary>编辑器色板与几何；锈色系，青=白名单、红=黑名单</summary>
    internal static class ItemFilterTheme
    {
        #region UI空间坐标（与调用语境无关）
        //UIHandle 的 Update/Draw 在 InterfaceScaleType.UI 层，须用这组换算
        //禁直接读 Main.screenWidth/Height，否则改 UI 缩放会漂
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
        public const int GridCols = 8;
        public const int CellSize = 46;
        public const int CellGap = 5;
        public const int VisibleRows = 5;
        public const int Padding = 16;
        public const int HeaderHeight = 58;
        public const int ButtonRowHeight = 34;
        public const int FooterHeight = 24;

        public const int GridWidth = GridCols * CellSize + (GridCols - 1) * CellGap;
        public const int GridHeight = VisibleRows * CellSize + (VisibleRows - 1) * CellGap;
        public const int PanelWidth = GridWidth + Padding * 2 + 10; //右侧留滚动条
        public const int PanelHeight = HeaderHeight + GridHeight + ButtonRowHeight + FooterHeight + Padding * 2;

        /// <summary>TP宿主最远距离(像素)</summary>
        public const float KeepDistance = 300f;
        #endregion

        public static Color ModeAccent(ItemFilterMode mode)
            => mode == ItemFilterMode.Whitelist ? AccentWhitelist : AccentBlacklist;

        /// <summary>异相位呼吸，返回0-1</summary>
        public static float Breath(float time, float seed, float speed = 2f)
            => MathF.Sin(time * speed + seed * 17.39f) * 0.5f + 0.5f;
    }
}
