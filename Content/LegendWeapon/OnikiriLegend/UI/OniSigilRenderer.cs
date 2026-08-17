using CalamityOverhaul.Content.UIs.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>
    /// 结印盘绘制：漆底盘座 + 六芒外环 + 内三角结印位 + 组合墨线 + 合鬼心。<br/>
    /// 全 CPU 笔触，无 shader——盘的形制先定下来再谈上不上 fx；
    /// 暗部一律走贴身投影，禁同心放大伪造羽化
    /// </summary>
    internal static class OniSigilRenderer
    {
        private static Texture2D Pixel => VaultAsset.placeholder2.Value;
        private static readonly Rectangle PixelSrc = new(0, 0, 1, 1);

        /// <summary>盘座外圈的一段蚀刻缺口，让圆环不是标准圆</summary>
        private const string RingNickD = "M -0.42 -0.06 C -0.2 -0.16 0.2 -0.16 0.42 -0.06";

        //====================== 盘座 ======================

        /// <summary>漆底盘座：贴身投影 + 双层漆环 + 金压线 + 蚀刻缺口</summary>
        public static void DrawBoard(SpriteBatch sb, in OniSigilWheel wheel, float alpha, float time) {
            if (alpha <= 0.01f) {
                return;
            }
            Vector2 c = wheel.Center;
            float r = wheel.Radius;

            //贴身投影：偏移不放大（放大同心=方块黑层）
            DrawRing(sb, c + new Vector2(2.5f, 3.5f), r * 1.02f, 9f,
                new Color(8, 2, 5) * (alpha * 0.5f), 96);
            //漆环本体：外沉内亮，卖圆柱漆面
            DrawRing(sb, c, r * 1.02f, 10f, Color.Lerp(OnikiriUITheme.Ink, Color.Black, 0.35f) * (alpha * 0.97f), 96);
            DrawRing(sb, c, r, 4.5f, Color.Lerp(OnikiriUITheme.Ink, OnikiriUITheme.Dark, 0.6f) * (alpha * 0.97f), 96);
            //下缘金压线 + 一线绯内衬（与顶梁同语）
            DrawRing(sb, c, r * 0.985f, 1.2f, OnikiriUITheme.GoldDeep * (alpha * 0.5f), 96);
            DrawRing(sb, c, r * 0.968f, 1f, OnikiriUITheme.Deep * (alpha * 0.34f), 96);

            //漆理：几道随机长度的淡纹，绕环走向
            for (int i = 0; i < 10; i++) {
                float u = OniBrush.Hash01(i * 47 + 13);
                float ang = u * MathHelper.TwoPi;
                float span = 0.10f + OniBrush.Hash01(i * 71 + 5) * 0.22f;
                DrawArc(sb, c, r * (0.99f + OniBrush.Hash01(i * 29 + 3) * 0.02f), 1f,
                    Color.Black * (alpha * 0.20f), ang, ang + span, 12);
            }

            //蚀刻缺口：手工件不该是标准圆
            SvgPath nick = SvgPathPen.Path(RingNickD);
            if (nick != null) {
                SvgPathPen.Stroke(sb, nick, c - new Vector2(0f, r * 0.99f), r * 0.34f, 0f,
                    Color.Lerp(OnikiriUITheme.Ink, Color.Black, 0.5f), 6f, alpha * 0.8f);
            }

            //盘面呼吸背光：极缓，静物不死
            float breath = 0.5f + 0.5f * MathF.Sin(time * 0.6f);
            OniBrush.DrawBacklight(sb, c, r * 0.9f, OnikiriUITheme.Deep,
                alpha * (0.05f + breath * 0.03f));
        }

        /// <summary>六芒星：两枚交叠正三角的墨线骨架</summary>
        public static void DrawHexagram(SpriteBatch sb, in OniSigilWheel wheel, float alpha, float time) {
            if (alpha <= 0.01f) {
                return;
            }
            //六个尖端连成两枚三角（0-2-4 与 1-3-5）
            for (int t = 0; t < 2; t++) {
                float a = alpha * (t == 0 ? 0.72f : 0.56f);
                for (int i = 0; i < 3; i++) {
                    Vector2 p0 = wheel.StarPos(t + i * 2);
                    Vector2 p1 = wheel.StarPos((t + (i + 1) * 2) % OniSigilWheel.NodeCount);
                    //起笔重收笔轻，避免六条等重直线读成矢量图
                    OniBrush.DrawGradientLine(sb, p0, p1,
                        OnikiriUITheme.Deep * a, OnikiriUITheme.Dark * (a * 0.4f), 1.6f);
                }
            }
            //尖端朱点，随位相位错开呼吸
            for (int i = 0; i < OniSigilWheel.NodeCount; i++) {
                float breath = OnikiriUITheme.Breath(time, i * 1.7f, 1.1f);
                OniBrush.DrawSoftDot(sb, wheel.StarPos(i), 2.4f + breath * 0.8f,
                    OnikiriUITheme.Seal, alpha * (0.35f + breath * 0.2f));
            }
        }

        //====================== 外环鬼位 ======================

        /// <summary>
        /// 一枚外环鬼位：役鬼印 + 名讳 + 复苏读数 + 将醒洇血。<br/>
        /// slot &gt;= 0 表示它正结印在盘上，印外加一圈朱环
        /// </summary>
        public static void DrawNode(SpriteBatch sb, DynamicSpriteFont font, in OniSigilWheel wheel,
            int index, OniGhostEntry entry, int slot, bool selected, float hover,
            float alpha, float time) {
            if (alpha <= 0.01f || entry == null) {
                return;
            }
            Vector2 p = wheel.NodePos(index);
            float size = wheel.NodeHit * 0.78f;
            bool onBoard = slot >= 0;
            float lift = 1f + hover * 0.1f + (selected ? 0.06f : 0f);

            //选中底衬：一记斜刀痕，不画方框
            if (selected) {
                OniBrush.DrawTaperedSlash(sb,
                    p + new Vector2(-size * 1.5f, size * 0.95f),
                    p + new Vector2(size * 1.5f, size * 0.75f),
                    2.4f, 2.2f, alpha * 0.85f);
            }
            //在盘上的鬼：印外一圈朱环 + 暖底
            if (onBoard) {
                OniBrush.DrawBacklight(sb, p, size * 2.1f, OnikiriUITheme.Deep, alpha * 0.22f);
                DrawRing(sb, p, size * 1.42f, 1.5f, OnikiriUITheme.Seal * (alpha * 0.85f), 40);
            }
            //将醒：印下垂一笔血墨
            if (entry.InDanger) {
                float bleed = 0.5f + 0.5f * MathF.Sin(time * 2.4f + index);
                OniBrush.DrawGradientLine(sb, p + new Vector2(0f, size * 0.9f),
                    p + new Vector2(-1.4f, size * (1.9f + bleed * 0.5f)),
                    OnikiriUITheme.Bright * (alpha * 0.7f), OnikiriUITheme.Deep * 0f, 1.8f);
            }

            //役鬼印：不在盘上的印画得残一些（integrity）
            float integrity = onBoard ? 1f : 0.72f + hover * 0.2f;
            OniBrush.DrawSealGlyph(sb, p, size * lift,
                alpha * (onBoard ? 1f : 0.7f + hover * 0.25f), 0f, integrity);

            //名讳横书在印下；读数再下一行
            string name = entry.Name?.Invoke() ?? string.Empty;
            Color nameCol = onBoard ? OnikiriUITheme.Paper
                : Color.Lerp(OnikiriUITheme.TextDim, OnikiriUITheme.HotWhite, hover);
            DrawCentered(sb, font, name, p + new Vector2(0f, size * 1.55f),
                nameCol * (alpha * (0.82f + hover * 0.18f)), 0.62f);

            string read = $"{(int)MathF.Round(entry.Revival * 100f)}%";
            Color readCol = entry.InDanger ? OnikiriUITheme.Bright : OnikiriUITheme.TextDim;
            DrawCentered(sb, font, read, p + new Vector2(0f, size * 1.55f + 15f),
                readCol * (alpha * 0.8f), 0.56f);
        }

        //====================== 内三角结印位 ======================

        /// <summary>三角三边：两条边端都占了才点亮，成立的组合写名在边中</summary>
        public static void DrawEdges(SpriteBatch sb, DynamicSpriteFont font, in OniSigilWheel wheel,
            float alpha, float time, Func<int, string> comboName) {
            if (alpha <= 0.01f) {
                return;
            }
            for (int e = 0; e < 3; e++) {
                (int a, int b) = OniSigilWheel.EdgeSlots(e);
                Vector2 p0 = wheel.SlotPos(a);
                Vector2 p1 = wheel.SlotPos(b);
                string label = comboName?.Invoke(e);
                bool live = !string.IsNullOrEmpty(label);

                if (live) {
                    //成立的组合：墨线通着，一段亮笔沿边巡行
                    OniBrush.DrawGradientLine(sb, p0, p1,
                        OnikiriUITheme.Bright * (alpha * 0.75f),
                        OnikiriUITheme.Deep * (alpha * 0.75f), 2.2f);
                    float run = (time * 0.22f + e * 0.33f) % 1f;
                    OniBrush.DrawSoftDot(sb, Vector2.Lerp(p0, p1, run), 3.4f,
                        OnikiriUITheme.HotWhite, alpha * 0.5f);
                    DrawCentered(sb, font, label, Vector2.Lerp(p0, p1, 0.5f) + new Vector2(0f, -9f),
                        OnikiriUITheme.Bright * (alpha * 0.9f), 0.58f);
                }
                else {
                    //未成立：只留一道断续的干笔，读得出"这里本该通"
                    OniBrush.DrawGradientLine(sb, p0, p1,
                        OnikiriUITheme.Dark * (alpha * 0.5f),
                        OnikiriUITheme.Dark * (alpha * 0.18f), 1.1f);
                }
            }
        }

        /// <summary>一个结印位：占了画朱印，空着画凿槽</summary>
        public static void DrawSlot(SpriteBatch sb, DynamicSpriteFont font, in OniSigilWheel wheel,
            int slot, OniGhostEntry entry, float hover, bool pending, float alpha, float time) {
            if (alpha <= 0.01f) {
                return;
            }
            Vector2 p = wheel.SlotPos(slot);
            float size = wheel.SlotHit * 0.72f;
            float lift = 1f + hover * 0.12f;

            //槽底凿痕：一圈内暗上缘 + 受光下唇，取代描边矩形
            DrawRing(sb, p, size * 1.25f, 1.4f, Color.Black * (alpha * 0.55f), 32);
            DrawRing(sb, p + new Vector2(0f, 1.2f), size * 1.25f, 1f,
                OnikiriUITheme.Paper * (alpha * 0.10f), 32);

            if (entry == null) {
                //空槽：只有凿槽与一点余烬，绝不是灰方块
                OniBrush.DrawSoftDot(sb, p, size * 0.5f, OnikiriUITheme.Dark, alpha * 0.5f);
                if (hover > 0.03f) {
                    DrawRing(sb, p, size * (1.25f + hover * 0.15f), 1.2f,
                        OnikiriUITheme.Seal * (alpha * hover * 0.8f), 32);
                }
                return;
            }

            OniBrush.DrawBacklight(sb, p, size * 2.4f, OnikiriUITheme.Deep,
                alpha * (0.24f + hover * 0.12f));
            OniBrush.DrawSealGlyph(sb, p, size * lift, alpha * (pending ? 0.45f : 1f));
            //候令期：印上压一道慢转的干笔，读得出"在等回执"
            if (pending) {
                DrawArc(sb, p, size * 1.5f, 1.6f, OnikiriUITheme.TextDim * (alpha * 0.7f),
                    time * 2.2f, time * 2.2f + 1.6f, 14);
            }

            string name = entry.Name?.Invoke() ?? string.Empty;
            DrawCentered(sb, font, name, p + new Vector2(0f, size * 1.7f),
                OnikiriUITheme.Paper * (alpha * 0.9f), 0.58f);
        }

        /// <summary>三角中心：三槽齐了才是合鬼印，否则是一枚空座</summary>
        public static void DrawCore(SpriteBatch sb, DynamicSpriteFont font, in OniSigilWheel wheel,
            bool complete, string label, float alpha, float time) {
            if (alpha <= 0.01f) {
                return;
            }
            Vector2 c = wheel.Center;
            float size = wheel.SlotHit * 0.5f;

            if (!complete) {
                DrawRing(sb, c, size, 1.1f, OnikiriUITheme.Dark * (alpha * 0.6f), 28);
                return;
            }
            float breath = 0.5f + 0.5f * MathF.Sin(time * 1.8f);
            OniBrush.DrawBacklight(sb, c, size * 4.2f, OnikiriUITheme.Bright,
                alpha * (0.16f + breath * 0.1f));
            //三印崩的座：三枚小印围心，外一圈朱环
            for (int i = 0; i < 3; i++) {
                float ang = OniSigilWheel.SlotAngle(i) + time * 0.25f;
                OniBrush.DrawSoftDot(sb, c + ang.ToRotationVector2() * (size * 0.9f), 2.6f,
                    OnikiriUITheme.Seal, alpha * 0.8f);
            }
            DrawRing(sb, c, size * 1.5f, 1.6f,
                OnikiriUITheme.Bright * (alpha * (0.6f + breath * 0.25f)), 32);
            OniBrush.DrawSealGlyph(sb, c, size * 1.1f, alpha, time * 0.12f);

            if (!string.IsNullOrEmpty(label)) {
                DrawCentered(sb, font, label, c + new Vector2(0f, size * 2.4f),
                    OnikiriUITheme.HotWhite * (alpha * 0.9f), 0.6f);
            }
        }

        //====================== 卷槽（去点鬼簿的门） ======================

        /// <summary>卷轴端面的纸涡（自己的 [-1,1] 小空间）</summary>
        private const string NicheSpiralD =
            "M 0.6 0.05 C 0.6 -0.52 -0.58 -0.52 -0.58 0.05 C -0.58 0.46 0.28 0.46 0.28 0.08";

        /// <summary>
        /// 盘座下缘凿出的卷槽，点鬼簿插在里面。<br/>
        /// 悬停语言是「抽卷」——卷自槽里升起一截、绳札松一分，不是图标变亮
        /// </summary>
        public static void DrawScrollNiche(SpriteBatch sb, DynamicSpriteFont font, Rectangle rect,
            float hover, float alpha, float time, string label) {
            if (alpha <= 0.01f) {
                return;
            }
            Texture2D pixel = Pixel;
            if (pixel == null) {
                return;
            }
            Vector2 half = new(0.5f);
            float a = alpha * (0.9f + hover * 0.1f);
            //抽书行程：卷升起一截，槽口漏出的暖光跟着涨
            float pull = hover * OnikiriUITheme.CodexBookPull;

            //槽体：凿进盘座的暗腔，上缘一道内暗、下缘一线受光唇
            Vector2 slotC = new(rect.Center.X, rect.Center.Y + 6f);
            Vector2 slotSize = new(rect.Width - 8f, rect.Height - 14f);
            sb.Draw(pixel, slotC, PixelSrc, Color.Black * (a * 0.82f), 0f, half,
                slotSize, SpriteEffects.None, 0f);
            sb.Draw(pixel, slotC - new Vector2(0f, slotSize.Y * 0.5f), PixelSrc,
                Color.Black * (a * 0.6f), 0f, half, new Vector2(slotSize.X, 2f),
                SpriteEffects.None, 0f);
            sb.Draw(pixel, slotC + new Vector2(0f, slotSize.Y * 0.5f), PixelSrc,
                OnikiriUITheme.Paper * (a * 0.10f), 0f, half, new Vector2(slotSize.X, 1f),
                SpriteEffects.None, 0f);

            //卷身：一截和纸圆筒，随抽书上移；两端天地轴各一枚朱帽
            Vector2 rollC = new(rect.Center.X, rect.Y + 20f - pull);
            float rollW = rect.Width - 26f;
            sb.Draw(pixel, rollC + new Vector2(1.2f, 1.8f), PixelSrc,
                new Color(8, 2, 5) * (a * 0.5f), 0f, half, new Vector2(rollW, 22f),
                SpriteEffects.None, 0f);
            sb.Draw(pixel, rollC, PixelSrc, OnikiriUITheme.Paper * (a * 0.86f), 0f, half,
                new Vector2(rollW, 22f), SpriteEffects.None, 0f);
            //纸层三线，读得出这是卷起来的
            for (int i = -1; i <= 1; i++) {
                sb.Draw(pixel, rollC + new Vector2(0f, i * 6f), PixelSrc,
                    OnikiriUITheme.TextDim * (a * 0.28f), 0f, half, new Vector2(rollW - 6f, 1f),
                    SpriteEffects.None, 0f);
            }
            foreach (float capX in new[] { -rollW * 0.5f, rollW * 0.5f }) {
                sb.Draw(pixel, rollC + new Vector2(capX, 0f), PixelSrc,
                    OnikiriUITheme.Deep * (a * 0.95f), 0f, half, new Vector2(6f, 26f),
                    SpriteEffects.None, 0f);
            }
            //端面纸涡：只在抽出来一截时才看得见
            if (hover > 0.05f) {
                SvgPath spiral = SvgPathPen.Path(NicheSpiralD);
                if (spiral != null) {
                    SvgPathPen.Stroke(sb, spiral, rollC + new Vector2(rollW * 0.5f, 0f), 11f, 0f,
                        OnikiriUITheme.TextDim, 1.2f, a * hover * 0.8f);
                }
            }

            //束带一匝 + 垂下的绳札，抽书时松一分
            sb.Draw(pixel, rollC, PixelSrc, OnikiriUITheme.Deep * (a * 0.9f), 0f, half,
                new Vector2(rollW * 0.34f, 24f), SpriteEffects.None, 0f);
            float sway = MathF.Sin(time * 1.1f) * (0.06f + hover * 0.06f);
            OniBrush.DrawGradientLine(sb, rollC + new Vector2(0f, 12f),
                rollC + new Vector2(sway * 16f, 12f + 14f + hover * 4f),
                OnikiriUITheme.Deep * (a * 0.85f), OnikiriUITheme.Dark * (a * 0.2f), 1.4f);

            //槽口暖光：抽出来一截时槽里漏出灯色
            if (hover > 0.03f) {
                OniBrush.DrawBacklight(sb, slotC - new Vector2(0f, 4f), rect.Width * 0.55f,
                    OnikiriUITheme.CandleWarm, a * hover * 0.16f);
            }

            //槽下荷札：卷的名字
            if (!string.IsNullOrEmpty(label)) {
                const float Scale = 0.56f;
                Vector2 size = font.MeasureString(label) * Scale;
                Utils.DrawBorderString(sb, label,
                    new Vector2(rect.Center.X - size.X * 0.5f, rect.Bottom + 2f),
                    Color.Lerp(OnikiriUITheme.TextDim, OnikiriUITheme.HotWhite, hover)
                        * (a * (0.7f + hover * 0.3f)), Scale);
            }
        }

        //====================== 基元 ======================

        /// <summary>折线圆环：1px 笔按段铺，避免同心 quad</summary>
        public static void DrawRing(SpriteBatch sb, Vector2 center, float radius,
            float thickness, Color color, int segments) {
            DrawArc(sb, center, radius, thickness, color, 0f, MathHelper.TwoPi, segments);
        }

        public static void DrawArc(SpriteBatch sb, Vector2 center, float radius,
            float thickness, Color color, float from, float to, int segments) {
            if (color.A == 0 && color == Color.Transparent || segments < 2 || radius <= 0.5f) {
                return;
            }
            Texture2D pixel = Pixel;
            if (pixel == null) {
                return;
            }
            float step = (to - from) / segments;
            Vector2 prev = center + from.ToRotationVector2() * radius;
            for (int i = 1; i <= segments; i++) {
                Vector2 next = center + (from + step * i).ToRotationVector2() * radius;
                Vector2 seg = next - prev;
                float len = seg.Length();
                if (len > 0.01f) {
                    sb.Draw(pixel, prev, PixelSrc, color, seg.ToRotation(),
                        new Vector2(0f, 0.5f), new Vector2(len + 0.6f, thickness),
                        SpriteEffects.None, 0f);
                }
                prev = next;
            }
        }

        private static void DrawCentered(SpriteBatch sb, DynamicSpriteFont font, string text,
            Vector2 center, Color color, float scale) {
            if (string.IsNullOrEmpty(text)) {
                return;
            }
            Vector2 size = font.MeasureString(text) * scale;
            Utils.DrawBorderString(sb, text, center - size * 0.5f, color, scale);
        }
    }
}
