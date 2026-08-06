using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using CalamityOverhaul.Content.UIs.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>点鬼簿静态绘制,卷轴/轴杆/绯月/名录/细节板</summary>
    internal static class OniRegisterRenderer
    {
        private static Texture2D Pixel => VaultAsset.placeholder2.Value;
        private static readonly Rectangle PixelSrc = new(0, 0, 1, 1);

        //====================== 卷轴纸体 ======================

        /// <summary>卷轴纸体 OniGhostScroll.fx,缺则 CPU</summary>
        public static void DrawScroll(SpriteBatch sb, Rectangle rect, float alpha, float reveal, float time) {
            //阴影按 alpha 平方衰减,展卷初期不出现整块暗影
            sb.Draw(Pixel, new Rectangle(rect.X + 6, rect.Y + 8, rect.Width, (int)(rect.Height * reveal)), PixelSrc,
                new Color(8, 2, 5) * (alpha * alpha * 0.6f));

            Effect effect = EffectLoader.OniGhostScroll?.Value;
            if (effect == null) {
                DrawFallbackScroll(sb, rect, alpha, reveal);
                return;
            }

            Rectangle extRect = rect;
            extRect.Inflate(OnikiriUITheme.ScrollEdgePad, OnikiriUITheme.ScrollEdgePad);

            float body = Math.Min(1f, alpha * 1.6f);
            effect.Parameters["uTime"]?.SetValue(time);
            effect.Parameters["uAlpha"]?.SetValue(body);
            effect.Parameters["uReveal"]?.SetValue(reveal);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(extRect.Width, extRect.Height));
            effect.Parameters["uEdgePad"]?.SetValue((float)OnikiriUITheme.ScrollEdgePad);
            effect.Parameters["uColHot"]?.SetValue(CrimsonSlashRenderer.ColHot);
            effect.Parameters["uColBright"]?.SetValue(CrimsonSlashRenderer.ColBright);
            effect.Parameters["uColDeep"]?.SetValue(CrimsonSlashRenderer.ColDeep);
            effect.Parameters["uColDark"]?.SetValue(CrimsonSlashRenderer.ColDark);

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.AnisotropicClamp, DepthStencilState.None,
                RasterizerState.CullNone, effect, Main.UIScaleMatrix);

            sb.Draw(Pixel, extRect, Color.White);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.PointClamp, DepthStencilState.None,
                RasterizerState.CullNone, null, Main.UIScaleMatrix);
        }

        /// <summary>CPU 降级卷轴</summary>
        public static void DrawFallbackScroll(SpriteBatch sb, Rectangle rect, float alpha, float reveal) {
            float unroll = reveal * (2f - reveal);
            Rectangle shown = new(rect.X, rect.Y, rect.Width, (int)(rect.Height * unroll));
            if (shown.Height < 4) {
                return;
            }
            sb.Draw(Pixel, shown, PixelSrc, OnikiriUITheme.Ink * (alpha * 0.96f));
            DrawRectBorder(sb, shown, OnikiriUITheme.Deep * (alpha * 0.58f), 2);
            Rectangle inner = shown;
            inner.Inflate(-5, -5);
            if (inner.Height > 8) {
                DrawRectBorder(sb, inner, OnikiriUITheme.Dark * (alpha * 0.85f), 1);
            }
            //上下绫带
            sb.Draw(Pixel, new Rectangle(shown.X, shown.Y, shown.Width, 14), PixelSrc, OnikiriUITheme.Dark * (alpha * 0.9f));
            if (unroll > 0.95f) {
                sb.Draw(Pixel, new Rectangle(shown.X, shown.Bottom - 14, shown.Width, 14), PixelSrc, OnikiriUITheme.Dark * (alpha * 0.9f));
            }
        }

        /// <summary>卷轴杆,顶定悬底随展卷下行</summary>
        public static void DrawRollers(SpriteBatch sb, Rectangle rect, float alpha, float reveal) {
            float unroll = reveal * (2f - reveal);
            int overhang = 20;

            //顶杆挂绳:两端向上渐隐,像挂在更高处的看不见的梁上
            Vector2 ropeL = new(rect.X + 26f, rect.Y - 7f);
            Vector2 ropeR = new(rect.Right - 26f, rect.Y - 7f);
            OniBrush.DrawGradientLine(sb, ropeL, ropeL - new Vector2(6f, 58f), OnikiriUITheme.Deep * (alpha * 0.7f), OnikiriUITheme.Dark * 0f, 1.4f);
            OniBrush.DrawGradientLine(sb, ropeR, ropeR - new Vector2(-6f, 58f), OnikiriUITheme.Deep * (alpha * 0.7f), OnikiriUITheme.Dark * 0f, 1.4f);

            DrawRoller(sb, rect.X - overhang, rect.Right + overhang, rect.Y - 7f, alpha);

            //底杆:行至展卷前沿,收尾停在卷底之下
            float frontier = rect.Y + unroll * (rect.Height + OnikiriUITheme.ScrollEdgePad + 8f);
            float rodY = Math.Min(frontier + 3f, rect.Bottom + 9f);
            DrawRoller(sb, rect.X - overhang, rect.Right + overhang, rodY, alpha);
        }

        /// <summary>单根轴杆</summary>
        private static void DrawRoller(SpriteBatch sb, float left, float right, float y, float alpha) {
            int w = (int)(right - left);
            //杆身三段明暗:上缘暗/中亮/下缘暗,卖圆柱感
            sb.Draw(Pixel, new Rectangle((int)left, (int)(y - 5f), w, 10), PixelSrc, OnikiriUITheme.Dark * (alpha * 0.95f));
            sb.Draw(Pixel, new Rectangle((int)left, (int)(y - 3f), w, 4), PixelSrc, new Color(74, 26, 22) * (alpha * 0.9f));
            sb.Draw(Pixel, new Rectangle((int)left, (int)(y - 2f), w, 1), PixelSrc, new Color(120, 52, 40) * (alpha * 0.7f));
            //端帽:朱漆小方帽 + 高光点
            foreach (float capX in new[] { left, right - 6f }) {
                sb.Draw(Pixel, new Rectangle((int)capX, (int)(y - 6f), 6, 12), PixelSrc, OnikiriUITheme.Deep * (alpha * 0.95f));
                sb.Draw(Pixel, new Rectangle((int)capX + 1, (int)(y - 4f), 2, 2), PixelSrc, OnikiriUITheme.Bright * (alpha * 0.55f));
            }
        }

        /// <summary>收卷木牌,点击关闭,牌绳 Verlet;text 缺省取点鬼簿收卷文案(改铭台传「纳刀」)</summary>
        public static void DrawCloseTag(SpriteBatch sb, DynamicSpriteFont font, OniRope rope, float alpha, float hover, float time, string text = null) {
            //绳与顶结
            rope.Draw(sb, OnikiriUITheme.Deep * 0.9f, OnikiriUITheme.Deep * 0.62f, 1.3f, alpha);
            sb.Draw(Pixel, rope[0], PixelSrc, OnikiriUITheme.Seal * (alpha * 0.9f), MathHelper.PiOver4, new Vector2(0.5f), new Vector2(3.8f), SpriteEffects.None, 0f);

            //牌体姿态由绳末段方向决定,hover 叠一丝高频轻颤
            Vector2 tagTop = rope.End;
            float rot = rope.EndRotation - MathHelper.PiOver2 + hover * (float)Math.Sin(time * 14f) * 0.015f;
            Vector2 down = (MathHelper.PiOver2 + rot).ToRotationVector2();
            Vector2 side = rot.ToRotationVector2();

            Vector2 tagSize = new(26f, 40f);
            Vector2 tagCenter = tagTop + down * (tagSize.Y * 0.5f);

            //牌体:漆木底 + 深红包边 + 顶部穿绳孔
            float lift = 1f + hover * 0.06f;
            Vector2 half = new(0.5f);
            sb.Draw(Pixel, tagCenter + new Vector2(1.4f, 1.8f), PixelSrc, new Color(8, 2, 5) * (alpha * 0.55f), rot, half, tagSize * lift, SpriteEffects.None, 0f);
            sb.Draw(Pixel, tagCenter, PixelSrc, OnikiriUITheme.Deep * (alpha * (0.55f + hover * 0.35f)), rot, half, (tagSize + new Vector2(3f, 3f)) * lift, SpriteEffects.None, 0f);
            sb.Draw(Pixel, tagCenter, PixelSrc, Color.Lerp(new Color(52, 18, 16), new Color(74, 26, 22), hover) * (alpha * 0.96f), rot, half, tagSize * lift, SpriteEffects.None, 0f);
            //木纹:两道极淡的纵向暗纹
            sb.Draw(Pixel, tagCenter - side * 5f, PixelSrc, OnikiriUITheme.Ink * (alpha * 0.3f), rot, half, new Vector2(1f, tagSize.Y * 0.8f) * lift, SpriteEffects.None, 0f);
            sb.Draw(Pixel, tagCenter + side * 6f, PixelSrc, OnikiriUITheme.Ink * (alpha * 0.22f), rot, half, new Vector2(1f, tagSize.Y * 0.7f) * lift, SpriteEffects.None, 0f);
            //穿绳孔
            sb.Draw(Pixel, tagTop + down * 4f, PixelSrc, OnikiriUITheme.Ink * (alpha * 0.9f), rot, half, new Vector2(3f), SpriteEffects.None, 0f);

            //牌文:CJK 逐字竖排 / 拉丁旋转 90°
            text ??= OniRegisterUI.CloseTagText.Value;
            Color textCol = Color.Lerp(OnikiriUITheme.Paper, OnikiriUITheme.HotWhite, hover) * (alpha * (0.8f + hover * 0.2f));
            const float Scale = 0.72f;
            if (OniBrush.ContainsCJK(text)) {
                float charH = font.MeasureString("字").Y * Scale + 1f;
                float totalH = charH * text.Length - 1f;
                Vector2 pen = tagCenter - down * (totalH * 0.5f - charH * 0.28f);
                foreach (char c in text) {
                    string s = c.ToString();
                    Vector2 size = font.MeasureString(s) * Scale;
                    Vector2 pos = pen - side * (size.X * 0.5f) - down * (size.Y * 0.35f);
                    Utils.DrawBorderString(sb, s, pos, textCol, Scale);
                    pen += down * charH;
                }
            }
            else {
                Vector2 size = font.MeasureString(text) * Scale;
                Vector2 pos = tagCenter + side * (size.Y * 0.34f) - down * (size.X * 0.5f);
                sb.DrawString(font, text, pos + new Vector2(1f, 1f), OnikiriUITheme.Ink * (alpha * 0.8f), MathHelper.PiOver2 + rot, Vector2.Zero, Scale, SpriteEffects.None, 0f);
                sb.DrawString(font, text, pos, textCol, MathHelper.PiOver2 + rot, Vector2.Zero, Scale, SpriteEffects.None, 0f);
            }
        }

        //====================== 吊挂太刀(去改铭台的门) ======================

        /// <summary>
        /// 悬挂的鞘中太刀微缩:对面屏(改铭台)的器物本体挂在梁下作切换门。
        /// 荷札书今名;Echo 金光巡鞘(金只许从这件东西上泄进纸面屏);Ceremony 半拔白光
        /// </summary>
        public static void DrawHangingTachi(SpriteBatch sb, DynamicSpriteFont font, OniHangingSwitch sw,
            float alpha, float time, string bladeName) {
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
            Vector2 half = new(0.5f);
            Vector2 P(float y, float x = 0f) => top + down * (y * s) + side * (x * s);
            Vector2 Sz(float w, float h) => new Vector2(w, h) * s;

            //挂绪结
            sb.Draw(Pixel, top, PixelSrc, OnikiriUITheme.Seal * a, MathHelper.PiOver4 + rot * 0.4f,
                half, Sz(4.2f, 4.2f), SpriteEffects.None, 0f);

            //整刀淡影
            sb.Draw(Pixel, P(52f) + new Vector2(1.5f, 2.2f) * s, PixelSrc, new Color(8, 2, 5) * (a * 0.45f),
                rot, half, Sz(10f, 96f), SpriteEffects.None, 0f);

            //预演半拔:柄镡整体上提,镡下露一线钢
            float c = sw.Ceremony01;
            float cEase = c * (2f - c);
            float drawOff = -cEase * 9f;

            //====柄:漆木+菱巻+柄头====
            sb.Draw(Pixel, P(19f + drawOff), PixelSrc, OnikiriUITheme.Dark * (a * 0.96f), rot, half,
                Sz(7.5f, 28f) * lift, SpriteEffects.None, 0f);
            sb.Draw(Pixel, P(5.5f + drawOff), PixelSrc, OnikiriUITheme.Deep * (a * 0.95f), rot, half,
                Sz(8.5f, 4f), SpriteEffects.None, 0f);
            for (int i = 0; i < 4; i++) {
                Vector2 p = P(10f + i * 6.2f + drawOff, (i % 2 == 0 ? 1f : -1f) * 1.2f);
                sb.Draw(Pixel, p, PixelSrc, OnikiriUITheme.Deep * (a * 0.9f), rot + MathHelper.PiOver4,
                    half, Sz(3.4f, 3.4f), SpriteEffects.None, 0f);
            }

            //====镡====
            Vector2 tsubaC = P(34f + drawOff);
            sb.Draw(Pixel, tsubaC, PixelSrc, OnikiriUITheme.Ink * (a * 0.98f), rot, half,
                Sz(15f, 3.6f), SpriteEffects.None, 0f);
            sb.Draw(Pixel, tsubaC, PixelSrc, OnikiriUITheme.Deep * (a * 0.8f), rot, half,
                Sz(11f, 2.2f), SpriteEffects.None, 0f);

            //半拔露出的钢
            if (cEase > 0.03f) {
                float steelLen = 9f * cEase * s;
                Vector2 steelC = P(37f + drawOff + 9f * cEase * 0.5f);
                sb.Draw(Pixel, steelC, PixelSrc, OnikiriUITheme.Paper * (a * 0.9f), rot, half,
                    new Vector2(5.5f * s, steelLen), SpriteEffects.None, 0f);
                sb.Draw(Pixel, steelC - side * (2.2f * s), PixelSrc, OnikiriUITheme.HotWhite * a, rot, half,
                    new Vector2(1.2f * s, steelLen), SpriteEffects.None, 0f);
            }

            //====鞘:黑漆+缘光+缓移漆光+下绪两匝+鞘尾金====
            Color lacq = Color.Lerp(OnikiriUITheme.Ink, OnikiriUITheme.Deep, 0.12f);
            Vector2 sayaC = P(68f);
            sb.Draw(Pixel, sayaC, PixelSrc, lacq * (a * 0.97f), rot, half,
                Sz(9f, 58f) * lift, SpriteEffects.None, 0f);
            sb.Draw(Pixel, sayaC - side * (4f * s), PixelSrc, OnikiriUITheme.Paper * (a * 0.18f), rot, half,
                Sz(1.1f, 56f), SpriteEffects.None, 0f);
            float sheenT = time * 0.09f - MathF.Floor(time * 0.09f);
            sb.Draw(Pixel, P(MathHelper.Lerp(42f, 92f, sheenT)), PixelSrc,
                OnikiriUITheme.Paper * (a * 0.10f * MathF.Sin(sheenT * MathHelper.Pi)), rot, half,
                Sz(7f, 4f), SpriteEffects.None, 0f);
            foreach (float y in new[] { 54f, 62f }) {
                sb.Draw(Pixel, P(y), PixelSrc, OnikiriUITheme.Deep * (a * 0.9f), rot, half,
                    Sz(10.5f, 2.6f), SpriteEffects.None, 0f);
            }
            sb.Draw(Pixel, P(58f, 6f), PixelSrc, OnikiriUITheme.Deep * (a * 0.85f), rot + MathHelper.PiOver4,
                half, Sz(3f, 3f), SpriteEffects.None, 0f);
            sb.Draw(Pixel, P(95f), PixelSrc, OnikiriUITheme.GoldDeep * (a * 0.95f), rot, half,
                Sz(9.5f, 4.5f), SpriteEffects.None, 0f);
            sb.Draw(Pixel, P(92.5f), PixelSrc, OnikiriUITheme.GoldInlay * (a * 0.7f), rot, half,
                Sz(9.5f, 1.1f), SpriteEffects.None, 0f);

            //====回声:金光巡鞘,那头的台在喘气====
            float echo = sw.Echo01;
            if (echo > 0.01f) {
                float pulse = MathF.Sin(echo * MathHelper.Pi);
                Vector2 gp = P(MathHelper.Lerp(40f, 92f, echo));
                sb.Draw(Pixel, gp, PixelSrc, OnikiriUITheme.GoldInlay * (a * 0.65f * pulse), rot, half,
                    Sz(9.5f, 1.6f), SpriteEffects.None, 0f);
                sb.Draw(Pixel, gp, PixelSrc, OnikiriUITheme.HotWhite * (a * 0.35f * pulse), rot, half,
                    Sz(5f, 1f), SpriteEffects.None, 0f);
            }

            //====预演白闪:沿鞘一线====
            if (c > 0.02f && c < 0.999f) {
                float flash = MathF.Sin(c * MathHelper.Pi);
                sb.Draw(Pixel, sayaC - side * (4f * s), PixelSrc, OnikiriUITheme.HotWhite * (a * 0.55f * flash),
                    rot, half, Sz(1.6f, 60f), SpriteEffects.None, 0f);
            }

            //====荷札:系在镡侧,书今名(拉丁名落朱印)====
            Vector2 tagAnchor = P(36f + drawOff, 8f);
            float tagRot = rot * 1.3f + 0.16f + MathF.Sin(time * 1.2f) * 0.05f;
            Vector2 tagDown = (MathHelper.PiOver2 + tagRot).ToRotationVector2();
            Vector2 tagTop = tagAnchor + tagDown * (4f * s);
            OniBrush.DrawGradientLine(sb, tagAnchor, tagTop, OnikiriUITheme.Deep * (a * 0.8f),
                OnikiriUITheme.Deep * (a * 0.5f), 1f * s);
            OniBrush.DrawPaperStrip(sb, tagTop, tagRot, Sz(13f, 30f), a * 0.95f, time * 0.06f);
            float nameScale = 0.52f * MathHelper.Lerp(1f, s, 0.55f);
            if (!string.IsNullOrEmpty(bladeName) && OniBrush.ContainsCJK(bladeName)) {
                float charH = font.MeasureString("字").Y * nameScale + 0.5f;
                Vector2 pen = tagTop + tagDown * (5f * s);
                int shown = 0;
                foreach (char chr in bladeName) {
                    if (shown++ >= 3) {
                        break;
                    }
                    string str = chr.ToString();
                    Vector2 size = font.MeasureString(str) * nameScale;
                    Utils.DrawBorderString(sb, str, pen - new Vector2(size.X * 0.5f, 0f),
                        OnikiriUITheme.Paper * (a * 0.9f), nameScale);
                    pen += tagDown * charH;
                }
            }
            else {
                OniBrush.DrawSealGlyph(sb, tagTop + tagDown * (10f * s), 5f * s, a * 0.9f, tagRot);
            }
        }

        //====================== 绯月 ======================

        /// <summary>绯月,危态竖瞳</summary>
        public static void DrawMoon(SpriteBatch sb, Vector2 center, float alpha, float time, float pupilOpen) {
            float breath = (float)Math.Sin(time * 0.5f) * 0.5f + 0.5f;
            OniBrush.DrawBacklight(sb, center, 96f + breath * 8f, OnikiriUITheme.Deep, alpha * 0.7f);
            OniBrush.DrawBacklight(sb, center, 46f, OnikiriUITheme.Bright, alpha * (0.5f + breath * 0.16f));
            OniBrush.DrawBacklight(sb, center, 22f, OnikiriUITheme.HotWhite, alpha * 0.34f);

            //竖瞳:月心一道收尖的墨色纵痕,睁开时两侧泛起一圈绯边
            if (pupilOpen > 0.02f) {
                float h = 30f * pupilOpen;
                sb.Draw(Pixel, center, PixelSrc, OnikiriUITheme.Ink * (alpha * 0.9f * pupilOpen), 0f,
                    new Vector2(0.5f), new Vector2(3.2f, h), SpriteEffects.None, 0f);
                sb.Draw(Pixel, center, PixelSrc, OnikiriUITheme.Ink * (alpha * 0.55f * pupilOpen), 0f,
                    new Vector2(0.5f), new Vector2(1.4f, h * 1.25f), SpriteEffects.None, 0f);
                OniBrush.DrawBacklight(sb, center, 14f, OnikiriUITheme.Bright, alpha * 0.5f * pupilOpen);
            }
        }

        //====================== 名录竖列 ======================

        /// <summary>名录竖列。</summary>
        public static void DrawEntryColumn(SpriteBatch sb, DynamicSpriteFont font, OniGhostEntry entry,
            Rectangle rect, float alpha, float hover, bool selected, bool equipped, float selectEase, float time, int index) {
            //界栏:名册的竖行朱丝栏
            sb.Draw(Pixel, new Rectangle(rect.Right + (int)(OnikiriUITheme.EntryColumnGap * 0.5f), rect.Y - 8, 1, rect.Height + 16), PixelSrc,
                OnikiriUITheme.Deep * (alpha * 0.20f));

            //选中/悬停底衬:一条暗色竖带
            float band = Math.Max(hover * 0.30f, selected ? 0.48f : 0f);
            if (band > 0.01f) {
                Rectangle bandRect = rect;
                bandRect.Inflate(3, 6);
                sb.Draw(Pixel, bandRect, PixelSrc, OnikiriUITheme.Dark * (alpha * band));
            }
            //选中扫线:左缘一笔自上而下的刀痕
            if (selected && selectEase > 0.02f) {
                OniBrush.DrawTaperedSlash(sb,
                    new Vector2(rect.X - 5f, rect.Y - 2f), new Vector2(rect.X - 5f, rect.Bottom + 2f),
                    2.0f, 1.4f, alpha * 0.9f, selectEase);
            }
            if (equipped) {
                Vector2 mark = new(rect.Center.X, rect.Y - 17f);
                OniBrush.DrawSealGlyph(sb, mark, 8f, alpha * 0.95f, time * 0.018f);
                OniBrush.DrawGradientLine(sb, mark + new Vector2(0f, 7f), new Vector2(rect.Center.X, rect.Y + 7f)
                    , OnikiriUITheme.Bright * (alpha * 0.72f), OnikiriUITheme.Deep * (alpha * 0.12f), 1.2f);
            }

            switch (entry.State) {
                case OniGhostState.Archive:
                    DrawNameColumn(sb, font, entry.Name(), rect,
                        OnikiriUITheme.TextDim * (alpha * (0.42f + hover * 0.18f)), alpha, 0f, time, index);
                    OniBrush.DrawSealGlyph(sb, new Vector2(rect.Center.X, rect.Bottom + 12f), 7f,
                        alpha * (0.35f + hover * 0.22f), 0.02f);
                    return;
                case OniGhostState.Dormant: {
                    float lift = hover * 0.25f + (selected ? 0.15f : 0f);
                    DrawNameColumn(sb, font, entry.Name(), rect, Color.Lerp(OnikiriUITheme.Paper, OnikiriUITheme.Bright, 0.16f) * (alpha * (0.75f + lift)), alpha, 1f, time, index);
                    OniBrush.DrawSealGlyph(sb, new Vector2(rect.Center.X, rect.Bottom + 16f), 10f, alpha * 0.9f, 0.04f,
                        MathHelper.Clamp(entry.Mastery + 0.3f, 0f, 1f));
                    return;
                }
                default: {
                    float lift = hover * 0.25f + (selected ? 0.15f : 0f);
                    DrawNameColumn(sb, font, entry.Name(), rect, OnikiriUITheme.Paper * (alpha * (0.78f + lift)), alpha, 0f, time, index);
                    OniBrush.DrawSealGlyph(sb, new Vector2(rect.Center.X, rect.Bottom + 16f), 10f, alpha * 0.9f, -0.03f);
                    return;
                }
            }
        }

        /// <summary>竖列名讳,bleed&gt;0 洇血,拉丁转 90°</summary>
        private static void DrawNameColumn(SpriteBatch sb, DynamicSpriteFont font, string name, Rectangle rect,
            Color color, float alpha, float bleed, float time, int index) {
            if (string.IsNullOrEmpty(name)) {
                return;
            }
            const float Scale = 1.02f;

            if (!OniBrush.ContainsCJK(name)) {
                //拉丁名:整串旋转 90°,自上而下
                Vector2 size = font.MeasureString(name) * Scale;
                Vector2 pos = new(rect.Center.X + size.Y * 0.5f, rect.Y + 4f);
                sb.DrawString(font, name, pos + new Vector2(1f, 1f), OnikiriUITheme.Ink * (alpha * 0.8f), MathHelper.PiOver2, Vector2.Zero, Scale, SpriteEffects.None, 0f);
                sb.DrawString(font, name, pos, color, MathHelper.PiOver2, Vector2.Zero, Scale, SpriteEffects.None, 0f);
                return;
            }

            float charH = font.MeasureString("字").Y * Scale + 4f;
            float y = rect.Y + 4f;
            for (int i = 0; i < name.Length; i++) {
                string s = name[i].ToString();
                Vector2 size = font.MeasureString(s) * Scale;
                Vector2 pos = new(rect.Center.X - size.X * 0.5f, y);
                Utils.DrawBorderString(sb, s, pos, color, Scale);

                //洇血:字脚垂下一两道缓慢生长的细痕,周期错开
                if (bleed > 0.01f) {
                    float seed = index * 7.3f + i * 2.9f;
                    float grow = (float)Math.Sin(time * 0.35f + seed) * 0.5f + 0.5f;
                    float dripLen = (2.5f + Hash01((int)(seed * 31f)) * 3.5f) * grow;
                    float dripX = pos.X + size.X * (0.3f + Hash01((int)(seed * 13f)) * 0.4f);
                    OniBrush.DrawGradientLine(sb,
                        new Vector2(dripX, y + size.Y - 3f), new Vector2(dripX, y + size.Y - 3f + dripLen),
                        OnikiriUITheme.Bright * (alpha * 0.55f * grow), OnikiriUITheme.Deep * (alpha * 0.05f), 1.2f);
                }
                y += charH;
                if (y > rect.Bottom - charH * 0.5f) {
                    break;
                }
            }
        }

        public static void DrawLoadoutSlot(SpriteBatch sb, DynamicSpriteFont font, Rectangle rect,
            OniGhostEntry equipped, float alpha, float hover, float time) {
            float pulse = 0.84f + 0.16f * (float)Math.Sin(time * 1.7f);
            sb.Draw(Pixel, rect, PixelSrc, OnikiriUITheme.Dark * (alpha * (0.34f + hover * 0.18f)));
            sb.Draw(Pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), PixelSrc,
                OnikiriUITheme.Deep * (alpha * (0.55f + hover * 0.28f)));
            sb.Draw(Pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), PixelSrc,
                OnikiriUITheme.Deep * (alpha * 0.28f));

            Vector2 seal = new(rect.X + 27f, rect.Center.Y);
            OniBrush.DrawSealGlyph(sb, seal, 12f, alpha * (equipped == null ? 0.35f : 0.92f * pulse),
                equipped == null ? 0f : OniGhostShadowDraw.SeedFromKey(equipped.Key) * 0.1f);

            string name = equipped?.Name?.Invoke() ?? OniRegisterUI.EmptySlotName.Value;
            Color nameColor = equipped == null ? OnikiriUITheme.Disabled
                : equipped.IsDormant ? OnikiriUITheme.Bright : OnikiriUITheme.Paper;
            Utils.DrawBorderString(sb, name, new Vector2(rect.X + 51f, rect.Y + 7f), nameColor * alpha, 0.76f);

            string state = equipped == null
                ? OniRegisterUI.EmptySlotHint.Value
                : string.Format(OniRegisterUI.MasteryFormat.Value, (int)MathF.Round(equipped.Mastery * 100f));
            if (equipped?.IsDormant == true) {
                state = OniRegisterUI.StateDormant.Value + " · " + state;
            }
            string line = VaultUtils.WrapTextJoin(state, font, rect.Width - 61f, 0.54f, 1, ellipsis: true);
            Utils.DrawBorderString(sb, line, new Vector2(rect.X + 51f, rect.Y + 26f),
                (equipped?.IsDormant == true ? OnikiriUITheme.Bright : OnikiriUITheme.TextDim) * (alpha * 0.78f), 0.54f);
        }

        //====================== 影绘细节板 ======================

        /// <summary>右侧细节板</summary>
        public static void DrawDetail(SpriteBatch sb, OniRegisterUI ui, Rectangle rect, float alpha) {
            OniGhostEntry entry = ui.SelectedEntry;
            if (rect.Width < 100) {
                return;
            }
            DynamicSpriteFont font = FontAssets.MouseText.Value;

            //裱板:墨底 + 深红双描边(与叙事面板 CPU 降级同语言)
            sb.Draw(Pixel, new Rectangle(rect.X + 5, rect.Y + 7, rect.Width, rect.Height), PixelSrc, new Color(8, 2, 5) * (alpha * 0.55f));
            sb.Draw(Pixel, rect, PixelSrc, OnikiriUITheme.Ink * (alpha * 0.95f));
            DrawRectBorder(sb, rect, OnikiriUITheme.Deep * (alpha * 0.55f), 2);
            Rectangle inner = rect;
            inner.Inflate(-5, -5);
            DrawRectBorder(sb, inner, OnikiriUITheme.Dark * (alpha * 0.8f), 1);

            //背光:纸后一盏暖灯,影绘的光源
            Vector2 lightCenter = new(rect.Center.X, rect.Y + rect.Height * 0.28f);
            OniBrush.DrawBacklight(sb, lightCenter, rect.Width * 0.42f, new Color(226, 160, 108), alpha * 0.5f);

            if (entry == null) {
                DrawEmptyDetail(sb, font, rect, lightCenter, alpha);
                return;
            }

            //影绘鬼形 + 眼
            DrawDetailShadow(sb, ui, entry, rect, lightCenter, alpha);

            //====文案区(右缘给线香让位)====
            float textTop = rect.Y + rect.Height * 0.48f;
            float textLeft = rect.X + 24f;
            float headerRight = rect.Right - 24f;
            bool hasIncense = entry.CanEquip;
            float textRight = hasIncense ? rect.Right - 88f : headerRight;

            //名讳 + 状态签
            string name = entry.Name?.Invoke() ?? entry.Key;
            Utils.DrawBorderString(sb, name, new Vector2(textLeft, textTop), OnikiriUITheme.HotWhite * alpha, 1.02f);
            (string stateText, Color stateCol) = StateLabel(entry);
            if (ui.IsEquipped(entry)) {
                stateText = OniRegisterUI.EquippedActive.Value + " · " + stateText;
                stateCol = OnikiriUITheme.Bright;
            }
            Vector2 stSize = font.MeasureString(stateText) * 0.68f;
            Utils.DrawBorderString(sb, stateText, new Vector2(headerRight - stSize.X, textTop + 6f), stateCol * alpha, 0.68f);
            OniBrush.DrawTaperedSlash(sb, new Vector2(textLeft - 4f, textTop + 26f), new Vector2(headerRight + 4f, textTop + 24f), 1.8f, 1.2f, alpha * 0.8f);

            //来历(打字机+湿墨) 与 赋力,各带一枚小签
            string origin = entry.Origin?.Invoke() ?? string.Empty;
            string power = entry.Power?.Invoke() ?? string.Empty;
            float y = textTop + 34f;
            Utils.DrawBorderString(sb, OniRegisterUI.OriginLabel.Value, new Vector2(textLeft, y), OnikiriUITheme.Deep * (alpha * 1.2f), 0.62f);
            y += 16f;
            y = DrawTypedWrapped(sb, font, origin, new Vector2(textLeft, y), textRight - textLeft,
                OnikiriUITheme.TextDim, 0.74f, alpha, ui.DetailVisibleChars, ui.DetailInkStrength,
                maxLines: 3, ellipsis: true);
            if (power.Length > 0 && ui.DetailVisibleChars > origin.Length) {
                y += 8f;
                Utils.DrawBorderString(sb, OniRegisterUI.PowerLabel.Value, new Vector2(textLeft, y), OnikiriUITheme.Deep * (alpha * 1.2f), 0.62f);
                y += 16f;
                DrawTypedWrapped(sb, font, power, new Vector2(textLeft, y), textRight - textLeft,
                    Color.Lerp(OnikiriUITheme.Paper, OnikiriUITheme.Bright, 0.28f), 0.74f, alpha,
                    ui.DetailVisibleChars - origin.Length, ui.DetailInkStrength,
                    maxLines: 3, ellipsis: true);
            }

            //线香驾驭度计
            if (entry.CanEquip) {
                DrawIncense(sb, ui, entry, alpha, font);
                string cost = string.Format(OniRegisterUI.AbilityCostFormat.Value,
                    (int)MathF.Round(entry.MasteryCost * 100f), (int)MathF.Round(entry.ErosionCost * 100f));
                string costLine = VaultUtils.WrapTextJoin(cost, font, rect.Width - 48f, 0.62f, 1, ellipsis: true);
                Utils.DrawBorderString(sb, costLine, new Vector2(rect.X + 24f, ui.ActionRect.Y - 23f),
                    OnikiriUITheme.TextDim * (alpha * 0.82f), 0.62f);
                DrawLoadoutAction(sb, font, ui, alpha);
            }
        }

        private static void DrawEmptyDetail(SpriteBatch sb, DynamicSpriteFont font, Rectangle rect,
            Vector2 lightCenter, float alpha) {
            OniBrush.DrawSealGlyph(sb, lightCenter, 28f, alpha * 0.22f);
            float textTop = rect.Y + rect.Height * 0.49f;
            Utils.DrawBorderString(sb, OniRegisterUI.EmptySlotName.Value,
                new Vector2(rect.X + 24f, textTop), OnikiriUITheme.Disabled * alpha, 1f);
            OniBrush.DrawTaperedSlash(sb, new Vector2(rect.X + 20f, textTop + 29f),
                new Vector2(rect.Right - 20f, textTop + 27f), 1.7f, 1f, alpha * 0.45f);
            string hint = VaultUtils.WrapTextJoin(OniRegisterUI.EmptySlotHint.Value, font,
                rect.Width - 48f, 0.74f, 5, ellipsis: true);
            Utils.DrawBorderString(sb, hint, new Vector2(rect.X + 24f, textTop + 43f),
                OnikiriUITheme.TextDim * (alpha * 0.8f), 0.74f);
        }

        private static void DrawLoadoutAction(SpriteBatch sb, DynamicSpriteFont font, OniRegisterUI ui, float alpha) {
            Rectangle rect = ui.ActionRect;
            float activeAlpha = ui.LoadoutPending ? 0.42f : 1f;
            float hover = ui.ActionHover;
            OniBrush.DrawBacklight(sb, rect.Center.ToVector2(), rect.Width * 0.42f,
                OnikiriUITheme.Deep, alpha * hover * 0.42f);
            sb.Draw(Pixel, new Rectangle(rect.X, rect.Center.Y, rect.Width, 1), PixelSrc,
                OnikiriUITheme.Deep * (alpha * (0.48f + hover * 0.38f) * activeAlpha));
            OniBrush.DrawSealGlyph(sb, new Vector2(rect.X + 18f, rect.Center.Y), 9f,
                alpha * (0.65f + hover * 0.3f) * activeAlpha);
            string text = ui.LoadoutActionText;
            Vector2 size = font.MeasureString(text) * 0.78f;
            Utils.DrawBorderString(sb, text,
                new Vector2(rect.Center.X - size.X * 0.5f + 8f, rect.Center.Y - size.Y * 0.5f),
                OnikiriUITheme.Paper * (alpha * activeAlpha), 0.78f);
        }

        /// <summary>影绘,闲置眼跟光标</summary>
        private static void DrawDetailShadow(SpriteBatch sb, OniRegisterUI ui, OniGhostEntry entry,
            Rectangle rect, Vector2 lightCenter, float alpha) {
            if (OniGhostShadowDraw.Available) {
                DrawDetailShadowShader(sb, ui, entry, rect, lightCenter, alpha);
                return;
            }

            Texture2D smoke = CWRAsset.SmokeSheet01.Value;
            int frameSize = smoke.Width / 2;
            Vector2 origin = new(frameSize * 0.5f);
            float time = ui.GlobalTimer;
            //休眠的影近乎不动
            float writhe = entry.IsDormant ? 0.24f : entry.IsArchive ? 0.42f : 1f;
            Vector2 basePos = lightCenter + new Vector2(0f, 12f);

            for (int i = 0; i < 3; i++) {
                int frame = (int)(time * 3.4f + i * 1.7f) % 4;
                Rectangle srcRect = new(frame % 2 * frameSize, frame / 2 * frameSize, frameSize, frameSize);
                float phase = i * 2.1f;
                Vector2 offset = new((float)Math.Sin(time * (0.6f + i * 0.22f) + phase) * 6f * writhe,
                    -16f + i * 15f + (float)Math.Cos(time * 0.5f + phase) * 3f * writhe);
                float scale = 0.24f + i * 0.05f;
                float rot = (float)Math.Sin(time * 0.3f + phase) * 0.14f * writhe;
                //影是"光被挡住":纯墨色,越靠光心越实
                sb.Draw(smoke, basePos + offset, srcRect, OnikiriUITheme.Ink * (alpha * (0.72f - i * 0.12f)),
                    rot, origin, scale, SpriteEffects.None, 0f);
            }

            //鬼火之眼:闲置凝视时瞳位缓缓压向光标方向(只转眼,不动身)
            if (entry.HasEyes) {
                float flick = 0.78f + 0.22f * (float)Math.Sin(time * 6.1f);
                Vector2 sway = new((float)Math.Sin(time * 0.6f) * 5f * writhe, (float)Math.Cos(time * 0.45f) * 3f * writhe);
                Vector2 eyeBase = basePos + sway + new Vector2(0f, -34f);
                Vector2 toMouse = (ui.MousePosition - eyeBase).SafeNormalize(Vector2.Zero) * 3.2f * ui.GlanceStrength;
                foreach (float side in new[] { -8f, 8f }) {
                    Vector2 eye = eyeBase + new Vector2(side, side < 0f ? 0f : 1.5f);
                    sb.Draw(Pixel, eye, PixelSrc, OnikiriUITheme.GhostDim * (alpha * 0.45f * flick), 0f,
                        new Vector2(0.5f), new Vector2(5.4f, 4.2f), SpriteEffects.None, 0f);
                    sb.Draw(Pixel, eye + toMouse, PixelSrc, OnikiriUITheme.GhostFire * (alpha * 0.92f * flick), 0f,
                        new Vector2(0.5f), new Vector2(2.4f, 1.9f), SpriteEffects.None, 0f);
                }
            }
        }

        /// <summary>
        /// 影绘 shader 路径:伪散射(放大模糊层沿光向偏移)卖"光透过纸"的厚度,
        /// 再叠清晰芯;封印的裹布与封字印保持 CPU 压在影上
        /// </summary>
        private static void DrawDetailShadowShader(SpriteBatch sb, OniRegisterUI ui, OniGhostEntry entry,
            Rectangle rect, Vector2 lightCenter, float alpha) {
            Vector2 basePos = lightCenter + new Vector2(0f, 14f);
            int w = (int)(rect.Width * 0.56f);
            int h = (int)(rect.Height * 0.46f);
            Rectangle quad = new((int)(basePos.X - w * 0.5f), (int)(basePos.Y - h * 0.52f), w, h);

            float writhe = entry.State switch {
                OniGhostState.Dormant => 0.16f,
                OniGhostState.Archive => 0.32f,
                _ => 0.62f,
            };
            float seed = OniGhostShadowDraw.SeedFromKey(entry.Key);
            //凝视:瞳位向光标方向压 UV 偏移(只转眼,不动身)
            Vector2 glance = Vector2.Zero;
            if (entry.HasEyes && ui.GlanceStrength > 0.01f) {
                Vector2 toMouse = ui.MousePosition - basePos;
                glance = toMouse.SafeNormalize(Vector2.Zero) * 0.024f * ui.GlanceStrength;
            }

            //伪散射层,放大低透沿光向微沉
            Rectangle diffuse = quad;
            diffuse.Inflate((int)(w * 0.125f), (int)(h * 0.125f));
            diffuse.Offset(0, 4);
            OniGhostShadowDraw.Draw(sb, diffuse, new OniGhostShadowParams {
                Writhe = writhe * 0.7f,
                Break = 0f,
                EyeOpen = 0f,
                Glance = glance,
                Seed = seed,
                Alpha = alpha * 0.30f,
                Time = ui.GlobalTimer,
            });

            //清晰芯
            float eyeOpen = entry.HasEyes ? 1f : 0f;
            OniGhostShadowDraw.Draw(sb, quad, new OniGhostShadowParams {
                Writhe = writhe,
                Break = 0f,
                EyeOpen = eyeOpen,
                Glance = glance,
                Seed = seed,
                Alpha = alpha * 0.92f,
                Time = ui.GlobalTimer,
            });
        }

        /// <summary>线香,燃去比=驾驭度</summary>
        private static void DrawIncense(SpriteBatch sb, OniRegisterUI ui, OniGhostEntry entry, float alpha, DynamicSpriteFont font) {
            Rectangle stick = ui.IncenseRect();
            Vector2 ember = ui.IncenseEmberPos();
            float time = ui.GlobalTimer;

            //香座:一枚小方钵
            sb.Draw(Pixel, new Rectangle(stick.X - 5, stick.Bottom, stick.Width + 10, 6), PixelSrc, OnikiriUITheme.Dark * (alpha * 0.9f));
            sb.Draw(Pixel, new Rectangle(stick.X - 3, stick.Bottom - 1, stick.Width + 6, 2), PixelSrc, OnikiriUITheme.Deep * (alpha * 0.7f));

            //余香:燃点以下仍在
            float remainTop = ember.Y;
            if (stick.Bottom - remainTop > 2f) {
                sb.Draw(Pixel, new Rectangle(stick.X, (int)remainTop, stick.Width, (int)(stick.Bottom - remainTop)), PixelSrc,
                    OnikiriUITheme.Paper * (alpha * 0.72f));
            }
            //已燃段的余痕:极淡的灰线
            if (remainTop - stick.Y > 2f) {
                sb.Draw(Pixel, new Rectangle(stick.Center.X, stick.Y, 1, (int)(remainTop - stick.Y)), PixelSrc,
                    OnikiriUITheme.TextDim * (alpha * 0.18f));
            }

            //燃点:炽红呼吸 + 一缕直上的青灰烟
            float flick = 0.7f + 0.3f * (float)Math.Sin(time * 5.3f);
            sb.Draw(Pixel, ember, PixelSrc, OnikiriUITheme.Bright * (alpha * 0.9f * flick), 0f,
                new Vector2(0.5f), new Vector2(4.2f, 3.2f), SpriteEffects.None, 0f);
            sb.Draw(Pixel, ember, PixelSrc, OnikiriUITheme.HotWhite * (alpha * 0.75f * flick), 0f,
                new Vector2(0.5f), new Vector2(2f, 1.6f), SpriteEffects.None, 0f);
            float wispSway = (float)Math.Sin(time * 1.7f) * 3f;
            OniBrush.DrawGradientLine(sb, ember - new Vector2(0f, 4f), ember - new Vector2(-wispSway, 30f),
                OnikiriUITheme.TextDim * (alpha * 0.4f), OnikiriUITheme.TextDim * 0f, 1.2f);

            //驾驭读数:居中吊在香座之下
            string mastery = string.Format(OniRegisterUI.MasteryFormat.Value, (int)(entry.Mastery * 100f));
            Color masteryCol = entry.IsDormant ? OnikiriUITheme.Bright : OnikiriUITheme.TextDim;
            Vector2 mSize = font.MeasureString(mastery) * 0.66f;
            Utils.DrawBorderString(sb, mastery, new Vector2(stick.Center.X - mSize.X * 0.5f, stick.Bottom + 12f), masteryCol * alpha, 0.66f);

            //休眠时香脚焦边起青焰
            if (entry.IsDormant) {
                OniBrush.DrawCharredEdge(sb, new Rectangle(stick.X - 4, stick.Bottom - 8, stick.Width + 8, 8), 0.8f, time, alpha * 0.9f);
            }
        }

        private static (string, Color) StateLabel(OniGhostEntry entry) => entry.State switch {
            OniGhostState.Ready => (OniRegisterUI.StateReady.Value, OnikiriUITheme.TextDim),
            OniGhostState.Dormant => (OniRegisterUI.StateDormant.Value, OnikiriUITheme.Bright),
            _ => (OniRegisterUI.StateArchive.Value, OnikiriUITheme.Disabled),
        };

        /// <summary>逐字换行+打字机+湿墨,返回块底 Y;freshColor 缺省湿墨绯红(改铭台传灼橙作烙印)</summary>
        internal static float DrawTypedWrapped(SpriteBatch sb, DynamicSpriteFont font, string text, Vector2 pos,
            float maxWidth, Color color, float scale, float alpha, int visibleChars, float inkStrength,
            Color? freshColor = null, int maxLines = int.MaxValue, bool ellipsis = false) {
            if (string.IsNullOrEmpty(text)) {
                return pos.Y;
            }
            List<string> lines = VaultUtils.WrapText(text, font, maxWidth, scale, maxLines, ellipsis);
            float lineH = font.MeasureString("字").Y * scale + 2f;
            int remaining = visibleChars;
            float y = pos.Y;
            foreach (string line in lines) {
                if (remaining <= 0) {
                    break;
                }
                bool isRevealLine = remaining < line.Length;
                string draw = isRevealLine ? line[..Math.Max(0, remaining)] : line;
                if (draw.Length > 0) {
                    Utils.DrawBorderString(sb, draw, new Vector2(pos.X, y), color * alpha, scale);
                    //湿墨:最新 1~2 字覆一层随时间褪去的绯红(或调用方指定的灼色)
                    if (isRevealLine && inkStrength > 0.02f) {
                        int tail = Math.Min(2, draw.Length);
                        string prefix = draw[..^tail];
                        string tailStr = draw[^tail..];
                        float prefixW = font.MeasureString(prefix).X * scale;
                        Utils.DrawBorderString(sb, tailStr, new Vector2(pos.X + prefixW, y),
                            (freshColor ?? OnikiriUITheme.Bright) * (alpha * 0.8f * inkStrength), scale);
                    }
                }
                remaining -= line.Length;
                y += lineH;
            }
            return y;
        }

        /// <summary>逐字换行后的整块高度(与 <see cref="DrawTypedWrapped"/> 同口径),供面板按内容实测定高</summary>
        internal static float MeasureWrappedHeight(DynamicSpriteFont font, string text, float maxWidth, float scale) {
            if (string.IsNullOrEmpty(text)) {
                return 0f;
            }
            float lineH = font.MeasureString("字").Y * scale + 2f;
            return VaultUtils.WrapText(text, font, maxWidth, scale).Count * lineH;
        }

        private static void DrawRectBorder(SpriteBatch sb, Rectangle rect, Color color, int thickness) {
            sb.Draw(Pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), PixelSrc, color);
            sb.Draw(Pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), PixelSrc, color * 0.75f);
            sb.Draw(Pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), PixelSrc, color * 0.88f);
            sb.Draw(Pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), PixelSrc, color * 0.88f);
        }

        private static float Hash01(int n) {
            unchecked {
                n = n * 374761393 + 668265263;
                n = (n ^ (n >> 13)) * 1274126177;
                return ((n ^ (n >> 16)) & 0x7FFFFFFF) / (float)int.MaxValue;
            }
        }
    }
}
