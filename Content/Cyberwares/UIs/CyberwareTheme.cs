using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Cyberwares.UIs
{
    /// <summary>
    ///赛博义体界面的主题配色和通用绘制工具集
    /// </summary>
    internal static class CyberwareTheme
    {
        #region 面板尺寸

        public const float PanelWidth = 800f;
        public const float PanelHeight = 540f;
        public const float SlotSize = 46f;
        public const float SlotPadding = 5f;

        //字号缩放系数，与 SHPCModPanel.FontScale 对齐，统一系列 UI 字号节奏
        public const float FontScale = 1.2f;

        //shader 内框边距，与 ScissorTest 的 margin 对齐，便于 CPU 装饰与着色器内边线对齐
        public const float ShaderEdgePad = 4f;

        //主面板中央人体能量光场半径（像素），随关闭动画 alpha 衰减归零
        public const float BodyHaloRadius = 120f;

        #endregion

        #region 配色方案

        //深色背景
        public static readonly Color BgDark = new(8, 8, 12);
        public static readonly Color BgPanel = new(14, 14, 20);
        public static readonly Color Border = new(45, 45, 55);

        //赛博红主强调色
        public static readonly Color Accent = new(255, 42, 42);
        //赛博金副强调色
        public static readonly Color AccentGold = new(220, 170, 40);
        //青色信息色
        public static readonly Color AccentCyan = new(0, 220, 220);

        //暗淡文字
        public static readonly Color TextDim = new(90, 90, 100);
        //普通文字
        public static readonly Color TextNormal = new(160, 160, 175);
        //明亮文字
        public static readonly Color TextBright = new(225, 225, 235);

        //网格线
        public static readonly Color GridLine = new(25, 25, 35);

        //深度层次
        public static readonly Color InnerShadow = new(4, 4, 8);
        public static readonly Color SectionBg = new(10, 10, 16);
        public static readonly Color SlotInnerBg = new(18, 18, 26);
        public static readonly Color EdgeGlow = new(255, 65, 65);

        //人体轮廓
        public static readonly Color BodyOutline = new(255, 50, 50);
        //人体填充
        public static readonly Color BodyFill = new(30, 12, 12);
        //人体内部线条
        public static readonly Color BodyInner = new(60, 20, 20);

        //空槽位背景
        public static readonly Color SlotEmpty = new(25, 25, 32);
        //槽位边框
        public static readonly Color SlotBorder = new(55, 55, 65);
        //连接线
        public static readonly Color Connector = new(60, 20, 20);

        #endregion

        #region 绘制工具

        /// <summary>
        ///绘制一条指定粗细的直线
        /// </summary>
        public static void DrawLine(SpriteBatch sb, Texture2D px, Vector2 start, Vector2 end, float thickness, Color color) {
            Vector2 diff = end - start;
            float length = diff.Length();
            if (length < 1f) return;
            sb.Draw(px, start, new Rectangle(0, 0, 1, 1), color, diff.ToRotation(),
                Vector2.Zero, new Vector2(length, thickness), SpriteEffects.None, 0f);
        }

        /// <summary>
        ///在像素网格坐标上填充一个矩形区域
        /// </summary>
        public static void FillGridRect(SpriteBatch sb, Texture2D px, Vector2 offset, float scale,
            int gx, int gy, int gw, int gh, Color color, float breathe = 0f) {
            Rectangle rect = new(
                (int)(offset.X + gx * scale),
                (int)(offset.Y + gy * scale + breathe),
                (int)(gw * scale),
                (int)(gh * scale)
            );
            sb.Draw(px, rect, new Rectangle(0, 0, 1, 1), color);
        }

        /// <summary>
        ///计算折线路径上指定比例位置的世界坐标
        /// </summary>
        public static Vector2 EvaluatePolyline(float t, Vector2 a, Vector2 b, Vector2 c, Vector2 d) {
            float dAB = Vector2.Distance(a, b);
            float dBC = Vector2.Distance(b, c);
            float dCD = Vector2.Distance(c, d);
            float total = dAB + dBC + dCD;
            if (total < 1f) return a;
            float dist = t * total;

            if (dist <= dAB) return Vector2.Lerp(a, b, dist / dAB);
            dist -= dAB;
            if (dist <= dBC) return Vector2.Lerp(b, c, dist / dBC);
            dist -= dBC;
            return Vector2.Lerp(c, d, Math.Clamp(dist / dCD, 0, 1));
        }

        #endregion
    }
}
