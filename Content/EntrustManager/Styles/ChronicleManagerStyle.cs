using CalamityOverhaul.Content.QuestLogs.Core;
using CalamityOverhaul.Content.QuestLogs.Styles.Chronicle;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.EntrustManager.Styles
{
    /// <summary>
    /// 委托卷宗的「远征纪要」皮肤：铺在任务书的羊皮纸内页上。<br/>
    /// 条目没有底盒——负空间 + 发丝线分行，左缘一记状态记号，进度走凿槽刻度。<br/>
    /// 面板级绘制（底衬/外框/粒子/样式键）在内嵌态不会被调用，故一概留空
    /// </summary>
    internal class ChronicleManagerStyle : BaseManagerStyle
    {
        private static DynamicSpriteFont Font => FontAssets.MouseText.Value;

        public override int GetEntryHeight() => 56;

        public override int GetEntryPadding() => 6;

        #region 面板级：内嵌态不参与

        public override void DrawPanelBackground(SpriteBatch sb, Rectangle panelRect, float alpha) { }

        public override void DrawPanelFrame(SpriteBatch sb, Rectangle panelRect, float alpha) { }

        public override void DrawParticles(SpriteBatch sb, Rectangle panelRect, float alpha) { }

        public override void DrawOverlayEffects(SpriteBatch sb, Rectangle panelRect, float alpha) { }

        public override Rectangle GetStyleSwitchButtonRect(Rectangle panelRect) => Rectangle.Empty;

        public override void DrawStyleSwitchButton(SpriteBatch sb, Rectangle panelRect,
            bool isHovered, float alpha) { }

        public override void DrawHeader(SpriteBatch sb, Rectangle headerRect, string title, float alpha) {
            ChroniclePen.Ink(sb, Font, title, new Vector2(headerRect.X + 10f, headerRect.Y + 8f),
                ChroniclePalette.Ink, 0.92f, alpha);
        }

        #endregion

        #region 页签带与页脚

        /// <summary>分类页签：纸面小字 + 选中的一道压痕，不是标签页盒子</summary>
        public override void DrawCategoryTabs(SpriteBatch sb, Rectangle tabRect, string[] categories,
            int selectedIndex, float alpha) {
            if (categories == null) {
                return;
            }
            const float Scale = 0.72f;
            float x = tabRect.X + 6f;
            for (int i = 0; i < categories.Length; i++) {
                string label = categories[i] ?? string.Empty;
                float w = Font.MeasureString(label).X * Scale + 18f;
                bool selected = i == selectedIndex;
                Vector2 pos = new(x + 9f, tabRect.Y + 5f);

                ChroniclePen.Ink(sb, Font, label, pos,
                    selected ? ChroniclePalette.Ink : ChroniclePalette.InkFaint, Scale, alpha);

                if (selected) {
                    //选中：一道金压线钉住这一类
                    ChroniclePen.GiltRule(sb, new Vector2(x + 8f, tabRect.Y + 20f),
                        w - 16f, alpha * 0.9f, 1.2f, false);
                }
                x += w + 3f;
            }
            //页签带与列表之间一道压痕
            ChroniclePen.Groove(sb, new Vector2(tabRect.X + 4f, tabRect.Bottom - 2f),
                tabRect.Width - 12f, alpha * 0.75f);
        }

        public override void DrawFooter(SpriteBatch sb, Rectangle footerRect, int totalQuests,
            int activeQuests, float alpha) {
            string text = QuestManagerUI.FooterStatsFormat?.Format(totalQuests, activeQuests)
                ?? $"{totalQuests} / {activeQuests}";
            ChroniclePen.Groove(sb, new Vector2(footerRect.X + 4f, footerRect.Y + 1f),
                footerRect.Width - 12f, alpha * 0.6f);
            ChroniclePen.Ink(sb, Font, text, new Vector2(footerRect.X + 10f, footerRect.Y + 7f),
                ChroniclePalette.InkMute, 0.7f, alpha * 0.9f);
        }

        /// <summary>悬停提示：页脚上方右对齐的淡墨小字，不用描边</summary>
        public override void DrawInteractionHints(SpriteBatch sb, Rectangle footerRect,
            EntrustEntryData entry, float alpha) {
            float hintY = footerRect.Y - 17f;
            const float Scale = 0.6f;

            void InkHint(string text, Color color) {
                if (string.IsNullOrEmpty(text)) {
                    return;
                }
                float w = Font.MeasureString(text).X * Scale;
                ChroniclePen.Ink(sb, Font, text, new Vector2(footerRect.Right - w - 12f, hintY),
                    color, Scale, alpha * 0.85f);
                hintY -= 14f;
            }

            if (entry.Status is QuestEntryStatus.Active or QuestEntryStatus.Tracked
                or QuestEntryStatus.Suspended) {
                InkHint(QuestManagerUI.SuspendHintText.Value, ChroniclePalette.InkFaint);
            }
            if (entry.Status is QuestEntryStatus.Active or QuestEntryStatus.Tracked) {
                InkHint(QuestManagerUI.TrackHintText.Value, ChroniclePalette.GoldDeep);
            }
            InkHint(QuestManagerUI.ExpandHintText.Value, ChroniclePalette.InkMute);
        }

        /// <summary>空态：纸上一行淡墨，两侧各一道短压痕托住</summary>
        public override void DrawEmptyHint(SpriteBatch sb, Rectangle contentRect, string text, float alpha) {
            Vector2 center = contentRect.Center.ToVector2();
            ChroniclePen.InkCentered(sb, Font, text, center, ChroniclePalette.InkFaint, 0.78f, alpha * 0.9f);
            float w = Font.MeasureString(text ?? string.Empty).X * 0.78f;
            ChroniclePen.Groove(sb, new Vector2(center.X - w * 0.5f - 34f, center.Y + 2f), 26f, alpha * 0.6f);
            ChroniclePen.Groove(sb, new Vector2(center.X + w * 0.5f + 8f, center.Y + 2f), 26f, alpha * 0.6f);
        }

        /// <summary>溢出指示：右缘一道随滚动走的朱迹，不是现代滑块</summary>
        public override void DrawScrollbar(SpriteBatch sb, Rectangle trackRect, float scrollRatio,
            float viewRatio, float alpha) {
            float markH = Math.Max(24f, trackRect.Height * MathHelper.Clamp(viewRatio, 0.06f, 1f));
            float y = MathHelper.Lerp(trackRect.Y, trackRect.Bottom - markH,
                MathHelper.Clamp(scrollRatio, 0f, 1f));
            ChroniclePen.Line(sb, new Vector2(trackRect.Center.X, y),
                new Vector2(trackRect.Center.X, y + markH), 2f, ChroniclePalette.Seal, alpha * 0.55f);
        }

        #endregion

        #region 条目

        public override void DrawEntrySeparator(SpriteBatch sb, Vector2 start, Vector2 end, float alpha) {
            ChroniclePen.HairLine(sb, start, end.X - start.X, alpha * 0.8f);
        }

        public override void DrawQuestEntry(SpriteBatch sb, Rectangle entryRect, EntrustEntryData entry,
            bool isSelected, bool isHovered, float alpha, int entryIndex) {
            //自定义条目皮肤优先，它自己画底
            bool bgHandled = entry.EntryStyle?.DrawEntryBackground(sb, entryRect, entry,
                isSelected, isHovered, alpha) ?? false;

            if (!bgHandled && isHovered) {
                //悬停：左缘一记朱刻痕，纸不压底色
                ChroniclePen.Line(sb, new Vector2(entryRect.X + 2f, entryRect.Y + 4f),
                    new Vector2(entryRect.X + 2f, entryRect.Y + GetEntryHeight() - 6f),
                    2.4f, ChroniclePalette.Seal, alpha * 0.8f);
            }

            //左缘状态记号：与图谱同一支笔
            Vector2 mark = new(entryRect.X + 18f, entryRect.Y + 17f);
            DrawStatusMark(sb, mark, entry.Status, alpha, entryIndex);

            float titleX = entryRect.X + 34f;
            float titleY = entryRect.Y + 6f;
            float iconOffset = entry.EntryStyle?.DrawEntryIcon(sb, new Vector2(titleX, titleY), entry, alpha) ?? 0f;
            titleX += iconOffset;

            //右侧状态明文，纯字不画徽章底框
            string statusText = GetEntryStatusText(entry.Status);
            const float StatusScale = 0.62f;
            float statusW = Font.MeasureString(statusText).X * StatusScale;
            float statusX = entryRect.Right - statusW - 16f;
            ChroniclePen.Ink(sb, Font, statusText, new Vector2(statusX, titleY + 2f),
                StatusInk(entry.Status), StatusScale, alpha * 0.95f);

            //标题，按剩余宽截断
            Color titleColor = entry.EntryStyle?.GetTitleColor(entry.Status, alpha)
                ?? (isHovered ? ChroniclePalette.Ink : StatusInk(entry.Status));
            string title = Shorten(entry.Title, Math.Max(40f, statusX - titleX - 10f), 0.88f);
            ChroniclePen.Ink(sb, Font, title, new Vector2(titleX, titleY), titleColor, 0.88f, alpha);

            //关注：标题后一枚小蜡点，比符号更像"被人挑出来的一条"
            if (entry.Status == QuestEntryStatus.Tracked) {
                float titleW = Font.MeasureString(title).X * 0.88f;
                if (titleX + titleW + 14f < statusX) {
                    ChroniclePen.WaxSeal(sb, new Vector2(titleX + titleW + 8f, titleY + 9f), 4.6f,
                        alpha, entryIndex * 7 + 3, globalTimer, false, true);
                }
            }

            //收起态：单行摘要
            float collapsed = 1f - entry.ExpandProgress;
            float summaryY = titleY + 20f;
            if (collapsed > 0.01f) {
                string summary = (entry.Summary ?? string.Empty)
                    .Replace("\r", string.Empty).Replace("\n", " ").Trim();
                summary = Shorten(summary, entryRect.Width - 60f - iconOffset, 0.72f);
                ChroniclePen.Ink(sb, Font, summary, new Vector2(titleX, summaryY),
                    ChroniclePalette.InkFaint, 0.72f, alpha * collapsed);
            }

            //可展开：右缘一记折角，展开后翻向上
            if (!string.IsNullOrEmpty(entry.Summary)) {
                DrawFoldCorner(sb, new Vector2(entryRect.Right - 12f, titleY + 8f),
                    entry.IsExpanded, alpha * (isHovered ? 0.9f : 0.5f));
            }

            //展开态：金压线 + 正文
            if (entry.ExpandProgress > 0.01f) {
                DrawExpanded(sb, entryRect, entry, titleX, alpha);
            }

            //进度：凿槽刻度 + 读数
            if (entry.Progress > 0f && entry.Status != QuestEntryStatus.Completed) {
                float barY = entry.ExpandProgress > 0.5f ? entryRect.Bottom - 13f : summaryY + 19f;
                int barW = Math.Min(132, entryRect.Width - 70);
                if (barW > 24) {
                    ChroniclePen.Tally(sb, new Rectangle((int)titleX, (int)barY, barW, 6),
                        entry.Progress, 12, alpha * 0.95f);
                    if (entry.ProgressText != null) {
                        ChroniclePen.Ink(sb, Font, entry.ProgressText,
                            new Vector2(titleX + barW + 10f, barY - 4f),
                            ChroniclePalette.GoldDeep, 0.64f, alpha * 0.95f);
                    }
                }
            }

            entry.EntryStyle?.DrawEntryOverlay(sb, entryRect, entry, alpha);
        }

        private void DrawExpanded(SpriteBatch sb, Rectangle entryRect, EntrustEntryData entry,
            float titleX, float alpha) {
            float expandAlpha = alpha * entry.ExpandProgress;
            float y = entryRect.Y + GetEntryHeight() - 6f;
            float wrapW = entryRect.Width - (titleX - entryRect.X) - 18f;

            ChroniclePen.GiltRule(sb, new Vector2(titleX, y), wrapW * 0.8f, expandAlpha * 0.85f);
            y += 8f;

            const float Scale = 0.72f;
            float line = Font.MeasureString("A").Y * Scale;
            foreach (string row in ChroniclePen.Wrap(Font, entry.Summary, wrapW, Scale)) {
                if (y > entryRect.Bottom - 4f) {
                    break;
                }
                ChroniclePen.Ink(sb, Font, row, new Vector2(titleX, y),
                    ChroniclePalette.InkMute, Scale, expandAlpha);
                y += line + 2f;
            }
        }

        /// <summary>折角：两笔一记纸角，展开后朝上翻</summary>
        private static void DrawFoldCorner(SpriteBatch sb, Vector2 pos, bool expanded, float alpha) {
            float dir = expanded ? -1f : 1f;
            Vector2 a = pos + new Vector2(-5f, -3f * dir);
            Vector2 b = pos + new Vector2(5f, -3f * dir);
            Vector2 c = pos + new Vector2(0f, 4f * dir);
            ChroniclePen.Line(sb, a, c, 1.5f, ChroniclePalette.InkMute, alpha);
            ChroniclePen.Line(sb, b, c, 1.5f, ChroniclePalette.InkMute, alpha);
            ChroniclePen.Line(sb, a + new Vector2(0f, 1.4f * dir), b + new Vector2(0f, 1.4f * dir),
                1f, ChroniclePalette.Candle, alpha * 0.35f);
        }

        /// <summary>条目状态记号：进行中=墨窝，关注=金窝，已结=裂蜡，失败=划去</summary>
        private void DrawStatusMark(SpriteBatch sb, Vector2 center, QuestEntryStatus status,
            float alpha, int seed) {
            switch (status) {
                case QuestEntryStatus.Completed:
                    ChroniclePen.NodeWell(sb, center, 8f, ChroniclePalette.InkMute, alpha, 1.3f);
                    ChroniclePen.WaxSeal(sb, center + new Vector2(3f, 3f), 5.5f, alpha,
                        seed * 11 + 5, globalTimer, true);
                    break;
                case QuestEntryStatus.Tracked:
                    ChroniclePen.NodeWell(sb, center, 8f, ChroniclePalette.Gold, alpha, 1.5f);
                    break;
                case QuestEntryStatus.Suspended:
                    ChroniclePen.NodeWell(sb, center, 8f, ChroniclePalette.InkFaint, alpha, 1.2f);
                    ChroniclePen.HatchDisc(sb, center, 6.5f, ChroniclePalette.InkFaint, alpha);
                    break;
                case QuestEntryStatus.Failed:
                    ChroniclePen.NodeWell(sb, center, 8f, ChroniclePalette.SealDeep, alpha, 1.3f);
                    ChroniclePen.Line(sb, center + new Vector2(-6f, -6f), center + new Vector2(6f, 6f),
                        1.8f, ChroniclePalette.SealDeep, alpha * 0.9f);
                    ChroniclePen.Line(sb, center + new Vector2(6f, -6f), center + new Vector2(-6f, 6f),
                        1.8f, ChroniclePalette.SealDeep, alpha * 0.9f);
                    break;
                default:
                    ChroniclePen.NodeWell(sb, center, 8f, ChroniclePalette.Ink, alpha, 1.4f);
                    break;
            }
        }

        private static string Shorten(string text, float maxWidth, float scale) {
            if (string.IsNullOrEmpty(text) || Font.MeasureString(text).X * scale <= maxWidth) {
                return text ?? string.Empty;
            }
            for (int len = text.Length - 1; len > 1; len--) {
                string probe = text[..len] + "…";
                if (Font.MeasureString(probe).X * scale <= maxWidth) {
                    return probe;
                }
            }
            return text[..1];
        }

        #endregion

        #region 颜色

        private static Color StatusInk(QuestEntryStatus status) => status switch {
            QuestEntryStatus.Tracked => ChroniclePalette.GoldDeep,
            QuestEntryStatus.Completed => ChroniclePalette.InkMute,
            QuestEntryStatus.Suspended => ChroniclePalette.InkFaint,
            QuestEntryStatus.Failed => ChroniclePalette.SealDeep,
            _ => ChroniclePalette.Ink,
        };

        public override Color GetShadowColor(float alpha) => ChroniclePalette.PaperDeep * (alpha * 0.5f);

        public override Color GetHeaderTextColor(float alpha) => ChroniclePalette.Ink * alpha;

        public override Color GetStatusColor(QuestEntryStatus status, float alpha)
            => StatusInk(status) * alpha;

        #endregion
    }
}
