using System;
using Terraria;
using Terraria.GameInput;

namespace CalamityOverhaul.Content.UIs.MainMenuOverUIs
{
    /// <summary>致谢 ED 色板/版式；坐标走 UI 空间，勿直接读 Main.screen*</summary>
    internal static class AckTheme
    {
        #region UI空间坐标（与调用语境无关，禁止直接读 Main.screenWidth/Height）
        public static float UIScreenW => PlayerInput.RealScreenWidth / Main.UIScale;
        public static float UIScreenH => PlayerInput.RealScreenHeight / Main.UIScale;
        public static Vector2 UIScreenSize => new(UIScreenW, UIScreenH);
        #endregion

        #region 色板
        //近黑，深→浅
        public static readonly Color Void = new(4, 5, 8);
        public static readonly Color Base = new(9, 10, 14);
        public static readonly Color Panel = new(15, 17, 23);
        //暖琥珀，对齐 HalibutTheme.Accent
        public static readonly Color Accent = new(255, 200, 97);
        public static readonly Color AccentHi = new(255, 226, 170);
        //冷钢青副色
        public static readonly Color Cool = new(120, 196, 224);
        public static readonly Color Text = new(233, 236, 240);
        public static readonly Color TextDim = new(154, 160, 172);
        public static readonly Color TextFaint = new(92, 99, 114);
        #endregion

        #region 版式
        public const float SideMarginRatio = 0.135f;//左右安全边距占屏比
        public const float SectionGap = 66f;//分节标题上留白
        public const float HeaderHeight = 82f;//元信息+角色名+分割线
        public const float NameRowHeight = 32f;
        public const float DonorRowHeight = 28f;
        public const float DonorColWidth = 210f;
        public const float FadeBand = 150f;//入视野淡入带(UI px)
        #endregion

        public static Color RoleColor(CreditRole role) => role switch {
            CreditRole.Artist => new Color(255, 178, 128),
            CreditRole.CodeAssistance => new Color(122, 198, 226),
            CreditRole.Musician => new Color(189, 158, 255),
            CreditRole.BalanceTester => new Color(142, 216, 172),
            _ => Accent,
        };

        /// <summary>异相位呼吸 0-1</summary>
        public static float Breath(float time, float seed, float speed = 2f)
            => MathF.Sin(time * speed + seed * 17.39f) * 0.5f + 0.5f;

        public static float EaseOutCubic(float t) => 1f - MathF.Pow(1f - Saturate(t), 3f);

        public static float EaseInOutCubic(float t) {
            t = Saturate(t);
            return t < 0.5f ? 4f * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 3f) * 0.5f;
        }

        public static float EaseOutQuint(float t) => 1f - MathF.Pow(1f - Saturate(t), 5f);

        /// <summary>带回弹缓出，标志/标题入场</summary>
        public static float EaseOutBack(float t) {
            t = Saturate(t);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * MathF.Pow(t - 1f, 3f) + c1 * MathF.Pow(t - 1f, 2f);
        }

        public static float Saturate(float t) => t < 0f ? 0f : t > 1f ? 1f : t;
    }
}
