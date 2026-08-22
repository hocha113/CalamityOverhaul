using CalamityOverhaul.Content.QuestLogs.Core;
using CalamityOverhaul.Content.QuestLogs.Styles.Chronicle;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.QuestLogs.Guide
{
    /// <summary>
    /// 教程卡：一张用蜡封摁在书页上的便笺。材质跟着书走，羊皮纸、褐墨、烫金压线、
    /// 手绘墨路。焦点不套高亮方框，小目标用铅笔手圈，长条目用左缘朱刻痕加一道铅笔划线
    /// </summary>
    internal static class QuestBookGuideRenderer
    {
        private static Texture2D Pixel => VaultAsset.placeholder2.Value;
        private static readonly Rectangle PixelSrc = new(0, 0, 1, 1);

        private const int CardW = 344;
        private const int PadX = 17;
        private const int PadTop = 15;
        private const int PadBottom = 12;
        private const int ButtonRowH = 30;
        private const int ScreenMargin = 22;
        private const int FocusGap = 26;

        private const float TitleScale = 0.94f;
        private const float BodyScale = 0.79f;
        private const float ButtonScale = 0.76f;
        private const float LinkScale = 0.72f;

        /// <summary>
        /// 本帧卡片占住的区域。任务书据此让开输入，否则点"下一步"会连带把图谱拖走
        /// </summary>
        public static Rectangle PointerBlock { get; private set; }

        /// <summary>上帧左键是否按着，点击只认按下沿，按住期间逐帧触发会把一次点击放大成连跳步</summary>
        private static bool prevMouseLeft;

        public static void ClearPointerBlock() => PointerBlock = Rectangle.Empty;

        public static void Draw(SpriteBatch sb) {
            bool leftEdge = Main.mouseLeft && !prevMouseLeft;
            prevMouseLeft = Main.mouseLeft;

            QuestBookGuidePlayer guide = QuestBookGuideFlow.LocalPlayer;
            if (guide == null || !QuestBookGuideFlow.IsRunningStep(guide.CurrentStep)) {
                ClearPointerBlock();
                return;
            }

            QuestBookStep step = guide.CurrentStep;
            float alpha = MathHelper.Clamp(guide.AnimProgress, 0f, 1f);
            float time = QuestBookGuideLead.ShaderTimer;
            int seed = (int)step * 37 + 11;

            GuideCopy copy = ResolveCopy(step, guide);
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            float contentW = CardW - PadX * 2;

            Rectangle focus = QuestBookGuideTargets.Resolve(step);
            int cardH = MeasureCard(copy, font, contentW, guide);
            Rectangle card = PlaceCard(focus, cardH);
            PointerBlock = card;

            DrawFocus(sb, focus, alpha, time, seed, guide.AnimProgress);
            DrawConnector(sb, card, focus, alpha, time, seed);
            DrawNote(sb, card, alpha, time, seed);
            DrawBody(sb, copy, font, card, contentW, alpha);
            DrawControls(sb, copy, font, card, alpha, time, guide, leftEdge);
        }

        #region 文案装配

        private readonly struct GuideCopy
        {
            public readonly string Title;
            public readonly string[] Lines;
            /// <summary>动手提示，朱色单起一行；讲解步为空</summary>
            public readonly string Act;
            /// <summary>主按钮文字；动手步为空，逼玩家真去做</summary>
            public readonly string Button;
            /// <summary>标题是否用警示色（未绑键那一版）</summary>
            public readonly bool Warn;

            public GuideCopy(string title, string[] lines, string act = null,
                string button = null, bool warn = false) {
                Title = title;
                Lines = lines;
                Act = act;
                Button = button;
                Warn = warn;
            }
        }

        private static GuideCopy ResolveCopy(QuestBookStep step, QuestBookGuidePlayer guide) {
            switch (step) {
                case QuestBookStep.Welcome:
                    return new GuideCopy(QuestBookGuideLead.WelcomeTitle.Value, [
                        QuestBookGuideLead.WelcomeLine1.Value,
                        QuestBookGuideLead.WelcomeLine2.Value,
                    ], button: QuestBookGuideLead.BtnNext.Value);

                //书不在图谱站时这步是动手步：让玩家自己点一次书口，
                //否则后面讲图谱的几步全都对不上画面
                case QuestBookStep.Rail:
                    if (QuestLog.Instance?.View != QuestLogView.Chart) {
                        return new GuideCopy(QuestBookGuideLead.RailTitle.Value, [
                            QuestBookGuideLead.RailLine1.Value,
                            QuestBookGuideLead.RailLine2.Value,
                        ], act: QuestBookGuideLead.RailAct.Value);
                    }
                    return new GuideCopy(QuestBookGuideLead.RailTitle.Value, [
                        QuestBookGuideLead.RailLine1.Value,
                        QuestBookGuideLead.RailLine2.Value,
                        QuestBookGuideLead.RailLine3.Value,
                    ], button: QuestBookGuideLead.BtnConfirm.Value);

                case QuestBookStep.ChartView:
                    return new GuideCopy(QuestBookGuideLead.ChartViewTitle.Value, [
                        QuestBookGuideLead.ChartViewLine1.Value,
                        QuestBookGuideLead.ChartViewLine2.Value,
                    ], act: QuestBookGuideLead.ChartViewAct.Value);

                case QuestBookStep.ChartNode:
                    return new GuideCopy(QuestBookGuideLead.ChartNodeTitle.Value, [
                        QuestBookGuideLead.ChartNodeLine1.Value,
                    ], act: QuestBookGuideLead.ChartNodeAct.Value);

                case QuestBookStep.ChartDetail:
                    return new GuideCopy(QuestBookGuideLead.ChartDetailTitle.Value, [
                        QuestBookGuideLead.ChartDetailLine1.Value,
                        QuestBookGuideLead.ChartDetailLine2.Value,
                    ], button: QuestBookGuideLead.BtnConfirm.Value);

                case QuestBookStep.ChapterOneOutro:
                    return new GuideCopy(QuestBookGuideLead.OutroTitle.Value, [
                        QuestBookGuideLead.OutroLine1.Value,
                        QuestBookGuideLead.OutroLine2.Value,
                    ], button: QuestBookGuideLead.BtnConfirm.Value);

                case QuestBookStep.KeyPrompt:
                    return ResolveKeyPromptCopy();

                case QuestBookStep.GotoEntrust:
                    return new GuideCopy(QuestBookGuideLead.GotoTitle.Value, [
                        QuestBookGuideLead.GotoLine1.Value,
                    ], act: QuestBookGuideLead.GotoAct.Value);

                case QuestBookStep.EntryAnatomy:
                    return new GuideCopy(QuestBookGuideLead.AnatomyTitle.Value, [
                        QuestBookGuideLead.AnatomyLine1.Value,
                        QuestBookGuideLead.AnatomyLine2.Value,
                    ], act: QuestBookGuideLead.AnatomyAct.Value);

                //样本行接进卷宗时就被自动关注了，「右键→关注」在它身上是反的，
                //改教一次取消与恢复
                case QuestBookStep.TrackEntry:
                    if (guide.TrackEntryPreTracked) {
                        return new GuideCopy(QuestBookGuideLead.TrackTitle.Value, [
                            QuestBookGuideLead.TrackAltLine1.Value,
                        ], act: QuestBookGuideLead.TrackAltAct.Value);
                    }
                    return new GuideCopy(QuestBookGuideLead.TrackTitle.Value, [
                        QuestBookGuideLead.TrackLine1.Value,
                    ], act: QuestBookGuideLead.TrackAct.Value);

                case QuestBookStep.TrackerWidget:
                    return new GuideCopy(QuestBookGuideLead.TrackerTitle.Value, [
                        QuestBookGuideLead.TrackerLine1.Value,
                        QuestBookGuideLead.TrackerLine2.Value,
                        QuestBookGuideLead.TrackerLine3.Value,
                    ], button: QuestBookGuideLead.BtnNext.Value);

                default:
                    return new GuideCopy(QuestBookGuideLead.SuspendTitle.Value, [
                        QuestBookGuideLead.SuspendLine1.Value,
                        QuestBookGuideLead.SuspendLine2.Value,
                        QuestBookGuideLead.SuspendLine3.Value,
                    ], button: QuestBookGuideLead.BtnConfirm.Value);
            }
        }

        private static GuideCopy ResolveKeyPromptCopy() {
            string bound = QuestBookGuideLead.BoundKeyName();
            if (bound != null) {
                return new GuideCopy(QuestBookGuideLead.KeyPromptTitle.Value, [
                    QuestBookGuideLead.KeyPromptLine1.Value,
                    QuestBookGuideLead.KeyPromptBound.Format(bound),
                ], button: QuestBookGuideLead.BtnOpenBook.Value);
            }
            //键被清空了也得给条活路：说清默认键，再留一个直接开的按钮
            return new GuideCopy(QuestBookGuideLead.KeyPromptUnboundTitle.Value, [
                QuestBookGuideLead.KeyPromptUnboundLine.Format("L"),
                QuestBookGuideLead.KeyPromptBindHint.Value,
            ], button: QuestBookGuideLead.BtnOpenBook.Value, warn: true);
        }

        #endregion

        #region 版面

        private static int MeasureCard(in GuideCopy copy, DynamicSpriteFont font, float contentW,
            QuestBookGuidePlayer guide) {
            float lineH = font.MeasureString("A").Y;
            float h = PadTop;
            h += lineH * TitleScale + 5f;
            //题下压一道金线
            h += 9f;
            foreach (string line in copy.Lines) {
                h += ChroniclePen.Wrap(font, line, contentW, BodyScale).Count * (lineH * BodyScale + 3f);
                h += 3f;
            }
            if (!string.IsNullOrEmpty(copy.Act)) {
                h += 6f;
                h += ChroniclePen.Wrap(font, copy.Act, contentW, BodyScale).Count * (lineH * BodyScale + 3f);
            }
            h += HasControlRow(in copy, guide) ? ButtonRowH + 6f : 4f;
            h += PadBottom;
            return (int)MathF.Ceiling(h);
        }

        private static bool HasControlRow(in GuideCopy copy, QuestBookGuidePlayer guide)
            => !string.IsNullOrEmpty(copy.Button) || guide.SkipOffered || guide.AutoAdvanceDelay > 0;

        /// <summary>
        /// 卡片站位：绕开焦点，依次试右 / 左 / 下 / 上，都塞不下就退到屏幕右下。<br/>
        /// 焦点缺席时居中偏上
        /// </summary>
        private static Rectangle PlaceCard(Rectangle focus, int cardH) {
            int sw = (int)QuestLogTheme.UIScreenW;
            int sh = (int)QuestLogTheme.UIScreenH;

            if (focus.Width <= 0) {
                return new Rectangle((sw - CardW) / 2, (int)(sh * 0.42f) - cardH / 2, CardW, cardH);
            }

            int centeredY = Math.Clamp(focus.Center.Y - cardH / 2, ScreenMargin, sh - cardH - ScreenMargin);
            int centeredX = Math.Clamp(focus.Center.X - CardW / 2, ScreenMargin, sw - CardW - ScreenMargin);

            int right = focus.Right + FocusGap;
            if (right + CardW <= sw - ScreenMargin) {
                return new Rectangle(right, centeredY, CardW, cardH);
            }
            int left = focus.X - FocusGap - CardW;
            if (left >= ScreenMargin) {
                return new Rectangle(left, centeredY, CardW, cardH);
            }
            int below = focus.Bottom + FocusGap;
            if (below + cardH <= sh - ScreenMargin) {
                return new Rectangle(centeredX, below, CardW, cardH);
            }
            int above = focus.Y - FocusGap - cardH;
            if (above >= ScreenMargin) {
                return new Rectangle(centeredX, above, CardW, cardH);
            }
            //焦点铺满一整页（画布这类），卡片退到右下角
            return new Rectangle(sw - CardW - ScreenMargin, sh - cardH - ScreenMargin, CardW, cardH);
        }

        #endregion

        #region 便笺

        private static void DrawNote(SpriteBatch sb, Rectangle card, float alpha, float time, int seed) {
            //贴身投影：只偏不放大，放大就成了方块黑层
            sb.Draw(Pixel, new Rectangle(card.X + 3, card.Y + 4, card.Width, card.Height), PixelSrc,
                ChroniclePalette.LeatherDeep * (alpha * 0.55f));
            sb.Draw(Pixel, card, PixelSrc, ChroniclePalette.Paper * alpha);

            //帘纹纤维，位置全走确定性散列
            int fibers = Math.Max(8, card.Height / 13);
            for (int i = 0; i < fibers; i++) {
                float y = card.Y + card.Height * QuestLogTheme.Hash01(seed + i * 71 + 11);
                float len = card.Width * (0.3f + QuestLogTheme.Hash01(seed + i * 53 + 17) * 0.58f);
                float x = card.X + (card.Width - len) * QuestLogTheme.Hash01(seed + i * 31 + 23);
                sb.Draw(Pixel, new Vector2(x, y), PixelSrc,
                    ChroniclePalette.PaperDeep * (alpha * 0.09f), 0f, Vector2.Zero,
                    new Vector2(len, 1f), SpriteEffects.None, 0f);
            }

            //页缘吃暗，四边各收一点
            for (int i = 0; i < 9; i++) {
                float fade = 1f - i / 9f;
                Color edge = ChroniclePalette.PaperDeep * (alpha * 0.12f * fade * fade);
                sb.Draw(Pixel, new Rectangle(card.X + i, card.Y, 1, card.Height), PixelSrc, edge);
                sb.Draw(Pixel, new Rectangle(card.Right - i - 1, card.Y, 1, card.Height), PixelSrc, edge);
                sb.Draw(Pixel, new Rectangle(card.X, card.Y + i, card.Width, 1), PixelSrc, edge);
                sb.Draw(Pixel, new Rectangle(card.X, card.Bottom - i - 1, card.Width, 1), PixelSrc, edge);
            }

            //蜡封当图钉，压在纸的上缘咬住书页
            ChroniclePen.WaxSeal(sb, new Vector2(card.X + 21f, card.Y + 2f), 11.5f, alpha,
                seed, time, broken: false, live: true);
        }

        private static void DrawBody(SpriteBatch sb, in GuideCopy copy, DynamicSpriteFont font,
            Rectangle card, float contentW, float alpha) {
            float lineH = font.MeasureString("A").Y;
            float x = card.X + PadX;
            float y = card.Y + PadTop;

            //题目让开蜡封，从纸的中段起笔
            Color titleColor = copy.Warn ? ChroniclePalette.Seal : ChroniclePalette.Ink;
            ChroniclePen.Ink(sb, font, copy.Title, new Vector2(x + 26f, y), titleColor, TitleScale, alpha);
            y += lineH * TitleScale + 5f;

            ChroniclePen.GiltRule(sb, new Vector2(x, y + 2f), contentW, alpha * 0.85f);
            y += 9f;

            foreach (string line in copy.Lines) {
                foreach (string wrapped in ChroniclePen.Wrap(font, line, contentW, BodyScale)) {
                    ChroniclePen.Ink(sb, font, wrapped, new Vector2(x, y),
                        ChroniclePalette.InkMute, BodyScale, alpha);
                    y += lineH * BodyScale + 3f;
                }
                y += 3f;
            }

            if (string.IsNullOrEmpty(copy.Act)) {
                return;
            }
            //动手提示走朱墨，与讲解正文分开
            y += 6f;
            foreach (string wrapped in ChroniclePen.Wrap(font, copy.Act, contentW, BodyScale)) {
                ChroniclePen.Ink(sb, font, wrapped, new Vector2(x, y),
                    ChroniclePalette.Seal, BodyScale, alpha);
                y += lineH * BodyScale + 3f;
            }
        }

        #endregion

        #region 按键行

        private static void DrawControls(SpriteBatch sb, in GuideCopy copy, DynamicSpriteFont font,
            Rectangle card, float alpha, float time, QuestBookGuidePlayer guide, bool leftEdge) {
            //倒计推进期间画一道金填，让玩家看清"它自己在往下走"
            if (guide.AutoAdvanceDelay > 0 && guide.AutoAdvanceTotal > 0) {
                float progress = 1f - guide.AutoAdvanceDelay / (float)guide.AutoAdvanceTotal;
                var bar = new Rectangle(card.X + PadX, card.Bottom - 7,
                    (int)((card.Width - PadX * 2) * MathHelper.Clamp(progress, 0f, 1f)), 2);
                sb.Draw(Pixel, bar, PixelSrc, ChroniclePalette.Gold * (alpha * 0.9f));
            }

            if (!HasControlRow(in copy, guide)) {
                return;
            }

            int rowY = card.Bottom - PadBottom - ButtonRowH;
            int cursorRight = card.Right - PadX;
            bool locked = guide.AutoAdvanceDelay > 0;

            if (!string.IsNullOrEmpty(copy.Button)) {
                int w = (int)Math.Clamp(font.MeasureString(copy.Button).X * ButtonScale + 30f, 86f,
                    card.Width - PadX * 2);
                var rect = new Rectangle(cursorRight - w, rowY, w, ButtonRowH - 4);
                if (BrassButton(sb, rect, copy.Button, font, alpha, time, locked, leftEdge)) {
                    guide.ConfirmStep();
                }
                cursorRight = rect.X - 10;
            }

            if (guide.SkipOffered) {
                string skip = QuestBookGuideLead.BtnSkipStep.Value;
                Vector2 size = font.MeasureString(skip) * LinkScale;
                var rect = new Rectangle(cursorRight - (int)size.X, rowY + 6, (int)size.X, (int)size.Y);
                if (InkLink(sb, rect, skip, font, alpha, locked, leftEdge)) {
                    guide.SkipStep();
                }
            }

            //收起教程常驻左下：任何一步都能走，走了还能从书里的「?」回来
            string dismiss = QuestBookGuideLead.BtnDismiss.Value;
            Vector2 dismissSize = font.MeasureString(dismiss) * LinkScale;
            var dismissRect = new Rectangle(card.X + PadX, rowY + 6,
                (int)dismissSize.X, (int)dismissSize.Y);
            if (InkLink(sb, dismissRect, dismiss, font, alpha, locked, leftEdge)) {
                guide.Dismiss();
            }
        }

        private static bool BrassButton(SpriteBatch sb, Rectangle rect, string label,
            DynamicSpriteFont font, float alpha, float time, bool locked, bool leftEdge) {
            bool hovered = !locked && rect.Contains(Main.mouseX, Main.mouseY);
            ChroniclePen.BrassTag(sb, rect, hovered, alpha, time);

            Vector2 size = font.MeasureString(label) * ButtonScale;
            ChroniclePen.LeatherInk(sb, font, label,
                new Vector2(rect.X + (rect.Width - size.X) * 0.5f, rect.Y + (rect.Height - size.Y) * 0.5f),
                hovered ? ChroniclePalette.Candle : ChroniclePalette.BrassHi, ButtonScale, alpha);

            if (hovered) {
                Main.LocalPlayer.mouseInterface = true;
            }
            return hovered && leftEdge;
        }

        /// <summary>纸上的次级操作：一行淡墨字，悬停转朱并划一道下线</summary>
        private static bool InkLink(SpriteBatch sb, Rectangle rect, string label,
            DynamicSpriteFont font, float alpha, bool locked, bool leftEdge) {
            Rectangle hit = rect;
            hit.Inflate(5, 5);
            bool hovered = !locked && hit.Contains(Main.mouseX, Main.mouseY);

            ChroniclePen.Ink(sb, font, label, new Vector2(rect.X, rect.Y),
                hovered ? ChroniclePalette.Seal : ChroniclePalette.InkFaint, LinkScale, alpha);
            if (hovered) {
                ChroniclePen.Line(sb, new Vector2(rect.X, rect.Bottom + 1f),
                    new Vector2(rect.Right, rect.Bottom + 1f), 1f, ChroniclePalette.Seal, alpha * 0.7f);
                Main.LocalPlayer.mouseInterface = true;
            }
            return hovered && leftEdge;
        }

        #endregion

        #region 焦点标记与引线

        private static void DrawFocus(SpriteBatch sb, Rectangle focus,
            float alpha, float time, int seed, float reveal) {
            if (focus.Width <= 0 || focus.Height <= 0) {
                return;
            }
            //焦点铺满大半页时不做标记：满页画一圈就成了假边框
            float coverage = focus.Width * focus.Height
                / MathF.Max(1f, QuestLogTheme.UIScreenW * QuestLogTheme.UIScreenH);
            if (coverage > 0.45f) {
                return;
            }

            float breath = QuestLogTheme.Breath(time, seed * 0.37f, 6f);

            //小目标：一记铅笔手圈，绕一圈甩出尾巴。
            //上限要容得下满缩放(2.0)时的节点矩形(2*(24*2+10)=116)，否则一放大就变成刻痕划线
            int longest = Math.Max(focus.Width, focus.Height);
            if (longest <= 128 && focus.Width < focus.Height * 2.4f) {
                float radius = longest * 0.62f + 7f + breath * 2.5f;
                ChroniclePen.CircleMark(sb, focus.Center.ToVector2(), radius,
                    ChroniclePalette.Seal, alpha * (0.7f + breath * 0.25f), seed, reveal);
                return;
            }

            //长条目：左缘一记朱刻痕 + 下缘一道手划的铅笔线
            float notchAlpha = alpha * (0.65f + breath * 0.3f);
            ChroniclePen.Line(sb, new Vector2(focus.X - 6f, focus.Y + 4f),
                new Vector2(focus.X - 6f, focus.Bottom - 4f), 3f, ChroniclePalette.Seal, notchAlpha);
            DrawPencilUnderline(sb, focus, notchAlpha, seed, reveal);
        }

        /// <summary>顺着下缘划的一道，笔走不直、起笔重收笔轻</summary>
        private static void DrawPencilUnderline(SpriteBatch sb, Rectangle focus, float alpha,
            int seed, float reveal) {
            const int Steps = 16;
            float span = focus.Width * MathHelper.Clamp(reveal, 0.05f, 1f);
            float baseY = focus.Bottom + 4f;
            Vector2 prev = new(focus.X, baseY + (QuestLogTheme.Hash01(seed) - 0.5f) * 2f);
            for (int i = 1; i <= Steps; i++) {
                float t = i / (float)Steps;
                float wobble = (QuestLogTheme.Hash01(seed * 13 + i * 7) - 0.5f) * 3.2f
                    * MathF.Sin(t * MathHelper.Pi);
                Vector2 next = new(focus.X + span * t, baseY + wobble);
                ChroniclePen.Line(sb, prev, next, MathHelper.Lerp(2.2f, 1f, t),
                    ChroniclePalette.Seal, alpha * 0.8f);
                prev = next;
            }
        }

        /// <summary>卡片与焦点之间的一条手绘墨路，路上有一点巡行的亮</summary>
        private static void DrawConnector(SpriteBatch sb, Rectangle card, Rectangle focus,
            float alpha, float time, int seed) {
            if (focus.Width <= 0 || card.Intersects(focus)) {
                return;
            }
            Vector2 from = NearestEdgePoint(card, focus.Center.ToVector2());
            Vector2 to = NearestEdgePoint(focus, card.Center.ToVector2());
            if (Vector2.Distance(from, to) < 26f) {
                return;
            }
            ChroniclePen.InkRoute(sb, from, to, unlocked: true, alpha * 0.85f, seed, time);
        }

        private static Vector2 NearestEdgePoint(Rectangle rect, Vector2 toward) {
            return new Vector2(
                MathHelper.Clamp(toward.X, rect.X, rect.Right),
                MathHelper.Clamp(toward.Y, rect.Y, rect.Bottom));
        }

        #endregion
    }
}
