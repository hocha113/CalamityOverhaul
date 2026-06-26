using System;
using Terraria;
using Terraria.GameInput;

namespace CalamityOverhaul.Content.UIs.MainMenuOverUIs
{
    /// <summary>
    /// ED 致谢界面的色板、版式常量与 UI 空间坐标换算。
    /// 风格参考明日方舟片尾：近黑底 + 单一暖琥珀强调 + 冷钢青副色 + 米白文字，留白克制
    /// </summary>
    internal static class AckTheme
    {
        #region UI空间坐标（与调用语境无关，禁止直接读 Main.screenWidth/Height）
        public static float UIScreenW => PlayerInput.RealScreenWidth / Main.UIScale;
        public static float UIScreenH => PlayerInput.RealScreenHeight / Main.UIScale;
        public static Vector2 UIScreenSize => new(UIScreenW, UIScreenH);
        #endregion

        #region 色板
        //近黑基底，由深到浅
        public static readonly Color Void = new(4, 5, 8);
        public static readonly Color Base = new(9, 10, 14);
        public static readonly Color Panel = new(15, 17, 23);
        //暖琥珀强调（与 HalibutTheme.Accent 对齐，维持模组识别度）
        public static readonly Color Accent = new(255, 200, 97);
        public static readonly Color AccentHi = new(255, 226, 170);
        //冷钢青副色，避免整屏暖调单一
        public static readonly Color Cool = new(120, 196, 224);
        //文字主色与次色
        public static readonly Color Text = new(233, 236, 240);
        public static readonly Color TextDim = new(154, 160, 172);
        public static readonly Color TextFaint = new(92, 99, 114);
        #endregion

        #region 版式
        //内容列左右安全边距占屏比
        public const float SideMarginRatio = 0.135f;
        //分节标题之上的额外留白
        public const float SectionGap = 70f;
        //分节标题占高
        public const float HeaderHeight = 60f;
        //单列名字行高
        public const float NameRowHeight = 32f;
        //捐赠者网格行高与列宽
        public const float DonorRowHeight = 28f;
        public const float DonorColWidth = 210f;
        //名字进入视野时的淡入带高度（UI空间像素）
        public const float FadeBand = 150f;
        #endregion

        /// <summary>
        /// 给定角色返回其强调色
        /// </summary>
        public static Color RoleColor(CreditRole role) => role switch {
            CreditRole.Artist => new Color(255, 178, 128),
            CreditRole.CodeAssistance => new Color(122, 198, 226),
            CreditRole.Musician => new Color(189, 158, 255),
            CreditRole.BalanceTester => new Color(142, 216, 172),
            _ => Accent,
        };

        /// <summary>异相位呼吸波，给定相位种子返回 0-1 的缓慢脉动</summary>
        public static float Breath(float time, float seed, float speed = 2f)
            => MathF.Sin(time * speed + seed * 17.39f) * 0.5f + 0.5f;

        public static float EaseOutCubic(float t) => 1f - MathF.Pow(1f - Saturate(t), 3f);

        public static float EaseInOutCubic(float t) {
            t = Saturate(t);
            return t < 0.5f ? 4f * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 3f) * 0.5f;
        }

        public static float EaseOutQuint(float t) => 1f - MathF.Pow(1f - Saturate(t), 5f);

        /// <summary>带轻微回弹的缓出，用于标志/标题入场</summary>
        public static float EaseOutBack(float t) {
            t = Saturate(t);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * MathF.Pow(t - 1f, 3f) + c1 * MathF.Pow(t - 1f, 2f);
        }

        public static float Saturate(float t) => t < 0f ? 0f : t > 1f ? 1f : t;
    }
}
