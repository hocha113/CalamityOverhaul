using CalamityOverhaul.Content.Narrative.Presentation.Skins.Draedon;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.Scenarios.Draedon.PQCDs.DraedonShops
{
    /// <summary>
    /// 嘉登交换终端的绘制层：全部为无状态静态方法，外框/分隔线/扫描线/粒子复用对话皮肤的 DraedonPanelDraw，
    /// 仅在其之上铺设标题带、货品记录、底部状态条
    /// </summary>
    internal static class DraedonShopRenderer
    {
        private static Texture2D Px => VaultAsset.placeholder2.Value;
        private static DynamicSpriteFont Font => FontAssets.MouseText.Value;

        /// <summary>单条货品记录的瞬时视觉状态</summary>
        internal readonly struct RecordVisual(int ordinal, int itemType, string name, int price,
            float hover, bool selected, bool affordable, float holdProgress, int holdCount)
        {
            public readonly int Ordinal = ordinal;
            public readonly int ItemType = itemType;
            public readonly string Name = name;
            public readonly int Price = price;
            public readonly float Hover = hover;
            public readonly bool Selected = selected;
            public readonly bool Affordable = affordable;
            public readonly float HoldProgress = holdProgress;
            public readonly int HoldCount = holdCount;
        }

        /// <summary>面板外框 + 终端底纹 + 浮游粒子</summary>
        public static void DrawChrome(SpriteBatch sb, Rectangle panelRect, float alpha, DraedonPanelState state) {
            DraedonPanelDraw.DrawPanel(sb, panelRect, alpha, state, DraedonPanelDetail.Full, shadowLayers: 9);
            state.DrawParticles(sb, alpha, 0.7f, 0.6f);
        }

        /// <summary>标题带：> 终端名 + 流动虚线分隔 + 右对齐余额</summary>
        public static void DrawHeader(SpriteBatch sb, Rectangle panelRect, float alpha, DraedonPanelState state,
            string title, string fundsLabel, long funds) {
            Vector2 titlePos = new(panelRect.X + DraedonShopTheme.SidePadding + 14f, panelRect.Y + 16f);
            DraedonPanelDraw.DrawSpeakerGlow(sb, titlePos, title, alpha, 1.05f);
            Utils.DrawBorderString(sb, title, titlePos, DraedonShopTheme.EdgeBright * alpha, 1.05f);

            float dividerY = panelRect.Y + 50f;
            DraedonPanelDraw.DrawDashDivider(sb,
                new Vector2(panelRect.X + DraedonShopTheme.SidePadding, dividerY),
                new Vector2(panelRect.Right - DraedonShopTheme.SidePadding, dividerY),
                alpha, state.DataStreamTimer);

            //余额带
            float fundsY = panelRect.Y + 60f;
            Utils.DrawBorderString(sb, fundsLabel, new Vector2(panelRect.X + DraedonShopTheme.SidePadding, fundsY),
                DraedonShopTheme.TextDim * alpha, 0.72f);
            DraedonShopStyle.DrawCoins(sb, new Vector2(panelRect.Right - DraedonShopTheme.SidePadding, fundsY),
                funds, alpha, 0.82f, DraedonShopTheme.TextBright, rightAlign: true);
        }

        /// <summary>一条货品记录</summary>
        public static void DrawRecord(SpriteBatch sb, Rectangle row, float alpha, DraedonPanelState state,
            in RecordVisual v, string buyLabel) {
            float hover = v.Hover;
            float pulse = MathF.Sin(state.CircuitPulseTimer * 1.4f + row.Y * 0.01f) * 0.5f + 0.5f;
            Color accent = v.Selected ? DraedonShopTheme.Gold : DraedonShopTheme.Edge;

            //悬停/选中底板：左亮右淡的渐变条 + 左侧强调竖条，绝不画闭合盒子
            float plateStrength = MathHelper.Clamp(hover + (v.Selected ? 0.4f : 0f), 0f, 1f);
            if (plateStrength > 0.001f) {
                const int strips = 12;
                for (int i = 0; i < strips; i++) {
                    float t = i / (float)strips;
                    sb.Draw(Px, new Rectangle(row.X + (int)(row.Width * t), row.Y, row.Width / strips + 1, row.Height),
                        new Rectangle(0, 0, 1, 1), accent * (alpha * plateStrength * 0.16f * (1f - t)));
                }
            }
            int barW = 2 + (int)(plateStrength * 3f);
            sb.Draw(Px, new Rectangle(row.X - DraedonShopTheme.SidePadding + 6, row.Y + 4, barW, row.Height - 8),
                new Rectangle(0, 0, 1, 1), accent * (alpha * (0.4f + 0.5f * plateStrength)));

            //悬停虚线描边 + 数据流指示
            if (hover > 0.01f || v.Selected) {
                Color techColor = v.Selected ? DraedonShopTheme.Gold : DraedonPanelDraw.GetEdgeColor(alpha, state.HologramFlicker);
                DraedonPanelDraw.DrawChoiceBorder(sb, row, techColor * (alpha * (0.25f + 0.45f * plateStrength)));
                DraedonPanelDraw.DrawChoiceDashIndicator(sb, row, techColor, plateStrength, alpha, state.DataStreamTimer);
            }

            //底部发光分隔
            int sepW = (int)((row.Width - 16) * (0.4f + 0.6f * plateStrength));
            sb.Draw(Px, new Rectangle(row.X + 8, row.Bottom - 1, sepW, 1), new Rectangle(0, 0, 1, 1),
                accent * (alpha * (0.18f + 0.3f * plateStrength)));

            //图标取景框
            int slide = (int)(hover * 5f);
            Rectangle iconRect = new(row.X + 6 + slide, row.Center.Y - DraedonShopTheme.IconBox / 2, DraedonShopTheme.IconBox, DraedonShopTheme.IconBox);
            DraedonShopStyle.DrawIconFrame(sb, iconRect, alpha, hover, pulse);
            DraedonShopStyle.DrawItemIcon(sb, v.ItemType, iconRect.Center.ToVector2(), DraedonShopTheme.IconBox - 12, alpha * (v.Affordable ? 1f : 0.6f), 1f + hover * 0.06f);

            //序号小标签 + 名称 + 价格
            float textX = iconRect.Right + 14 + slide;
            Utils.DrawBorderString(sb, $"{v.Ordinal:00}//", new Vector2(textX, row.Y + 9f),
                DraedonShopTheme.TextDim * (alpha * 0.85f), 0.5f);

            Color nameColor = Color.Lerp(v.Affordable ? DraedonShopTheme.Text : DraedonShopTheme.TextDim,
                DraedonShopTheme.TextBright, hover) * alpha;
            Utils.DrawBorderString(sb, DraedonShopStyle.Trim(v.Name, 22), new Vector2(textX, row.Y + 22f), nameColor, 0.92f);

            Color priceTint = v.Affordable ? DraedonShopTheme.TextBright : DraedonShopTheme.Danger;
            DraedonShopStyle.DrawCoins(sb, new Vector2(textX, row.Y + 48f), v.Price, alpha, 0.78f, priceTint);

            //右侧操作标签
            string act = (hover > 0.4f ? "> " : "") + buyLabel;
            float actScale = 0.78f + hover * 0.06f;
            float actW = Font.MeasureString(act).X * actScale;
            Color actColor = (v.Affordable ? accent : DraedonShopTheme.Danger) * (alpha * (0.55f + 0.45f * hover));
            Vector2 actPos = new(row.Right - actW - 14, row.Center.Y - Font.MeasureString(act).Y * actScale * 0.5f);
            if (hover > 0.01f && v.Affordable) {
                Color g = accent * (alpha * 0.4f * hover);
                for (int i = 0; i < 4; i++) {
                    Utils.DrawBorderString(sb, act, actPos + (MathHelper.TwoPi * i / 4f).ToRotationVector2(), g * 0.5f, actScale);
                }
            }
            Utils.DrawBorderString(sb, act, actPos, actColor, actScale);

            //连续购买计数
            if (v.HoldCount > 0) {
                string count = $"x{v.HoldCount + 1}";
                float cp = MathF.Sin(Main.GameUpdateCount * 0.2f) * 0.5f + 0.5f;
                Utils.DrawBorderString(sb, count, new Vector2(actPos.X - Font.MeasureString(count).X * 0.62f - 6f, actPos.Y),
                    Color.Lerp(DraedonShopTheme.Gold, DraedonShopTheme.EdgeBright, cp) * alpha, 0.62f);
            }

            //长按蓄力进度
            if (v.HoldProgress > 0.001f && v.HoldProgress < 1f) {
                int pw = (int)((row.Width - 16) * v.HoldProgress);
                Color pc = Color.Lerp(DraedonShopTheme.Edge, DraedonShopTheme.Gold, v.HoldProgress) * (alpha * 0.9f);
                sb.Draw(Px, new Rectangle(row.X + 8, row.Bottom - 3, pw, 2), new Rectangle(0, 0, 1, 1), pc);
            }
        }

        /// <summary>列表为空时的占位提示</summary>
        public static void DrawEmpty(SpriteBatch sb, Rectangle viewport, float alpha, string text) {
            float pulse = MathF.Sin(Main.GlobalTimeWrappedHourly * 2.5f) * 0.25f + 0.6f;
            Vector2 size = Font.MeasureString(text) * 0.85f;
            Utils.DrawBorderString(sb, text, viewport.Center.ToVector2() - size / 2f,
                DraedonShopTheme.TextDim * (alpha * pulse), 0.85f);
        }

        /// <summary>底部状态条：操作提示 + 翻页计数</summary>
        public static void DrawFooter(SpriteBatch sb, Rectangle panelRect, float alpha, DraedonPanelState state,
            string hint, string pageText) {
            float y = panelRect.Bottom - DraedonShopTheme.FooterHeight + 16f;
            DraedonPanelDraw.DrawDashDivider(sb,
                new Vector2(panelRect.X + DraedonShopTheme.SidePadding, y - 6f),
                new Vector2(panelRect.Right - DraedonShopTheme.SidePadding, y - 6f),
                alpha * 0.7f, -state.DataStreamTimer);

            Utils.DrawBorderString(sb, hint, new Vector2(panelRect.X + DraedonShopTheme.SidePadding, y + 4f),
                DraedonShopTheme.TextDim * (alpha * 0.9f), 0.62f);

            if (!string.IsNullOrEmpty(pageText)) {
                float w = Font.MeasureString(pageText).X * 0.62f;
                Utils.DrawBorderString(sb, pageText, new Vector2(panelRect.Right - DraedonShopTheme.SidePadding - w, y + 4f),
                    DraedonShopTheme.Text * (alpha * 0.9f), 0.62f);
            }
        }

        /// <summary>右侧滚动条：青色轨道 + 随拖拽变亮的指示块 + 几道刻度</summary>
        public static void DrawScrollbar(SpriteBatch sb, Rectangle track, float alpha, float scrollPx, float maxScroll,
            float indicatorHeight, float activeGlow, DraedonPanelState state) {
            //轨道
            sb.Draw(Px, track, new Rectangle(0, 0, 1, 1), DraedonShopTheme.EdgeDim * (alpha * 0.3f));
            float blink = MathF.Sin(state.CircuitPulseTimer * 0.7f) * 0.5f + 0.5f;
            for (int i = 0; i < track.Height; i += 14) {
                sb.Draw(Px, new Rectangle(track.X - 3, track.Y + i, 3, 1), new Rectangle(0, 0, 1, 1),
                    DraedonShopTheme.Edge * (alpha * 0.22f * blink));
            }

            float progress = maxScroll > 0f ? scrollPx / maxScroll : 0f;
            int indH = (int)indicatorHeight;
            int indY = track.Y + (int)(progress * (track.Height - indH));
            Rectangle indicator = new(track.X - 1, indY, track.Width + 2, indH);

            Color indColor = Color.Lerp(DraedonShopTheme.Edge, DraedonShopTheme.EdgeBright, 0.3f + 0.7f * activeGlow) * alpha;
            if (activeGlow > 0.01f) {
                Rectangle glow = indicator;
                glow.Inflate(2, 2);
                sb.Draw(Px, glow, new Rectangle(0, 0, 1, 1), DraedonShopTheme.Glow * (alpha * 0.25f * activeGlow));
            }
            sb.Draw(Px, indicator, new Rectangle(0, 0, 1, 1), indColor);
            sb.Draw(Px, new Rectangle(indicator.X, indicator.Y, indicator.Width, 1), new Rectangle(0, 0, 1, 1), DraedonShopTheme.EdgeBright * alpha);
            sb.Draw(Px, new Rectangle(indicator.X, indicator.Bottom - 1, indicator.Width, 1), new Rectangle(0, 0, 1, 1), DraedonShopTheme.EdgeBright * (alpha * 0.7f));
        }
    }
}
