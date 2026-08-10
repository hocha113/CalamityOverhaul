using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameInput;

namespace CalamityOverhaul.Content.NPCs.TBUGs.UIs
{
    /// <summary>TBUG 界面主题：黑墙终端——纯黑底、终端绿、报错品红、琥珀警示</summary>
    internal static class TBUGTheme
    {
        #region 尺寸与缩放

        public const float FontScale = 1.2f;

        //shader 内框边距
        public const float ShaderEdgePad = 4f;

        /// <summary>UI 空间屏宽；UIHandle 的 Update/Draw 跑在 UIScale 空间，别直接读 Main.screenWidth</summary>
        public static float UIScreenW => PlayerInput.RealScreenWidth / Main.UIScale;
        /// <summary>UI 空间屏高</summary>
        public static float UIScreenH => PlayerInput.RealScreenHeight / Main.UIScale;

        #endregion

        #region 配色方案

        //黑墙底色：近纯黑带一丝绿相
        public static readonly Color BgDark = new(2, 6, 3);
        public static readonly Color BgPanel = new(4, 10, 6);
        public static readonly Color Border = new(20, 62, 32);

        //终端绿主强调
        public static readonly Color Accent = new(54, 255, 108);
        //暗绿次级
        public static readonly Color AccentDim = new(18, 128, 56);
        //报错品红
        public static readonly Color AccentErr = new(255, 32, 110);
        //琥珀警示/价格
        public static readonly Color AccentAmber = new(255, 196, 64);

        //暗淡文字
        public static readonly Color TextDim = new(58, 102, 72);
        //普通文字
        public static readonly Color TextNormal = new(122, 192, 142);
        //明亮文字
        public static readonly Color TextBright = new(198, 255, 214);

        //网格线
        public static readonly Color GridLine = new(8, 24, 12);

        //深度层次
        public static readonly Color SectionBg = new(3, 12, 6);
        public static readonly Color RowBg = new(8, 22, 12);
        public static readonly Color EdgeGlow = new(60, 255, 120);

        #endregion

        #region 绘制工具

        public static void DrawLine(SpriteBatch sb, Texture2D px, Vector2 start, Vector2 end, float thickness, Color color) {
            Vector2 diff = end - start;
            float length = diff.Length();
            if (length < 1f) return;
            sb.Draw(px, start, new Rectangle(0, 0, 1, 1), color, diff.ToRotation(),
                Vector2.Zero, new Vector2(length, thickness), SpriteEffects.None, 0f);
        }

        #endregion
    }
}
