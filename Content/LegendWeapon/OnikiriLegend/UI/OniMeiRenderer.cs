using CalamityOverhaul.Content.UIs.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>改铭台静态绘制:烛光/台账主板/铭位牌与注记引线/陈列刀叙饰/鏨盘扇/烙印木牌/大字/静物/工具</summary>
    internal static class OniMeiRenderer
    {
        private static Texture2D Pixel => VaultAsset.placeholder2.Value;
        private static readonly Rectangle PixelSrc = new(0, 0, 1, 1);

        private static float Hash01(int n) {
            unchecked {
                n = n * 374761393 + 668265263;
                n = (n ^ (n >> 13)) * 1274126177;
                return ((n ^ (n >> 16)) & 0x7FFFFFFF) / (float)int.MaxValue;
            }
        }

        //====================== 烛光 ======================

        /// <summary>底缘烛光:光自屏下缘涌上,焰心呼吸+低频摇曳(对位点鬼簿的绯月在上)</summary>
        public static void DrawCandleGlow(SpriteBatch sb, Vector2 bladeCenter, float alpha, float time, Vector2 parallax) {
            float flick = 0.86f + 0.10f * (float)Math.Sin(time * 2.1f) + 0.04f * (float)Math.Sin(time * 7.3f + 1.7f);
            Vector2 glowBase = new Vector2(bladeCenter.X, OnikiriUITheme.UIScreenH + 60f) + parallax;
            //三层暖光,越往上越淡
            OniBrush.DrawBacklight(sb, glowBase, 460f * flick, OnikiriUITheme.CandleWarm, alpha * 0.5f);
            OniBrush.DrawBacklight(sb, glowBase + new Vector2(-140f, 20f), 300f, OnikiriUITheme.BurnDim, alpha * 0.22f * flick);
            OniBrush.DrawBacklight(sb, glowBase + new Vector2(150f, 30f), 260f, OnikiriUITheme.Deep, alpha * 0.25f);
        }

        //====================== 台账主板 ======================

        /// <summary>开台落板的纵向让位:板与棚内器物共用,免得开屏那一下册子脱出格外</summary>
        public static int PanelRevealOffset(float reveal)
            => (int)((1f - VaultUtils.EaseOutCubic(MathHelper.Clamp(reveal / 0.42f, 0f, 1f))) * 14f);

        /// <summary>
        /// 改铭台账主板:黑漆卡面(shader TechLacquer 优先,缺席退回 CPU 简笔)+
        /// 题头(朱印+题字+烙痕线)+脚注界线;reveal 驱动卡面自下浮上。<br/>
        /// board 是板全体(台账区 + 底部书棚带),rect 只是台账区，板必须一个 quad 画完,
        /// 拆成两块会在接缝处各长一条上缘金压线
        /// </summary>
        public static void DrawLedgerPanel(SpriteBatch sb, DynamicSpriteFont font, Rectangle board,
            Rectangle rect, string title, float alpha, float reveal, float time) {
            float ease = VaultUtils.EaseOutCubic(MathHelper.Clamp(reveal / 0.42f, 0f, 1f));
            if (ease <= 0.01f || alpha <= 0.01f) {
                return;
            }
            float a = alpha * ease;
            int drop = PanelRevealOffset(reveal);
            Rectangle shownBoard = board;
            shownBoard.Y += drop;
            Rectangle shown = rect;
            shown.Y += drop;

            //板影:紧贴落影(大面板禁止同心扩层羽化,否则叠出方块黑层)
            OniBrush.DrawPanelDropShadow(sb, shownBoard.Center.ToVector2(),
                new Vector2(shownBoard.Width, shownBoard.Height * 0.98f), a * a * 0.9f,
                new Vector2(5f, 8f));

            if (OniMeiStandDraw.Available) {
                OniMeiStandDraw.DrawLacquerBoard(sb, shownBoard, a, time);
            }
            else {
                DrawPanelFallback(sb, shownBoard, a);
            }

            //题头:朱印+题字,下一笔烙痕线
            OniBrush.DrawSealGlyph(sb, new Vector2(shown.X + 30f, shown.Y + OnikiriUITheme.MeiPanelHeaderH * 0.52f),
                11f, a * 0.95f);
            Utils.DrawBorderString(sb, title, new Vector2(shown.X + 48f, shown.Y + 12f),
                OnikiriUITheme.HotWhite * a, 0.98f);
            OniBrush.DrawTaperedSlash(sb,
                new Vector2(shown.X + 14f, shown.Y + OnikiriUITheme.MeiPanelHeaderH),
                new Vector2(shown.Right - 14f, shown.Y + OnikiriUITheme.MeiPanelHeaderH - 2f),
                1.8f, 1.2f, a * 0.8f);

            //脚注界线:一线淡朱丝栏(状态行住其下)
            float footY = shown.Bottom - OnikiriUITheme.MeiPanelFooterH;
            OniBrush.DrawGradientLine(sb, new Vector2(shown.X + 18f, footY),
                new Vector2(shown.Right - 18f, footY),
                OnikiriUITheme.Deep * (a * 0.42f), OnikiriUITheme.Deep * (a * 0.2f), 1.1f);

            //棚带横木:台账到书棚的分界,金细线衬一线绯，与题头同一套规矩,读作同板异格
            float railY = shown.Bottom;
            OniBrush.DrawGradientLine(sb, new Vector2(shownBoard.X + 12f, railY),
                new Vector2(shownBoard.Right - 12f, railY),
                OnikiriUITheme.GoldDeep * (a * 0.55f), OnikiriUITheme.GoldDeep * (a * 0.22f), 1.5f);
            OniBrush.DrawGradientLine(sb, new Vector2(shownBoard.X + 12f, railY + 3f),
                new Vector2(shownBoard.Right - 12f, railY + 3f),
                OnikiriUITheme.Deep * (a * 0.30f), OnikiriUITheme.Deep * (a * 0.12f), 1f);
        }

        /// <summary>CPU 简笔黑漆卡面(shader 降级):漆黑纵深+上缘金压线衬绯线+侧缘沉色+漆下木理</summary>
        private static void DrawPanelFallback(SpriteBatch sb, Rectangle shown, float alpha) {
            Color lacqTop = Color.Lerp(OnikiriUITheme.Ink, Color.Black, 0.35f) * (alpha * 0.97f);
            Color lacqBot = Color.Lerp(OnikiriUITheme.Ink, OnikiriUITheme.Dark, 0.75f) * (alpha * 0.97f);
            int h2 = shown.Height / 2;
            sb.Draw(Pixel, new Rectangle(shown.X, shown.Y, shown.Width, h2), PixelSrc, lacqTop);
            sb.Draw(Pixel, new Rectangle(shown.X, shown.Y + h2, shown.Width, shown.Height - h2), PixelSrc, lacqBot);
            //漆下木理:几道纵走淡纹
            for (int i = 0; i < 3; i++) {
                float u = 0.24f + Hash01(i * 47 + 11) * 0.56f;
                sb.Draw(Pixel, new Vector2(shown.X + shown.Width * u, shown.Center.Y), PixelSrc,
                    Color.Black * (alpha * 0.22f), 0f, new Vector2(0.5f),
                    new Vector2(1f, shown.Height * 0.94f), SpriteEffects.None, 0f);
            }
            //上缘金压线,内衬一线绯红
            sb.Draw(Pixel, new Rectangle(shown.X, shown.Y, shown.Width, 2), PixelSrc,
                OnikiriUITheme.GoldDeep * (alpha * 0.8f));
            sb.Draw(Pixel, new Rectangle(shown.X, shown.Y + 4, shown.Width, 1), PixelSrc,
                OnikiriUITheme.Deep * (alpha * 0.6f));
            //侧缘断口沉色
            sb.Draw(Pixel, new Rectangle(shown.X, shown.Y, 6, shown.Height), PixelSrc, Color.Black * (alpha * 0.3f));
            sb.Draw(Pixel, new Rectangle(shown.Right - 6, shown.Y, 6, shown.Height), PixelSrc, Color.Black * (alpha * 0.3f));
        }

        //====================== 铭位牌 / 锚钉 / 注记引线 ======================

        /// <summary>
        /// 铭位牌:台账上的菱章(影/缘/体/字形),空位=虚线菱呼吸;
        /// ripple=开屏点名涟漪,stamp=接铭盖章回弹(0~1,1=闲)
        /// </summary>
        public static void DrawMedallion(SpriteBatch sb, Vector2 pos, string key, bool gold,
            float hover, float select, float alpha, float time, float ripple, float stamp) {
            float g = OnikiriUITheme.MeiMedallionSize;
            bool stamping = stamp > 0.01f && stamp < 0.995f;
            //盖章回弹:接铭一瞬鼓起再落座
            float pop = stamping ? 1f + 0.22f * MathF.Sin(stamp * MathHelper.Pi) * (1f - stamp * 0.5f) : 1f;
            float lift = (1f + hover * 0.08f + select * 0.05f) * pop;
            Vector2 half = new(0.5f);

            if (key == null) {
                //空位:虚线菱呼吸,不给实体章身
                DrawSlotEmpty(sb, pos, g * 0.94f * lift, hover, select, alpha, time, 0f);
            }
            else {
                Color rim = gold
                    ? Color.Lerp(OnikiriUITheme.GoldDeep, OnikiriUITheme.GoldInlay, 0.4f + hover * 0.5f)
                    : Color.Lerp(OnikiriUITheme.Deep, OnikiriUITheme.Bright, hover * 0.6f);
                sb.Draw(Pixel, pos + new Vector2(1.8f, 2.6f), PixelSrc, new Color(8, 2, 5) * (alpha * 0.55f),
                    MathHelper.PiOver4, half, new Vector2(g * 1.06f * lift), SpriteEffects.None, 0f);
                sb.Draw(Pixel, pos, PixelSrc, rim * (alpha * 0.9f),
                    MathHelper.PiOver4, half, new Vector2(g * 1.06f * lift), SpriteEffects.None, 0f);
                sb.Draw(Pixel, pos, PixelSrc, OnikiriUITheme.Ink * (alpha * 0.97f),
                    MathHelper.PiOver4, half, new Vector2(g * 0.96f * lift), SpriteEffects.None, 0f);
                sb.Draw(Pixel, pos, PixelSrc, OnikiriUITheme.Paper * (alpha * 0.16f),
                    MathHelper.PiOver4, half, new Vector2(g * 0.82f * lift), SpriteEffects.None, 0f);

                OniMeiGlyphStyle style = OniMeiGlyphStyle.Engraved(alpha);
                style.Time = time;
                style.Inlay = gold ? 1f : 0f;
                style.Accent = gold ? OnikiriUITheme.GoldInlay : OnikiriUITheme.Bright;
                style.Lit = 0.16f + MathF.Max(hover * 0.5f, select * 0.62f)
                    + (stamping ? MathF.Sin(stamp * MathHelper.Pi) * 0.5f : 0f);
                OniMeiGlyph.Draw(sb, key, pos, OnikiriUITheme.MeiMedallionGlyph * lift, style);
            }

            //悬停/选中:菱缘一圈短刻度旋行
            float show = MathF.Max(hover, select * 0.9f);
            if (show > 0.03f) {
                Color col = Color.Lerp(OnikiriUITheme.Paper, OnikiriUITheme.Seal, select) * (alpha * 0.55f * show);
                float spin = time * (0.2f + hover * 0.25f);
                float r = g * 0.66f * lift;
                for (int i = 0; i < 10; i++) {
                    float ang = spin + MathHelper.TwoPi * i / 10f;
                    sb.Draw(Pixel, pos + ang.ToRotationVector2() * r, PixelSrc, col, ang,
                        new Vector2(0f, 0.5f), new Vector2(4.2f + select * 1.4f, 1.1f), SpriteEffects.None, 0f);
                }
            }

            //开屏点名 / 接铭落章的刻度涟漪
            DrawRippleRing(sb, pos, g * 0.7f, ripple, OnikiriUITheme.Seal, alpha);
            if (stamping) {
                DrawRippleRing(sb, pos, g * 0.7f, stamp,
                    gold ? OnikiriUITheme.GoldInlay : OnikiriUITheme.Seal, alpha);
            }
        }

        /// <summary>一圈刻度涟漪(开屏点名/接铭盖章共用)</summary>
        private static void DrawRippleRing(SpriteBatch sb, Vector2 pos, float radius, float t, Color color, float alpha) {
            if (t <= 0.01f || t >= 0.995f) {
                return;
            }
            float ra = (1f - t) * (1f - t);
            float rr = radius * (0.55f + t * 1.6f);
            Color col = color * (alpha * 0.8f * ra);
            for (int i = 0; i < 12; i++) {
                float ang = MathHelper.TwoPi * i / 12f + t * 0.9f;
                sb.Draw(Pixel, pos + ang.ToRotationVector2() * rr, PixelSrc, col, ang,
                    new Vector2(0f, 0.5f), new Vector2(4f + t * 3f, 1.1f), SpriteEffects.None, 0f);
            }
        }

        /// <summary>刀身绳结:栋侧一枚朱结压在搭点上,结下垂一缕短穗随风;点结等价点牌</summary>
        public static void DrawAnchorKnot(SpriteBatch sb, Vector2 pos, bool engraved, bool gold,
            float hover, float alpha, float time) {
            if (alpha <= 0.02f) {
                return;
            }
            float breath = OnikiriUITheme.Breath(time, pos.Y * 0.013f, 1.4f);
            Color knot = engraved
                ? gold ? Color.Lerp(OnikiriUITheme.GoldDeep, OnikiriUITheme.GoldInlay, 0.5f) : OnikiriUITheme.Seal
                : Color.Lerp(OnikiriUITheme.Dark, OnikiriUITheme.Seal, 0.45f);
            float aKnot = alpha * (0.75f + hover * 0.25f + breath * 0.08f);
            float s = 4.4f + hover * 1.4f;

            //结影/结体/结心亮
            sb.Draw(Pixel, pos + new Vector2(1.2f, 1.6f), PixelSrc, new Color(8, 2, 5) * (alpha * 0.5f),
                MathHelper.PiOver4, new Vector2(0.5f), new Vector2(s), SpriteEffects.None, 0f);
            sb.Draw(Pixel, pos, PixelSrc, knot * aKnot,
                MathHelper.PiOver4, new Vector2(0.5f), new Vector2(s), SpriteEffects.None, 0f);
            sb.Draw(Pixel, pos + new Vector2(-0.8f, -0.8f), PixelSrc,
                Color.Lerp(knot, OnikiriUITheme.HotWhite, 0.4f) * (aKnot * 0.55f),
                MathHelper.PiOver4, new Vector2(0.5f), new Vector2(s * 0.4f), SpriteEffects.None, 0f);

            //短穗:结下垂一缕,风里轻摆(和 DrawHangingKnot 的流苏同语)
            float sway = (float)Math.Sin(time * 1.9f + pos.X * 0.02f) * 0.24f + hover * (float)Math.Sin(time * 7f) * 0.08f;
            Vector2 tasselEnd = pos + (MathHelper.PiOver2 + sway).ToRotationVector2() * (9f + hover * 3f);
            OniBrush.DrawGradientLine(sb, pos, tasselEnd,
                (gold && engraved ? OnikiriUITheme.GoldInlay : OnikiriUITheme.Bright) * (alpha * 0.6f),
                OnikiriUITheme.Deep * (alpha * 0.08f), 1.3f);

            if (hover > 0.05f) {
                OniBrush.DrawSoftDot(sb, pos, 12f, OnikiriUITheme.Bright, alpha * 0.25f * hover);
            }
        }

        /// <summary>注记墨线取点:二次贝塞尔,控制点自弦中点垂下一手，静垂感,不作物理摆</summary>
        private static Vector2 InkPoint(Vector2 start, Vector2 end, float t) {
            Vector2 mid = (start + end) * 0.5f;
            float sag = MathF.Min(Vector2.Distance(start, end) * 0.09f, 42f);
            Vector2 ctrl = mid + new Vector2(0f, sag);
            float u = 1f - t;
            return start * (u * u) + ctrl * (2f * u * t) + end * (t * t);
        }

        /// <summary>
        /// 注记墨线:牌→刀身搭点的一笔缓垂墨线，起笔略顿、行笔渐细、收笔出锋,
        /// 金阶收笔染金;drawEase 走笔揭示(开屏),lit 悬停/选中时墨里透绯
        /// </summary>
        public static void DrawLeaderInk(SpriteBatch sb, Vector2 start, Vector2 end, float drawEase,
            float lit, bool gold, float alpha, float time, int index) {
            if (drawEase <= 0.02f || alpha <= 0.02f) {
                return;
            }
            const int Segs = 18;
            float reveal = VaultUtils.EaseOutCubic(drawEase);
            //墨息:极缓的浓淡呼吸,静而不死
            float breath = 0.92f + 0.08f * OnikiriUITheme.Breath(time, index * 2.1f, 0.9f);
            float aInk = alpha * (0.5f + lit * 0.4f) * breath;
            int shown = Math.Max(1, (int)MathF.Ceiling(reveal * Segs));

            for (int i = 0; i < shown; i++) {
                float t0 = i / (float)Segs;
                float t1 = (i + 1f) / Segs;
                if (i == shown - 1 && reveal < 1f) {
                    t1 = MathHelper.Lerp(t0, t1, MathHelper.Clamp(reveal * Segs - i, 0.05f, 1f));
                }
                Vector2 p0 = InkPoint(start, end, t0);
                Vector2 p1 = InkPoint(start, end, t1);
                Vector2 d = p1 - p0;
                float len = d.Length();
                if (len < 0.01f) {
                    continue;
                }
                float tm = (t0 + t1) * 0.5f;
                //笔形:起笔一口顿(前 8% 略鼓),行笔渐细
                float thick = MathHelper.Lerp(2.3f, 0.9f, MathF.Pow(tm, 0.8f));
                if (tm < 0.08f) {
                    thick *= 1f + (1f - tm / 0.08f) * 0.3f;
                }
                Color col = Color.Lerp(OnikiriUITheme.Deep, OnikiriUITheme.Seal, tm);
                if (gold) {
                    col = Color.Lerp(col, OnikiriUITheme.GoldDeep,
                        MathHelper.Clamp((tm - 0.7f) / 0.3f, 0f, 1f) * 0.7f);
                }
                sb.Draw(Pixel, p0, PixelSrc, col * aInk, d.ToRotation(), new Vector2(0f, 0.5f),
                    new Vector2(len + 0.7f, thick), SpriteEffects.None, 0f);
                //悬停:墨芯透绯一线
                if (lit > 0.03f) {
                    sb.Draw(Pixel, p0, PixelSrc,
                        (gold ? OnikiriUITheme.GoldInlay : OnikiriUITheme.Bright) * (alpha * 0.30f * lit),
                        d.ToRotation(), new Vector2(0f, 0.5f),
                        new Vector2(len + 0.7f, thick * 0.4f), SpriteEffects.None, 0f);
                }
            }

            //起笔墨点:牌端一粒沉点
            sb.Draw(Pixel, start, PixelSrc, OnikiriUITheme.Deep * (aInk * 1.1f), MathHelper.PiOver4,
                new Vector2(0.5f), new Vector2(3.2f), SpriteEffects.None, 0f);
            //走笔中的笔锋
            if (reveal < 0.995f) {
                OniBrush.DrawSoftDot(sb, InkPoint(start, end, reveal), 4.6f,
                    OnikiriUITheme.HotWhite, alpha * 0.65f);
            }
        }

        /// <summary>接铭归线:一粒亮光带短尾沿墨线自刀流回牌位(t 0~1),鏨下的字入账</summary>
        public static void DrawInkPacket(SpriteBatch sb, Vector2 start, Vector2 end, float t, bool gold, float alpha) {
            if (t <= 0.01f || t >= 0.995f || alpha <= 0.02f) {
                return;
            }
            float k = t * t * (3f - 2f * t);
            Vector2 pos = InkPoint(start, end, 1f - k);
            Vector2 tail = InkPoint(start, end, MathHelper.Clamp(1f - k + 0.08f, 0f, 1f));
            Color col = gold ? OnikiriUITheme.GoldInlay : OnikiriUITheme.Bright;
            OniBrush.DrawGradientLine(sb, tail, pos, col * (alpha * 0.15f), col * (alpha * 0.75f), 2.2f);
            OniBrush.DrawSoftDot(sb, pos, 7f, col, alpha * 0.85f);
            OniBrush.DrawSoftDot(sb, pos, 3f, OnikiriUITheme.HotWhite, alpha * 0.8f);
        }

        //====================== 陈列刀 ======================

        /// <summary>
        /// 陈列刀:烛暖背光毯垫底+本体(<see cref="OniMeiBladeDraw"/> 原生姿态)+
        /// 偶发刀鸣流光沿真实刃缘;originPx/screenPos/scale=本帧变换(检分镜头的不动点即锚)
        /// </summary>
        public static void DrawExhibit(SpriteBatch sb, Vector2 originPx, Vector2 screenPos, float scale,
            float alpha, float time, float songRun) {
            if (alpha <= 0.01f) {
                return;
            }
            //背光:烛暖大晕托底,绯深内晕坠在下半(光毯,不是光球身体)
            Vector2 center = screenPos + (OniMeiBladeDraw.SpriteCenter - originPx) * scale;
            OniBrush.DrawBacklight(sb, center, 150f * scale, OnikiriUITheme.CandleWarm, alpha * 0.16f);
            OniBrush.DrawBacklight(sb, center + new Vector2(0f, 26f * scale), 90f * scale,
                OnikiriUITheme.Deep, alpha * 0.18f);

            OniMeiBladeDraw.Draw(sb, originPx, screenPos, scale, alpha, time);

            //刀鸣:一线白光沿刃缘颤过(软芯+辉光),走真实刃形的弧
            if (songRun >= 0f && OniMeiBladeDraw.Ready) {
                float t = songRun / 90f;
                float u = MathHelper.Lerp(0.04f, 0.68f, t);
                float pulse = (float)Math.Sin(t * MathHelper.Pi);
                Vector2 pos = screenPos + (OniMeiBladeDraw.EdgePx(u, 1.5f) - originPx) * scale;
                OniBrush.DrawSoftStreak(sb, pos, OniMeiBladeDraw.EdgeTangent(u), 27f * scale, 1.5f,
                    OnikiriUITheme.HotWhite, alpha * 0.8f * pulse, glowMul: 1.1f);
                OniBrush.DrawSoftDot(sb, pos, 5.5f * scale, OnikiriUITheme.Bright, alpha * 0.30f * pulse);
            }
        }

        //====================== 铭位 ======================

        /// <summary>空铭位:凿框虚线菱,悬停白亮微转</summary>
        public static void DrawSlotEmpty(SpriteBatch sb, Vector2 pos, float size, float hover, float select,
            float alpha, float time, float rotation) {
            float breath = 0.55f + 0.25f * (float)Math.Sin(time * 1.8f + pos.X * 0.01f);
            float a = alpha * (breath * 0.5f + hover * 0.5f + select * 0.3f);
            float r = size * 0.5f;
            float spin = rotation + hover * (float)Math.Sin(time * 2.2f) * 0.03f;

            //菱形四边各两段虚线
            Vector2[] corners = new Vector2[4];
            for (int i = 0; i < 4; i++) {
                corners[i] = pos + (spin + MathHelper.PiOver2 * i + MathHelper.PiOver4).ToRotationVector2() * r;
            }
            Color line = Color.Lerp(OnikiriUITheme.TextDim, OnikiriUITheme.Paper, hover) * a;
            for (int e = 0; e < 4; e++) {
                Vector2 c0 = corners[e];
                Vector2 c1 = corners[(e + 1) % 4];
                DrawDash(sb, Vector2.Lerp(c0, c1, 0.08f), Vector2.Lerp(c0, c1, 0.36f), line, 1.2f);
                DrawDash(sb, Vector2.Lerp(c0, c1, 0.64f), Vector2.Lerp(c0, c1, 0.92f), line, 1.2f);
            }
            //心点:一粒极小的凿位标记
            sb.Draw(Pixel, pos, PixelSrc, line * 0.8f, spin + MathHelper.PiOver4,
                new Vector2(0.5f), new Vector2(2.6f + hover * 1.2f), SpriteEffects.None, 0f);
        }

        private static void DrawDash(SpriteBatch sb, Vector2 a, Vector2 b, Color color, float thick) {
            Vector2 edge = b - a;
            float len = edge.Length();
            if (len < 0.5f) {
                return;
            }
            sb.Draw(Pixel, a, PixelSrc, color, edge.ToRotation(), new Vector2(0f, 0.5f),
                new Vector2(len, thick), SpriteEffects.None, 0f);
        }

        //====================== 鏨盘扇 ======================

        /// <summary>
        /// 扇骨+菱纹章:pos 为当前绘制位置,章内阴刻字形,悬停点亮;isCurrent 顶角朱点。<br/>
        /// locked = 未凿位:骨细一档、章面素钢无朱缘、字形只留凹痕不点绯红,顶角改一枚空凿口
        /// </summary>
        public static void DrawFanRib(SpriteBatch sb, Vector2 pivot, Vector2 pos, string glyphKey, bool gold,
            bool isCurrent, float vis, float hover, float alpha, float time, float glyphSize,
            bool locked = false) {
            Vector2 drawPos = pos;
            float a = alpha * vis * (locked ? 0.72f : 1f);

            //骨:未凿的那几根更细更暗,一眼分得出这排是"还没到手的"
            OniBrush.DrawGradientLine(sb, pivot, drawPos,
                OnikiriUITheme.Dark * (a * (locked ? 0.55f : 0.8f)),
                (locked ? OnikiriUITheme.Disabled : OnikiriUITheme.Deep) * (a * 0.9f),
                locked ? 1.4f : 2f);

            //菱章:影/缘/体
            float g = glyphSize * (locked ? 0.88f : 1f);
            float lift = 1f + hover * 0.1f;
            Vector2 half = new(0.5f);
            Color rim = locked
                ? Color.Lerp(OnikiriUITheme.Disabled, OnikiriUITheme.TextDim, hover * 0.7f)
                : gold
                    ? Color.Lerp(OnikiriUITheme.GoldDeep, OnikiriUITheme.GoldInlay, 0.4f + hover * 0.5f)
                    : Color.Lerp(OnikiriUITheme.Deep, OnikiriUITheme.Bright, hover * 0.6f);
            sb.Draw(Pixel, drawPos + new Vector2(1.6f, 2.2f), PixelSrc, new Color(8, 2, 5) * (a * 0.55f),
                MathHelper.PiOver4, half, new Vector2(g * 1.06f * lift), SpriteEffects.None, 0f);
            sb.Draw(Pixel, drawPos, PixelSrc, rim * (a * 0.9f),
                MathHelper.PiOver4, half, new Vector2(g * 1.06f * lift), SpriteEffects.None, 0f);
            sb.Draw(Pixel, drawPos, PixelSrc, OnikiriUITheme.Ink * (a * 0.97f),
                MathHelper.PiOver4, half, new Vector2(g * 0.96f * lift), SpriteEffects.None, 0f);

            //章内字形:钢底一小片衬字
            sb.Draw(Pixel, drawPos, PixelSrc, OnikiriUITheme.Paper * (a * (locked ? 0.10f : 0.20f)),
                MathHelper.PiOver4, half, new Vector2(g * 0.82f * lift), SpriteEffects.None, 0f);
            OniMeiGlyphStyle style = OniMeiGlyphStyle.Engraved(a);
            style.Time = time;
            style.Inlay = gold && !locked ? 1f : 0f;
            style.Accent = locked
                ? OnikiriUITheme.TextDim
                : gold ? OnikiriUITheme.GoldInlay : OnikiriUITheme.Bright;
            //静息基础微亮:非金阶暗字暗底不靠悬停也读得出;金阶有金填缝,维持原样
            //未凿位只给凹痕的一点点受光,悬停也不点亮到"可用"的程度
            style.Lit = locked
                ? 0.04f + hover * 0.14f
                : gold ? hover * 0.7f : 0.12f + hover * 0.58f;
            OniMeiGlyph.Draw(sb, glyphKey, drawPos, g * 0.72f * lift, style);

            //顶角:已凿走朱印软点,未凿留一枚空凿口(暗心亮唇的小方坑)
            Vector2 mark = drawPos + new Vector2(0f, -g * 0.72f);
            if (locked) {
                sb.Draw(Pixel, mark, PixelSrc, new Color(6, 2, 4) * (a * 0.9f),
                    MathHelper.PiOver4, half, new Vector2(3.4f), SpriteEffects.None, 0f);
                sb.Draw(Pixel, mark - new Vector2(0.6f, 0.8f), PixelSrc,
                    OnikiriUITheme.TextDim * (a * 0.5f),
                    MathHelper.PiOver4, half, new Vector2(1.6f), SpriteEffects.None, 0f);
                return;
            }
            if (isCurrent) {
                OniBrush.DrawSoftDot(sb, mark, 3.6f, OnikiriUITheme.Seal, a * 0.95f);
            }
        }

        /// <summary>
        /// 錾样匣一格:缩小扇菱章(无扇骨线)。台上拓片,非背包方格
        /// </summary>
        public static void DrawTrayCell(SpriteBatch sb, Vector2 pos, string glyphKey, bool gold, bool isCurrent,
            int stack, float vis, float hover, float alpha, float time) {
            float ease = vis * (2f - vis);
            float a = alpha * ease;
            if (a <= 0.01f) {
                return;
            }

            float g = OnikiriUITheme.MeiTrayGlyphSize;
            float lift = 1f + hover * 0.1f;
            Vector2 half = new(0.5f);
            Color rim = gold
                ? Color.Lerp(OnikiriUITheme.GoldDeep, OnikiriUITheme.GoldInlay, 0.4f + hover * 0.5f)
                : Color.Lerp(OnikiriUITheme.Deep, OnikiriUITheme.Bright, hover * 0.6f);

            sb.Draw(Pixel, pos + new Vector2(1.4f, 2f), PixelSrc, new Color(8, 2, 5) * (a * 0.5f),
                MathHelper.PiOver4, half, new Vector2(g * 1.06f * lift), SpriteEffects.None, 0f);
            sb.Draw(Pixel, pos, PixelSrc, rim * (a * 0.9f),
                MathHelper.PiOver4, half, new Vector2(g * 1.06f * lift), SpriteEffects.None, 0f);
            sb.Draw(Pixel, pos, PixelSrc, OnikiriUITheme.Ink * (a * 0.97f),
                MathHelper.PiOver4, half, new Vector2(g * 0.96f * lift), SpriteEffects.None, 0f);
            sb.Draw(Pixel, pos, PixelSrc, OnikiriUITheme.Paper * (a * 0.20f),
                MathHelper.PiOver4, half, new Vector2(g * 0.82f * lift), SpriteEffects.None, 0f);

            OniMeiGlyphStyle style = OniMeiGlyphStyle.Engraved(a);
            style.Time = time;
            style.Inlay = gold ? 1f : 0f;
            style.Accent = gold ? OnikiriUITheme.GoldInlay : OnikiriUITheme.Bright;
            //静息基础微亮:与扇骨同款,匣格不靠悬停也读得出
            style.Lit = gold ? hover * 0.7f : 0.12f + hover * 0.58f;
            OniMeiGlyph.Draw(sb, glyphKey, pos, g * 0.72f * lift, style);

            if (isCurrent) {
                OniBrush.DrawSoftDot(sb, pos + new Vector2(0f, -g * 0.72f), 3.2f, OnikiriUITheme.Seal, a * 0.95f);
            }

            //堆叠:右下角纸色小字,勿原版灰标
            if (stack > 1) {
                string n = stack > 99 ? "99+" : stack.ToString();
                DynamicSpriteFont font = FontAssets.MouseText.Value;
                Vector2 size = font.MeasureString(n) * 0.8f;
                Vector2 corner = pos + new Vector2(g * 0.22f, g * 0.18f);
                Utils.DrawBorderString(sb, n, corner - size * 0.5f, OnikiriUITheme.Paper * a, 0.8f);
            }
        }

        //====================== 铭谱书棚(台账板底的一格) ======================

        /// <summary>册面纹章:外圆内菱,与图鉴封面同纹</summary>
        private const string CodexMonD =
            "M 0,-1 C 0.5523,-1 1,-0.5523 1,0 C 1,0.5523 0.5523,1 0,1"
            + " C -0.5523,1 -1,0.5523 -1,0 C -1,-0.5523 -0.5523,-1 0,-1 Z"
            + " M 0,-0.52 L 0.52,0 L 0,0.52 L -0.52,0 Z";

        /// <summary>题签:手贴的短册,四边不平行、裁口略歪(纵向,x 已按签宽收窄)</summary>
        private const string CodexLabelD =
            "M -0.44,-1 L 0.46,-0.96 C 0.5,-0.95 0.52,-0.9 0.51,-0.82"
            + " L 0.47,0.88 C 0.46,0.96 0.42,1 0.35,0.99"
            + " L -0.47,0.93 C -0.52,0.92 -0.53,0.86 -0.52,0.78 Z";

        /// <summary>书口:纸叠外缘微鼓的一条弧(册子最不像方盒的一边;鼓度按纸厚,别当成弓背)</summary>
        private const string CodexForeEdgeD = "M 0,-1 C 0.09,-0.62 0.10,0.6 0,1";

        /// <summary>四目缀じ:三段走线,孔处断开(孔另点)</summary>
        private const string CodexStitchD =
            "M 0,-0.9 L 0,-0.46 M 0,-0.2 L 0,0.24 M 0,0.5 L 0,0.92";

        /// <summary>栞:压在封面上的一条,尾端剪燕尾(与摊开册同款)</summary>
        private const string CodexBookmarkD =
            "M -0.26,-1 L 0.26,-1 L 0.26,0.62 L 0,0.2 L -0.26,0.62 Z";

        /// <summary>
        /// 台账板底那一格书棚:格内暗 → 插着的柿渋褐木线装册 → 压住册子下缘的书唇。<br/>
        /// 悬停不是"图标变亮",是册子自格里被抽起一截(书口散开、栞滑出、金压线受光)
        /// </summary>
        public static void DrawCodexNiche(SpriteBatch sb, DynamicSpriteFont font, Rectangle nicheIn,
            Rectangle bookIn, string label, float hover, float alpha, float reveal, float time) {
            if (alpha <= 0.01f) {
                return;
            }
            //与板共用落板让位,否则开屏那一下册子会脱出格外
            int drop = PanelRevealOffset(reveal);
            Rectangle niche = nicheIn;
            niche.Y += drop;
            Rectangle book = bookIn;
            book.Y += drop - (int)(hover * OnikiriUITheme.CodexBookPull);
            //烛在屏下,格口下缘先亮;摇曳与台面烛光同源
            float flick = 0.88f + 0.09f * MathF.Sin(time * 2.1f) + 0.03f * MathF.Sin(time * 7.3f + 1.7f);
            //开台自画:线稿按弧长描出,与两扇门同一套揭示
            float ink = VaultUtils.EaseOutCubic(MathHelper.Clamp((reveal - 0.5f) / 0.42f, 0f, 1f));

            DrawNicheHollow(sb, niche, alpha, flick);
            DrawCodexBook(sb, font, book, label, hover, alpha, ink, time);
            DrawNicheLip(sb, niche, alpha, hover, flick);

            //取书示意:格口漫出一点烛暖(随烛摇,不是常亮的按钮辉)
            if (hover > 0.02f) {
                OniBrush.DrawBacklight(sb, new Vector2(niche.Center.X, niche.Bottom - 10f), 54f,
                    OnikiriUITheme.CandleWarm, alpha * hover * 0.26f * flick);
            }
        }

        /// <summary>棚格:板上凿进去的一格,里壁越上越深(灯在下),格底吃到一线烛光</summary>
        private static void DrawNicheHollow(SpriteBatch sb, Rectangle niche, float a, float flick) {
            sb.Draw(Pixel, niche, PixelSrc, new Color(9, 4, 6) * (a * 0.90f));
            sb.Draw(Pixel, new Rectangle(niche.X, niche.Y, niche.Width, 5), PixelSrc,
                Color.Black * (a * 0.55f));
            sb.Draw(Pixel, new Rectangle(niche.X, niche.Y, 3, niche.Height), PixelSrc,
                Color.Black * (a * 0.34f));
            sb.Draw(Pixel, new Rectangle(niche.Right - 3, niche.Y, 3, niche.Height), PixelSrc,
                Color.Black * (a * 0.34f));
            //格口上楣:板面到格内那道断口压一线金
            OniBrush.DrawGradientLine(sb, new Vector2(niche.X + 2f, niche.Y - 1f),
                new Vector2(niche.Right - 2f, niche.Y - 1f),
                OnikiriUITheme.GoldDeep * (a * 0.42f), OnikiriUITheme.GoldDeep * (a * 0.18f), 1.2f);
            sb.Draw(Pixel, new Rectangle(niche.X + 2, niche.Bottom - 1, niche.Width - 4, 1), PixelSrc,
                OnikiriUITheme.CandleWarm * (a * 0.16f * flick));
        }

        /// <summary>
        /// 插在格里的线装册:书口纸叠 → 柿渋褐木封面(与摊开的册子同料) → 金压线衬绯 →
        /// 四目缀じ → 题签题字 → 纹章 → 栞
        /// </summary>
        private static void DrawCodexBook(SpriteBatch sb, DynamicSpriteFont font, Rectangle book,
            string label, float hover, float a, float ink, float time) {
            //册影落在格内壁上(紧贴错位,不扩层)
            OniBrush.DrawPanelDropShadow(sb, book.Center.ToVector2(),
                new Vector2(book.Width, book.Height), a * 0.85f, new Vector2(3f, 4f));

            //====书口:露在封面右侧的一叠纸边,自外向内收暗;抽书时散开一点====
            SvgPath fore = SvgPathPen.Path(CodexForeEdgeD);
            Vector2 foreAt = new(book.Right - 1f + hover * 2f, book.Center.Y);
            float foreScale = book.Height * 0.47f;
            float step = 1.05f + hover * 0.35f;
            for (int i = 0; i < 6; i++) {
                float t = i / 5f;
                SvgPathPen.Stroke(sb, fore, foreAt - new Vector2(i * step, 0f), foreScale, 0f,
                    Color.Lerp(new Color(232, 222, 202), new Color(140, 122, 100), t), 1.7f,
                    a * (0.94f - t * 0.22f));
            }

            //====封面:柿渋褐木。烛在屏下,故上暗下暖,与台面同一个光向====
            Rectangle cover = new(book.X, book.Y, book.Width - 6, book.Height);
            Color woodShade = Color.Lerp(new Color(38, 22, 17), new Color(52, 31, 23), hover * 0.5f);
            Color woodLit = Color.Lerp(new Color(84, 51, 36), new Color(110, 67, 45), hover * 0.5f);
            const int bands = 3;
            for (int i = 0; i < bands; i++) {
                int y0 = cover.Y + cover.Height * i / bands;
                int y1 = cover.Y + cover.Height * (i + 1) / bands;
                sb.Draw(Pixel, new Rectangle(cover.X, y0, cover.Width, y1 - y0), PixelSrc,
                    Color.Lerp(woodShade, woodLit, (i + 0.5f) / bands) * (a * 0.97f));
            }
            //木理:两三道纵纹
            for (int i = 0; i < 3; i++) {
                float u = 0.2f + Hash01(i * 61 + 5) * 0.58f;
                sb.Draw(Pixel, new Vector2(cover.X + cover.Width * u, cover.Center.Y), PixelSrc,
                    new Color(26, 14, 10) * (a * 0.26f), 0f, new Vector2(0.5f),
                    new Vector2(1f, cover.Height * 0.9f), SpriteEffects.None, 0f);
            }
            //顶缘吃格楣的影(书立在格里,上头是板不是光)
            sb.Draw(Pixel, new Rectangle(cover.X, cover.Y, cover.Width, 2), PixelSrc,
                new Color(14, 7, 6) * (a * 0.5f));
            //====金压线 + 内衬一线绯:与顶梁/台账/摊开的册子同语====
            DrawThinFrame(sb, cover, OnikiriUITheme.GoldDeep * (a * (0.52f + hover * 0.34f)), 2);
            Rectangle liner = cover;
            liner.Inflate(-3, -3);
            DrawThinFrame(sb, liner, OnikiriUITheme.Deep * (a * 0.28f), 1);
            //上两角磨钝:压在金线之后,连金边一起吃掉尺规角(下缘藏在书唇后,不必切)
            Color chamfer = new(9, 4, 6);
            DrawChamfer(sb, cover.X, cover.Y, 1, 1, 3, chamfer, a * 0.9f);
            DrawChamfer(sb, cover.Right - 1, cover.Y, -1, 1, 3, chamfer, a * 0.9f);

            //====四目缀じ:一条走线带亮芯,孔另点====
            float stitchX = cover.X + 8f;
            SvgPathPen.Stroke(sb, SvgPathPen.Path(CodexStitchD), new Vector2(stitchX, cover.Center.Y),
                cover.Height * 0.46f, 0f, OnikiriUITheme.Deep, 1.3f, a * 0.8f, 0f, ink,
                OnikiriUITheme.CandleWarm);
            for (int i = 0; i < 4; i++) {
                Vector2 hole = new(stitchX, cover.Y + cover.Height * ((i + 1) / 5f));
                sb.Draw(Pixel, hole, PixelSrc, new Color(8, 3, 5) * (a * 0.85f),
                    0f, new Vector2(0.5f), new Vector2(2.2f, 3.2f), SpriteEffects.None, 0f);
            }

            //====题签:偏左贴的一枚短册(让开缀线),签面题字====
            Vector2 labelAt = new(cover.X + cover.Width * 0.44f, cover.Y + cover.Height * 0.38f);
            float labelScale = cover.Height * 0.26f;
            sb.Draw(Pixel, labelAt, PixelSrc, new Color(238, 230, 212) * (a * 0.93f), 0f,
                new Vector2(0.5f), new Vector2(labelScale * 0.94f, labelScale * 1.96f),
                SpriteEffects.None, 0f);
            SvgPathPen.Stroke(sb, SvgPathPen.Path(CodexLabelD), labelAt, labelScale, 0f,
                new Color(168, 152, 126), 1.1f, a * 0.7f, 0f, ink);
            DrawLabelInk(sb, font, label, labelAt, labelScale, a * ink);

            //====纹章:压在封面右下,不自转(桌上的书不会转)====
            SvgPathPen.Stroke(sb, SvgPathPen.Path(CodexMonD),
                new Vector2(cover.Right - 15f, cover.Bottom - 17f), 8.5f, 0f,
                OnikiriUITheme.GoldDeep, 1.4f, a * (0.5f + hover * 0.4f), 0f, ink,
                OnikiriUITheme.GoldInlay);

            //====栞:自册顶垂下压在封面上,抽书时多滑出一截====
            float markLen = 26f + hover * 9f;
            SvgPathPen.Stroke(sb, SvgPathPen.Path(CodexBookmarkD),
                new Vector2(cover.Right - 13f, cover.Y + markLen * 0.5f - 4f), markLen * 0.5f,
                MathF.Sin(time * 0.7f) * 0.03f, OnikiriUITheme.Deep, 2f, a * 0.88f, 0f, 1f,
                OnikiriUITheme.Bright);
        }

        /// <summary>签面题字:两三字竖排,长名/西文写不下就退回三记墨迹(签只有二十来像素宽)</summary>
        private static void DrawLabelInk(SpriteBatch sb, DynamicSpriteFont font, string label,
            Vector2 center, float labelScale, float a) {
            Color inkCol = Color.Lerp(new Color(38, 26, 24), OnikiriUITheme.Deep, 0.28f) * a;
            if (!string.IsNullOrEmpty(label) && OniBrush.ContainsCJK(label) && label.Length <= 3) {
                const float scale = 0.55f;
                float lineH = font.MeasureString("铭").Y * scale * 0.86f;
                float top = center.Y - lineH * label.Length * 0.5f;
                for (int i = 0; i < label.Length; i++) {
                    string ch = label[i].ToString();
                    Vector2 size = font.MeasureString(ch) * scale;
                    //和纸浅字单次落笔,四向描边会在这个字号糊成一团
                    sb.DrawString(font, ch, new Vector2(center.X - size.X * 0.5f, top + lineH * i),
                        inkCol, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                }
                return;
            }
            for (int i = 0; i < 3; i++) {
                sb.Draw(Pixel, new Vector2(center.X, center.Y + (i - 1) * labelScale * 0.5f), PixelSrc,
                    inkCol * 0.85f, 0f, new Vector2(0.5f),
                    new Vector2(labelScale * (0.5f - i * 0.06f), 1.6f), SpriteEffects.None, 0f);
            }
        }

        /// <summary>直角切角:逐行退让 size 像素,拿格内暗色吃掉尺规角</summary>
        private static void DrawChamfer(SpriteBatch sb, int x, int y, int sx, int sy, int size,
            Color color, float a) {
            for (int i = 0; i < size; i++) {
                int w = size - i;
                sb.Draw(Pixel, new Rectangle(sx > 0 ? x : x - w + 1, sy > 0 ? y + i : y - i, w, 1),
                    PixelSrc, color * a);
            }
        }

        /// <summary>四边描一圈线框(册面金压线)</summary>
        private static void DrawThinFrame(SpriteBatch sb, Rectangle rect, Color color, int thick) {
            sb.Draw(Pixel, new Rectangle(rect.X, rect.Y, rect.Width, thick), PixelSrc, color);
            sb.Draw(Pixel, new Rectangle(rect.X, rect.Bottom - thick, rect.Width, thick), PixelSrc, color);
            sb.Draw(Pixel, new Rectangle(rect.X, rect.Y, thick, rect.Height), PixelSrc, color);
            sb.Draw(Pixel, new Rectangle(rect.Right - thick, rect.Y, thick, rect.Height), PixelSrc, color);
        }

        /// <summary>书唇:挡在册子下缘的前板,顶棱吃一线烛光，这条光是"册子插在格里"的成立点</summary>
        private static void DrawNicheLip(SpriteBatch sb, Rectangle niche, float a, float hover, float flick) {
            int h = (int)OnikiriUITheme.CodexLipH;
            Rectangle lip = new(niche.X - 4, niche.Bottom - h, niche.Width + 8, h);
            //唇影压在册面下缘
            sb.Draw(Pixel, new Rectangle(lip.X, lip.Y - 4, lip.Width, 4), PixelSrc,
                new Color(8, 3, 5) * (a * 0.5f));
            int mid = h / 2;
            sb.Draw(Pixel, new Rectangle(lip.X, lip.Y, lip.Width, mid), PixelSrc,
                new Color(46, 26, 20) * (a * 0.98f));
            sb.Draw(Pixel, new Rectangle(lip.X, lip.Y + mid, lip.Width, h - mid), PixelSrc,
                new Color(28, 15, 12) * (a * 0.98f));
            //顶棱受光 + 一线金
            sb.Draw(Pixel, new Rectangle(lip.X, lip.Y, lip.Width, 1), PixelSrc,
                OnikiriUITheme.CandleWarm * (a * (0.30f + hover * 0.18f) * flick));
            OniBrush.DrawGradientLine(sb, new Vector2(lip.X + 3f, lip.Y + 2.5f),
                new Vector2(lip.Right - 3f, lip.Y + 2.5f),
                OnikiriUITheme.GoldDeep * (a * 0.40f), OnikiriUITheme.GoldDeep * (a * 0.16f), 1.1f);
            //两端木口沉色:唇是插进板里的一截,不是浮在板上的条
            sb.Draw(Pixel, new Rectangle(lip.X, lip.Y, 3, h), PixelSrc, Color.Black * (a * 0.35f));
            sb.Draw(Pixel, new Rectangle(lip.Right - 3, lip.Y, 3, h), PixelSrc, Color.Black * (a * 0.35f));
        }

        /// <summary>匣底一截淡墨轨;页点为朱印软点</summary>
        public static void DrawTrayRail(SpriteBatch sb, Vector2 left, Vector2 right, float alpha, float time,
            int page, int pageCount) {
            if (alpha <= 0.01f) {
                return;
            }
            OniBrush.DrawGradientLine(sb, left, right,
                OnikiriUITheme.Dark * (alpha * 0.35f), OnikiriUITheme.Deep * (alpha * 0.55f), 1.6f);
            if (pageCount <= 1) {
                return;
            }
            float midX = (left.X + right.X) * 0.5f;
            float y = right.Y + 14f;
            for (int i = 0; i < pageCount; i++) {
                float x = midX + (i - (pageCount - 1) * 0.5f) * 12f;
                float lit = i == page ? 1f : 0.35f;
                OniBrush.DrawSoftDot(sb, new Vector2(x, y), i == page ? 3.4f : 2.4f,
                    OnikiriUITheme.Seal, alpha * lit);
            }
        }

        /// <summary>
        /// 錾样匣木板:复用烙印木牌的手裁木纹板体,无系绳(坐在台底,与左牌并列)
        /// </summary>
        public static void DrawTrayPlank(SpriteBatch sb, Rectangle rect, float alpha, float time) {
            if (alpha <= 0.01f) {
                return;
            }
            OniBrush.DrawPanelDropShadow(sb, rect.Center.ToVector2(),
                new Vector2(rect.Width, rect.Height), alpha * 0.85f, new Vector2(5f, 7f));

            if (OniMeiStandDraw.Available) {
                Rectangle plank = rect;
                plank.Inflate(6, 6);
                OniMeiStandDraw.DrawWoodPlank(sb, plank, alpha, time);
            }
            else {
                sb.Draw(Pixel, new Rectangle(rect.X - 3, rect.Y - 3, rect.Width + 6, rect.Height + 6), PixelSrc,
                    OnikiriUITheme.Deep * (alpha * 0.5f));
                sb.Draw(Pixel, rect, PixelSrc, new Color(52, 18, 16) * (alpha * 0.97f));
                for (int i = 0; i < 4; i++) {
                    float u = 0.12f + Hash01(i * 71 + 9) * 0.76f;
                    sb.Draw(Pixel, new Vector2(rect.X + rect.Width * u, rect.Center.Y), PixelSrc,
                        OnikiriUITheme.Ink * (alpha * 0.28f), 0f, new Vector2(0.5f),
                        new Vector2(1f, rect.Height * 0.82f), SpriteEffects.None, 0f);
                }
            }

            //题下朱线,与木牌题头分隔同气
            float lineY = rect.Y + 34f;
            OniBrush.DrawGradientLine(sb,
                new Vector2(rect.X + 22f, lineY),
                new Vector2(rect.Right - 22f, lineY),
                OnikiriUITheme.Seal * (alpha * 0.55f),
                OnikiriUITheme.Deep * (alpha * 0.35f), 1.4f);
        }

        /// <summary>除铭骨:暗章锉叉,悬停转绯红;pos 为当前绘制位置</summary>
        public static void DrawFanRibErase(SpriteBatch sb, Vector2 pivot, Vector2 pos, float vis, float hover,
            float alpha, float time, float glyphSize) {
            Vector2 drawPos = pos;
            float a = alpha * vis;

            OniBrush.DrawGradientLine(sb, pivot, drawPos, OnikiriUITheme.Dark * (a * 0.7f),
                OnikiriUITheme.Dark * (a * 0.9f), 2f);

            float g = glyphSize;
            float lift = 1f + hover * 0.1f;
            Vector2 half = new(0.5f);
            Color rim = Color.Lerp(OnikiriUITheme.Disabled, OnikiriUITheme.Bright, hover) * (a * 0.85f);
            sb.Draw(Pixel, drawPos + new Vector2(1.6f, 2.2f), PixelSrc, new Color(8, 2, 5) * (a * 0.5f),
                MathHelper.PiOver4, half, new Vector2(g * 1.02f * lift), SpriteEffects.None, 0f);
            sb.Draw(Pixel, drawPos, PixelSrc, rim,
                MathHelper.PiOver4, half, new Vector2(g * 1.02f * lift), SpriteEffects.None, 0f);
            sb.Draw(Pixel, drawPos, PixelSrc, OnikiriUITheme.Ink * (a * 0.97f),
                MathHelper.PiOver4, half, new Vector2(g * 0.92f * lift), SpriteEffects.None, 0f);

            //锉叉:两笔交错刀痕
            float r = g * 0.3f * lift;
            Color cross = Color.Lerp(OnikiriUITheme.TextDim, OnikiriUITheme.Bright, hover) * a;
            DrawDash(sb, drawPos + new Vector2(-r, -r), drawPos + new Vector2(r, r), cross, 2f);
            DrawDash(sb, drawPos + new Vector2(r, -r), drawPos + new Vector2(-r, r), cross, 2f);
        }

        //====================== 烙印木牌 ======================

        /// <summary>木牌正文字号</summary>
        internal const float TagBodyScale = 0.8f;
        /// <summary>木牌栏目标字号(出处/赋效/代价)</summary>
        internal const float TagLabelScale = 0.7f;
        /// <summary>题头区高(题名+类目签+烙痕线),正文自此起笔</summary>
        internal const float TagHeaderH = 52f;

        /// <summary>栏目标行进(标签字高+行距)</summary>
        private static float TagLabelStep(DynamicSpriteFont font)
            => font.MeasureString("字").Y * TagLabelScale + 3f;

        /// <summary>
        /// 按内容实测木牌高度(与 <see cref="DrawWoodTag"/> 排版同口径),
        /// OniMeiUI.LayoutCompute 逐帧调用,底边锚定向上生长
        /// </summary>
        public static float MeasureTagHeight(DynamicSpriteFont font, float tagWidth,
            string origin, string power, string burden) {
            float width = tagWidth - 28f - 16f;
            float labelStep = TagLabelStep(font);
            float h = TagHeaderH;
            h += labelStep + OniRegisterRenderer.MeasureWrappedHeight(font, origin, width, TagBodyScale);
            if (power.Length > 0) {
                h += 6f + labelStep + OniRegisterRenderer.MeasureWrappedHeight(font, power, width, TagBodyScale);
            }
            if (burden.Length > 0) {
                h += 6f + labelStep + OniRegisterRenderer.MeasureWrappedHeight(font, burden, width, TagBodyScale);
            }
            return h + 16f;
        }

        /// <summary>
        /// 细节木牌:手裁板体(shader 木纹焦边优先)+系绳挂钉+烙印文字打字机,
        /// 金阶盖金签,除铭题绯红;它是挂在台边的一块荷札,不是浮空面板
        /// </summary>
        public static void DrawWoodTag(SpriteBatch sb, DynamicSpriteFont font, Rectangle rect,
            string title, string kindLabel, string origin, string power, string burden, bool gold, bool erase,
            int visibleChars, float burnFresh, float alpha, float time) {
            //板影:紧贴落影(大面板禁止同心扩层羽化)
            OniBrush.DrawPanelDropShadow(sb, rect.Center.ToVector2(),
                new Vector2(rect.Width, rect.Height), alpha * 0.85f, new Vector2(5f, 7f));

            //系绳:从穿绳孔上挑到台缘一枚钉,让牌"挂"在世界里
            Vector2 hole = new(rect.X + 14f, rect.Y + 12f);
            Vector2 nail = hole + new Vector2(-22f, -40f);
            float sway = (float)Math.Sin(time * 1.1f) * 1.6f;
            Vector2 mid = (hole + nail) * 0.5f + new Vector2(5f + sway, 6f);
            OniBrush.DrawGradientLine(sb, nail, mid, OnikiriUITheme.Deep * (alpha * 0.85f),
                OnikiriUITheme.Deep * (alpha * 0.7f), 1.5f);
            OniBrush.DrawGradientLine(sb, mid, hole, OnikiriUITheme.Deep * (alpha * 0.7f),
                OnikiriUITheme.Dark * (alpha * 0.85f), 1.5f);
            sb.Draw(Pixel, nail, PixelSrc, OnikiriUITheme.GoldDeep * (alpha * 0.95f), MathHelper.PiOver4,
                new Vector2(0.5f), new Vector2(4.2f), SpriteEffects.None, 0f);
            sb.Draw(Pixel, nail + new Vector2(-0.8f, -0.8f), PixelSrc, OnikiriUITheme.GoldInlay * (alpha * 0.6f),
                MathHelper.PiOver4, new Vector2(0.5f), new Vector2(1.8f), SpriteEffects.None, 0f);

            //板体:shader 手裁木板(木纹/焦边/缺角/绳孔),缺席退回简笔
            if (OniMeiStandDraw.Available) {
                Rectangle plank = rect;
                plank.Inflate(6, 6);
                OniMeiStandDraw.DrawWoodPlank(sb, plank, alpha, time);
            }
            else {
                //CPU 简笔:包边+板体+纵纹+绳孔
                sb.Draw(Pixel, new Rectangle(rect.X - 3, rect.Y - 3, rect.Width + 6, rect.Height + 6), PixelSrc,
                    OnikiriUITheme.Deep * (alpha * 0.5f));
                sb.Draw(Pixel, rect, PixelSrc, new Color(52, 18, 16) * (alpha * 0.97f));
                for (int i = 0; i < 5; i++) {
                    float u = 0.1f + Hash01(i * 61 + 3) * 0.8f;
                    sb.Draw(Pixel, new Vector2(rect.X + rect.Width * u, rect.Center.Y), PixelSrc,
                        OnikiriUITheme.Ink * (alpha * 0.28f), 0f, new Vector2(0.5f),
                        new Vector2(1f, rect.Height * 0.85f), SpriteEffects.None, 0f);
                }
                sb.Draw(Pixel, hole, PixelSrc, OnikiriUITheme.Ink * (alpha * 0.9f),
                    MathHelper.PiOver4, new Vector2(0.5f), new Vector2(4.4f), SpriteEffects.None, 0f);
            }

            float textLeft = rect.X + 28f;
            float headerRight = rect.Right - 16f;

            //题名(烙黑边白热字) + 右侧同行类目签与金签
            Color titleCol = erase ? OnikiriUITheme.Bright : OnikiriUITheme.HotWhite;
            Utils.DrawBorderString(sb, title, new Vector2(textLeft, rect.Y + 9f), titleCol * alpha, 1.05f);
            float rightX = headerRight;
            Vector2 kSize = font.MeasureString(kindLabel) * 0.72f;
            rightX -= kSize.X;
            Utils.DrawBorderString(sb, kindLabel, new Vector2(rightX, rect.Y + 13f),
                OnikiriUITheme.TextDim * alpha, 0.72f);
            if (gold) {
                string goldMark = OniMeiUI.GoldMark.Value;
                Vector2 gSize = font.MeasureString(goldMark) * 0.68f;
                rightX -= gSize.X + 10f;
                Utils.DrawBorderString(sb, goldMark, new Vector2(rightX, rect.Y + 14f),
                    OnikiriUITheme.GoldInlay * (alpha * 0.95f), 0.68f);
            }
            //题下一笔烙痕
            OniBrush.DrawTaperedSlash(sb, new Vector2(rect.X + 12f, rect.Y + 44f),
                new Vector2(rect.Right - 12f, rect.Y + 42f), 1.8f, 1.2f, alpha * 0.75f);

            //出处 + 赋效 + 代价,烙印打字机(最新字覆灼橙);凿前必见真实数值
            //排版口径与 MeasureTagHeight 严格一致,牌高即内容高
            float labelStep = TagLabelStep(font);
            float y = rect.Y + TagHeaderH;
            Utils.DrawBorderString(sb, OniMeiUI.OriginLabel.Value, new Vector2(textLeft, y),
                OnikiriUITheme.Deep * (alpha * 1.2f), TagLabelScale);
            y += labelStep;
            y = OniRegisterRenderer.DrawTypedWrapped(sb, font, origin, new Vector2(textLeft, y),
                headerRight - textLeft, OnikiriUITheme.TextDim, TagBodyScale, alpha, visibleChars, burnFresh,
                OnikiriUITheme.BurnHot);
            if (power.Length > 0 && visibleChars > origin.Length) {
                y += 6f;
                Utils.DrawBorderString(sb, OniMeiUI.PowerLabel.Value, new Vector2(textLeft, y),
                    OnikiriUITheme.Deep * (alpha * 1.2f), TagLabelScale);
                y += labelStep;
                Color powerCol = gold
                    ? Color.Lerp(OnikiriUITheme.Paper, OnikiriUITheme.GoldInlay, 0.4f)
                    : Color.Lerp(OnikiriUITheme.Paper, OnikiriUITheme.Bright, 0.28f);
                y = OniRegisterRenderer.DrawTypedWrapped(sb, font, power, new Vector2(textLeft, y),
                    headerRight - textLeft, powerCol, TagBodyScale, alpha, visibleChars - origin.Length, burnFresh,
                    OnikiriUITheme.BurnHot);
            }
            if (burden.Length > 0 && visibleChars > origin.Length + power.Length) {
                y += 6f;
                Utils.DrawBorderString(sb, OniMeiUI.BurdenLabel.Value, new Vector2(textLeft, y),
                    OnikiriUITheme.Seal * (alpha * 1.2f), TagLabelScale);
                y += labelStep;
                //代价用压暗绯红,与赋效的亮色分列可辨
                Color burdenCol = Color.Lerp(OnikiriUITheme.TextDim, OnikiriUITheme.Bright, 0.45f);
                OniRegisterRenderer.DrawTypedWrapped(sb, font, burden, new Vector2(textLeft, y),
                    headerRight - textLeft, burdenCol, TagBodyScale, alpha,
                    visibleChars - origin.Length - power.Length, burnFresh, OnikiriUITheme.BurnHot);
            }
        }

        //====================== 右缘刀铭大字 ======================

        /// <summary>竖排大字刀铭:charVis 0~1 按笔顺写入,fresh 新字带灼热;字脚金压线,底一枚朱印</summary>
        public static void DrawNameColumn(SpriteBatch sb, DynamicSpriteFont font, string name, Vector2 top,
            float alpha, float charVis, bool fresh, float time) {
            if (string.IsNullOrEmpty(name) || alpha <= 0.01f) {
                return;
            }
            float scale = OnikiriUITheme.MeiNameScale;

            //背衬:一道极淡的纵向朱丝栏
            float colH = OnikiriUITheme.UIScreenH * 0.52f;
            sb.Draw(Pixel, top + new Vector2(0f, colH * 0.5f - 20f), PixelSrc, OnikiriUITheme.Deep * (alpha * 0.22f),
                0f, new Vector2(0.5f), new Vector2(1.2f, colH), SpriteEffects.None, 0f);

            if (!OniBrush.ContainsCJK(name)) {
                //拉丁名:整串旋 90°
                Vector2 size = font.MeasureString(name) * scale;
                int visChars = Math.Max(1, (int)Math.Ceiling(name.Length * MathHelper.Clamp(charVis, 0f, 1f)));
                string shown = name[..Math.Min(visChars, name.Length)];
                Vector2 pos = new(top.X + size.Y * 0.5f, top.Y);
                sb.DrawString(font, shown, pos + new Vector2(1.5f, 1.5f), OnikiriUITheme.Ink * (alpha * 0.85f),
                    MathHelper.PiOver2, Vector2.Zero, scale, SpriteEffects.None, 0f);
                sb.DrawString(font, shown, pos, OnikiriUITheme.Paper * alpha,
                    MathHelper.PiOver2, Vector2.Zero, scale, SpriteEffects.None, 0f);
                return;
            }

            float charH = font.MeasureString("字").Y * scale + 8f;
            int total = name.Length;
            float visF = MathHelper.Clamp(charVis, 0f, 1f) * total;
            float y = top.Y;
            for (int i = 0; i < total; i++) {
                float charA = MathHelper.Clamp(visF - i, 0f, 1f);
                if (charA <= 0.01f) {
                    break;
                }
                string s = name[i].ToString();
                Vector2 size = font.MeasureString(s) * scale;
                Vector2 pos = new(top.X - size.X * 0.5f, y);
                bool newest = fresh && visF - i < 1.6f;
                Color col = newest
                    ? Color.Lerp(OnikiriUITheme.BurnHot, OnikiriUITheme.HotWhite, MathHelper.Clamp(visF - i - 0.6f, 0f, 1f))
                    : OnikiriUITheme.Paper;
                Utils.DrawBorderString(sb, s, pos, col * (alpha * charA), scale);
                //字脚金压线
                sb.Draw(Pixel, new Vector2(top.X, y + size.Y - 2f), PixelSrc,
                    OnikiriUITheme.GoldDeep * (alpha * charA * 0.55f), 0f, new Vector2(0.5f),
                    new Vector2(size.X * 0.72f, 1.2f), SpriteEffects.None, 0f);
                y += charH;
            }
            //名讳底一枚小朱印
            if (visF >= total) {
                OniBrush.DrawSealGlyph(sb, new Vector2(top.X, y + 10f), 10f, alpha * 0.9f, 0.05f);
            }
        }

        //====================== 静物 / 题字 / 页签 ======================

        /// <summary>台上小静物:鏨、砥石、丁子油瓶,伏在陈列刀柄下(baseP=静物基线中点)</summary>
        public static void DrawStillLife(SpriteBatch sb, Vector2 baseP, float alpha, float time) {

            //砥石:圆角矮块,上面浅色磨面
            sb.Draw(Pixel, baseP + new Vector2(2f, 3f), PixelSrc, new Color(8, 2, 5) * (alpha * 0.5f), 0.02f,
                new Vector2(0.5f), new Vector2(38f, 12f), SpriteEffects.None, 0f);
            sb.Draw(Pixel, baseP, PixelSrc, new Color(64, 52, 44) * (alpha * 0.95f), 0.02f,
                new Vector2(0.5f), new Vector2(38f, 12f), SpriteEffects.None, 0f);
            sb.Draw(Pixel, baseP - new Vector2(0f, 4f), PixelSrc, new Color(96, 82, 68) * (alpha * 0.8f), 0.02f,
                new Vector2(0.5f), new Vector2(36f, 3f), SpriteEffects.None, 0f);

            //鏨:斜倚在砥石旁,钢杆+暗柄+锋尖一点光
            Vector2 chiselC = baseP + new Vector2(44f, 1f);
            float cRot = -0.34f;
            sb.Draw(Pixel, chiselC, PixelSrc, OnikiriUITheme.TextDim * (alpha * 0.9f), cRot,
                new Vector2(0f, 0.5f), new Vector2(26f, 3f), SpriteEffects.None, 0f);
            sb.Draw(Pixel, chiselC + cRot.ToRotationVector2() * 18f, PixelSrc, OnikiriUITheme.Dark * (alpha * 0.95f), cRot,
                new Vector2(0f, 0.5f), new Vector2(9f, 4.4f), SpriteEffects.None, 0f);
            OniBrush.DrawSoftDot(sb, chiselC, 2.2f, OnikiriUITheme.HotWhite, alpha * 0.5f);

            //丁子油瓶:深琉璃小瓶+木塞+烛光高光
            Vector2 bottleC = baseP + new Vector2(-42f, -6f);
            sb.Draw(Pixel, bottleC + new Vector2(1.5f, 2f), PixelSrc, new Color(8, 2, 5) * (alpha * 0.5f), 0f,
                new Vector2(0.5f), new Vector2(11f, 15f), SpriteEffects.None, 0f);
            sb.Draw(Pixel, bottleC, PixelSrc, new Color(38, 14, 12) * (alpha * 0.96f), 0f,
                new Vector2(0.5f), new Vector2(11f, 15f), SpriteEffects.None, 0f);
            sb.Draw(Pixel, bottleC - new Vector2(0f, 10f), PixelSrc, new Color(38, 14, 12) * (alpha * 0.96f), 0f,
                new Vector2(0.5f), new Vector2(4.6f, 6f), SpriteEffects.None, 0f);
            sb.Draw(Pixel, bottleC - new Vector2(0f, 14f), PixelSrc, OnikiriUITheme.GoldDeep * (alpha * 0.9f), 0f,
                new Vector2(0.5f), new Vector2(4f, 3f), SpriteEffects.None, 0f);
            float glint = 0.5f + 0.3f * (float)Math.Sin(time * 1.3f);
            OniBrush.DrawSoftStreak(sb, bottleC + new Vector2(-2.6f, -3f), MathHelper.PiOver2, 7f, 1.4f,
                OnikiriUITheme.CandleWarm, alpha * 0.5f * glint, 0.6f);
        }

        //====================== 吊挂卷轴(回点鬼簿的门) ======================

        //卷轴微缩的 SVG 路径:归一 [-1,1],y 向下,旧单位 0..86 除以 43;
        //粗笔画当体(曲线实体)、细笔画作线、亮芯作光
        private const float ScrollN = 43f;
        private const string ScrollRodD = "M -0.349 -0.837 L 0.349 -0.837";
        private const string ScrollPaperD = "M 0 -0.72 C 0.016 -0.4 0.016 0.1 0 0.49";
        private const string ScrollPaperEdgeLD = "M -0.075 -0.72 C -0.059 -0.4 -0.059 0.1 -0.075 0.49";
        private const string ScrollPaperEdgeRD = "M 0.078 -0.72 C 0.094 -0.4 0.094 0.1 0.078 0.49";
        private const string ScrollLayersD =
            "M -0.145 -0.488 L 0.145 -0.488 M -0.15 -0.163 L 0.15 -0.163 M -0.14 0.163 L 0.14 0.163";
        private const string ScrollBandD = "M -0.18 -0.116 L 0.18 -0.116";
        /// <summary>地轴纸筒(以自身中心为原点,随预演整体下落)</summary>
        private const string ScrollRollD = "M -0.302 0 L 0.302 0";
        /// <summary>轴端纸卷截面涡(自己的 [-1,1] 小空间)</summary>
        private const string ScrollSpiralD =
            "M 0.62 0.05 C 0.62 -0.55 -0.6 -0.55 -0.6 0.05 C -0.6 0.48 0.3 0.48 0.3 0.08 C 0.3 -0.22 -0.24 -0.22 -0.24 0.02";

        /// <summary>
        /// 悬挂的收卷点鬼簿微缩:对面屏(纸面)的器物本体挂在梁钩下作切换门。
        /// SVG 曲线笔铺形(开屏按弧长自画),纸垂随风;Echo 鬼火漏缝(本屏唯一许可的青，簿那头在闹);
        /// Ceremony 地轴弹开一截瞥见名录
        /// </summary>
        public static void DrawHangingScroll(SpriteBatch sb, OniHangingSwitch sw, float alpha, float time, bool danger) {
            if (alpha <= 0.01f) {
                return;
            }
            sw.DrawRope(sb, alpha);

            float s = OnikiriUITheme.HangSwitchScale;
            float rot = sw.Rot;
            Vector2 top = sw.End;
            Vector2 down = (MathHelper.PiOver2 + rot).ToRotationVector2();
            Vector2 side = rot.ToRotationVector2();
            float a = alpha * (0.92f + sw.HoverEase * 0.08f);
            float lift = 1f + sw.HoverEase * 0.08f;
            float scale = ScrollN * s;
            Vector2 center = top + down * scale;
            Vector2 half = new(0.5f);
            Vector2 P(float y, float x = 0f) => top + down * (y * s) + side * (x * s);
            //开屏线稿揭示:门"自己画出来"
            float reveal = MathHelper.Clamp((alpha - 0.08f) / 0.72f, 0f, 1f);
            reveal = 1f - (1f - reveal) * (1f - reveal);

            SvgPath rod = SvgPathPen.Path(ScrollRodD);
            SvgPath paper = SvgPathPen.Path(ScrollPaperD);
            SvgPath edgeL = SvgPathPen.Path(ScrollPaperEdgeLD);
            SvgPath edgeR = SvgPathPen.Path(ScrollPaperEdgeRD);
            SvgPath layers = SvgPathPen.Path(ScrollLayersD);
            SvgPath band = SvgPathPen.Path(ScrollBandD);
            SvgPath roll = SvgPathPen.Path(ScrollRollD);
            SvgPath spiral = SvgPathPen.Path(ScrollSpiralD);

            //挂绪结
            sb.Draw(Pixel, top, PixelSrc, OnikiriUITheme.Seal * a, MathHelper.PiOver4 + rot * 0.4f,
                half, new Vector2(4.2f) * s, SpriteEffects.None, 0f);

            //纸身淡影(曲线实体的错位深笔,非方块)
            Vector2 shadowOff = new(1.5f * s, 2.2f * s);
            SvgPathPen.Stroke(sb, paper, center + shadowOff, scale, rot,
                new Color(8, 2, 5), 15f * s, a * 0.40f, 0f, reveal);

            //====天杆:曲线笔铺杆体+顶脊光,朱漆端帽仍是小方章====
            SvgPathPen.Stroke(sb, rod, center, scale, rot, OnikiriUITheme.Dark, 3.6f * s, a * 0.96f);
            SvgPathPen.Stroke(sb, rod, center - down * (1.1f * s), scale, rot,
                new Color(120, 52, 40), 1f * s, a * 0.6f);
            foreach (float x in new[] { -16f, 16f }) {
                sb.Draw(Pixel, P(7f, x), PixelSrc, OnikiriUITheme.Deep * (a * 0.95f), rot, half,
                    new Vector2(5f, 6f) * s, SpriteEffects.None, 0f);
                sb.Draw(Pixel, P(5.8f, x), PixelSrc, OnikiriUITheme.Bright * (a * 0.5f), rot, half,
                    new Vector2(1.6f) * s, SpriteEffects.None, 0f);
            }

            //====纸垂两条：挂在天杆上，危险状态时抖得更急====
            Vector2 rodTopC = P(7f);
            Rectangle shideRect = new((int)(rodTopC.X - 13f * s), (int)(rodTopC.Y + 1f * s), (int)(26f * s), (int)(6f * s));
            float shideTime = time * (danger ? 1.7f : 1f);
            OniBrush.DrawSingleShide(sb, shideRect, 0.10f, 12f * s, a * 0.95f, shideTime, 0.4f);
            OniBrush.DrawSingleShide(sb, shideRect, 0.90f, 13f * s, a * 0.9f, shideTime, 2.3f);

            //====纸身:微弓的垂纸,左缘承光右缘沉影,卷层三线,束带一匝====
            float c = sw.Ceremony01;
            float cEase = c * (2f - c);
            SvgPathPen.Stroke(sb, paper, center, scale, rot,
                OnikiriUITheme.Paper, 13f * s * lift, a * 0.62f, 0f, reveal);
            SvgPathPen.Stroke(sb, edgeL, center, scale, rot,
                OnikiriUITheme.Paper, 4.2f * s, a * 0.80f, 0f, reveal);
            SvgPathPen.Stroke(sb, edgeR, center, scale, rot,
                OnikiriUITheme.Paper, 2.4f * s, a * 0.40f, 0f, reveal);
            SvgPathPen.Stroke(sb, layers, center, scale, rot,
                OnikiriUITheme.TextDim, 1f * s, a * 0.30f, 0f, reveal);
            SvgPathPen.Stroke(sb, band, center, scale, rot,
                OnikiriUITheme.Deep, 2.6f * s, a * 0.9f, 0f, reveal);
            sb.Draw(Pixel, P(38f, 8f), PixelSrc, OnikiriUITheme.Deep * (a * 0.85f), rot + MathHelper.PiOver4,
                half, new Vector2(3f) * s, SpriteEffects.None, 0f);

            //====回声:鬼火自卷缝漏一丝(软焰,非硬条)====
            float echo = sw.Echo01;
            if (echo > 0.01f) {
                float pulse = MathF.Sin(echo * MathHelper.Pi);
                Vector2 seam = P(30f + echo * 14f, -6f);
                OniBrush.DrawSoftStreak(sb, seam - down * (2.5f * s * pulse), rot + MathHelper.PiOver2,
                    7f * s * pulse, 1.6f * s, OnikiriUITheme.GhostDim, a * 0.5f * pulse, 0.7f);
                OniBrush.DrawSoftDot(sb, seam, 3.2f * s * pulse, OnikiriUITheme.GhostFire, a * 0.7f * pulse);
            }

            //====地轴纸筒:预演时向下弹开,缝里瞥见名录====
            float dropY = 66f + cEase * 16f;
            if (cEase > 0.03f) {
                float gap = (dropY - 64f) * s;
                Vector2 gapC = P(64f + (dropY - 64f) * 0.5f);
                float flash = MathF.Sin(c * MathHelper.Pi);
                OniBrush.DrawBacklight(sb, gapC, 18f * s, OnikiriUITheme.GhostDim, a * 0.4f * flash);
                sb.Draw(Pixel, gapC, PixelSrc, OnikiriUITheme.Paper * (a * 0.85f), rot, half,
                    new Vector2(11f * s, gap), SpriteEffects.None, 0f);
                sb.Draw(Pixel, P(64f + (dropY - 64f) * 0.45f, -2.5f), PixelSrc, OnikiriUITheme.Ink * (a * 0.7f), rot, half,
                    new Vector2(1.2f * s, gap * 0.55f), SpriteEffects.None, 0f);
                sb.Draw(Pixel, P(64f + (dropY - 64f) * 0.55f, 2.5f), PixelSrc, OnikiriUITheme.Ink * (a * 0.6f), rot, half,
                    new Vector2(1.2f * s, gap * 0.4f), SpriteEffects.None, 0f);
            }
            //纸筒:粗笔滚圆(影/体/顶光/底沉),端头朱帽+纸卷截面涡
            Vector2 rollC = P(dropY);
            SvgPathPen.Stroke(sb, roll, rollC + shadowOff * 0.8f, scale, rot,
                new Color(8, 2, 5), 7.4f * s, a * 0.45f);
            SvgPathPen.Stroke(sb, roll, rollC, scale, rot,
                Color.Lerp(OnikiriUITheme.Paper, OnikiriUITheme.TextDim, 0.35f), 6.8f * s, a * 0.9f);
            SvgPathPen.Stroke(sb, roll, rollC - down * (2.1f * s), scale, rot,
                OnikiriUITheme.Paper, 1.4f * s, a * 0.75f);
            SvgPathPen.Stroke(sb, roll, rollC + down * (2.5f * s), scale, rot,
                OnikiriUITheme.Dark, 1.4f * s, a * 0.55f);
            foreach (float x in new[] { -14.2f, 14.2f }) {
                sb.Draw(Pixel, P(dropY, x), PixelSrc, OnikiriUITheme.Deep * (a * 0.95f), rot, half,
                    new Vector2(3.4f, 7.6f) * s, SpriteEffects.None, 0f);
            }
            foreach (float x in new[] { -11.4f, 11.4f }) {
                SvgPathPen.Stroke(sb, spiral, P(dropY, x), 3.4f * s, rot,
                    OnikiriUITheme.TextDim, 1.1f * s, a * 0.7f, 0f, reveal);
            }
        }

        //====================== 吊挂结印盘(去结印盘的门) ======================

        //盘微缩:实心盘体走 OniSigilBoard 同一支漆盘 shader(与工位材质同源),
        //细笔线稿/朱点/绯绪作前景;SVG 归一 [-1,1]
        /// <summary>吊坠盘半径(乘 HangSigilScale)</summary>
        private const float SigilPendR = 30f;
        /// <summary>盘缘:不闭合的一圈,缺口在右上(回声亮笔的跑道;CPU 回退时兼作缘线)</summary>
        private const string SigilRimD =
            "M 0.18 -0.965 C 0.62 -0.86 0.965 -0.5 0.965 0 C 0.965 0.533 0.533 0.965 0 0.965"
            + " C -0.533 0.965 -0.965 0.533 -0.965 0 C -0.965 -0.533 -0.533 -0.965 0 -0.965";
        /// <summary>六芒:两枚交叠正三角(尖端上/下)</summary>
        private const string SigilStarD =
            "M 0 -0.78 L 0.675 0.39 L -0.675 0.39 Z M 0 0.78 L -0.675 -0.39 L 0.675 -0.39 Z";
        /// <summary>内三角:三个结印位连成的骨</summary>
        private const string SigilTriD = "M 0 -0.42 L 0.364 0.21 L -0.364 0.21 Z";
        /// <summary>吊环:盘顶挂在钩上的那一枚铁环</summary>
        private const string SigilLoopD =
            "M -0.5 0.5 C -0.5 -0.5 0.5 -0.5 0.5 0.5";

        /// <summary>
        /// 悬挂的结印盘微缩:对面工位的器物挂在梁钩下作切换门。<br/>
        /// SVG 曲线笔铺形(开屏按弧长自画);盘上结了几印就亮几枚位印;
        /// Echo 盘缘走一道朱光(盘那头在动);Ceremony 盘轻旋半格再回正
        /// </summary>
        public static void DrawHangingSigil(SpriteBatch sb, OniHangingSwitch sw, float alpha,
            float time, bool danger, int boundCount) {
            if (alpha <= 0.01f) {
                return;
            }
            sw.DrawRope(sb, alpha);

            float s = OnikiriUITheme.HangSigilScale;
            float rot = sw.Rot;
            Vector2 top = sw.End;
            Vector2 down = (MathHelper.PiOver2 + rot).ToRotationVector2();
            float a = alpha * (0.92f + sw.HoverEase * 0.08f);
            float lift = 1f + sw.HoverEase * 0.06f;
            float discR = SigilPendR * s * lift;
            //吊环占顶上一小截,盘心再往下一个半径
            Vector2 loopC = top + down * (6f * s);
            Vector2 center = top + down * (discR + 13f * s);
            Vector2 half = new(0.5f);
            float reveal = MathHelper.Clamp((alpha - 0.08f) / 0.72f, 0f, 1f);
            reveal = 1f - (1f - reveal) * (1f - reveal);
            //预演:盘轻旋半格,像被手拨了一下
            float c = sw.Ceremony01;
            float spin = MathF.Sin(c * MathHelper.Pi) * 0.26f;
            float bodyRot = rot + spin;

            SvgPath rim = SvgPathPen.Path(SigilRimD);
            SvgPath star = SvgPathPen.Path(SigilStarD);
            SvgPath tri = SvgPathPen.Path(SigilTriD);
            SvgPath loop = SvgPathPen.Path(SigilLoopD);

            //挂绪结 + 铁吊环
            sb.Draw(Pixel, top, PixelSrc, OnikiriUITheme.Seal * a, MathHelper.PiOver4 + rot * 0.4f,
                half, new Vector2(3.6f) * s, SpriteEffects.None, 0f);
            SvgPathPen.Stroke(sb, loop, loopC, 6f * s, rot,
                Color.Lerp(OnikiriUITheme.Ink, Color.Black, 0.2f), 2f * s, a * 0.95f);

            //盘影:贴身错位实心影(不做同心放大)
            OniSigilRenderer.DrawFilledCircle(sb, center + new Vector2(1.6f, 2.4f) * s, discR,
                new Color(8, 2, 5) * (a * 0.42f * reveal));

            //盘体:与工位同一支漆盘 shader(材质同源);缺编退回实心漆底+缘线
            if (OniSigilBoardDraw.Available) {
                (Vector3 lit, Vector3 dangerV, float complete) = OniSigilBoardDraw.ReadSlotState();
                OniSigilBoardDraw.DrawDisc(sb, center, discR, discR * 0.70f, discR * 0.378f,
                    lit, dangerV, complete, a * reveal, time, OniSigilBoardDraw.BoardSeed, bodyRot);
            }
            else {
                OniSigilRenderer.DrawFilledCircle(sb, center, discR,
                    Color.Lerp(OnikiriUITheme.Ink, Color.Black, 0.30f) * (a * 0.96f * reveal));
                SvgPathPen.Stroke(sb, rim, center, discR, bodyRot,
                    Color.Lerp(OnikiriUITheme.Ink, Color.Black, 0.2f), 3f * s, a * 0.95f, 0f, reveal);
                OniSigilRenderer.DrawRing(sb, center, discR * 0.94f, 1f,
                    OnikiriUITheme.GoldDeep * (a * 0.5f * reveal), 40);
                OniSigilRenderer.DrawRing(sb, center, discR * 0.90f, 1f,
                    OnikiriUITheme.Deep * (a * 0.32f * reveal), 40);
            }

            //六芒与内三角:细笔线稿压在盘面上(与工位同构),盘上结了印内三角才通着
            SvgPathPen.Stroke(sb, star, center, discR * 0.90f, bodyRot,
                OnikiriUITheme.Deep, 1.3f * s, a * 0.60f, 0f, reveal);
            float triAlpha = boundCount >= 3 ? 0.95f : 0.30f + boundCount * 0.18f;
            SvgPathPen.Stroke(sb, tri, center, discR * 0.90f, bodyRot,
                boundCount >= 3 ? OnikiriUITheme.Bright : OnikiriUITheme.Dark,
                1.4f * s, a * triAlpha, 0f, reveal);

            //已结印的位:三枚朱点按盘上实数点亮(位随摆角转,与眠焰同锚)
            for (int i = 0; i < 3; i++) {
                float ang = -MathHelper.PiOver2 + MathHelper.TwoPi * i / 3f + bodyRot;
                Vector2 p = center + ang.ToRotationVector2() * (discR * 0.378f);
                bool litDot = i < boundCount;
                OniBrush.DrawSoftDot(sb, p, (litDot ? 2.8f : 1.7f) * s,
                    litDot ? OnikiriUITheme.Seal : OnikiriUITheme.Dark, a * (litDot ? 0.85f : 0.4f));
            }
            //盘心:满盘才点合鬼印
            if (boundCount >= 3) {
                float breath = 0.5f + 0.5f * MathF.Sin(time * 1.8f);
                OniBrush.DrawBacklight(sb, center, 11f * s, OnikiriUITheme.Bright,
                    a * (0.16f + breath * 0.12f));
                OniBrush.DrawSealGlyph(sb, center, 5.2f * s, a * 0.9f, time * 0.1f);
            }

            //绯绪:盘底垂一束穗,随摆与风轻荡
            Vector2 tasselTop = center + (MathHelper.PiOver2 + bodyRot).ToRotationVector2() * discR;
            float tasselSway = MathF.Sin(time * 1.6f) * 0.12f + rot * 0.6f;
            Vector2 knot = tasselTop + (MathHelper.PiOver2 + tasselSway).ToRotationVector2() * (3f * s);
            sb.Draw(Pixel, knot, PixelSrc, OnikiriUITheme.Seal * (a * reveal),
                MathHelper.PiOver4 + tasselSway * 0.5f, half, new Vector2(2.8f * s),
                SpriteEffects.None, 0f);
            for (int i = -1; i <= 1; i++) {
                float fr = MathHelper.PiOver2 + tasselSway + i * 0.11f
                    + MathF.Sin(time * 2.1f + i * 1.7f) * 0.05f;
                float len = (12f + (i == 0 ? 3f : 0f)) * s;
                OniBrush.DrawGradientLine(sb, knot, knot + fr.ToRotationVector2() * len,
                    OnikiriUITheme.Deep * (a * 0.85f * reveal),
                    OnikiriUITheme.Dark * (a * 0.15f), 1.2f * s);
            }

            //悬停:缘上一线金亮起,读得出"这扇门应手"
            if (sw.HoverEase > 0.03f) {
                OniSigilRenderer.DrawRing(sb, center, discR * 1.04f, 1.1f,
                    OnikiriUITheme.GoldInlay * (a * 0.45f * sw.HoverEase), 44);
            }

            //====回声:盘缘走一道朱光(将醒时更急)====
            float echo = sw.Echo01;
            if (echo > 0.01f) {
                float pulse = MathF.Sin(echo * MathHelper.Pi);
                SvgPathPen.StrokeRunner(sb, rim, center, discR, bodyRot,
                    danger ? OnikiriUITheme.Bright : OnikiriUITheme.Seal, 2.2f * s,
                    a * 0.6f * pulse, echo, 0.18f, OnikiriUITheme.HotWhite);
            }
        }

        /// <summary>切换门悬浮说明:小裱墨牌(跟随光标),题名+移步提示;两屏共用</summary>
        public static void DrawSwitchHoverTag(SpriteBatch sb, Vector2 mouse,
            string title, string hint, float alpha) {
            OniTooltipPanel.Draw(sb, mouse, title, 0.78f, alpha,
                new OniTooltipLine(hint, OnikiriUITheme.TextDim));
        }

        //====================== 仪式工具 ======================

        /// <summary>鏨具:钢杆斜压在笔锋上,随击震颤;pose 0~1 入位</summary>
        public static void DrawChiselTool(SpriteBatch sb, Vector2 tip, float pose, Vector2 shake, float time) {
            float a = pose;
            //入位:自上方落到位
            Vector2 tipDraw = tip + new Vector2(0f, -26f * (1f - pose * (2f - pose)));
            float rot = -1.02f;
            Vector2 shaft = rot.ToRotationVector2();

            //杆影/杆体/杆脊光
            sb.Draw(Pixel, tipDraw + new Vector2(1.5f, 2f), PixelSrc, new Color(8, 2, 5) * (a * 0.5f), rot,
                new Vector2(0f, 0.5f), new Vector2(38f, 4.6f), SpriteEffects.None, 0f);
            sb.Draw(Pixel, tipDraw, PixelSrc, OnikiriUITheme.TextDim * (a * 0.95f), rot,
                new Vector2(0f, 0.5f), new Vector2(38f, 4.2f), SpriteEffects.None, 0f);
            sb.Draw(Pixel, tipDraw + new Vector2(-0.8f, -1.2f), PixelSrc, OnikiriUITheme.Paper * (a * 0.45f), rot,
                new Vector2(0f, 0.5f), new Vector2(34f, 1.2f), SpriteEffects.None, 0f);
            //杆尾铜箍(受锤的一端)
            sb.Draw(Pixel, tipDraw + shaft * 36f, PixelSrc, OnikiriUITheme.GoldDeep * (a * 0.95f), rot,
                new Vector2(0.5f), new Vector2(6f, 7f), SpriteEffects.None, 0f);
            //锋尖一点白(软辉)
            OniBrush.DrawSoftDot(sb, tipDraw, 2.4f + shake.Length() * 0.7f, OnikiriUITheme.HotWhite, a * 0.8f);
        }

        /// <summary>锉刀:横杆在字形上往复,t 0~1 锉程</summary>
        public static void DrawFileTool(SpriteBatch sb, Vector2 center, float size, float t, float alpha, float time) {
            float a = alpha * MathHelper.Clamp(t / 0.15f, 0f, 1f) * MathHelper.Clamp((1f - t) / 0.1f + 0.4f, 0f, 1f);
            float sweep = (float)Math.Sin(time * 13f) * size * 0.4f;
            Vector2 pos = center + new Vector2(sweep, -size * 0.18f);
            float rot = 0.05f;
            sb.Draw(Pixel, pos + new Vector2(1.5f, 2f), PixelSrc, new Color(8, 2, 5) * (a * 0.5f), rot,
                new Vector2(0.5f), new Vector2(size * 1.15f, 6f), SpriteEffects.None, 0f);
            sb.Draw(Pixel, pos, PixelSrc, OnikiriUITheme.Dark * (a * 0.96f), rot,
                new Vector2(0.5f), new Vector2(size * 1.15f, 5.4f), SpriteEffects.None, 0f);
            OniBrush.DrawSoftStreak(sb, pos - new Vector2(0f, 2.4f), rot, size * 1.1f, 1.2f,
                OnikiriUITheme.TextDim, a * 0.55f, 0.25f);
            //柄头
            sb.Draw(Pixel, pos + rot.ToRotationVector2() * (size * 0.62f), PixelSrc, OnikiriUITheme.Deep * (a * 0.9f), rot,
                new Vector2(0.5f), new Vector2(9f, 6.5f), SpriteEffects.None, 0f);
        }
    }
}
