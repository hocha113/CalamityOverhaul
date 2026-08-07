using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions;
using CalamityOverhaul.Content.UIs.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>
    /// 铭谱绘制：线装册子的封面/纸面/中缝/栞，左页名录格与页签，右页详情。<br/>
    /// 纹样一律走 <see cref="SvgPathPen"/> 的嵌入路径串——无贴图，缩放不糊
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
            sb.Draw(Pixel, page, PixelSrc, new Color(226, 214, 190) * (alpha * 0.97f));
            //纤维：几十道极淡的横纹，只调明度不改形
            for (int i = 0; i < 26; i++) {
                float u = Hash01(i * 71 + seed * 313);
                float y = page.Y + page.Height * u;
                float len = page.Width * (0.35f + Hash01(i * 37 + seed * 91) * 0.6f);
                float x = page.X + (page.Width - len) * Hash01(i * 53 + seed * 17);
                sb.Draw(Pixel, new Vector2(x, y), PixelSrc,
                    new Color(196, 182, 158) * (alpha * 0.22f),
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
            Vector2 size = font.MeasureString(head) * 0.92f;
            Utils.DrawBorderString(sb, head, center - size * 0.5f,
                OnikiriUITheme.Ink * alpha, 0.92f);

            //分槽：一行三段，满卷者标金
            float y = page.Y + 40f;
            for (int i = 0; i < 3; i++) {
                OniMeiSlotKind slot = (OniMeiSlotKind)i;
                OniMeiCodexTally tally = OniMeiCodexData.Tally(player, slot);
                string label = OniMeiCodexUI.TallySlotFormat.Format(
                    OniMeiCodexUI.TabLabel(i + 1), tally.Owned, tally.Total);
                float x = page.X + page.Width * (0.2f + i * 0.3f);
                Vector2 textSize = font.MeasureString(label) * 0.68f;
                bool full = tally.Owned >= tally.Total && tally.Total > 0;
                Utils.DrawBorderString(sb, label, new Vector2(x - textSize.X * 0.5f, y),
                    (full ? OnikiriUITheme.GoldDeep : OnikiriUITheme.TextDim) * alpha, 0.68f);
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

            Color ink = active ? OnikiriUITheme.Paper : OnikiriUITheme.TextDim;
            Utils.DrawBorderString(sb, label,
                new Vector2(body.X + 26f, body.Y + lift + 4f), ink * alpha, 0.72f);
        }

        //====================== 名录格 ======================

        /// <summary>
        /// 一格：字形在上、铭名在下。<br/>
        /// 已得走阴刻，未得整体压暗并罩一层薄墨——看得见轮廓，读不出内容
        /// </summary>
        public static void DrawCell(SpriteBatch sb, DynamicSpriteFont font, Rectangle rect,
            in OniMeiCodexRow row, bool selected, float hover, float alpha, float time) {
            float a = alpha;
            bool locked = !row.Owned;

            //选中：一枚压在纸上的浅色卡台 + 朱框
            if (selected) {
                Rectangle card = Inflate(rect, -4);
                sb.Draw(Pixel, card, PixelSrc, new Color(246, 238, 222) * (a * 0.85f));
                DrawFrame(sb, card, OnikiriUITheme.Seal * (a * 0.55f), 1f);
            }
            else if (hover > 0.01f) {
                Rectangle card = Inflate(rect, -6);
                sb.Draw(Pixel, card, PixelSrc, new Color(240, 230, 212) * (a * hover * 0.6f));
            }

            Vector2 glyphAt = new(rect.Center.X, rect.Y + rect.Height * 0.40f);
            float size = OnikiriUITheme.CodexCellGlyphSize * (1f + hover * 0.06f);

            OniMeiGlyphStyle style = OniMeiGlyphStyle.Engraved(a * (locked ? 0.5f : 1f));
            style.Time = time;
            style.Inlay = row.Gold && !locked ? 1f : 0f;
            style.Accent = locked
                ? OnikiriUITheme.Disabled
                : row.Gold ? OnikiriUITheme.GoldInlay : OnikiriUITheme.Bright;
            style.Lit = locked ? 0.02f : 0.10f + hover * 0.45f;
            OniMeiGlyph.Draw(sb, row.Key, glyphAt, size, style);

            //铭名：未得也照给——铭名本身就是线索
            string name = row.Name;
            float scale = 0.66f;
            Vector2 textSize = font.MeasureString(name) * scale;
            if (textSize.X > rect.Width - 8f) {
                scale *= (rect.Width - 8f) / Math.Max(1f, textSize.X);
                textSize = font.MeasureString(name) * scale;
            }
            Color nameColor = locked
                ? OnikiriUITheme.Disabled
                : row.Gold ? OnikiriUITheme.GoldDeep : OnikiriUITheme.Ink;
            Utils.DrawBorderString(sb, name,
                new Vector2(rect.Center.X - textSize.X * 0.5f, rect.Bottom - textSize.Y - 8f),
                nameColor * a, scale);

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
            //未得：底下一道未凿的凿口短线
            if (locked) {
                sb.Draw(Pixel, new Vector2(rect.Center.X - 9f, rect.Bottom - 5f), PixelSrc,
                    OnikiriUITheme.Disabled * (a * 0.55f), 0f, Vector2.Zero,
                    new Vector2(18f, 1f), SpriteEffects.None, 0f);
            }
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
            Vector2 size = font.MeasureString(label) * 0.66f;
            Utils.DrawBorderString(sb, label,
                new Vector2((prev.Center.X + next.Center.X) * 0.5f - size.X * 0.5f,
                    prev.Center.Y - size.Y * 0.5f),
                OnikiriUITheme.TextDim * alpha, 0.66f);
        }

        //====================== 右页详情 ======================

        /// <summary>
        /// 详情页：大字形（换选时按笔序重凿一遍）→ 铭名与徽记 → 分栏正文。<br/>
        /// 已得展全文，未得只给线索与縁分
        /// </summary>
        public static void DrawDetail(SpriteBatch sb, DynamicSpriteFont font, Rectangle page,
            in OniMeiCodexRow row, float reveal, float alpha, float time) {
            bool locked = !row.Owned;
            float y = page.Y + 20f;

            //大字形：衬一枚暗底章，字形压在上头
            Vector2 glyphAt = new(page.Center.X, y + OnikiriUITheme.CodexDetailGlyphSize * 0.5f);
            float g = OnikiriUITheme.CodexDetailGlyphSize;
            sb.Draw(Pixel, glyphAt, PixelSrc, OnikiriUITheme.Ink * (alpha * (locked ? 0.55f : 0.92f)),
                MathHelper.PiOver4, new Vector2(0.5f), new Vector2(g * 0.86f),
                SpriteEffects.None, 0f);
            sb.Draw(Pixel, glyphAt, PixelSrc, OnikiriUITheme.Paper * (alpha * 0.10f),
                MathHelper.PiOver4, new Vector2(0.5f), new Vector2(g * 0.74f),
                SpriteEffects.None, 0f);

            OniMeiGlyphStyle style = OniMeiGlyphStyle.Engraved(alpha * (locked ? 0.62f : 1f));
            style.Time = time;
            style.ChiselReveal = reveal;
            style.Inlay = row.Gold && !locked ? 1f : 0f;
            style.Accent = locked
                ? OnikiriUITheme.TextDim
                : row.Gold ? OnikiriUITheme.GoldInlay : OnikiriUITheme.Bright;
            style.Lit = locked ? 0.05f : 0.28f;
            OniMeiGlyph.Draw(sb, row.Key, glyphAt, g * 0.62f, style);
            y += g + 10f;

            //铭名
            string name = row.Name;
            Vector2 nameSize = font.MeasureString(name) * 1.18f;
            Utils.DrawBorderString(sb, name, new Vector2(page.Center.X - nameSize.X * 0.5f, y),
                (locked ? OnikiriUITheme.Disabled : OnikiriUITheme.Ink) * alpha, 1.18f);
            y += nameSize.Y + 4f;

            //徽记行：槽位 / 金象嵌 / 未凿 / 此刻在刀
            List<(string text, Color color)> badges = [
                (OniMeiCodexUI.TabLabel((int)row.Slot + 1), OnikiriUITheme.TextDim),
            ];
            if (row.Gold) {
                badges.Add((OniMeiUI.GoldMark?.Value ?? "", OnikiriUITheme.GoldDeep));
            }
            if (locked) {
                badges.Add((OniMeiCodexUI.LockedTitle.Value, OnikiriUITheme.Disabled));
            }
            if (row.Engraved) {
                badges.Add((OniMeiCodexUI.EngravedMark.Value, OnikiriUITheme.Seal));
            }
            DrawBadgeRow(sb, font, page.Center.X, y, badges, alpha);
            y += 22f;

            //分界朱线
            sb.Draw(Pixel, new Rectangle(page.X + 26, (int)y, page.Width - 52, 1), PixelSrc,
                OnikiriUITheme.Seal * (alpha * 0.45f));
            y += 12f;

            float wrapW = page.Width - 52f;
            float bodyX = page.X + 26f;
            if (locked) {
                y = DrawSection(sb, font, bodyX, y, wrapW, OniMeiCodexUI.SectionAcquire.Value,
                    OniMeiCodexData.AcquireLine(in row), OnikiriUITheme.Ink, alpha);
                y = DrawProgress(sb, font, bodyX, y, wrapW, in row, alpha);
                y = DrawSection(sb, font, bodyX, y, wrapW, OniMeiCodexUI.SectionSource.Value,
                    OniMeiCodexData.SourceLine(in row), OnikiriUITheme.TextDim, alpha);
                DrawSection(sb, font, bodyX, y, wrapW, "", OniMeiCodexUI.HiddenBody.Value,
                    OnikiriUITheme.Disabled, alpha * 0.85f);
                return;
            }

            y = DrawSection(sb, font, bodyX, y, wrapW, OniMeiUI.OriginLabel?.Value ?? "",
                row.Definition?.Origin?.Value ?? "", OnikiriUITheme.TextDim, alpha);
            y = DrawSection(sb, font, bodyX, y, wrapW, OniMeiUI.PowerLabel?.Value ?? "",
                row.Definition?.Power?.Value ?? "", OnikiriUITheme.Ink, alpha);
            y = DrawSection(sb, font, bodyX, y, wrapW, OniMeiUI.BurdenLabel?.Value ?? "",
                row.Definition?.Burden?.Value ?? "", new Color(126, 46, 40), alpha);
            DrawSection(sb, font, bodyX, y, wrapW, OniMeiCodexUI.SectionSource.Value,
                OniMeiCodexData.SourceLine(in row), OnikiriUITheme.TextDim, alpha * 0.9f);
        }

        /// <summary>一行小徽记，居中铺开</summary>
        private static void DrawBadgeRow(SpriteBatch sb, DynamicSpriteFont font, float centerX,
            float y, List<(string text, Color color)> badges, float alpha) {
            const float scale = 0.66f;
            const float gap = 10f;
            float total = 0f;
            foreach ((string text, _) in badges) {
                total += font.MeasureString(text).X * scale + gap + 12f;
            }
            float x = centerX - total * 0.5f;
            foreach ((string text, Color color) in badges) {
                float w = font.MeasureString(text).X * scale + 12f;
                Rectangle box = new((int)x, (int)y, (int)w, 18);
                sb.Draw(Pixel, box, PixelSrc, color * (alpha * 0.14f));
                DrawFrame(sb, box, color * (alpha * 0.45f), 1f);
                Utils.DrawBorderString(sb, text, new Vector2(x + 6f, y + 1f), color * alpha, scale);
                x += w + gap;
            }
        }

        /// <summary>标签 + 正文一段；返回下一段的起始 Y</summary>
        private static float DrawSection(SpriteBatch sb, DynamicSpriteFont font, float x, float y,
            float wrapW, string label, string body, Color bodyColor, float alpha) {
            if (string.IsNullOrWhiteSpace(body)) {
                return y;
            }
            if (!string.IsNullOrEmpty(label)) {
                Utils.DrawBorderString(sb, label, new Vector2(x, y),
                    OnikiriUITheme.Seal * (alpha * 0.85f), 0.68f);
                y += 17f;
            }
            const float scale = 0.72f;
            List<string> lines = VaultUtils.WrapText(body, font, wrapW, scale);
            foreach (string line in lines) {
                string text = line.TrimEnd();
                if (text.Length == 0) {
                    continue;
                }
                Utils.DrawBorderString(sb, text, new Vector2(x + 6f, y), bodyColor * alpha, scale);
                y += font.MeasureString("A").Y * scale + 2f;
            }
            return y + 8f;
        }

        /// <summary>縁分：一条凿槽式进度条 + 读数（不可计数的縁只出读数）</summary>
        private static float DrawProgress(SpriteBatch sb, DynamicSpriteFont font, float x, float y,
            float wrapW, in OniMeiCodexRow row, float alpha) {
            Utils.DrawBorderString(sb, OniMeiCodexUI.SectionProgress.Value, new Vector2(x, y),
                OnikiriUITheme.Seal * (alpha * 0.85f), 0.68f);
            y += 17f;

            string text = OniMeiCodexData.ProgressLine(in row);
            if (row.DeedCountable) {
                Rectangle groove = new((int)(x + 6f), (int)y + 4, (int)(wrapW - 90f), 7);
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
                Vector2 size = font.MeasureString(text) * 0.68f;
                Utils.DrawBorderString(sb, text,
                    new Vector2(groove.Right + 10f, groove.Y - size.Y * 0.25f),
                    OnikiriUITheme.Ink * alpha, 0.68f);
                return y + 20f;
            }
            Utils.DrawBorderString(sb, text, new Vector2(x + 6f, y),
                OnikiriUITheme.Ink * alpha, 0.72f);
            return y + 24f;
        }

        //====================== 合卷牌 ======================

        /// <summary>册底右角一枚小木牌</summary>
        public static void DrawCloseTag(SpriteBatch sb, DynamicSpriteFont font, Rectangle rect,
            string text, float hover, float alpha) {
            Color face = Color.Lerp(new Color(62, 38, 28), new Color(104, 62, 42), hover);
            sb.Draw(Pixel, rect, PixelSrc, face * (alpha * 0.95f));
            DrawFrame(sb, rect, OnikiriUITheme.GoldDeep * (alpha * (0.35f + hover * 0.4f)), 1f);
            Vector2 size = font.MeasureString(text) * 0.72f;
            Utils.DrawBorderString(sb, text,
                new Vector2(rect.Center.X - size.X * 0.5f, rect.Center.Y - size.Y * 0.5f),
                Color.Lerp(OnikiriUITheme.TextDim, OnikiriUITheme.Paper, hover) * alpha, 0.72f);
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
