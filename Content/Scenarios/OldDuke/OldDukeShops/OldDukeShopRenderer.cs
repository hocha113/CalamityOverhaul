using CalamityOverhaul.Content.Narrative.Presentation.Skins.Sulfsea;
using CalamityOverhaul.OtherMods.ImproveGame;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldDuke.OldDukeShops
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
            float slideOffset = (1f - VaultUtils.EaseOutCubic(animation.PanelSlideProgress)) * 200f;

            return new Vector2(
                screenCenter.X - 580f / 2f + slideOffset,
                screenCenter.Y - 720f / 2f
            );
        }

        public void Draw(SpriteBatch spriteBatch, Vector2 panelPosition, SulfseaPanelState sulfseaState) {
            if (animation.UIAlpha <= 0f) return;

            DrawMainPanel(spriteBatch, panelPosition, sulfseaState);
            sulfseaState.DrawForeground(spriteBatch, animation.UIAlpha);

            DrawHeader(spriteBatch, panelPosition, sulfseaState.SulfurPulse);
            DrawCurrencyDisplay(spriteBatch, panelPosition);
            DrawItemList(spriteBatch, panelPosition, sulfseaState.ToxicWavePhase);
            interaction.DrawScrollBar(spriteBatch, panelPosition, animation.UIAlpha, sulfseaState.SulfurPulse);
            DrawScrollHint(spriteBatch, panelPosition);
        }

        private void DrawMainPanel(SpriteBatch spriteBatch, Vector2 panelPosition, SulfseaPanelState sulfseaState) {
            Rectangle panelRect = new((int)panelPosition.X, (int)panelPosition.Y, 580, 720);
            SulfseaPanelDraw.DrawShaderBackground(spriteBatch, panelRect, animation.UIAlpha, sulfseaState);
            float pulse = (float)Math.Sin(sulfseaState.SulfurPulse * 2.2f) * 0.5f + 0.5f;
            SulfseaPanelDraw.DrawFrame(spriteBatch, panelRect, animation.UIAlpha, pulse);
        }

        #region 标题绘制
        private void DrawHeader(SpriteBatch spriteBatch, Vector2 panelPosition, float sulfurPulse) {
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
            DrawCloseButton(spriteBatch, panelPosition, sulfurPulse);

            //分割线
            DrawHeaderDivider(spriteBatch, panelPosition, sulfurPulse);
        }

        private void DrawCloseButton(SpriteBatch spriteBatch, Vector2 panelPosition, float sulfurPulse) {
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
                float glowPulse = (float)Math.Sin(sulfurPulse * 2f) * 0.5f + 0.5f;
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

        private void DrawHeaderDivider(SpriteBatch spriteBatch, Vector2 panelPosition, float sulfurPulse) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 lineStart = panelPosition + new Vector2(40, 80);
            Vector2 lineEnd = panelPosition + new Vector2(540, 80);

            Color edgeColor = Color.Lerp(new Color(70, 100, 35), new Color(130, 160, 65), (float)Math.Sin(sulfurPulse) * 0.5f + 0.5f) * (animation.UIAlpha * 0.9f);

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
            var bigBags = player.GetBigBagItems() ?? [];
            CalculateInventory([.. bigBags], ref totalCopper);
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
        private void DrawItemList(SpriteBatch spriteBatch, Vector2 panelPosition, float toxicWavePhase) {
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

                float failFlash = animation.SlotFailFlash[i];
                DrawShopItemSlot(spriteBatch, shopItem, slotPos, isHovered, isSelected, hoverProgress, failFlash, font, index, isHolding, toxicWavePhase);
            }
        }

        private void DrawShopItemSlot(SpriteBatch spriteBatch, OldDukeShopItem shopItem, Vector2 position,
            bool isHovered, bool isSelected, float hoverProgress, float failFlash, DynamicSpriteFont font, int currentItemIndex, bool isHolding, float toxicWavePhase) {
            Rectangle slotRect = new Rectangle(
                (int)position.X,
                (int)position.Y,
                510,
                OldDukeShopInteraction.ItemSlotHeight - 6
            );

            //绘制槽位背景
            DrawSlotBackground(spriteBatch, slotRect, isHovered, isSelected, isHolding, hoverProgress, failFlash, toxicWavePhase);

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
            bool isSelected, bool isHolding, float hoverProgress, float failFlash, float toxicWavePhase) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            Color bgBase = new Color(20, 30, 10) * (animation.UIAlpha * 0.3f);
            Color bgHover = new Color(50, 70, 25) * (animation.UIAlpha * 0.5f);
            Color slotBg = Color.Lerp(bgBase, bgHover, hoverProgress);
            if (isSelected && hoverProgress > 0.5f) {
                slotBg = Color.Lerp(slotBg, new Color(65, 90, 30) * (animation.UIAlpha * 0.55f), 0.35f);
            }

            spriteBatch.Draw(pixel, slotRect, new Rectangle(0, 0, 1, 1), slotBg);

            if (hoverProgress > 0.01f) {
                float toxicGlow = (float)Math.Sin(toxicWavePhase * 2f + hoverProgress * 3f) * 0.5f + 0.5f;
                Color toxicColor = new Color(100, 140, 50) * (animation.UIAlpha * 0.15f * hoverProgress * toxicGlow);
                spriteBatch.Draw(pixel, slotRect, new Rectangle(0, 0, 1, 1), toxicColor);
            }

            if (failFlash > 0.01f) {
                Color failColor = new Color(200, 70, 60) * (animation.UIAlpha * failFlash * 0.45f);
                spriteBatch.Draw(pixel, slotRect, new Rectangle(0, 0, 1, 1), failColor);
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
            float holdProgress = Math.Min(1f, interaction.HoldingPurchaseTimer / (float)OldDukeShopInteraction.HoldThreshold);

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
            //检查是否有足够的海洋残片
            int oceanFragmentCount = player.InquireItem(true, ModContent.ItemType<Oceanfragments>());
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
