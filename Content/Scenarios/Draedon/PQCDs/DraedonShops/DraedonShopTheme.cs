using System;
using Terraria;
using Terraria.GameInput;

namespace CalamityOverhaul.Content.Scenarios.Draedon.PQCDs.DraedonShops
{
    /// <summary>
    /// 嘉登交换终端的色板与几何常量，配色与 DraedonPanelDraw 对话皮肤一致：近黑底 + 青蓝终端辉光，
    /// 仅以暖金强调可购买/选中、以警示红表达买不起/禁用，避免整屏冷色单调
    /// </summary>
    internal static class DraedonShopTheme
    {
        #region UI空间坐标（与调用语境无关）
        //UIHandle 的 Update/Draw 运行在 InterfaceScaleType.UI 层内，布局必须用下面这组换算，
        //禁止直接读 Main.screenWidth/Height，否则改变 UI 缩放时元素会漂移
        public static float UIScreenW => PlayerInput.RealScreenWidth / Main.UIScale;
        public static float UIScreenH => PlayerInput.RealScreenHeight / Main.UIScale;
        public static Vector2 UIScreenSize => new(UIScreenW, UIScreenH);
        public static Vector2 UIMouse => new Vector2(PlayerInput.MouseX, PlayerInput.MouseY) / Main.UIScale;
        #endregion

        #region 色板（终端青）
        //最深底色
        public static readonly Color Void = new(4, 8, 18);
        //面板底色（回退绘制用）
        public static readonly Color Deep = new(8, 16, 26);
        //描边主色
        public static readonly Color Edge = new(0, 175, 195);
        //高亮描边/扫光
        public static readonly Color EdgeBright = new(0, 220, 210);
        //暗部描边
        public static readonly Color EdgeDim = new(0, 110, 125);
        //辉光主色
        public static readonly Color Glow = new(0, 205, 200);
        //文字主色与次色
        public static readonly Color TextBright = new(220, 245, 255);
        public static readonly Color Text = new(165, 220, 235);
        public static readonly Color TextDim = new(70, 120, 135);
        //暖金，可购买/选中强调
        public static readonly Color Gold = new(255, 205, 120);
        //警示红，买不起/禁用
        public static readonly Color Danger = new(255, 95, 95);
        #endregion

        #region 几何
        public const int PanelWidth = 500;
        public const int PanelHeight = 640;
        //标题 + 分隔线 + 余额带
        public const int HeaderHeight = 92;
        //底部操作提示 + 翻页计数
        public const int FooterHeight = 50;
        //单条货品记录高度
        public const int RowHeight = 76;
        //面板左右内边距
        public const int SidePadding = 22;
        //货品图标取景框边长
        public const int IconBox = 56;
        //呼叫面板尺寸
        public const int CallPanelWidth = 264;
        public const int CallPanelHeight = 360;
        //两块面板之间的间隙
        public const int PanelGap = 18;
        #endregion

        /// <summary>异相位呼吸波，返回 0-1 的缓慢脉动</summary>
        public static float Breath(float time, float seed, float speed = 2f)
            => MathF.Sin(time * speed + seed * 17.39f) * 0.5f + 0.5f;
    }
}
