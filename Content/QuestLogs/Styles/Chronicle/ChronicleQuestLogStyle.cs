using CalamityOverhaul.Content.QuestLogs.Core;
using CalamityOverhaul.Content.QuestLogs.Guide;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.QuestLogs.Styles.Chronicle
{
    /// <summary>
    /// 「远征纪要」：摊在皮革桌板上的一本远征记录册。<br/>
    /// 左页是图例，右页是任务图；节点是凿进纸里的圆窝，路线是手绘墨路，
    /// 结卷的条目压一枚蜡封。外框（页眉/页脚/左页/合卷键）由本样式自绘
    /// </summary>
    internal sealed class ChronicleQuestLogStyle : IQuestLogStyle
    {
        private float time;
        private QuestLogLayout layout;
        //连线的抖动种子按绘制序号取，绘制序稳定故墨路不会随平移缩放重新洗一遍
        private int routeSeed;

        public string DisplayName => "远征纪要";

        public bool SupportsNightMode => false;

        public bool DrawsOwnChrome => true;

        private static DynamicSpriteFont Font => FontAssets.MouseText.Value;

        public void UpdateStyle() {
            time += 1f / 60f;
            if (time > 10000f) {
                time -= 10000f;
            }
        }

        public void SyncLayout(in QuestLogLayout current) => layout = current;

        public Vector4 GetPadding() => new(18, 22, 18, 18);

        #region 桌面与外框

        public void DrawBackground(SpriteBatch spriteBatch, QuestLog log, Rectangle panelRect) {
            //每帧绘制自此开始，连线序号在此归零
            routeSeed = 0;
            ChroniclePen.DrawSurface(spriteBatch, in layout, log.MainPanelAlpha, time);
        }

        public void DrawChrome(SpriteBatch sb, QuestLog log, in QuestLogLayout current) {
            float a = log.MainPanelAlpha;
            if (a <= 0.01f) {
                return;
            }

            DrawHeader(sb, a);
            DrawRail(sb, log, in current, a);
            DrawFooter(sb, in current, a);
            DrawCloseTag(sb, current.MainClose, a);
            DrawHelpTag(sb, current.MainHelp, a);
        }

        /// <summary>重看教程键：同一块黄铜牌，牌面阴刻一个问号</summary>
        private void DrawHelpTag(SpriteBatch sb, Rectangle rect, float a) {
            bool hovered = rect.Contains(Main.MouseScreen.ToPoint());
            if (hovered) {
                Main.hoverItemName = QuestBookGuideLead.HelpButtonHover.Value;
            }
            ChroniclePen.BrassTag(sb, rect, hovered, a, time);

            Vector2 size = Font.MeasureString("?") * 0.9f;
            var pos = new Vector2(rect.X + (rect.Width - size.X) * 0.5f,
                rect.Y + (rect.Height - size.Y) * 0.5f);
            //阴刻：暗痕在上，受光的填漆偏下一格
            ChroniclePen.Ink(sb, Font, "?", pos,
                hovered ? ChroniclePalette.SealDeep : ChroniclePalette.BrassDeep, 0.9f, a * 0.95f);
            ChroniclePen.Ink(sb, Font, "?", pos + new Vector2(0f, 1.3f),
                ChroniclePalette.BrassHi, 0.9f, a * 0.28f);
        }

        /// <summary>页眉：皮面上的烫金卷名 + 结卷刻度</summary>
        private void DrawHeader(SpriteBatch sb, float a) {
            Rectangle header = layout.Header;
            string title = QuestLog.ChronicleTitle?.Value ?? "远 征 纪 要";
            ChroniclePen.LeatherInk(sb, Font, title, new Vector2(26f, header.Y + 14f),
                ChroniclePalette.GoldHi, 1.08f, a);

            //卷名下的一道烫金压线，向右渐隐
            float titleW = Font.MeasureString(title).X * 1.08f;
            ChroniclePen.GiltRule(sb, new Vector2(26f, header.Y + 42f), titleW + 260f, a * 0.9f);

            //结卷刻度：只在页眉右侧报数，槽 + 齿 + 金填
            (int done, int total) = CountSealed();
            float ratio = total > 0 ? done / (float)total : 0f;
            Rectangle tally = new(header.Right - 232, header.Y + 26, 150, 7);
            ChroniclePen.Tally(sb, tally, ratio, Math.Clamp(total, 6, 20), a);

            string reading = QuestLog.ChronicleProgress?.Format(done, total) ?? $"{done} / {total}";
            ChroniclePen.LeatherInk(sb, Font, reading,
                new Vector2(header.Right - 232f, header.Y + 6f), ChroniclePalette.Brass, 0.76f, a);
        }

        /// <summary>左页：站点书口 → 章目 → 底部图例</summary>
        private void DrawRail(SpriteBatch sb, QuestLog log, in QuestLogLayout current, float a) {
            DrawStationTabs(sb, log, in current, a);
            if (log.View != QuestLogView.Chart) {
                return;
            }
            DrawChapters(sb, log, in current, a);
            DrawLegend(sb, in current, a);
        }

        /// <summary>
        /// 站点书口：书页侧面伸出来的两枚纸舌，当前那枚探得更出、边缘吃暗更重。<br/>
        /// 不画标签页方框，纸的厚度与投影就是选中态
        /// </summary>
        private void DrawStationTabs(SpriteBatch sb, QuestLog log, in QuestLogLayout current, float a) {
            Point mouse = Main.MouseScreen.ToPoint();
            for (int i = 0; i < log.StationCount; i++) {
                Rectangle tab = QuestLogTheme.RailTab(in current, i);
                QuestLogView station = log.StationAt(i);
                bool selected = log.View == station;
                bool hovered = tab.Contains(mouse);
                //选中的纸舌向右探出，悬停探一半
                float push = selected ? 10f : hovered ? 5f : 0f;
                Rectangle body = new(tab.X, tab.Y, (int)(tab.Width + push), tab.Height);

                Texture2D px = VaultAsset.placeholder2.Value;
                //贴身投影
                sb.Draw(px, new Rectangle(body.X + 2, body.Y + 3, body.Width, body.Height),
                    ChroniclePalette.PaperDeep * (a * 0.5f));
                Color face = selected
                    ? Color.Lerp(ChroniclePalette.Paper, Color.White, 0.2f)
                    : Color.Lerp(ChroniclePalette.Paper, ChroniclePalette.PaperDeep, hovered ? 0.18f : 0.34f);
                sb.Draw(px, body, face * a);

                //纸舌右缘撕口，逐段错位
                for (int y = body.Y; y < body.Bottom; y += 5) {
                    float bite = QuestLogTheme.Hash01(y * 13 + i * 71) * 3.2f;
                    sb.Draw(px, new Rectangle(body.Right - (int)bite - 1, y, (int)bite + 1, 5),
                        ChroniclePalette.PaperDeep * (a * 0.5f));
                }
                //上缘受光、下缘吃暗，纸有厚度
                sb.Draw(px, new Rectangle(body.X, body.Y, body.Width, 1),
                    ChroniclePalette.Candle * (a * 0.22f));
                sb.Draw(px, new Rectangle(body.X, body.Bottom - 1, body.Width, 1),
                    ChroniclePalette.PaperDeep * (a * 0.6f));

                //选中的一枚压一道金压线在左缘
                if (selected) {
                    ChroniclePen.Line(sb, new Vector2(body.X + 3f, body.Y + 4f),
                        new Vector2(body.X + 3f, body.Bottom - 4f), 1.6f, ChroniclePalette.Gold, a * 0.85f);
                }

                string label = station == QuestLogView.Chart
                    ? QuestLog.ChronicleStationChart?.Value ?? "任务图谱"
                    : QuestLog.ChronicleStationEntrust?.Value ?? "委托卷宗";
                ChroniclePen.Ink(sb, Font, label, new Vector2(body.X + 12f, body.Y + 7f),
                    selected ? ChroniclePalette.Ink : ChroniclePalette.InkMute, 0.8f, a);
            }
        }

        /// <summary>章目：根节点列目，点一行把图谱平移过去</summary>
        private void DrawChapters(SpriteBatch sb, QuestLog log, in QuestLogLayout current, float a) {
            float top = QuestLogTheme.RailContentTop(in current, 2);
            float x = current.Rail.X + 20f;
            ChroniclePen.Ink(sb, Font, QuestLog.ChronicleChapterTitle?.Value ?? "章 目",
                new Vector2(x, top - 4f), ChroniclePalette.Ink, 0.84f, a * 0.9f);
            ChroniclePen.GiltRule(sb, new Vector2(x, top + 16f), current.Rail.Width - 52f, a * 0.5f);

            int capacity = Math.Min(log.ChapterRoots.Count, QuestLogTheme.RailChapterCapacity(in current));
            for (int i = 0; i < capacity; i++) {
                QuestNode node = log.ChapterRoots[i];
                Rectangle row = QuestLogTheme.RailChapter(in current, i);
                bool hovered = log.HoveredChapter == i;
                NodeState state = StateOf(node);

                //行间发丝线取代行底盒
                if (i > 0) {
                    ChroniclePen.HairLine(sb, new Vector2(row.X + 4f, row.Y - 2f), row.Width - 8f, a * 0.5f);
                }
                //悬停：左缘一道朱刻痕
                if (hovered) {
                    ChroniclePen.Line(sb, new Vector2(row.X - 2f, row.Y + 2f),
                        new Vector2(row.X - 2f, row.Bottom - 2f), 2f, ChroniclePalette.Seal, a * 0.8f);
                }

                //状态小记号
                Vector2 dot = new(row.X + 7f, row.Center.Y);
                ChroniclePen.NodeWell(sb, dot, 4.6f, StateInk(state), a * 0.9f, 1.2f);
                if (state == NodeState.Sealed || state == NodeState.Unclaimed) {
                    SvgSealDot(sb, dot, state == NodeState.Sealed, a);
                }

                string name = node.DisplayName?.Value ?? node.ID;
                ChroniclePen.Ink(sb, Font, Shorten(name, row.Width - 26f, 0.74f),
                    new Vector2(row.X + 18f, row.Y + 2f),
                    hovered ? ChroniclePalette.Ink : StateInk(state), 0.74f, a);
            }
        }

        /// <summary>章目行上的微型蜡点：一枚 3px 的绯色，够读出"这条结了"</summary>
        private void SvgSealDot(SpriteBatch sb, Vector2 center, bool broken, float a) {
            ChroniclePen.WaxSeal(sb, center + new Vector2(3.4f, 3.2f), 3.4f, a, 5, time, broken, !broken);
        }

        /// <summary>按像素宽截断，末尾留一点省略</summary>
        private static string Shorten(string text, float maxWidth, float scale) {
            if (string.IsNullOrEmpty(text) || Font.MeasureString(text).X * scale <= maxWidth) {
                return text;
            }
            for (int len = text.Length - 1; len > 1; len--) {
                string probe = text[..len] + "…";
                if (Font.MeasureString(probe).X * scale <= maxWidth) {
                    return probe;
                }
            }
            return text[..1];
        }

        /// <summary>图例：四种状态各画一枚真的记号，不是色块图注</summary>
        private void DrawLegend(SpriteBatch sb, in QuestLogLayout current, float a) {
            Rectangle rail = current.Rail;
            float x = rail.X + 22f;
            float y = QuestLogTheme.RailLegendTop(in current);

            ChroniclePen.Ink(sb, Font, QuestLog.ChronicleLegendTitle?.Value ?? "图 例",
                new Vector2(x, y), ChroniclePalette.Ink, 0.84f, a * 0.85f);
            ChroniclePen.GiltRule(sb, new Vector2(x, y + 20f), rail.Width - 52f, a * 0.5f);

            y += 40f;
            const float Gap = 34f;
            DrawLegendRow(sb, new Vector2(x + 10f, y), NodeState.Sealed,
                QuestLog.ChronicleLegendSealed?.Value, a);
            DrawLegendRow(sb, new Vector2(x + 10f, y + Gap), NodeState.Unclaimed,
                QuestLog.ChronicleLegendUnclaimed?.Value, a);
            DrawLegendRow(sb, new Vector2(x + 10f, y + Gap * 2f), NodeState.Active,
                QuestLog.ChronicleLegendActive?.Value, a);
            DrawLegendRow(sb, new Vector2(x + 10f, y + Gap * 3f), NodeState.Locked,
                QuestLog.ChronicleLegendLocked?.Value, a);
        }

        private void DrawLegendRow(SpriteBatch sb, Vector2 pos, NodeState state, string label, float a) {
            const float R = 10f;
            //图例里的记号与图上的同一支笔，故直接复用状态绘制
            DrawStateMark(sb, pos, R, state, 0.5f, 7 + (int)state * 13, a * 0.95f, false);
            ChroniclePen.Ink(sb, Font, label ?? string.Empty,
                pos + new Vector2(R + 16f, -8f), StateInk(state), 0.76f, a * 0.9f);
        }

        /// <summary>页脚：皮面提示。站点切换归左栏书口，这里不再摆第二个入口</summary>
        private void DrawFooter(SpriteBatch sb, in QuestLogLayout current, float a) {
            Rectangle footer = current.Footer;
            ChroniclePen.LeatherInk(sb, Font, QuestLog.ChronicleHint?.Value ?? string.Empty,
                new Vector2(26f, footer.Y + 15f), ChroniclePalette.Brass, 0.74f, a * 0.85f);
        }

        /// <summary>合卷键：黄铜牌 + 阴刻的一记斜叉</summary>
        private void DrawCloseTag(SpriteBatch sb, Rectangle rect, float a) {
            bool hovered = rect.Contains(Main.MouseScreen.ToPoint());
            ChroniclePen.BrassTag(sb, rect, hovered, a, time);

            Vector2 c = rect.Center.ToVector2();
            float r = rect.Width * 0.23f;
            Color cut = hovered ? ChroniclePalette.SealDeep : ChroniclePalette.BrassDeep;
            //阴刻：暗痕在上，受光在下偏一格
            ChroniclePen.Line(sb, c + new Vector2(-r, -r), c + new Vector2(r, r), 1.8f, cut, a * 0.95f);
            ChroniclePen.Line(sb, c + new Vector2(r, -r), c + new Vector2(-r, r), 1.8f, cut, a * 0.95f);
            ChroniclePen.Line(sb, c + new Vector2(-r, -r + 1.4f), c + new Vector2(r, r + 1.4f), 1f,
                ChroniclePalette.BrassHi, a * 0.28f);
            ChroniclePen.Line(sb, c + new Vector2(r, -r + 1.4f), c + new Vector2(-r, r + 1.4f), 1f,
                ChroniclePalette.BrassHi, a * 0.28f);
        }

        private static (int done, int total) CountSealed() {
            int done = 0, total = 0;
            foreach (var node in QuestNode.AllQuests) {
                //未现身的隐藏任务不进分母，否则完成比例永远差着看不见的几条
                if (node.IsHiddenNow) {
                    continue;
                }
                total++;
                if (node.IsCompleted) {
                    done++;
                }
            }
            return (done, total);
        }

        #endregion

        #region 节点与墨路

        private enum NodeState
        {
            Locked,
            Active,
            Unclaimed,
            Sealed,
        }

        private static NodeState StateOf(QuestNode node) {
            if (node.HasUnclaimedRewards) {
                return NodeState.Unclaimed;
            }
            if (node.IsCompleted) {
                return NodeState.Sealed;
            }
            return node.IsUnlocked ? NodeState.Active : NodeState.Locked;
        }

        private static Color StateInk(NodeState state) => state switch {
            NodeState.Sealed => ChroniclePalette.InkMute,
            NodeState.Unclaimed => ChroniclePalette.GoldDeep,
            NodeState.Active => ChroniclePalette.Ink,
            _ => ChroniclePalette.InkFaint,
        };

        /// <summary>一枚记号：窝 + 状态附件（影线 / 巡光 / 蜡封）</summary>
        private void DrawStateMark(SpriteBatch sb, Vector2 center, float radius, NodeState state,
            float scale, int seed, float a, bool hovered) {
            Color ring = state switch {
                NodeState.Sealed => ChroniclePalette.InkMute,
                NodeState.Unclaimed => ChroniclePalette.Gold,
                NodeState.Active => ChroniclePalette.Ink,
                _ => ChroniclePalette.InkFaint,
            };
            if (hovered) {
                ring = Color.Lerp(ring, ChroniclePalette.GoldHi, 0.45f);
            }

            ChroniclePen.NodeWell(sb, center, radius, ring, a, MathF.Max(1.2f, 1.7f * scale));

            switch (state) {
                case NodeState.Locked:
                    ChroniclePen.HatchDisc(sb, center, radius - 1.5f, ChroniclePalette.InkFaint, a);
                    break;
                case NodeState.Active:
                    //在行中：环上一段亮笔慢慢巡行
                    DrawRingRunner(sb, center, radius, seed, a);
                    break;
                case NodeState.Unclaimed:
                    ChroniclePen.WaxSeal(sb, center + new Vector2(radius * 0.72f, radius * 0.74f),
                        radius * 0.52f, a, seed, time, false, true);
                    break;
                case NodeState.Sealed:
                    ChroniclePen.WaxSeal(sb, center + new Vector2(radius * 0.72f, radius * 0.74f),
                        radius * 0.52f, a * 0.95f, seed, time, true);
                    break;
            }
        }

        /// <summary>沿窝缘巡行的一段亮笔，标出"这条还在走"</summary>
        private void DrawRingRunner(SpriteBatch sb, Vector2 center, float radius, int seed, float a) {
            float head = (time * 0.22f + QuestLogTheme.Hash01(seed * 7 + 5)) % 1f;
            const int Seg = 7;
            for (int i = 0; i < Seg; i++) {
                float t0 = head + i / 46f;
                float t1 = head + (i + 1) / 46f;
                float fade = 1f - i / (float)Seg;
                Vector2 p0 = center + (t0 * MathHelper.TwoPi).ToRotationVector2() * radius;
                Vector2 p1 = center + (t1 * MathHelper.TwoPi).ToRotationVector2() * radius;
                ChroniclePen.Line(sb, p0, p1, 1.8f, ChroniclePalette.Gold, a * 0.75f * fade);
            }
        }

        public void DrawNode(SpriteBatch sb, QuestNode node, Vector2 drawPos, float scale,
            bool isHovered, float alpha) {
            NodeState state = StateOf(node);
            int seed = Math.Abs(node.ID?.GetHashCode() ?? 0) % 9973;
            float radius = 21f * scale;

            DrawStateMark(sb, drawPos, radius, state, scale, seed, alpha, isHovered);
            DrawIcon(sb, node, drawPos, scale, state, alpha);

            //悬停：手圈一道，而不是套一个高亮方框
            if (isHovered) {
                ChroniclePen.CircleMark(sb, drawPos, radius * 1.42f, ChroniclePalette.Ink,
                    alpha * 0.8f, seed);
            }

            string name = node.DisplayName?.Value;
            if (string.IsNullOrEmpty(name)) {
                return;
            }
            float textScale = 0.74f * MathHelper.Clamp(scale, 0.7f, 1.25f);
            Vector2 size = Font.MeasureString(name) * textScale;
            Vector2 namePos = new(drawPos.X - size.X * 0.5f, drawPos.Y + radius + 7f);
            Color ink = isHovered ? ChroniclePalette.Ink : StateInk(state);
            ChroniclePen.Ink(sb, Font, name, namePos, ink, textScale, alpha);
            if (isHovered) {
                //名下一道压痕，读作"笔尖划过"
                ChroniclePen.Groove(sb, namePos + new Vector2(0f, size.Y - 1f), size.X, alpha * 0.7f);
            }
        }

        private static void DrawIcon(SpriteBatch sb, QuestNode node, Vector2 center, float scale,
            NodeState state, float alpha) {
            Texture2D tex = node.GetIconTexture();
            if (tex == null) {
                return;
            }
            Rectangle? source = node.GetIconSourceRect(tex);
            if (!source.HasValue) {
                return;
            }
            Rectangle frame = source.Value;
            float box = 26f * scale;
            float iconScale = 1f;
            if (frame.Width > box || frame.Height > box) {
                iconScale = box / Math.Max(frame.Width, frame.Height);
            }
            //未启程的条目只留剪影，纸上不该有鲜亮的图
            Color tint = state == NodeState.Locked
                ? ChroniclePalette.InkFaint * 0.85f
                : Color.Lerp(Color.White, ChroniclePalette.Paper, state == NodeState.Sealed ? 0.35f : 0.12f);
            sb.Draw(tex, center, frame, tint * alpha, 0f, frame.Size() / 2f, iconScale,
                SpriteEffects.None, 0f);
        }

        public void DrawConnection(SpriteBatch sb, Vector2 start, Vector2 end, bool isUnlocked, float alpha) {
            //序号必须先递增再判可见，否则视口外的连线被跳过时后面的墨路会集体换一副抖动
            int seed = routeSeed++;
            Rectangle cull = layout.Canvas;
            cull.Inflate(140, 140);
            if (!cull.Contains(start.ToPoint()) && !cull.Contains(end.ToPoint())) {
                return;
            }
            ChroniclePen.InkRoute(sb, start, end, isUnlocked, alpha, seed, time);
        }

        #endregion

        #region 详情：贴在右页上的一张记录条

        private const float SlipPadX = 22f;
        private const float BodyScale = 0.82f;

        public Rectangle GetCloseButtonRect(Rectangle panelRect)
            => new(panelRect.Right - 46, panelRect.Y + 18, 26, 26);

        public Rectangle GetRewardButtonRect(Rectangle panelRect)
            => new(panelRect.X + 26, panelRect.Bottom - 64, 168, 30);

        public void DrawQuestDetail(SpriteBatch sb, QuestNode node, Rectangle panelRect, float alpha)
            => DrawDetail(sb, node, in layout, alpha, 0f);

        public float MeasureDetailHeight(QuestNode node, in QuestLogLayout current) {
            float width = current.Detail.Width - SlipPadX * 2f - 20f;
            return MeasureBody(node, width) + 150f;
        }

        /// <summary>正文总高，与绘制同口径</summary>
        private static float MeasureBody(QuestNode node, float wrapWidth) {
            float h = 0f;
            float line = Font.MeasureString("A").Y;
            string desc = node.DetailedDescription?.Value ?? node.Description?.Value;
            if (!string.IsNullOrEmpty(desc)) {
                h += ChroniclePen.Wrap(Font, desc, wrapWidth, BodyScale).Count * line * BodyScale + 14f;
            }
            if (node.Objectives != null && node.Objectives.Count > 0) {
                h += line * 0.86f + 10f;
                h += node.Objectives.Count * (line * BodyScale + 8f);
            }
            if (node.Rewards != null && node.Rewards.Count > 0) {
                h += line * 0.86f + 12f;
                h += MathF.Ceiling(node.Rewards.Count / 4f) * 44f;
            }
            return h;
        }

        public void DrawDetail(SpriteBatch sb, QuestNode node, in QuestLogLayout current,
            float alpha, float scroll) {
            if (alpha <= 0.01f || node == null) {
                return;
            }
            Rectangle rect = current.Detail;
            Rectangle slip = new(rect.X + 10, rect.Y + 12, rect.Width - 26, rect.Height - 26);
            DrawSlip(sb, slip, alpha);

            float x = slip.X + SlipPadX;
            float wrapW = slip.Width - SlipPadX * 2f;
            float line = Font.MeasureString("A").Y;
            NodeState state = StateOf(node);
            int seed = Math.Abs(node.ID?.GetHashCode() ?? 0) % 9973;

            //题头：条目名 + 状态蜡封（结卷的贴条右上压一枚）
            float y = slip.Y + 20f;
            ChroniclePen.Ink(sb, Font, node.DisplayName?.Value ?? string.Empty,
                new Vector2(x, y), ChroniclePalette.Ink, 1.0f, alpha);
            y += line * 1.05f + 4f;
            ChroniclePen.GiltRule(sb, new Vector2(x, y), wrapW * 0.72f, alpha * 0.8f);
            y += 12f;

            if (state == NodeState.Sealed || state == NodeState.Unclaimed) {
                ChroniclePen.WaxSeal(sb, new Vector2(slip.Right - 52f, slip.Y + 66f), 21f,
                    alpha, seed, time, state == NodeState.Sealed, state == NodeState.Unclaimed);
            }

            //收起键先画，窗口再矮也不能只剩命中区没有图形
            DrawCloseTag(sb, GetCloseButtonRect(rect), alpha);

            //正文区裁剪，滚动只动正文
            Rectangle body = new(slip.X + 4, (int)y, slip.Width - 8, slip.Bottom - (int)y - 76);
            if (body.Height < 40) {
                return;
            }
            Rectangle prevScissor = sb.GraphicsDevice.ScissorRectangle;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, new RasterizerState { ScissorTestEnable = true }, null,
                Main.UIScaleMatrix);
            sb.GraphicsDevice.ScissorRectangle = VaultUtils.GetClippingRectangle(sb, body);

            DrawBody(sb, node, x, body.Y - scroll, wrapW, line, alpha);

            sb.End();
            sb.GraphicsDevice.ScissorRectangle = prevScissor;
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);

            //溢出提示：右缘一道随滚动走的短墨迹，不是现代滑块
            float contentH = MeasureBody(node, wrapW);
            if (contentH > body.Height + 2f) {
                float t = MathHelper.Clamp(scroll / MathF.Max(1f, contentH - body.Height), 0f, 1f);
                float markY = MathHelper.Lerp(body.Y + 8f, body.Bottom - 40f, t);
                ChroniclePen.Line(sb, new Vector2(slip.Right - 9f, markY),
                    new Vector2(slip.Right - 9f, markY + 32f), 2f, ChroniclePalette.Seal, alpha * 0.55f);
            }

            //领赏：黄铜牌，全部领完则不再出现
            if (node.IsCompleted && node.Rewards != null && node.Rewards.Exists(r => !r.Claimed)) {
                Rectangle btn = GetRewardButtonRect(rect);
                bool hovered = btn.Contains(Main.MouseScreen.ToPoint());
                ChroniclePen.BrassTag(sb, btn, hovered, alpha, time);
                ChroniclePen.InkCentered(sb, Font, QuestLog.ReceiveAwardText?.Value ?? string.Empty,
                    btn.Center.ToVector2(), hovered ? ChroniclePalette.SealDeep : ChroniclePalette.BrassDeep,
                    0.82f, alpha);
            }
        }

        /// <summary>正文：描述 → 目标 → 奖励</summary>
        private void DrawBody(SpriteBatch sb, QuestNode node, float x, float y, float wrapW,
            float line, float alpha) {
            string desc = node.DetailedDescription?.Value ?? node.Description?.Value;
            if (!string.IsNullOrEmpty(desc)) {
                foreach (string row in ChroniclePen.Wrap(Font, desc, wrapW, BodyScale)) {
                    ChroniclePen.Ink(sb, Font, row, new Vector2(x, y), ChroniclePalette.InkMute,
                        BodyScale, alpha);
                    y += line * BodyScale;
                }
                y += 14f;
            }

            if (node.Objectives != null && node.Objectives.Count > 0) {
                ChroniclePen.Ink(sb, Font, QuestLog.ObjectiveText?.Value ?? string.Empty,
                    new Vector2(x, y), ChroniclePalette.Ink, 0.86f, alpha);
                y += line * 0.86f + 10f;
                foreach (var objective in node.Objectives) {
                    y = DrawObjectiveRow(sb, objective, x, y, wrapW, line, alpha);
                }
            }

            if (node.Rewards != null && node.Rewards.Count > 0) {
                ChroniclePen.Ink(sb, Font, QuestLog.RewardText?.Value ?? string.Empty,
                    new Vector2(x, y), ChroniclePalette.Ink, 0.86f, alpha);
                y += line * 0.86f + 12f;
                DrawRewards(sb, node, x, y, alpha);
            }
        }

        /// <summary>
        /// 一条目标：句首一记短划，完成的划掉并补一记勾。<br/>
        /// 进度数字压在右缘，不做条形进度
        /// </summary>
        private float DrawObjectiveRow(SpriteBatch sb, QuestObjective objective, float x, float y,
            float wrapW, float line, float alpha) {
            string text = objective.GetDisplayText();
            bool done = objective.IsCompleted;
            Color ink = done ? ChroniclePalette.InkFaint : ChroniclePalette.InkMute;

            //句首短划，长度略有出入
            float dash = 7f + QuestLogTheme.Hash01(text?.Length ?? 0) * 3f;
            ChroniclePen.Line(sb, new Vector2(x + 2f, y + line * BodyScale * 0.55f),
                new Vector2(x + 2f + dash, y + line * BodyScale * 0.5f), 1.4f, ink, alpha * 0.9f);

            float textX = x + 16f;
            ChroniclePen.Ink(sb, Font, text, new Vector2(textX, y), ink, BodyScale, alpha);
            float textW = Font.MeasureString(text ?? string.Empty).X * BodyScale;

            if (done) {
                //划掉：一道略斜的墨线穿过，末端翘一点
                float mid = y + line * BodyScale * 0.52f;
                ChroniclePen.Line(sb, new Vector2(textX - 2f, mid + 1f),
                    new Vector2(textX + textW + 3f, mid - 1.5f), 1.3f, ChroniclePalette.Ink, alpha * 0.55f);
            }
            else if (objective.RequiredProgress > 1) {
                string progress = $"{objective.CurrentProgress}/{objective.RequiredProgress}";
                float pw = Font.MeasureString(progress).X * 0.74f;
                ChroniclePen.Ink(sb, Font, progress, new Vector2(x + wrapW - pw, y + 1f),
                    ChroniclePalette.GoldDeep, 0.74f, alpha * 0.95f);
            }

            return y + line * BodyScale + 8f;
        }

        /// <summary>奖励：物件躺在浅凿的窝里，不是描边格子</summary>
        private static void DrawRewards(SpriteBatch sb, QuestNode node, float x, float y, float alpha) {
            const float Cell = 40f;
            for (int i = 0; i < node.Rewards.Count; i++) {
                QuestReward reward = node.Rewards[i];
                int col = i % 4;
                int row = i / 4;
                Vector2 center = new(x + 20f + col * Cell, y + 16f + row * 44f);

                ChroniclePen.NodeWell(sb, center, 16f,
                    reward.Claimed ? ChroniclePalette.InkFaint : ChroniclePalette.Ink, alpha, 1.3f);

                Main.instance.LoadItem(reward.ItemType);
                Texture2D tex = TextureAssets.Item[reward.ItemType]?.Value;
                if (tex != null) {
                    Rectangle frame = Main.itemAnimations[reward.ItemType] != null
                        ? Main.itemAnimations[reward.ItemType].GetFrame(tex)
                        : tex.Frame();
                    float scale = 1f;
                    if (frame.Width > 22 || frame.Height > 22) {
                        scale = 22f / Math.Max(frame.Width, frame.Height);
                    }
                    Color tint = reward.Claimed ? ChroniclePalette.InkFaint * 0.8f : Color.White;
                    sb.Draw(tex, center, frame, tint * alpha, 0f, frame.Size() / 2f, scale,
                        SpriteEffects.None, 0f);
                }

                if (reward.Amount > 1) {
                    ChroniclePen.Ink(sb, Font, $"x{reward.Amount}",
                        center + new Vector2(6f, 6f), ChroniclePalette.GoldDeep, 0.7f, alpha);
                }
            }
        }

        /// <summary>贴在页上的记录条：裁边纸片 + 贴身投影，左缘撕口</summary>
        private static void DrawSlip(SpriteBatch sb, Rectangle slip, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            //投影只偏不放大
            sb.Draw(px, new Rectangle(slip.X + 3, slip.Y + 4, slip.Width, slip.Height),
                ChroniclePalette.PaperDeep * (alpha * 0.55f));
            //纸片本体比底页略白，像另一批纸
            sb.Draw(px, slip, Color.Lerp(ChroniclePalette.Paper, Color.White, 0.16f) * alpha);

            //左右缘撕口：逐段错位，右缘更碎
            for (int y = slip.Y; y < slip.Bottom; y += 6) {
                int i = y / 6;
                float leftBite = QuestLogTheme.Hash01(i * 19 + 3) * 3.4f;
                float rightBite = QuestLogTheme.Hash01(i * 23 + 11) * 4.6f;
                sb.Draw(px, new Rectangle(slip.X, y, (int)leftBite + 1, 6),
                    ChroniclePalette.PaperDeep * (alpha * 0.55f));
                sb.Draw(px, new Rectangle(slip.Right - (int)rightBite - 1, y, (int)rightBite + 1, 6),
                    ChroniclePalette.PaperDeep * (alpha * 0.5f));
            }

            //纸缘吃暗
            for (int i = 0; i < 8; i++) {
                float fade = 1f - i / 8f;
                Color edge = ChroniclePalette.PaperDeep * (alpha * 0.12f * fade * fade);
                sb.Draw(px, new Rectangle(slip.X, slip.Y + i, slip.Width, 1), edge);
                sb.Draw(px, new Rectangle(slip.X, slip.Bottom - i - 1, slip.Width, 1), edge);
            }
        }

        #endregion

        #region 页脚黄铜活儿

        //旧接口按 panelRect 推算按钮位；本样式的总控住在页脚，故一律读同步来的分区
        public Rectangle GetStyleSwitchButtonRect(Rectangle panelRect)
            => QuestLogTheme.FooterStyleButton(layout.Footer);

        public void DrawStyleSwitchButton(SpriteBatch sb, Rectangle panelRect, bool isHovered, float alpha) {
            Rectangle rect = GetStyleSwitchButtonRect(panelRect);
            ChroniclePen.BrassTag(sb, rect, isHovered, alpha, time);
            //换卷：两片叠起的书页
            Vector2 c = rect.Center.ToVector2();
            Color cut = ChroniclePalette.BrassDeep;
            ChroniclePen.Line(sb, c + new Vector2(-7f, -5f), c + new Vector2(2f, -5f), 1.5f, cut, alpha * 0.9f);
            ChroniclePen.Line(sb, c + new Vector2(-7f, -5f), c + new Vector2(-7f, 4f), 1.5f, cut, alpha * 0.9f);
            ChroniclePen.Line(sb, c + new Vector2(-2f, 0f), c + new Vector2(7f, 0f), 1.5f, cut, alpha * 0.9f);
            ChroniclePen.Line(sb, c + new Vector2(7f, 0f), c + new Vector2(7f, 6f), 1.5f, cut, alpha * 0.9f);
            ChroniclePen.Line(sb, c + new Vector2(-2f, 6f), c + new Vector2(7f, 6f), 1.5f, cut, alpha * 0.9f);
        }

        //本样式不设日夜，夜间槽位空着不给后面的键让路
        public Rectangle GetNightModeButtonRect(Rectangle panelRect) => Rectangle.Empty;

        public void DrawNightModeButton(SpriteBatch sb, Rectangle panelRect, bool isHovered,
            float alpha, bool isNightMode) { }

        public Rectangle GetResetViewButtonRect(Rectangle panelRect)
            => QuestLogTheme.FooterResetButton(layout.Footer);

        public void DrawResetViewButton(SpriteBatch sb, Rectangle panelRect, Vector2 directionToCenter,
            bool isHovered, float alpha) {
            Rectangle rect = GetResetViewButtonRect(panelRect);
            ChroniclePen.BrassTag(sb, rect, isHovered, alpha, time);
            //归位：一枚指回中心的针
            Vector2 c = rect.Center.ToVector2();
            float rot = directionToCenter.LengthSquared() > 1f ? directionToCenter.ToRotation() : 0f;
            Vector2 tip = c + rot.ToRotationVector2() * 8f;
            Vector2 tail = c - rot.ToRotationVector2() * 7f;
            ChroniclePen.Line(sb, tail, tip, 1.8f, ChroniclePalette.BrassDeep, alpha * 0.95f);
            ChroniclePen.Line(sb, tip, tip + (rot + 2.5f).ToRotationVector2() * 5f, 1.6f,
                ChroniclePalette.BrassDeep, alpha * 0.95f);
            ChroniclePen.Line(sb, tip, tip + (rot - 2.5f).ToRotationVector2() * 5f, 1.6f,
                ChroniclePalette.BrassDeep, alpha * 0.95f);
        }

        public Rectangle GetClaimAllButtonRect(Rectangle panelRect)
            => QuestLogTheme.FooterClaimButton(layout.Footer);

        public void DrawClaimAllButton(SpriteBatch sb, Rectangle panelRect, bool isHovered, float alpha) {
            Rectangle rect = GetClaimAllButtonRect(panelRect);
            ChroniclePen.BrassTag(sb, rect, isHovered, alpha, time);
            ChroniclePen.InkCentered(sb, Font, QuestLog.QuickReceiveAwardText?.Value ?? string.Empty,
                rect.Center.ToVector2(), isHovered ? ChroniclePalette.SealDeep : ChroniclePalette.BrassDeep,
                0.78f, alpha);
            //未领的赏在牌角挂一枚小蜡封，提醒有东西没拿
            ChroniclePen.WaxSeal(sb, new Vector2(rect.X + 2f, rect.Y + 2f), 6.5f, alpha, 31, time, false, true);
        }

        public void DrawProgressBar(SpriteBatch sb, QuestLog log, Rectangle panelRect) {
            //结卷刻度已画在页眉，此处不再另起一条
        }

        #endregion
    }
}
