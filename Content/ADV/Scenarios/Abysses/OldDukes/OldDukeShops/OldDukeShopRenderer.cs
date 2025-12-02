using CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Items;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.OldDukeShops
{
    /// <summary>
    /// 老公爵商店渲染器
    /// </summary>
    internal class OldDukeShopRenderer
    {
        private readonly Player player;
        private readonly List<OldDukeShopItem> shopItems;
        private readonly OldDukeShopAnimation animation;
        private readonly OldDukeShopInteraction interaction;

        public OldDukeShopRenderer(Player player, List<OldDukeShopItem> shopItems,
            OldDukeShopAnimation animation, OldDukeShopInteraction interaction) {
            this.player = player;
            this.shopItems = shopItems;
            this.animation = animation;
            this.interaction = interaction;
        }

        /// <summary>
        /// 计算面板中心位置
        /// </summary>
        public Vector2 CalculatePanelPosition() {
            Vector2 screenCenter = new Vector2(Main.screenWidth, Main.screenHeight) / 2f;

            //使用缓动函数实现滑入动画（从右侧滑入，与Draedon的左侧不同）
            float slideOffset = (1f - CWRUtils.EaseOutCubic(animation.PanelSlideProgress)) * 200f;

            return new Vector2(
                screenCenter.X - 580f / 2f + slideOffset,
                screenCenter.Y - 720f / 2f
            );
        }

        /// <summary>
        /// 绘制主面板和内容
        /// </summary>
        public void Draw(SpriteBatch spriteBatch, Vector2 panelPosition, OldDukeShopEffects effects) {
            if (animation.UIAlpha <= 0f) return;

            //绘制主面板
            DrawMainPanel(spriteBatch, panelPosition);

            //绘制特效粒子（在内容下方）
            effects.DrawEffects(spriteBatch, animation.UIAlpha);

            //绘制标题
            DrawHeader(spriteBatch, panelPosition);

            //绘制货币显示
            DrawCurrencyDisplay(spriteBatch, panelPosition);

            //绘制物品列表
            DrawItemList(spriteBatch, panelPosition);

            //绘制滚动条
            interaction.DrawScrollBar(spriteBatch, panelPosition, animation.UIAlpha, animation.SulfurPulse);

            //绘制滚动提示
            DrawScrollHint(spriteBatch, panelPosition);
        }

        #region 主面板绘制
        private void DrawMainPanel(SpriteBatch spriteBatch, Vector2 panelPosition) {
            Rectangle panelRect = new Rectangle(
                (int)panelPosition.X,
                (int)panelPosition.Y,
                580,
                720
            );

            Texture2D pixel = VaultAsset.placeholder2.Value;

            //阴影
            Rectangle shadow = panelRect;
            shadow.Offset(6, 8);
            spriteBatch.Draw(pixel, shadow, new Rectangle(0, 0, 1, 1), Color.Black * (animation.UIAlpha * 0.60f));

            //绘制渐变背景
            DrawGradientBackground(spriteBatch, panelRect, pixel);

            //瘴气覆盖层
            float miasmaEffect = (float)Math.Sin(animation.MiasmaTimer * 1.1f) * 0.5f + 0.5f;
            Color miasmaTint = new Color(45, 55, 20) * (animation.UIAlpha * 0.4f * miasmaEffect);
            spriteBatch.Draw(pixel, panelRect, new Rectangle(0, 0, 1, 1), miasmaTint);

            //绘制毒波纹理效果
            DrawToxicWaveOverlay(spriteBatch, panelRect, pixel);

            //内边框微光
            float pulse = (float)Math.Sin(animation.SulfurPulse) * 0.5f + 0.5f;
            Rectangle inner = panelRect;
            inner.Inflate(-6, -6);
            spriteBatch.Draw(pixel, inner, new Rectangle(0, 0, 1, 1), new Color(80, 100, 35) * (animation.UIAlpha * 0.09f * (0.5f + pulse * 0.5f)));

            //绘制硫磺框架
            DrawSulfseaFrame(spriteBatch, panelRect, pulse, pixel);
        }

        private void DrawGradientBackground(SpriteBatch spriteBatch, Rectangle panelRect, Texture2D pixel) {
            int segments = 30;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = panelRect.Y + (int)(t * panelRect.Height);
                int y2 = panelRect.Y + (int)(t2 * panelRect.Height);
                Rectangle r = new(panelRect.X, y1, panelRect.Width, Math.Max(1, y2 - y1));

                //硫磺海配色
                Color sulfurDeep = new Color(12, 18, 8);
                Color toxicMid = new Color(28, 38, 15);
                Color acidEdge = new Color(65, 85, 30);

                float breathing = (float)Math.Sin(animation.SulfurPulse) * 0.5f + 0.5f;
                Color blendBase = Color.Lerp(sulfurDeep, toxicMid, (float)Math.Sin(animation.SulfurPulse * 0.5f + t * 1.4f) * 0.5f + 0.5f);
                Color c = Color.Lerp(blendBase, acidEdge, t * 0.7f * (0.3f + breathing * 0.7f));
                c *= animation.UIAlpha * 0.92f;

                spriteBatch.Draw(pixel, r, new Rectangle(0, 0, 1, 1), c);
            }
        }

        private void DrawToxicWaveOverlay(SpriteBatch spriteBatch, Rectangle rect, Texture2D pixel) {
            int bands = 6;
            for (int i = 0; i < bands; i++) {
                float t = i / (float)bands;
                float y = rect.Y + 18 + t * (rect.Height - 36);
                float amp = 7f + (float)Math.Sin((animation.ToxicWavePhase + t) * 2.2f) * 4.5f;
                float thickness = 2.2f;
                int segments = 42;
                Vector2 prev = Vector2.Zero;
                for (int s = 0; s <= segments; s++) {
                    float p = s / (float)segments;
                    float localY = y + (float)Math.Sin(animation.ToxicWavePhase * 2.2f + p * MathHelper.TwoPi * 1.3f + t) * amp;
                    Vector2 point = new(rect.X + 8 + p * (rect.Width - 16), localY);
                    if (s > 0) {
                        Vector2 diff = point - prev;
                        float len = diff.Length();
                        if (len > 0.01f) {
                            float rot = diff.ToRotation();
                            Color c = new Color(60, 90, 30) * (animation.UIAlpha * 0.08f);
                            spriteBatch.Draw(pixel, prev, new Rectangle(0, 0, 1, 1), c, rot, Vector2.Zero, new Vector2(len, thickness), SpriteEffects.None, 0f);
                        }
                    }
                    prev = point;
                }
            }
        }

        private void DrawSulfseaFrame(SpriteBatch spriteBatch, Rectangle rect, float pulse, Texture2D pixel) {
            Color edge = Color.Lerp(new Color(70, 100, 35), new Color(130, 160, 65), pulse) * (animation.UIAlpha * 0.85f);

            //主边框
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), new Rectangle(0, 0, 1, 1), edge);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), new Rectangle(0, 0, 1, 1), edge * 0.75f);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.88f);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.88f);

            //内边框
            Rectangle inner = rect;
            inner.Inflate(-5, -5);
            Color innerC = new Color(140, 170, 70) * (animation.UIAlpha * 0.22f * pulse);
            spriteBatch.Draw(pixel, new Rectangle(inner.X, inner.Y, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerC);
            spriteBatch.Draw(pixel, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerC * 0.7f);
            spriteBatch.Draw(pixel, new Rectangle(inner.X, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerC * 0.88f);
            spriteBatch.Draw(pixel, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerC * 0.88f);

            //角星装饰
            DrawCornerStar(spriteBatch, new Vector2(rect.X + 10, rect.Y + 10), animation.UIAlpha * 0.9f);
            DrawCornerStar(spriteBatch, new Vector2(rect.Right - 10, rect.Y + 10), animation.UIAlpha * 0.9f);
            DrawCornerStar(spriteBatch, new Vector2(rect.X + 10, rect.Bottom - 10), animation.UIAlpha * 0.65f);
            DrawCornerStar(spriteBatch, new Vector2(rect.Right - 10, rect.Bottom - 10), animation.UIAlpha * 0.65f);
        }

        private static void DrawCornerStar(SpriteBatch spriteBatch, Vector2 pos, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float size = 5f;
            Color c = new Color(160, 190, 80) * alpha;
            spriteBatch.Draw(px, pos, new Rectangle(0, 0, 1, 1), c, 0f, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.26f), SpriteEffects.None, 0f);
            spriteBatch.Draw(px, pos, new Rectangle(0, 0, 1, 1), c * 0.8f, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.26f), SpriteEffects.None, 0f);
        }
        #endregion

        #region 标题绘制
        private void DrawHeader(SpriteBatch spriteBatch, Vector2 panelPosition) {
            DynamicSpriteFont font = FontAssets.DeathText.Value;
            string title = OldDukeShopUI.TitleText.Value;
            float titleSclse = 1f;
            Vector2 titlePos = panelPosition + new Vector2(400, 35);
            Vector2 titleSize = font.MeasureString(title) * titleSclse;
            titlePos.X -= titleSize.X / 2f;

            //标题发光效果
            Color glowColor = new Color(160, 190, 80) * (animation.UIAlpha * 0.75f);
            for (int i = 0; i < 4; i++) {
                float ang = MathHelper.TwoPi * i / 4f;
                Vector2 offset = ang.ToRotationVector2() * 2.5f;
                Utils.DrawBorderString(spriteBatch, title, titlePos + offset, glowColor * 0.6f, titleSclse);
            }

            Utils.DrawBorderString(spriteBatch, title, titlePos, Color.White * animation.UIAlpha, titleSclse);

            //绘制关闭按钮
            DrawCloseButton(spriteBatch, panelPosition);

            //分割线
            DrawHeaderDivider(spriteBatch, panelPosition);
        }

        /// <summary>
        /// 绘制关闭按钮
        /// </summary>
        private void DrawCloseButton(SpriteBatch spriteBatch, Vector2 panelPosition) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            Vector2 closeButtonPos = panelPosition + new Vector2(580 - OldDukeShopInteraction.CloseButtonSize - 15, 15);
            Rectangle closeButtonRect = new Rectangle(
                (int)closeButtonPos.X,
                (int)closeButtonPos.Y,
                OldDukeShopInteraction.CloseButtonSize,
                OldDukeShopInteraction.CloseButtonSize
            );

            bool isHovered = interaction.IsCloseButtonHovered;
            float hoverProgress = isHovered ? 1f : 0f;

            //按钮背景
            Color bgBase = new Color(30, 40, 15) * (animation.UIAlpha * 0.6f);
            Color bgHover = new Color(80, 50, 40) * (animation.UIAlpha * 0.8f);
            Color buttonBg = Color.Lerp(bgBase, bgHover, hoverProgress);
            spriteBatch.Draw(pixel, closeButtonRect, new Rectangle(0, 0, 1, 1), buttonBg);

            //悬停时的发光效果
            if (isHovered) {
                float glowPulse = (float)Math.Sin(animation.SulfurPulse * 2f) * 0.5f + 0.5f;
                Color glowColor = new Color(180, 90, 70) * (animation.UIAlpha * 0.3f * glowPulse);
                Rectangle glowRect = closeButtonRect;
                glowRect.Inflate(3, 3);
                spriteBatch.Draw(pixel, glowRect, new Rectangle(0, 0, 1, 1), glowColor);
            }

            //按钮边框
            Color edgeColor = Color.Lerp(
                new Color(70, 100, 35) * (animation.UIAlpha * 0.6f),
                new Color(180, 90, 70) * (animation.UIAlpha * 0.9f),
                hoverProgress
            );
            spriteBatch.Draw(pixel, new Rectangle(closeButtonRect.X, closeButtonRect.Y, closeButtonRect.Width, 2),
                new Rectangle(0, 0, 1, 1), edgeColor);
            spriteBatch.Draw(pixel, new Rectangle(closeButtonRect.X, closeButtonRect.Bottom - 2, closeButtonRect.Width, 2),
                new Rectangle(0, 0, 1, 1), edgeColor);
            spriteBatch.Draw(pixel, new Rectangle(closeButtonRect.X, closeButtonRect.Y, 2, closeButtonRect.Height),
                new Rectangle(0, 0, 1, 1), edgeColor);
            spriteBatch.Draw(pixel, new Rectangle(closeButtonRect.Right - 2, closeButtonRect.Y, 2, closeButtonRect.Height),
                new Rectangle(0, 0, 1, 1), edgeColor);

            //绘制X符号
            Vector2 center = new Vector2(closeButtonRect.X + closeButtonRect.Width / 2f, closeButtonRect.Y + closeButtonRect.Height / 2f);
            float xSize = 12f + hoverProgress * 2f;
            float thickness = 2.5f + hoverProgress * 0.5f;

            Color xColor = Color.Lerp(
                new Color(140, 170, 75) * animation.UIAlpha,
                new Color(220, 110, 90) * animation.UIAlpha,
                hoverProgress
            );

            //左上到右下的线
            Vector2 start1 = center + new Vector2(-xSize, -xSize);
            Vector2 end1 = center + new Vector2(xSize, xSize);
            DrawXLine(spriteBatch, start1, end1, xColor, thickness, pixel);

            //右上到左下的线
            Vector2 start2 = center + new Vector2(xSize, -xSize);
            Vector2 end2 = center + new Vector2(-xSize, xSize);
            DrawXLine(spriteBatch, start2, end2, xColor, thickness, pixel);
        }

        private static void DrawXLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color, float thickness, Texture2D pixel) {
            Vector2 edge = end - start;
            float length = edge.Length();
            if (length < 0.1f) return;

            float rotation = edge.ToRotation();
            spriteBatch.Draw(pixel, start, new Rectangle(0, 0, 1, 1), color, rotation,
                Vector2.Zero, new Vector2(length, thickness), SpriteEffects.None, 0);
        }

        private void DrawHeaderDivider(SpriteBatch spriteBatch, Vector2 panelPosition) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 lineStart = panelPosition + new Vector2(40, 80);
            Vector2 lineEnd = panelPosition + new Vector2(540, 80);

            Color edgeColor = Color.Lerp(new Color(70, 100, 35), new Color(130, 160, 65), (float)Math.Sin(animation.SulfurPulse) * 0.5f + 0.5f) * (animation.UIAlpha * 0.9f);

            DrawGradientLine(spriteBatch, lineStart, lineEnd, edgeColor, edgeColor * 0.08f, 1.5f, pixel);
        }

        private static void DrawGradientLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end,
            Color startColor, Color endColor, float thickness, Texture2D pixel) {
            Vector2 edge = end - start;
            float length = edge.Length();
            if (length < 1f) return;

            edge.Normalize();
            float rotation = (float)Math.Atan2(edge.Y, edge.X);
            int segments = Math.Max(1, (int)(length / 11f));

            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                Vector2 segPos = start + edge * (length * t);
                float segLength = length / segments;
                Color color = Color.Lerp(startColor, endColor, t);
                spriteBatch.Draw(pixel, segPos, new Rectangle(0, 0, 1, 1), color, rotation,
                    new Vector2(0, 0.5f), new Vector2(segLength, thickness), SpriteEffects.None, 0);
            }
        }
        #endregion

        #region 货币显示
        private long CalculateTotalCurrency() {
            long totalCopper = 0;
            CalculateInventory(player.inventory, ref totalCopper);
            CalculateInventory(player.bank.item, ref totalCopper);
            CalculateInventory(player.bank2.item, ref totalCopper);
            CalculateInventory(player.bank3.item, ref totalCopper);
            CalculateInventory(player.bank4.item, ref totalCopper);
            return totalCopper;
        }

        private static void CalculateInventory(Item[] items, ref long totalCopper) {
            if (items == null) return;

            for (int i = 0; i < items.Length; i++) {
                Item item = items[i];
                if (!item.Alives()) continue;

                if (item.type == ModContent.ItemType<Oceanfragments>()) totalCopper += item.stack;
            }
        }

        private void DrawCurrencyDisplay(SpriteBatch spriteBatch, Vector2 panelPosition) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;

            //获取海洋残片数量
            int oceanFragmentCount = (int)CalculateTotalCurrency();

            //绘制货币图标和数量
            Vector2 currencyPos = panelPosition + new Vector2(40, 100);

            //绘制海洋残片图标
            Item oceanFragmentItem = new Item(ModContent.ItemType<Oceanfragments>());
            Main.instance.LoadItem(oceanFragmentItem.type);

            float iconScale = 0.8f + (float)Math.Sin(animation.CurrencyDisplayPulse) * 0.1f;
            VaultUtils.SimpleDrawItem(spriteBatch, oceanFragmentItem.type, currencyPos + new Vector2(16, 16), 10, iconScale * 4f, 0, Color.White * animation.UIAlpha);

            //绘制数量文本
            string countText = oceanFragmentCount.ToString();
            Vector2 textPos = currencyPos + new Vector2(40, 8);
            Utils.DrawBorderString(spriteBatch, countText, textPos, Color.White * animation.UIAlpha, 1f);

            //货币名称
            string currencyName = OldDukeShopUI.CurrencyName.Value;
            Vector2 nameSize = font.MeasureString(currencyName) * 0.7f;
            Vector2 namePos = currencyPos + new Vector2(40, 26);
            Utils.DrawBorderString(spriteBatch, currencyName, namePos, new Color(140, 170, 75) * animation.UIAlpha, 0.7f);
        }
        #endregion

        #region 物品列表绘制
        private void DrawItemList(SpriteBatch spriteBatch, Vector2 panelPosition) {
            if (shopItems.Count == 0) return;

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Vector2 itemListPos = panelPosition + new Vector2(35, 140);

            for (int i = 0; i < Math.Min(OldDukeShopInteraction.MaxVisibleItems, shopItems.Count - interaction.ScrollOffset); i++) {
                int index = i + interaction.ScrollOffset;
                OldDukeShopItem shopItem = shopItems[index];

                Vector2 slotPos = itemListPos + new Vector2(0, i * OldDukeShopInteraction.ItemSlotHeight);

                bool isHovered = interaction.HoveredIndex == index;
                bool isSelected = interaction.SelectedIndex == index;
                bool isHolding = interaction.HoldingPurchaseIndex == index && interaction.HoldingPurchaseTimer > 0;

                float hoverProgress = animation.SlotHoverProgress[i];

                DrawShopItemSlot(spriteBatch, shopItem, slotPos, isHovered, isSelected, hoverProgress, font, index, isHolding);
            }
        }

        private void DrawShopItemSlot(SpriteBatch spriteBatch, OldDukeShopItem shopItem, Vector2 position,
            bool isHovered, bool isSelected, float hoverProgress, DynamicSpriteFont font, int currentItemIndex, bool isHolding) {
            Rectangle slotRect = new Rectangle(
                (int)position.X,
                (int)position.Y,
                510,
                OldDukeShopInteraction.ItemSlotHeight - 6
            );

            //绘制槽位背景
            DrawSlotBackground(spriteBatch, slotRect, isHovered, isSelected, isHolding, hoverProgress);

            //绘制长按进度条
            if (isHolding) {
                DrawHoldProgressBar(spriteBatch, slotRect);
            }

            //绘制连续购买计数器
            if (interaction.ConsecutivePurchaseCount > 0 && isHolding) {
                DrawPurchaseCounter(spriteBatch, slotRect);
            }

            //绘制物品图标
            DrawItemIcon(spriteBatch, shopItem, position + new Vector2(10, 10), hoverProgress);

            //绘制物品名称
            DrawItemName(spriteBatch, shopItem, position + new Vector2(70, 15), hoverProgress);

            //绘制价格
            DrawPriceDisplay(spriteBatch, shopItem, position + new Vector2(70, 42), hoverProgress);

            //绘制酸液数据流效果
            if (hoverProgress > 0.3f) {
                DrawAcidStreamEffect(spriteBatch, position, hoverProgress);
            }
        }

        private void DrawSlotBackground(SpriteBatch spriteBatch, Rectangle slotRect, bool isHovered,
            bool isSelected, bool isHolding, float hoverProgress) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //槽位背景
            Color bgBase = new Color(20, 30, 10) * (animation.UIAlpha * 0.3f);
            Color bgHover = new Color(50, 70, 25) * (animation.UIAlpha * 0.5f);
            Color slotBg = Color.Lerp(bgBase, bgHover, hoverProgress);

            spriteBatch.Draw(pixel, slotRect, new Rectangle(0, 0, 1, 1), slotBg);

            //悬停时的毒液效果
            if (hoverProgress > 0.01f) {
                float toxicGlow = (float)Math.Sin(animation.ToxicWavePhase * 2f + hoverProgress * 3f) * 0.5f + 0.5f;
                Color toxicColor = new Color(100, 140, 50) * (animation.UIAlpha * 0.15f * hoverProgress * toxicGlow);
                spriteBatch.Draw(pixel, slotRect, new Rectangle(0, 0, 1, 1), toxicColor);
            }

            //槽位边框
            Color edgeColor = Color.Lerp(
                new Color(60, 80, 35) * (animation.UIAlpha * 0.25f),
                new Color(130, 160, 65) * (animation.UIAlpha * 0.6f),
                hoverProgress
            );

            spriteBatch.Draw(pixel, new Rectangle(slotRect.X, slotRect.Y, slotRect.Width, 1), new Rectangle(0, 0, 1, 1), edgeColor);
            spriteBatch.Draw(pixel, new Rectangle(slotRect.X, slotRect.Bottom - 1, slotRect.Width, 1), new Rectangle(0, 0, 1, 1), edgeColor);
            spriteBatch.Draw(pixel, new Rectangle(slotRect.X, slotRect.Y, 1, slotRect.Height), new Rectangle(0, 0, 1, 1), edgeColor);
            spriteBatch.Draw(pixel, new Rectangle(slotRect.Right - 1, slotRect.Y, 1, slotRect.Height), new Rectangle(0, 0, 1, 1), edgeColor);
        }

        private void DrawHoldProgressBar(SpriteBatch spriteBatch, Rectangle slotRect) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float holdProgress = Math.Min(1f, interaction.HoldingPurchaseTimer / 20f);

            Rectangle progressBg = new Rectangle(slotRect.X + 3, slotRect.Bottom - 6, slotRect.Width - 6, 3);
            spriteBatch.Draw(pixel, progressBg, new Rectangle(0, 0, 1, 1), Color.Black * (animation.UIAlpha * 0.5f));

            Rectangle progressFill = new Rectangle(progressBg.X, progressBg.Y, (int)(progressBg.Width * holdProgress), progressBg.Height);
            Color progressColor = Color.Lerp(new Color(140, 170, 70), new Color(180, 210, 90), holdProgress) * animation.UIAlpha;
            spriteBatch.Draw(pixel, progressFill, new Rectangle(0, 0, 1, 1), progressColor);
        }

        private void DrawPurchaseCounter(SpriteBatch spriteBatch, Rectangle slotRect) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            string counterText = $"x{interaction.ConsecutivePurchaseCount}";
            Vector2 counterPos = new Vector2(slotRect.Right - 40, slotRect.Y + 5);

            Color counterColor = new Color(180, 210, 90) * animation.UIAlpha;
            Utils.DrawBorderString(spriteBatch, counterText, counterPos, counterColor, 0.9f);
        }

        private void DrawItemIcon(SpriteBatch spriteBatch, OldDukeShopItem shopItem, Vector2 position, float hoverProgress) {
            float iconScale = 0.8f + hoverProgress * 0.2f;
            float iconFloatOffset = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f + position.Y * 0.01f) * 2f * hoverProgress;

            VaultUtils.SimpleDrawItem(spriteBatch, shopItem.itemType, position + new Vector2(25, 25 + iconFloatOffset),
                10, iconScale * 5f, 0, Color.White * animation.UIAlpha);
        }

        private void DrawItemName(SpriteBatch spriteBatch, OldDukeShopItem shopItem, Vector2 position, float hoverProgress) {
            Item item = new Item(shopItem.itemType);
            string itemName = item.Name;
            if (shopItem.stack > 1) {
                itemName += $" x{shopItem.stack}";
            }

            Color nameColor = Color.White * animation.UIAlpha;

            //悬停发光
            if (hoverProgress > 0.3f) {
                Color glowColor = new Color(160, 190, 80) * (animation.UIAlpha * hoverProgress * 0.5f);
                for (int i = 0; i < 4; i++) {
                    float ang = MathHelper.TwoPi * i / 4f;
                    Vector2 offset = ang.ToRotationVector2() * (1f * hoverProgress);
                    Utils.DrawBorderString(spriteBatch, itemName, position + offset, glowColor * 0.3f, 0.85f);
                }
            }

            Utils.DrawBorderString(spriteBatch, itemName, position, nameColor, 0.85f);
        }

        private void DrawPriceDisplay(SpriteBatch spriteBatch, OldDukeShopItem shopItem, Vector2 position, float hoverProgress) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;

            //检查是否有足够的海洋残片
            int oceanFragmentCount = player.CountItem(ModContent.ItemType<Oceanfragments>());
            bool canAfford = oceanFragmentCount >= shopItem.price;

            //绘制海洋残片图标
            Item oceanFragmentItem = new Item(ModContent.ItemType<Oceanfragments>());
            float iconScale = 0.6f + hoverProgress * 0.1f;
            VaultUtils.SimpleDrawItem(spriteBatch, oceanFragmentItem.type, position + new Vector2(8, 8),
                10, iconScale * 3f, 0, Color.White * animation.UIAlpha);

            //价格文本
            string priceText = shopItem.price.ToString();
            Vector2 textPos = position + new Vector2(24, 0);

            Color priceColor = canAfford
                ? new Color(140, 170, 75) * animation.UIAlpha
                : new Color(180, 80, 80) * animation.UIAlpha;

            Utils.DrawBorderString(spriteBatch, priceText, textPos, priceColor, 0.8f);
        }

        private void DrawAcidStreamEffect(SpriteBatch spriteBatch, Vector2 position, float hoverProgress) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            float streamTimer = (float)Main.timeForVisualEffects * 0.08f;
            for (int i = 0; i < 3; i++) {
                float offset = (streamTimer + i * 0.33f) % 1f;
                Vector2 streamPos = position + new Vector2(offset * 510f, 5 + i * 22);

                Color streamColor = new Color(140, 170, 70) * (animation.UIAlpha * 0.2f * hoverProgress * (float)Math.Sin(offset * MathHelper.Pi));
                spriteBatch.Draw(pixel, streamPos, new Rectangle(0, 0, 1, 1), streamColor, 0f,
                    Vector2.Zero, new Vector2(15f, 2f), SpriteEffects.None, 0f);
            }
        }
        #endregion

        #region 滚动提示
        private void DrawScrollHint(SpriteBatch spriteBatch, Vector2 panelPosition) {
            if (shopItems.Count <= OldDukeShopInteraction.MaxVisibleItems) return;

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            string hint = OldDukeShopUI.HintTooltip.Value;
            Vector2 hintSize = font.MeasureString(hint) * 0.65f;
            Vector2 hintPos = panelPosition + new Vector2(290 - hintSize.X / 2f, 685);

            float blinkAlpha = (float)Math.Sin(animation.AcidFlowTimer * 1.5f) * 0.5f + 0.5f;
            Color hintColor = new Color(140, 170, 75) * (animation.UIAlpha * 0.5f * blinkAlpha);

            Utils.DrawBorderString(spriteBatch, hint, hintPos, hintColor, 0.65f);
        }
        #endregion
    }
}
