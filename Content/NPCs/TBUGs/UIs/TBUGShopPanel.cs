using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;

namespace CalamityOverhaul.Content.NPCs.TBUGs.UIs
{
    /// <summary>
    /// 商店货架列表：行 = 图标/名称/价格，单击购买（无确认弹窗，挂起门防连发）；
    /// 只管列表区，窗口壳与标题在 <see cref="TBUGShopUI"/>
    /// </summary>
    internal class TBUGShopPanel
    {
        private const int RowHeight = 58;
        private const float ScrollBarWidth = 5f;

        private Rectangle listRect;
        private float scrollOffset;
        private int oldScrollWheelValue;
        private int hoverIndex = -1;
        private readonly float[] hoverT = new float[64];

        private bool purchasePending;
        private uint purchaseSerial;

        /// <summary>底栏反馈文案与剩余展示帧</summary>
        private LocalizedText feedbackText;
        private int feedbackFrames;
        private bool feedbackGood;

        public void ResetView() {
            scrollOffset = 0f;
            hoverIndex = -1;
            Array.Clear(hoverT);
            purchasePending = false;
            feedbackText = null;
            feedbackFrames = 0;
            oldScrollWheelValue = Mouse.GetState().ScrollWheelValue;
        }

        public bool HasFeedback => feedbackFrames > 0 && feedbackText != null;
        public string FeedbackText => feedbackText?.Value ?? string.Empty;
        public bool FeedbackGood => feedbackGood;

        private static int RowCount => TBUGCatalog.Entries.Count;

        private float MaxScroll() {
            float content = RowCount * RowHeight;
            return MathF.Max(0f, content - listRect.Height);
        }

