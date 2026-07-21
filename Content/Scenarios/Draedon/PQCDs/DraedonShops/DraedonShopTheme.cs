using System;
using Terraria;
using Terraria.GameInput;

namespace CalamityOverhaul.Content.Scenarios.Draedon.PQCDs.DraedonShops
{
    /// <summary>交换终端色板与布局常量</summary>
    internal static class DraedonShopTheme
    {
        #region UI空间坐标（与调用语境无关）
        //UI层布局用UIScreenW/H,勿读screenWidth
        public static float UIScreenW => PlayerInput.RealScreenWidth / Main.UIScale;
        public static float UIScreenH => PlayerInput.RealScreenHeight / Main.UIScale;
        public static Vector2 UIScreenSize => new(UIScreenW, UIScreenH);
        public static Vector2 UIMouse => new Vector2(PlayerInput.MouseX, PlayerInput.MouseY) / Main.UIScale;
        #endregion

        #region 色板（终端青）
        public static readonly Color Void = new(4, 8, 18);
        public static readonly Color Deep = new(8, 16, 26);
        public static readonly Color Edge = new(0, 175, 195);
        public static readonly Color EdgeBright = new(0, 220, 210);
        public static readonly Color EdgeDim = new(0, 110, 125);
        public static readonly Color Glow = new(0, 205, 200);
        public static readonly Color TextBright = new(220, 245, 255);
        public static readonly Color Text = new(165, 220, 235);
        public static readonly Color TextDim = new(70, 120, 135);
        public static readonly Color Gold = new(255, 205, 120); //可购买强调
        public static readonly Color Danger = new(255, 95, 95); //买不起/禁用
        #endregion

        #region 几何
        public const int PanelWidth = 500;
        public const int PanelHeight = 640;
        public const int HeaderHeight = 92; //标题+分隔+余额
        public const int FooterHeight = 50;
        public const int RowHeight = 76;
        public const int SidePadding = 22;
        public const int IconBox = 56;
        public const int CallPanelWidth = 264;
        public const int CallPanelHeight = 360;
        public const int PanelGap = 18;
        #endregion

        /// <summary>异相位呼吸波,0-1</summary>
        public static float Breath(float time, float seed, float speed = 2f)
            => MathF.Sin(time * speed + seed * 17.39f) * 0.5f + 0.5f;
    }
}
