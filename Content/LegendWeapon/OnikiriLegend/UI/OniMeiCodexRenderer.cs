using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions;
using CalamityOverhaul.Content.UIs.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>
    /// 铭谱绘制：线装册子的封面/纸面/中缝/栞，左页名录格与页签，右页详情。<br/>
    /// 纹样一律走 <see cref="SvgPathPen"/> 的嵌入路径串，无贴图，缩放不糊
    /// </summary>
    internal static class OniMeiCodexRenderer
    {
        private static Texture2D Pixel => VaultAsset.placeholder2.Value;
        private static readonly Rectangle PixelSrc = new(0, 0, 1, 1);

        #region 纹样路径
        /// <summary>封面纹章：外圆 + 内菱 + 中心一凿</summary>
        private const string MonData =
            "M 0,-1 C 0.5523,-1 1,-0.5523 1,0 C 1,0.5523 0.5523,1 0,1"
            + " C -0.5523,1 -1,0.5523 -1,0 C -1,-0.5523 -0.5523,-1 0,-1 Z"
            + " M 0,-0.52 L 0.52,0 L 0,0.52 L -0.52,0 Z";

        /// <summary>全册页签：三道并排的短笔（一摞纸的读法）</summary>
        private const string IconAll =
            "M -0.62,-0.5 L 0.62,-0.5 M -0.62,0 L 0.62,0 M -0.62,0.5 L 0.62,0.5";

        /// <summary>茎铭：刀茎一条 + 目钉孔</summary>
        private const string IconNakago =
            "M -0.16,-0.86 L -0.16,0.86 L 0.16,0.86 L 0.16,-0.86 Z"
            + " M 0,0.34 C 0.12,0.34 0.2,0.42 0.2,0.52 C 0.2,0.62 0.12,0.7 0,0.7"
            + " C -0.12,0.7 -0.2,0.62 -0.2,0.52 C -0.2,0.42 -0.12,0.34 0,0.34 Z";

        /// <summary>樋位：一条开在刃上的血槽，两端收尖</summary>
        private const string IconHi =
            "M -0.78,0.1 C -0.5,-0.34 0.34,-0.5 0.8,-0.24"
            + " C 0.34,-0.16 -0.42,0.22 -0.78,0.1 Z"
            + " M -0.6,0.52 L 0.66,0.2";

        /// <summary>雕位：缠身的一道盘曲（倶利伽罗的简笔）</summary>
        private const string IconHorimono =
            "M -0.1,0.9 C -0.9,0.4 0.8,0.2 0.1,-0.2 C -0.66,-0.5 0.5,-0.72 0.16,-0.9";

        /// <summary>翻页角标：一记折角</summary>
        private const string IconChevron = "M 0.34,-0.72 L -0.3,0 L 0.34,0.72";

        /// <summary>栞（书签带）：垂下的一条，尾端剪成燕尾</summary>
        private const string BookmarkData =
            "M -1,-1 L 1,-1 L 1,0.55 L 0,0.1 L -1,0.55 Z";

        /// <summary>收集度托架：一副合抱的括线</summary>
        private const string TallyBracket =
            "M -1,-0.72 L -0.82,-0.9 L 0.82,-0.9 L 1,-0.72"
            + " M -1,0.72 L -0.82,0.9 L 0.82,0.9 L 1,0.72";
        #endregion

        private static float Hash01(int n) {
            unchecked {
                n = n * 374761393 + 668265263;
                n = (n ^ (n >> 13)) * 1274126177;
                return ((n ^ (n >> 16)) & 0x7FFFFFFF) / (float)int.MaxValue;
            }
        }

        //和纸正文 / 次级 / 未凿灰：褐墨谱系（与任务书 ChroniclePalette 同路数）。
        //灰调淡墨在亮纸上读作没墨水，次级与未凿一律偏暖压深
        private static readonly Color PaperBody = new(38, 26, 24);
        private static readonly Color PaperMute = new(70, 50, 38);
        internal static readonly Color PaperAsh = new(116, 94, 70);
        private static readonly Color PaperBurden = new(148, 48, 40);

        /// <summary>
        /// 和纸墨字：同色三笔加重——主笔 + 右偏加厚竖画 + 下偏半墨加厚横画，
        /// 读作蘸饱墨的笔锋压进纸里（与任务书 ChroniclePen.InkStrike 同手法，偏移随字号收放）。<br/>
        /// 仍禁 DrawBorderString：四向黑描边叠在深墨上会糊成一团
        /// </summary>
        internal static void DrawPaperInk(SpriteBatch sb, DynamicSpriteFont font, string text,
            Vector2 pos, Color color, float scale, float alpha = 1f,
            float originX = 0f, float originY = 0f) {
            if (string.IsNullOrEmpty(text) || alpha <= 0.01f) {
                return;
            }
            Vector2 size = font.MeasureString(text) * scale;
            Vector2 origin = new(size.X * originX / scale, size.Y * originY / scale);
            Color ink = color * alpha;
            float d = MathHelper.Clamp(scale * 0.9f, 0.55f, 1.05f);
            sb.DrawString(font, text, pos + new Vector2(d, 0f), ink * 0.85f,
                0f, origin, scale, SpriteEffects.None, 0f);
            sb.DrawString(font, text, pos + new Vector2(0f, d * 0.75f), ink * 0.5f,
                0f, origin, scale, SpriteEffects.None, 0f);
            sb.DrawString(font, text, pos, ink, 0f, origin, scale, SpriteEffects.None, 0f);
        }

        //====================== 册子本体 ======================

        /// <summary>
        /// 摊开的线装册：木封面板 → 两页和纸 → 中缝装订 → 栞。<br/>
        /// 中缝不是一条黑线，是"两叠纸被线缝住"的暗带 + 缝孔
        /// </summary>
        public static void DrawBook(SpriteBatch sb, Rectangle book, Rectangle left, Rectangle right,
            float alpha, float time) {
            if (alpha <= 0.01f) {
                return;
            }
            //封面外投影：贴着册缘两段，不做同心羽化
            OniBrush.DrawPanelDropShadow(sb, book.Center.ToVector2(),
                new Vector2(book.Width, book.Height), alpha);

            //封面板（shader 缺席时退回 CPU 木色）
            if (OniMeiStandDraw.Available) {
                OniMeiStandDraw.DrawWoodPlank(sb, book, alpha * 0.96f, time);
            }
            else {
                sb.Draw(Pixel, book, PixelSrc, new Color(58, 34, 26) * (alpha * 0.96f));
            }

            //封面金压线，内衬一线绯红（与顶梁、台账同语）
            DrawFrame(sb, book, OnikiriUITheme.GoldDeep * (alpha * 0.55f), 2f);
            DrawFrame(sb, Inflate(book, -3), OnikiriUITheme.Deep * (alpha * 0.30f), 1f);

            //两页和纸
            DrawPaper(sb, left, alpha, time, 0);
            DrawPaper(sb, right, alpha, time, 1);

            //中缝：暗带 + 缝孔 + 一线受光
            int spineX = book.Center.X;
            int spineTop = left.Y - 6;
            int spineH = left.Height + 12;
            sb.Draw(Pixel, new Rectangle(spineX - 9, spineTop, 18, spineH), PixelSrc,
                new Color(30, 16, 14) * (alpha * 0.55f));
            sb.Draw(Pixel, new Rectangle(spineX - 1, spineTop, 2, spineH), PixelSrc,
                new Color(12, 5, 6) * (alpha * 0.75f));
            sb.Draw(Pixel, new Rectangle(spineX + 6, spineTop, 1, spineH), PixelSrc,
                OnikiriUITheme.CandleWarm * (alpha * 0.12f));
            //缝孔：四目缀じ的针脚
            for (int i = 0; i < 4; i++) {
                float t = (i + 1) / 5f;
                Vector2 hole = new(spineX, spineTop + spineH * t);
                sb.Draw(Pixel, hole, PixelSrc, new Color(8, 3, 5) * (alpha * 0.9f),
                    0f, new Vector2(0.5f), new Vector2(3.2f, 5f), SpriteEffects.None, 0f);
                OniBrush.DrawSoftDot(sb, hole, 2.2f, OnikiriUITheme.GoldDeep, alpha * 0.30f);
            }

            //栞：自册顶垂下，随时间极缓摆
            float sway = (float)Math.Sin(time * 0.7f) * 0.045f;
            SvgPath bookmark = SvgPathPen.Path(BookmarkData);
            Vector2 markTop = new(right.Right - 44f, book.Y - 10f);
            SvgPathPen.Stroke(sb, bookmark, markTop + new Vector2(0f, 46f), 46f, sway,
                OnikiriUITheme.Deep, 2f, alpha * 0.85f, core: OnikiriUITheme.Bright);

            //封面纹章：压在册顶正中的暗纹，只留一点受光
            SvgPath mon = SvgPathPen.Path(MonData);
            SvgPathPen.Stroke(sb, mon, new Vector2(book.Center.X, book.Y - 22f), 17f, 0f,
                OnikiriUITheme.GoldDeep, 1.6f, alpha * 0.5f);
        }

        /// <summary>一页和纸：底色 + 纤维横纹 + 页缘吃暗 + 朱丝栏</summary>
        private static void DrawPaper(SpriteBatch sb, Rectangle page, float alpha, float time, int seed) {
            sb.Draw(Pixel, page, PixelSrc, new Color(232, 222, 202) * (alpha * 0.98f));
            //纤维：极淡横纹，勿抢正文（过浓会与墨字糊在一起）
            for (int i = 0; i < 18; i++) {
                float u = Hash01(i * 71 + seed * 313);
                float y = page.Y + page.Height * u;
                float len = page.Width * (0.35f + Hash01(i * 37 + seed * 91) * 0.6f);
                float x = page.X + (page.Width - len) * Hash01(i * 53 + seed * 17);
                sb.Draw(Pixel, new Vector2(x, y), PixelSrc,
                    new Color(196, 182, 158) * (alpha * 0.08f),
                    0f, Vector2.Zero, new Vector2(len, 1f), SpriteEffects.None, 0f);
            }
            //页缘吃暗：纸摊在木板上，边上总是暗的
            sb.Draw(Pixel, new Rectangle(page.X, page.Y, page.Width, 3), PixelSrc,
                new Color(150, 132, 108) * (alpha * 0.35f));
            sb.Draw(Pixel, new Rectangle(page.X, page.Bottom - 3, page.Width, 3), PixelSrc,
                new Color(150, 132, 108) * (alpha * 0.35f));
            sb.Draw(Pixel, new Rectangle(page.X, page.Y, 3, page.Height), PixelSrc,
                new Color(150, 132, 108) * (alpha * 0.30f));
            sb.Draw(Pixel, new Rectangle(page.Right - 3, page.Y, 3, page.Height), PixelSrc,
                new Color(150, 132, 108) * (alpha * 0.30f));
            //朱丝栏：上下各一道
            sb.Draw(Pixel, new Rectangle(page.X + 8, page.Y + 6, page.Width - 16, 1), PixelSrc,
                OnikiriUITheme.Seal * (alpha * 0.28f));
            sb.Draw(Pixel, new Rectangle(page.X + 8, page.Bottom - 7, page.Width - 16, 1), PixelSrc,
                OnikiriUITheme.Seal * (alpha * 0.28f));
        }

        //====================== 页眉收集度 ======================

        /// <summary>左页顶：全册收集度 + 三槽分计，托在一副括线里</summary>
        public static void DrawTally(SpriteBatch sb, DynamicSpriteFont font, Rectangle page,
            Player player, float alpha) {
            OniMeiCodexTally all = OniMeiCodexData.Tally(player);
            Vector2 center = new(page.Center.X, page.Y + 22f);

            SvgPath bracket = SvgPathPen.Path(TallyBracket);
            SvgPathPen.Stroke(sb, bracket, center, 86f, 0f,
                OnikiriUITheme.Deep, 1.5f, alpha * 0.55f);

            string head = OniMeiCodexUI.TallyFormat.Format(all.Owned, all.Total);
            const float headScale = 1.05f;
            Vector2 size = font.MeasureString(head) * headScale;
            DrawPaperInk(sb, font, head, center - size * 0.5f, PaperBody, headScale, alpha);

            //分槽：一行三段，满卷者标金
            float y = page.Y + 42f;
            for (int i = 0; i < 3; i++) {
                OniMeiSlotKind slot = (OniMeiSlotKind)i;
                OniMeiCodexTally tally = OniMeiCodexData.Tally(player, slot);
                string label = OniMeiCodexUI.TallySlotFormat.Format(
                    OniMeiCodexUI.TabLabel(i + 1), tally.Owned, tally.Total);
                float x = page.X + page.Width * (0.2f + i * 0.3f);
                const float slotScale = 0.84f;
                Vector2 textSize = font.MeasureString(label) * slotScale;
                bool full = tally.Owned >= tally.Total && tally.Total > 0;
                DrawPaperInk(sb, font, label, new Vector2(x - textSize.X * 0.5f, y),
                    full ? OnikiriUITheme.GoldDeep : PaperMute, slotScale, alpha);
            }
        }

        //====================== 页签 ======================

        /// <summary>
        /// 分卷页签：骑在页顶的一片小木牌，左端一枚分卷纹（全册三笔 / 茎 / 樋 / 雕）。<br/>
        /// 选中者抬起并压出，与未选那几片一眼分得开
        /// </summary>
        public static void DrawTab(SpriteBatch sb, DynamicSpriteFont font, Rectangle rect,
            int tab, string label, bool active, float hover, float alpha) {
            float lift = active ? 3f : hover * 2f;
            Rectangle body = new(rect.X, rect.Y - (int)lift, rect.Width, rect.Height + (int)lift);
            Color face = active
                ? Color.Lerp(new Color(96, 58, 40), new Color(126, 78, 50), 0.5f)
                : Color.Lerp(new Color(58, 36, 28), new Color(84, 52, 36), hover * 0.8f);
            sb.Draw(Pixel, body, PixelSrc, face * (alpha * 0.95f));
            sb.Draw(Pixel, new Rectangle(body.X, body.Y, body.Width, 1), PixelSrc,
                OnikiriUITheme.GoldDeep * (alpha * (active ? 0.7f : 0.3f)));

            string data = tab switch {
                0 => IconAll,
                1 => IconNakago,
                2 => IconHi,
                _ => IconHorimono,
            };
            SvgPathPen.Stroke(sb, SvgPathPen.Path(data),
                new Vector2(body.X + 14f, body.Center.Y + lift * 0.3f), 8f, 0f,
                active ? OnikiriUITheme.GoldInlay : OnikiriUITheme.TextDim,
                1.3f, alpha * (active ? 0.95f : 0.6f));

            Color ink = active ? OnikiriUITheme.Paper : Color.Lerp(OnikiriUITheme.TextDim, OnikiriUITheme.Paper, 0.55f);
            //木牌字：浅色填在深木上，可用描边；字号抬到可读
            Utils.DrawBorderString(sb, label,
                new Vector2(body.X + 26f, body.Y + lift + 3f), ink * alpha, 0.88f);
        }

        //====================== 名录格 ======================

        /// <summary>
        /// 一格：字形在上、铭名在下。<br/>
        /// 已得走阴刻，未得整体压暗并罩一层薄墨，看得见轮廓，读不出内容
        /// </summary>
        public static void DrawCell(SpriteBatch sb, DynamicSpriteFont font, Rectangle rect,
            in OniMeiCodexRow row, bool selected, float hover, float alpha, float time) {
            float a = alpha;
            bool locked = !row.Owned;

            //选中与悬停互斥，避免叠两层浅色卡
            if (selected) {
                Rectangle card = Inflate(rect, -4);
                sb.Draw(Pixel, card, PixelSrc, new Color(246, 238, 222) * (a * 0.55f));
                DrawFrame(sb, card, OnikiriUITheme.Seal * (a * 0.55f), 1f);
            }
            else if (hover > 0.01f) {
                Rectangle card = Inflate(rect, -6);
                sb.Draw(Pixel, card, PixelSrc, new Color(240, 230, 212) * (a * hover * 0.45f));
            }

            Vector2 glyphAt = new(rect.Center.X, rect.Y + rect.Height * 0.36f);
            float size = OnikiriUITheme.CodexCellGlyphSize * (1f + hover * 0.06f);

            OniMeiGlyphStyle style = OniMeiGlyphStyle.Engraved(a * (locked ? 0.5f : 1f));
            style.Time = time;
            style.Inlay = row.Gold && !locked ? 1f : 0f;
            style.Accent = locked
                ? PaperAsh
                : row.Gold ? OnikiriUITheme.GoldInlay : OnikiriUITheme.Bright;
            style.Lit = locked ? 0.02f : 0.10f + hover * 0.45f;
            OniMeiGlyph.Draw(sb, row.Key, glyphAt, size, style);

            //铭名：未得也照给，铭名本身就是线索
            string name = row.Name;
            float scale = 0.86f;
            Vector2 textSize = font.MeasureString(name) * scale;
            if (textSize.X > rect.Width - 10f) {
                scale *= (rect.Width - 10f) / Math.Max(1f, textSize.X);
                textSize = font.MeasureString(name) * scale;
            }
            Color nameColor = locked
                ? PaperAsh
                : row.Gold ? OnikiriUITheme.GoldDeep : PaperBody;
            DrawPaperInk(sb, font, name,
                new Vector2(rect.Center.X - textSize.X * 0.5f, rect.Bottom - textSize.Y - 6f),
                nameColor, scale, a);

            //现铭：右上一枚朱点
            if (row.Engraved) {
                OniBrush.DrawSoftDot(sb, new Vector2(rect.Right - 12f, rect.Y + 12f), 3.6f,
                    OnikiriUITheme.Seal, a * 0.95f);
            }
            //金象嵌：左上一枚小金角
            if (row.Gold) {
                sb.Draw(Pixel, new Vector2(rect.X + 10f, rect.Y + 10f), PixelSrc,
                    OnikiriUITheme.GoldInlay * (a * (locked ? 0.35f : 0.85f)),
                    MathHelper.PiOver4, new Vector2(0.5f), new Vector2(4.6f),
                    SpriteEffects.None, 0f);
            }
            //未得：名下不再叠凿口短线（会与铭名重叠糊掉）
        }

        //====================== 翻页 ======================

        /// <summary>左右折角 + 中间页码</summary>
        public static void DrawPager(SpriteBatch sb, DynamicSpriteFont font, Rectangle prev,
            Rectangle next, string label, bool canPrev, bool canNext, int hoverArrow, float alpha) {
            SvgPath chevron = SvgPathPen.Path(IconChevron);
            if (canPrev) {
                SvgPathPen.Stroke(sb, chevron, prev.Center.ToVector2(), 9f, 0f,
                    hoverArrow < 0 ? OnikiriUITheme.Bright : OnikiriUITheme.Deep,
                    1.8f, alpha * (hoverArrow < 0 ? 1f : 0.65f));
            }
            if (canNext) {
                SvgPathPen.Stroke(sb, chevron, next.Center.ToVector2(), 9f, MathHelper.Pi,
                    hoverArrow > 0 ? OnikiriUITheme.Bright : OnikiriUITheme.Deep,
                    1.8f, alpha * (hoverArrow > 0 ? 1f : 0.65f));
            }
            const float pageScale = 0.82f;
            Vector2 size = font.MeasureString(label) * pageScale;
            DrawPaperInk(sb, font, label,
                new Vector2((prev.Center.X + next.Center.X) * 0.5f - size.X * 0.5f,
                    prev.Center.Y - size.Y * 0.5f),
                PaperMute, pageScale, alpha);
        }


        //====================== 右页详情 ======================

        private const float DetailNameScale = 1.22f;
        private const float DetailSectionLabelScale = 0.84f;
        private const float DetailSectionBodyScale = 0.90f;
        private const float DetailBodyBottomPad = 16f;

        /// <summary>页眉底边 Y（朱线下方正文区起点）</summary>
        internal static float DetailHeaderEndY(DynamicSpriteFont font, Rectangle page, in OniMeiCodexRow row) {
            float y = page.Y + 20f;
            y += OnikiriUITheme.CodexDetailGlyphSize + 10f;
            y += font.MeasureString(row.Name ?? "").Y * DetailNameScale + 6f;
            y += 26f; //徽记行
            y += 14f; //朱线后留白
            return y;
        }

        /// <summary>正文裁剪矩形：页眉之下到页底内边</summary>
        internal static Rectangle DetailBodyRect(DynamicSpriteFont font, Rectangle page, in OniMeiCodexRow row) {
            int top = (int)DetailHeaderEndY(font, page, in row);
            int bottom = page.Bottom - (int)DetailBodyBottomPad;
            int height = Math.Max(0, bottom - top);
            return new Rectangle(page.X + 22, top, page.Width - 44, height);
        }

        /// <summary>正文总高度（与 DrawDetailBody 同口径）</summary>
        internal static float MeasureDetailBody(DynamicSpriteFont font, float wrapW, in OniMeiCodexRow row) {
            float y = 0f;
            if (!row.Owned) {
                y = MeasureSection(font, wrapW, OniMeiCodexUI.SectionAcquire.Value,
                    OniMeiCodexData.AcquireLine(in row), y);
                y = MeasureProgress(in row, y);
                y = MeasureSection(font, wrapW, OniMeiCodexUI.SectionSource.Value,
                    OniMeiCodexData.SourceLine(in row), y);
                y = MeasureSection(font, wrapW, "", OniMeiCodexUI.HiddenBody.Value, y);
                return y;
            }
            y = MeasureSection(font, wrapW, OniMeiUI.OriginLabel?.Value ?? "",
                row.Definition?.Origin?.Value ?? "", y);
            y = MeasureSection(font, wrapW, OniMeiUI.PowerLabel?.Value ?? "",
                row.Definition?.Power?.Value ?? "", y);
            y = MeasureSection(font, wrapW, OniMeiUI.BurdenLabel?.Value ?? "",
                row.Definition?.Burden?.Value ?? "", y);
            y = MeasureSection(font, wrapW, OniMeiCodexUI.SectionSource.Value,
                OniMeiCodexData.SourceLine(in row), y);
            return y;
        }

        /// <summary>
        /// 详情页：固定页眉 + Scissor 裁剪的可滚正文。<br/>
        /// 返回正文内容总高，供 UI 计算 MaxScroll
        /// </summary>
        public static float DrawDetail(SpriteBatch sb, DynamicSpriteFont font, Rectangle page,
            in OniMeiCodexRow row, float reveal, float scroll, float alpha, float time) {
            bool locked = !row.Owned;
            float y = page.Y + 20f;

            //固定页眉
            Vector2 glyphAt = new(page.Center.X, y + OnikiriUITheme.CodexDetailGlyphSize * 0.5f);
            float g = OnikiriUITheme.CodexDetailGlyphSize;
            sb.Draw(Pixel, glyphAt, PixelSrc, OnikiriUITheme.Ink * (alpha * (locked ? 0.42f : 0.72f)),
                MathHelper.PiOver4, new Vector2(0.5f), new Vector2(g * 0.86f),
                SpriteEffects.None, 0f);

            OniMeiGlyphStyle style = OniMeiGlyphStyle.Engraved(alpha * (locked ? 0.62f : 1f));
            style.Time = time;
            style.ChiselReveal = reveal;
            style.Inlay = row.Gold && !locked ? 1f : 0f;
            style.Accent = locked
                ? PaperMute
                : row.Gold ? OnikiriUITheme.GoldInlay : OnikiriUITheme.Bright;
            style.Lit = locked ? 0.05f : 0.28f;
            OniMeiGlyph.Draw(sb, row.Key, glyphAt, g * 0.62f, style);
            y += g + 10f;

            string name = row.Name;
            Vector2 nameSize = font.MeasureString(name) * DetailNameScale;
            DrawPaperInk(sb, font, name, new Vector2(page.Center.X - nameSize.X * 0.5f, y),
                locked ? PaperAsh : PaperBody, DetailNameScale, alpha);
            y += nameSize.Y + 6f;

            List<(string text, Color color)> badges = [
                (OniMeiCodexUI.TabLabel((int)row.Slot + 1), PaperMute),
            ];
            if (row.Gold) {
                badges.Add((OniMeiUI.GoldMark?.Value ?? "", OnikiriUITheme.GoldDeep));
            }
            if (locked) {
                badges.Add((OniMeiCodexUI.LockedTitle.Value, PaperAsh));
            }
            if (row.Engraved) {
                badges.Add((OniMeiCodexUI.EngravedMark.Value, OnikiriUITheme.Seal));
            }
            DrawBadgeRow(sb, font, page.Center.X, y, badges, alpha);
            y += 26f;

            sb.Draw(Pixel, new Rectangle(page.X + 26, (int)y, page.Width - 52, 1), PixelSrc,
                OnikiriUITheme.Seal * (alpha * 0.45f));
            y += 14f;

            Rectangle bodyRect = new(page.X + 22, (int)y, page.Width - 44,
                Math.Max(0, page.Bottom - (int)DetailBodyBottomPad - (int)y));
            float wrapW = bodyRect.Width - 8f;
            float contentH = MeasureDetailBody(font, wrapW, in row);
            float maxScroll = Math.Max(0f, contentH - bodyRect.Height);
            float scrollClamped = Math.Clamp(scroll, 0f, maxScroll);

            if (bodyRect.Height <= 0) {
                return contentH;
            }

            //Scissor 正文（对齐 OverhaulSettingsUI）
            sb.End();
            Rectangle prevScissor = sb.GraphicsDevice.ScissorRectangle;
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, new RasterizerState { ScissorTestEnable = true }, null, Main.UIScaleMatrix);
            sb.GraphicsDevice.ScissorRectangle = VaultUtils.GetClippingRectangle(sb, bodyRect);

            float bodyY = bodyRect.Y - scrollClamped;
            DrawDetailBody(sb, font, bodyRect.X + 4f, bodyY, wrapW, in row, alpha);

            sb.End();
            sb.GraphicsDevice.ScissorRectangle = prevScissor;
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);

            //溢出：右缘细朱迹随滚动走（非现代滑块）
            if (maxScroll > 0.5f) {
                float ratio = scrollClamped / maxScroll;
                float trackH = bodyRect.Height - 12f;
                float markH = Math.Max(10f, trackH * (bodyRect.Height / Math.Max(bodyRect.Height, contentH)));
                float markY = bodyRect.Y + 6f + ratio * (trackH - markH);
                sb.Draw(Pixel, new Rectangle(bodyRect.Right - 3, (int)markY, 2, (int)markH), PixelSrc,
                    OnikiriUITheme.Seal * (alpha * 0.55f));
            }

            return contentH;
        }

        /// <summary>可滚正文各段</summary>
        private static void DrawDetailBody(SpriteBatch sb, DynamicSpriteFont font, float x, float y,
            float wrapW, in OniMeiCodexRow row, float alpha) {
            if (!row.Owned) {
                y = DrawSection(sb, font, x, y, wrapW, OniMeiCodexUI.SectionAcquire.Value,
                    OniMeiCodexData.AcquireLine(in row), PaperBody, alpha);
                y = DrawProgress(sb, font, x, y, wrapW, in row, alpha);
                y = DrawSection(sb, font, x, y, wrapW, OniMeiCodexUI.SectionSource.Value,
                    OniMeiCodexData.SourceLine(in row), PaperMute, alpha);
                DrawSection(sb, font, x, y, wrapW, "", OniMeiCodexUI.HiddenBody.Value, PaperAsh, alpha);
                return;
            }
            y = DrawSection(sb, font, x, y, wrapW, OniMeiUI.OriginLabel?.Value ?? "",
                row.Definition?.Origin?.Value ?? "", PaperMute, alpha);
            y = DrawSection(sb, font, x, y, wrapW, OniMeiUI.PowerLabel?.Value ?? "",
                row.Definition?.Power?.Value ?? "", PaperBody, alpha);
            y = DrawSection(sb, font, x, y, wrapW, OniMeiUI.BurdenLabel?.Value ?? "",
                row.Definition?.Burden?.Value ?? "", PaperBurden, alpha);
            DrawSection(sb, font, x, y, wrapW, OniMeiCodexUI.SectionSource.Value,
                OniMeiCodexData.SourceLine(in row), PaperMute, alpha);
        }

        /// <summary>一行徽记：纯字 + 间隔点，不画底色框</summary>
        private static void DrawBadgeRow(SpriteBatch sb, DynamicSpriteFont font, float centerX,
            float y, List<(string text, Color color)> badges, float alpha) {
            const float scale = 0.82f;
            const float gap = 14f;
            float total = 0f;
            for (int i = 0; i < badges.Count; i++) {
                total += font.MeasureString(badges[i].text).X * scale;
                if (i < badges.Count - 1) {
                    total += gap;
                }
            }
            float x = centerX - total * 0.5f;
            for (int i = 0; i < badges.Count; i++) {
                (string text, Color color) = badges[i];
                float w = font.MeasureString(text).X * scale;
                DrawPaperInk(sb, font, text, new Vector2(x, y), color, scale, alpha);
                x += w;
                if (i < badges.Count - 1) {
                    DrawPaperInk(sb, font, "·", new Vector2(x + gap * 0.28f, y), PaperAsh, scale, alpha * 0.7f);
                    x += gap;
                }
            }
        }

        /// <summary>标签 + 正文一段；返回下一段的起始 Y</summary>
        private static float DrawSection(SpriteBatch sb, DynamicSpriteFont font, float x, float y,
            float wrapW, string label, string body, Color bodyColor, float alpha) {
            if (string.IsNullOrWhiteSpace(body)) {
                return y;
            }
            if (!string.IsNullOrEmpty(label)) {
                DrawPaperInk(sb, font, label, new Vector2(x, y), OnikiriUITheme.Seal, DetailSectionLabelScale, alpha);
                y += 20f;
            }
            List<string> lines = VaultUtils.WrapText(body, font, wrapW, DetailSectionBodyScale);
            float lineH = font.MeasureString("A").Y * DetailSectionBodyScale + 3f;
            foreach (string line in lines) {
                string text = line.TrimEnd();
                if (text.Length == 0) {
                    continue;
                }
                DrawPaperInk(sb, font, text, new Vector2(x + 4f, y), bodyColor, DetailSectionBodyScale, alpha);
                y += lineH;
            }
            return y + 10f;
        }

        private static float MeasureSection(DynamicSpriteFont font, float wrapW, string label, string body, float y) {
            if (string.IsNullOrWhiteSpace(body)) {
                return y;
            }
            if (!string.IsNullOrEmpty(label)) {
                y += 20f;
            }
            List<string> lines = VaultUtils.WrapText(body, font, wrapW, DetailSectionBodyScale);
            float lineH = font.MeasureString("A").Y * DetailSectionBodyScale + 3f;
            foreach (string line in lines) {
                if (line.TrimEnd().Length == 0) {
                    continue;
                }
                y += lineH;
            }
            return y + 10f;
        }

        /// <summary>縁分：凿槽式进度条 + 读数</summary>
        private static float DrawProgress(SpriteBatch sb, DynamicSpriteFont font, float x, float y,
            float wrapW, in OniMeiCodexRow row, float alpha) {
            DrawPaperInk(sb, font, OniMeiCodexUI.SectionProgress.Value, new Vector2(x, y),
                OnikiriUITheme.Seal, DetailSectionLabelScale, alpha);
            y += 20f;

            string text = OniMeiCodexData.ProgressLine(in row);
            if (row.DeedCountable) {
                Rectangle groove = new((int)(x + 4f), (int)y + 5, (int)(wrapW - 110f), 8);
                sb.Draw(Pixel, groove, PixelSrc, new Color(178, 164, 140) * (alpha * 0.75f));
                sb.Draw(Pixel, new Rectangle(groove.X, groove.Y, groove.Width, 1), PixelSrc,
                    new Color(120, 106, 88) * (alpha * 0.7f));
                int fill = (int)(groove.Width * row.DeedRatio);
                if (fill > 0) {
                    sb.Draw(Pixel, new Rectangle(groove.X, groove.Y, fill, groove.Height), PixelSrc,
                        OnikiriUITheme.Deep * (alpha * 0.92f));
                    OniBrush.DrawSoftDot(sb, new Vector2(groove.X + fill, groove.Center.Y), 4f,
                        OnikiriUITheme.Bright, alpha * 0.6f);
                }
                const float readScale = 0.84f;
                Vector2 size = font.MeasureString(text) * readScale;
                DrawPaperInk(sb, font, text,
                    new Vector2(groove.Right + 10f, groove.Y - size.Y * 0.2f),
                    PaperBody, readScale, alpha);
                return y + 24f;
            }
            DrawPaperInk(sb, font, text, new Vector2(x + 4f, y), PaperBody, DetailSectionBodyScale, alpha);
            return y + 26f;
        }

        private static float MeasureProgress(in OniMeiCodexRow row, float y) {
            y += 20f;
            if (row.DeedCountable) {
                return y + 24f;
            }
            return y + 26f;
        }

        //====================== 合卷牌 ======================

        /// <summary>册底右角一枚小木牌</summary>
        public static void DrawCloseTag(SpriteBatch sb, DynamicSpriteFont font, Rectangle rect,
            string text, float hover, float alpha) {
            Color face = Color.Lerp(new Color(62, 38, 28), new Color(104, 62, 42), hover);
            sb.Draw(Pixel, rect, PixelSrc, face * (alpha * 0.95f));
            DrawFrame(sb, rect, OnikiriUITheme.GoldDeep * (alpha * (0.35f + hover * 0.4f)), 1f);
            const float scale = 0.88f;
            Vector2 size = font.MeasureString(text) * scale;
            Utils.DrawBorderString(sb, text,
                new Vector2(rect.Center.X - size.X * 0.5f, rect.Center.Y - size.Y * 0.5f),
                Color.Lerp(OnikiriUITheme.Paper, OnikiriUITheme.HotWhite, hover) * alpha, scale);
        }

        //====================== 小工具 ======================

        private static Rectangle Inflate(Rectangle rect, int by)
            => new(rect.X - by, rect.Y - by, rect.Width + by * 2, rect.Height + by * 2);

        /// <summary>四边描一圈线框</summary>
        private static void DrawFrame(SpriteBatch sb, Rectangle rect, Color color, float thick) {
            int t = Math.Max(1, (int)thick);
            sb.Draw(Pixel, new Rectangle(rect.X, rect.Y, rect.Width, t), PixelSrc, color);
            sb.Draw(Pixel, new Rectangle(rect.X, rect.Bottom - t, rect.Width, t), PixelSrc, color);
            sb.Draw(Pixel, new Rectangle(rect.X, rect.Y, t, rect.Height), PixelSrc, color);
            sb.Draw(Pixel, new Rectangle(rect.Right - t, rect.Y, t, rect.Height), PixelSrc, color);
        }
    }
}