        public void Update(Rectangle rect, Point mousePoint, Player localPlayer) {
            listRect = rect;
            if (feedbackFrames > 0) {
                feedbackFrames--;
            }

            //滚轮：仅指针在列表内时接管
            int wheel = Mouse.GetState().ScrollWheelValue;
            int delta = wheel - oldScrollWheelValue;
            oldScrollWheelValue = wheel;
            bool inside = listRect.Contains(mousePoint);
            if (inside && delta != 0) {
                scrollOffset = Math.Clamp(scrollOffset - delta * 0.35f, 0f, MaxScroll());
                Terraria.GameInput.PlayerInput.LockVanillaMouseScroll("CalamityOverhaul/TBUGShop");
            }

            //悬停行
            int newHover = -1;
            if (inside) {
                int idx = (int)((mousePoint.Y - listRect.Y + scrollOffset) / RowHeight);
                if (idx >= 0 && idx < RowCount) {
                    newHover = idx;
                }
            }
            if (newHover != hoverIndex && newHover >= 0) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.35f });
            }
            hoverIndex = newHover;
            for (int i = 0; i < Math.Min(RowCount, hoverT.Length); i++) {
                bool on = i == hoverIndex;
                hoverT[i] = MathHelper.Clamp(hoverT[i] + (on ? 0.18f : -0.18f), 0f, 1f);
            }
        }

        /// <summary>列表区左键；返回 true 表示吃掉这次点击</summary>
        public bool HandleClick(Point mousePoint, Player localPlayer) {
            if (hoverIndex < 0 || !listRect.Contains(mousePoint)) {
                return false;
            }
            TBUGCatalogEntry entry = TBUGCatalog.Entries[hoverIndex];
            DoBuy(entry.ItemType, localPlayer);
            return true;
        }

        private void DoBuy(int itemType, Player localPlayer) {
            if (purchasePending) {
                SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.5f });
                return;
            }

            purchasePending = true;
            uint serial = ++purchaseSerial;
            bool sent = TBUGShopNet.SendPurchaseRequest(localPlayer,
                TBUGSession.BoundWhoAmI, itemType,
                (code, price) => HandlePurchaseResult(serial, code));
            if (!sent) {
                purchasePending = false;
                SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.5f });
            }
        }

        private void HandlePurchaseResult(uint serial, TBUGShopResult code) {
            if (serial != purchaseSerial) {
                return;
            }
            purchasePending = false;
            feedbackGood = code == TBUGShopResult.Success;
            feedbackText = TBUGShopUI.ResultText(code);
            feedbackFrames = 150;
            if (feedbackGood) {
                SoundEngine.PlaySound(SoundID.Coins);
                SoundEngine.PlaySound(SoundID.Grab with { Pitch = 0.1f });
            }
            else {
                SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.5f });
            }
        }

        public void Draw(SpriteBatch sb, float alpha, Player localPlayer) {
            Texture2D px = VaultAsset.placeholder2.Value;
            long balance = TBUGUIStyle.CountCoins(localPlayer);

            //列表裁剪：滚出区外的行直接跳过（行数少，无需真 scissor）
            int count = RowCount;
            for (int i = 0; i < count; i++) {
                float rowTop = listRect.Y + i * RowHeight - scrollOffset;
                if (rowTop + RowHeight < listRect.Y - 2 || rowTop > listRect.Bottom + 2) {
                    continue;
                }
                Rectangle rowRect = new(listRect.X, (int)rowTop, listRect.Width - (int)ScrollBarWidth - 6, RowHeight - 2);
                DrawRow(sb, rowRect, TBUGCatalog.Entries[i],
                    i < hoverT.Length ? hoverT[i] : 0f, alpha, balance);
            }

            //滚动条
            float maxScroll = MaxScroll();
            if (maxScroll > 0.5f) {
                Rectangle track = new(listRect.Right - (int)ScrollBarWidth, listRect.Y, (int)ScrollBarWidth, listRect.Height);
                sb.Draw(px, track, TBUGTheme.GridLine * (alpha * 0.8f));
                float viewRatio = listRect.Height / (float)(count * RowHeight);
                int thumbH = Math.Max(24, (int)(listRect.Height * viewRatio));
                int thumbY = listRect.Y + (int)((listRect.Height - thumbH) * (scrollOffset / maxScroll));
                sb.Draw(px, new Rectangle(track.X, thumbY, track.Width, thumbH), TBUGTheme.AccentDim * alpha);
            }

            //挂起遮罩：等回执时列表压暗
            if (purchasePending) {
                sb.Draw(px, listRect, TBUGTheme.BgDark * (alpha * 0.45f));
            }
        }

        private void DrawRow(SpriteBatch sb, Rectangle rect, TBUGCatalogEntry entry,
            float hover, float alpha, long balance) {
            long price = TBUGCatalog.GetDisplayPrice(entry.ItemType);
            bool affordable = balance >= price;

            Color accent = affordable ? TBUGTheme.Accent : TBUGTheme.AccentErr;
            int slide = TBUGUIStyle.DrawCommandRow(sb, rect, accent, hover, alpha);

            float rowAlpha = alpha * (affordable ? 1f : 0.55f);
            TBUGUIStyle.DrawItemIcon(sb, entry.ItemType,
                new Vector2(rect.X + 30 + slide, rect.Center.Y), 38f, rowAlpha);

            //名称
            Item sample = ContentSamples.ItemsByType.TryGetValue(entry.ItemType, out Item it) ? it : null;
            string name = TBUGUIStyle.Trim(sample?.Name ?? Lang.GetItemNameValue(entry.ItemType), 26);
            float nameScale = 0.56f * TBUGTheme.FontScale;
            Vector2 nameSize = FontAssets.MouseText.Value.MeasureString(name) * nameScale;
            Utils.DrawBorderString(sb, name,
                new Vector2(rect.X + 56 + slide, rect.Y + (rect.Height - nameSize.Y) * 0.5f - 8f),
                TBUGTheme.TextBright * rowAlpha, nameScale);

            //价格：名称下方一行
            TBUGUIStyle.DrawPrice(sb,
                new Vector2(rect.X + 56 + slide, rect.Y + rect.Height * 0.5f + 4f),
                price, rowAlpha, 0.46f * TBUGTheme.FontScale, rightAlign: false);

            //右缘操作提示
            string action = hover > 0.4f ? ">>" : ">";
            Utils.DrawBorderString(sb, action,
                new Vector2(rect.Right - 30 - hover * 4f, rect.Y + rect.Height * 0.5f - 8f),
                accent * (alpha * (0.35f + 0.65f * hover)), 0.6f * TBUGTheme.FontScale);
        }
    }
}
