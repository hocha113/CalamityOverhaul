using CalamityOverhaul.Content.GameModes.UI;
using System;
using Terraria;
using Terraria.GameInput;

namespace CalamityOverhaul.Content.GameModes.Blessings.UI
{
    /// <summary>
    /// 祝福界面的色板与布局唯一来源。色板与游戏模式表现脸同源：
    /// 修罗态紫红描金，死神永生态苍银冷白，shader 与 CPU 前景一族取色。
    /// 跨语境布局一律走 UI 空间访问器，禁止直读 <see cref="Main.screenWidth"/>
    /// </summary>
    internal static class BlessingTheme
    {
        /// <summary>UI 空间下的屏幕宽</summary>
        public static float UIScreenW => PlayerInput.RealScreenWidth / Main.UIScale;

        /// <summary>UI 空间下的屏幕高</summary>
        public static float UIScreenH => PlayerInput.RealScreenHeight / Main.UIScale;

        /// <summary>当前表现脸（修罗/死神永生随世界自动切换）</summary>
        public static GameModeFace Face => GameModeSystem.FaceOf(GameModeKind.Asura);

        /// <summary>主 accent</summary>
        public static Color Accent => GameModeTheme.Accent(Face);

        /// <summary>余烬亮色</summary>
        public static Color Ember => GameModeTheme.Ember(Face);

        /// <summary>近黑底色</summary>
        public static Color NightBase => GameModeTheme.NightBase;

        /// <summary>休眠骨灰色（石珠/未解锁）</summary>
        public static Color BoneDim => GameModeTheme.BoneDim;

        //——往生轮布局——

        /// <summary>轮心（UI 空间）</summary>
        public static Vector2 WheelCenter => new(UIScreenW * 0.5f, UIScreenH * 0.52f);

        /// <summary>主环半径</summary>
        public static float WheelRadius
            => Math.Clamp(Math.Min(UIScreenW, UIScreenH) * 0.32f, 190f, 320f);

        /// <summary>珠位半径（px）</summary>
        public const float BeadRadius = 26f;

        /// <summary>珠位符纹描边半尺寸</summary>
        public const float BeadSigilScale = 15f;

        /// <summary>中心详情盘半径</summary>
        public static float CenterRadius => WheelRadius * 0.52f;

        /// <summary>点燃/熄灭按钮矩形</summary>
        public static Rectangle KindleButton {
            get {
                Vector2 c = WheelCenter;
                float r = WheelRadius;
                return new Rectangle((int)(c.X - 76f), (int)(c.Y + r * 0.30f - 21f), 152, 42);
            }
        }

        //——引魂灯 HUD 布局——

        /// <summary>引魂灯占位尺寸</summary>
        public static readonly Point LanternSize = new(52, 74);

        /// <summary>堆叠自然锚点（灯座底缘）</summary>
        public static Vector2 LanternAnchor => new(30f, UIScreenH - 32f);

        /// <summary>由堆叠避让后的锚点折算灯身矩形</summary>
        public static Rectangle LanternRect(Vector2 anchor)
            => new((int)anchor.X, (int)(anchor.Y - LanternSize.Y), LanternSize.X, LanternSize.Y);
    }
}
