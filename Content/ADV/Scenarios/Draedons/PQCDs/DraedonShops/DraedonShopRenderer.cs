using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.PQCDs.DraedonShops
{
    /// <summary>
    /// 渲染器
    /// </summary>
    internal class DraedonShopRenderer
    {
        private readonly Player player;
        private readonly List<ShopItem> shopItems;
        private readonly DraedonShopAnimation animation;
        private readonly DraedonShopInteraction interaction;

        private const int PanelWidth = 680;
        private const int PanelHeight = 640;
        private const float TechSideMargin = 35f;

        public DraedonShopRenderer(Player player, List<ShopItem> shopItems,
            DraedonShopAnimation animation, DraedonShopInteraction interaction) {
            this.player = player;
            this.shopItems = shopItems;
            this.animation = animation;
            this.interaction = interaction;
        }

        /// <summary>
        /// 计算面板位置
        /// </summary>
        public Vector2 CalculatePanelPosition() {
            float slideOffset = (1f - CWRUtils.EaseOutCubic(animation.PanelSlideProgress)) * PanelWidth;
            return new Vector2(Main.screenWidth - PanelWidth + slideOffset,
                (Main.screenHeight - PanelHeight) / 2f);
        }

        /// <summary>
        /// 绘制所有内容
        /// </summary>
        public void Draw(SpriteBatch spriteBatch, Vector2 panelPosition, DraedonShopEffects effects) {
            if (animation.UIAlpha <= 0f) return;

            DrawMainPanel(spriteBatch, panelPosition);
            DrawHeader(spriteBatch, panelPosition);
            DrawItemList(spriteBatch, panelPosition);
            effects.DrawEffects(spriteBatch, animation.UIAlpha);
            DrawScrollHint(spriteBatch, panelPosition);
        }

        private void DrawMainPanel(SpriteBatch spriteBatch, Vector2 panelPosition) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle panelRect = new Rectangle(
                (int)panelPosition.X,
                (int)panelPosition.Y,
                PanelWidth,
                PanelHeight
            );

            //深度阴影
            Rectangle shadow = panelRect;
            shadow.Offset(6, 8);
            spriteBatch.Draw(pixel, shadow, Color.Black * (animation.UIAlpha * 0.7f));

            //主背景渐变
            DrawGradientBackground(spriteBatch, panelRect, pixel);

            //全息闪烁叠加
            float flicker = (float)Math.Sin(animation.HologramFlicker * 1.5f) * 0.5f + 0.5f;
            spriteBatch.Draw(pixel, panelRect, new Color(15, 30, 45) * (animation.UIAlpha * 0.25f * flicker));

            //六角网格
            DrawHexGrid(spriteBatch, panelRect, pixel);

            //扫描线
            DrawScanLines(spriteBatch, panelRect, pixel);

            //内部脉冲发光
            float innerPulse = (float)Math.Sin(animation.CircuitPulseTimer * 1.3f) * 0.5f + 0.5f;
            Rectangle inner = panelRect;
            inner.Inflate(-5, -5);
            spriteBatch.Draw(pixel, inner, new Color(40, 180, 255) * (animation.UIAlpha * 0.12f * innerPulse));

            //科技边框
            DrawTechFrame(spriteBatch, panelRect, innerPulse, pixel);
        }

        private void DrawGradientBackground(SpriteBatch spriteBatch, Rectangle panelRect, Texture2D pixel) {
            int segments = 50;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = panelRect.Y + (int)(t * panelRect.Height);
                int y2 = panelRect.Y + (int)(t2 * panelRect.Height);
                Rectangle segment = new Rectangle(panelRect.X, y1, panelRect.Width, Math.Max(1, y2 - y1));

                Color techDark = new Color(8, 12, 22);
                Color techMid = new Color(18, 28, 42);
                Color techEdge = new Color(35, 55, 85);

                float pulse = (float)Math.Sin(animation.CircuitPulseTimer * 0.6f + t * 2.5f) * 0.5f + 0.5f;
                Color blendBase = Color.Lerp(techDark, techMid, pulse);
                Color finalColor = Color.Lerp(blendBase, techEdge, t * 0.5f) * (animation.UIAlpha * 0.94f);

                spriteBatch.Draw(pixel, segment, finalColor);
            }
        }

        private void DrawHexGrid(SpriteBatch spriteBatch, Rectangle rect, Texture2D pixel) {
            //水平网格线
            int hexRows = 12;
            float hexHeight = rect.Height / (float)hexRows;

            for (int row = 0; row < hexRows; row++) {
                float t = row / (float)hexRows;
                float y = rect.Y + row * hexHeight;
                float phase = animation.HexGridPhase + t * MathHelper.Pi;
                float brightness = (float)Math.Sin(phase) * 0.5f + 0.5f;

                Color gridColor = new Color(25, 90, 140) * (animation.UIAlpha * 0.05f * brightness);
                spriteBatch.Draw(pixel, new Rectangle(rect.X + 15, (int)y, rect.Width - 30, 1), gridColor);
            }

            //垂直网格线
            int hexCols = 15;
            float hexWidth = rect.Width / (float)hexCols;
            for (int col = 0; col < hexCols; col++) {
                float t = col / (float)hexCols;
                float x = rect.X + col * hexWidth;
                float phase = animation.HexGridPhase + t * MathHelper.Pi * 0.7f;
                float brightness = (float)Math.Sin(phase) * 0.5f + 0.5f;

                Color gridColor = new Color(25, 90, 140) * (animation.UIAlpha * 0.04f * brightness);
                spriteBatch.Draw(pixel, new Rectangle((int)x, rect.Y + 20, 1, rect.Height - 40), gridColor);
            }
        }

        private void DrawScanLines(SpriteBatch spriteBatch, Rectangle rect, Texture2D pixel) {
            float scanY = rect.Y + (float)Math.Sin(animation.ScanLineTimer) * 0.5f * rect.Height + rect.Height * 0.5f;

            for (int i = -3; i <= 3; i++) {
                float offsetY = scanY + i * 3.5f;
                if (offsetY < rect.Y || offsetY > rect.Bottom) continue;

                float intensity = 1f - Math.Abs(i) * 0.25f;
                Color scanColor = new Color(60, 180, 255) * (animation.UIAlpha * 0.18f * intensity);
                spriteBatch.Draw(pixel, new Rectangle(rect.X + 12, (int)offsetY, rect.Width - 24, 2), scanColor);
            }
        }

        private void DrawTechFrame(SpriteBatch spriteBatch, Rectangle rect, float pulse, Texture2D pixel) {
            Color borderColor = Color.Lerp(new Color(40, 160, 240), new Color(80, 200, 255), pulse) * (animation.UIAlpha * 0.9f);

            //外边框
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 4), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 4, rect.Width, 4), borderColor * 0.75f);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 4, rect.Height), borderColor * 0.9f);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - 4, rect.Y, 4, rect.Height), borderColor * 0.9f);

            //内发光边框
            Rectangle inner = rect;
            inner.Inflate(-8, -8);
            Color innerGlow = new Color(100, 200, 255) * (animation.UIAlpha * 0.25f * pulse);
            spriteBatch.Draw(pixel, new Rectangle(inner.X, inner.Y, inner.Width, 2), innerGlow);
            spriteBatch.Draw(pixel, new Rectangle(inner.X, inner.Bottom - 2, inner.Width, 2), innerGlow * 0.7f);
            spriteBatch.Draw(pixel, new Rectangle(inner.X, inner.Y, 2, inner.Height), innerGlow * 0.9f);
            spriteBatch.Draw(pixel, new Rectangle(inner.Right - 2, inner.Y, 2, inner.Height), innerGlow * 0.9f);

            //角落电路装饰
            DrawCornerCircuit(spriteBatch, new Vector2(rect.X + 15, rect.Y + 15), animation.UIAlpha);
            DrawCornerCircuit(spriteBatch, new Vector2(rect.Right - 15, rect.Y + 15), animation.UIAlpha);
            DrawCornerCircuit(spriteBatch, new Vector2(rect.X + 15, rect.Bottom - 15), animation.UIAlpha * 0.7f);
            DrawCornerCircuit(spriteBatch, new Vector2(rect.Right - 15, rect.Bottom - 15), animation.UIAlpha * 0.7f);
        }

        private static void DrawCornerCircuit(SpriteBatch sb, Vector2 pos, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float size = 8f;
            Color c = new Color(100, 220, 255) * alpha;

            sb.Draw(px, pos, null, c, 0f, new Vector2(0.5f), new Vector2(size, size * 0.2f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, null, c * 0.85f, MathHelper.PiOver2, new Vector2(0.5f), new Vector2(size, size * 0.2f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, null, c * 0.6f, 0f, new Vector2(0.5f), new Vector2(size * 0.5f), SpriteEffects.None, 0f);
        }

        private void DrawHeader(SpriteBatch spriteBatch, Vector2 panelPosition) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;

            //标题
            string title = "DRAEDON QUANTUM STORE";
            Vector2 titleSize = font.MeasureString(title) * 1.3f;
            Vector2 titlePos = panelPosition + new Vector2((PanelWidth - titleSize.X) / 2f, 25);

            //标题发光效果
            Color glowColor = new Color(80, 220, 255) * (animation.UIAlpha * 0.9f);
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8f;
                Vector2 offset = angle.ToRotationVector2() * 3f;
                Utils.DrawBorderString(spriteBatch, title, titlePos + offset, glowColor * 0.5f, 1.3f);
            }
            Utils.DrawBorderString(spriteBatch, title, titlePos, Color.White * animation.UIAlpha, 1.3f);

            //分隔线
            Vector2 lineStart = panelPosition + new Vector2(40, 75);
            Vector2 lineEnd = lineStart + new Vector2(PanelWidth - 80, 0);
            DrawGradientLine(spriteBatch, lineStart, lineEnd,
                new Color(60, 160, 240) * (animation.UIAlpha * 0.9f),
                new Color(60, 160, 240) * (animation.UIAlpha * 0.1f), 2f);

            //货币显示
            DrawCurrencyDisplay(spriteBatch, panelPosition);
        }

        private void DrawCurrencyDisplay(SpriteBatch spriteBatch, Vector2 panelPosition) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Vector2 currencyPos = panelPosition + new Vector2(40, 85);

            //计算玩家总货币
            long totalCopper = CalculateTotalCurrency();

            int platinum = (int)(totalCopper / 1000000);
            int gold = (int)(totalCopper % 1000000 / 10000);
            int silver = (int)(totalCopper % 10000 / 100);
            int copper = (int)(totalCopper % 100);

            //FUNDS文字
            string fundsText = "FUNDS: ";
            float pulse = (float)Math.Sin(animation.CoinDisplayPulse) * 0.5f + 0.5f;
            Color fundsTitleColor = Color.Lerp(new Color(200, 220, 255), new Color(255, 255, 255), pulse * 0.3f) * animation.UIAlpha;
            Utils.DrawBorderString(spriteBatch, fundsText, currencyPos, fundsTitleColor, 0.85f);

            Vector2 fundsTextSize = font.MeasureString(fundsText) * 0.85f;
            Vector2 coinPos = currencyPos + new Vector2(fundsTextSize.X, -2);

            float coinScale = 0.8f;
            float spacing = 8f;

            //绘制各种货币
            if (platinum > 0) {
                DrawCoinWithAmount(spriteBatch, coinPos, ItemID.PlatinumCoin, platinum, coinScale, animation.UIAlpha, pulse);
                coinPos.X += GetCoinDisplayWidth(platinum, coinScale) + spacing;
            }

            if (gold > 0) {
                DrawCoinWithAmount(spriteBatch, coinPos, ItemID.GoldCoin, gold, coinScale, animation.UIAlpha, pulse);
                coinPos.X += GetCoinDisplayWidth(gold, coinScale) + spacing;
            }

            if (silver > 0) {
                DrawCoinWithAmount(spriteBatch, coinPos, ItemID.SilverCoin, silver, coinScale, animation.UIAlpha, pulse);
                coinPos.X += GetCoinDisplayWidth(silver, coinScale) + spacing;
            }

            if (copper > 0 || totalCopper == 0) {
                DrawCoinWithAmount(spriteBatch, coinPos, ItemID.CopperCoin, copper, coinScale, animation.UIAlpha, pulse);
            }
        }

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

                if (item.type == ItemID.CopperCoin) totalCopper += item.stack;
                if (item.type == ItemID.SilverCoin) totalCopper += item.stack * 100;
                if (item.type == ItemID.GoldCoin) totalCopper += item.stack * 10000;
                if (item.type == ItemID.PlatinumCoin) totalCopper += item.stack * 1000000;
            }
        }

        private static void DrawCoinWithAmount(SpriteBatch spriteBatch, Vector2 position, int coinType,
            int amount, float scale, float alpha, float pulse) {
            Main.instance.LoadItem(coinType);
            Texture2D coinTexture = TextureAssets.Item[coinType].Value;
            Rectangle coinFrame = coinTexture.Bounds;

            float iconScale = Math.Min(24f / coinFrame.Width, 24f / coinFrame.Height) * scale;
            Vector2 iconPos = position + new Vector2(12, 12);

            //货币发光效果
            Color glowColor = GetCoinGlowColor(coinType, pulse) * (alpha * 0.6f);
            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.TwoPi * i / 4f;
                Vector2 offset = angle.ToRotationVector2() * (1.2f * scale);
                spriteBatch.Draw(coinTexture, iconPos + offset, coinFrame, glowColor, 0f,
                    coinFrame.Size() / 2f, iconScale * 1.1f, SpriteEffects.None, 0f);
            }

            spriteBatch.Draw(coinTexture, iconPos, coinFrame, Color.White * alpha, 0f,
                coinFrame.Size() / 2f, iconScale, SpriteEffects.None, 0f);

            //绘制数量文字
            string amountText = amount.ToString();
            Vector2 textPos = position + new Vector2(26, 4);
            Color textColor = Color.Lerp(Color.White, GetCoinColor(coinType), 0.4f) * alpha;

            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.TwoPi * i / 4f;
                Vector2 offset = angle.ToRotationVector2() * (1.0f * scale);
                Utils.DrawBorderString(spriteBatch, amountText, textPos + offset, glowColor * 0.4f, 0.75f * scale);
            }
            Utils.DrawBorderString(spriteBatch, amountText, textPos, textColor, 0.75f * scale);
        }

        private static Color GetCoinColor(int coinType) {
            return coinType switch {
                ItemID.PlatinumCoin => new Color(220, 220, 255),
                ItemID.GoldCoin => new Color(255, 215, 0),
                ItemID.SilverCoin => new Color(192, 192, 192),
                ItemID.CopperCoin => new Color(184, 115, 51),
                _ => Color.White
            };
        }

        private static Color GetCoinGlowColor(int coinType, float pulse) {
            Color baseColor = GetCoinColor(coinType);
            return Color.Lerp(baseColor, Color.White, pulse * 0.3f);
        }

        private static float GetCoinDisplayWidth(int amount, float scale) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            string amountText = amount.ToString();
            float textWidth = font.MeasureString(amountText).X * 0.75f * scale;
            return 26 + textWidth;
        }

        private void DrawItemList(SpriteBatch spriteBatch, Vector2 panelPosition) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            int itemListY = (int)(panelPosition.Y + 120);
            int itemListX = (int)(panelPosition.X + 30);

            for (int i = 0; i < Math.Min(DraedonShopInteraction.MaxVisibleItems, shopItems.Count - interaction.ScrollOffset); i++) {
                int index = i + interaction.ScrollOffset;
                ShopItem shopItem = shopItems[index];

                Vector2 itemPos = new Vector2(itemListX, itemListY + i * DraedonShopInteraction.ItemSlotHeight);
                bool isHovered = index == interaction.HoveredIndex;
                bool isSelected = index == interaction.SelectedIndex;
                float hoverProgress = animation.SlotHoverProgress[Math.Min(index, animation.SlotHoverProgress.Length - 1)];

                DrawShopItemSlot(spriteBatch, shopItem, itemPos, isHovered, isSelected, hoverProgress, font, index);
            }
        }

        private void DrawShopItemSlot(SpriteBatch spriteBatch, ShopItem shopItem, Vector2 position,
            bool isHovered, bool isSelected, float hoverProgress, DynamicSpriteFont font, int currentItemIndex) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle slotRect = new Rectangle(
                (int)position.X,
                (int)position.Y,
                PanelWidth - 60,
                DraedonShopInteraction.ItemSlotHeight - 8
            );

            bool isHolding = interaction.HoldingPurchaseIndex == currentItemIndex && interaction.HoldingPurchaseTimer > 0;

            //槽位背景和边框
            DrawSlotBackground(spriteBatch, slotRect, isHovered, isSelected, isHolding, hoverProgress, pixel);

            //长按购买进度条
            if (isHolding && interaction.HoldingPurchaseTimer < 20) {
                DrawHoldProgressBar(spriteBatch, slotRect, pixel);
            }

            //连续购买计数显示
            if (isHolding && interaction.ConsecutivePurchaseCount > 0 && interaction.HoldingPurchaseTimer >= 20) {
                DrawPurchaseCounter(spriteBatch, slotRect);
            }

            //物品图标
            DrawItemIcon(spriteBatch, shopItem, position, hoverProgress);

            //物品名称
            DrawItemName(spriteBatch, shopItem, position, hoverProgress);

            //价格显示
            DrawPriceDisplay(spriteBatch, shopItem, position, hoverProgress);

            //数据流效果
            DrawDataStreamEffect(spriteBatch, position, hoverProgress, pixel);
        }

        private void DrawSlotBackground(SpriteBatch spriteBatch, Rectangle slotRect, bool isHovered,
            bool isSelected, bool isHolding, float hoverProgress, Texture2D pixel) {
            Color baseSlotColor = new Color(20, 40, 60);
            Color hoverSlotColor = new Color(40, 80, 120);
            Color selectedSlotColor = new Color(60, 120, 180);
            Color holdingSlotColor = new Color(80, 150, 100);

            Color slotColor = baseSlotColor;
            if (isHolding && interaction.HoldingPurchaseTimer >= 20) {
                slotColor = Color.Lerp(hoverSlotColor, holdingSlotColor, 0.7f);
            }
            else if (isSelected) {
                slotColor = Color.Lerp(hoverSlotColor, selectedSlotColor, 0.6f);
            }
            else if (isHovered) {
                slotColor = Color.Lerp(baseSlotColor, hoverSlotColor, hoverProgress);
            }
            slotColor *= animation.UIAlpha * 0.6f;

            spriteBatch.Draw(pixel, slotRect, slotColor);

            //槽位边框
            float borderPulse = (float)Math.Sin(animation.CircuitPulseTimer * 1.5f + slotRect.Y * 0.01f) * 0.5f + 0.5f;

            if (isHolding && interaction.HoldingPurchaseTimer >= 20) {
                float rapidPulse = (float)Math.Sin(Main.GameUpdateCount * 0.3f) * 0.5f + 0.5f;
                borderPulse = Math.Max(borderPulse, rapidPulse * 0.9f);
            }

            Color borderColor = Color.Lerp(
                new Color(40, 140, 200),
                new Color(80, 200, 255),
                borderPulse * hoverProgress
            ) * (animation.UIAlpha * (0.4f + hoverProgress * 0.4f));

            if (isHolding && interaction.HoldingPurchaseTimer >= 20) {
                borderColor = Color.Lerp(borderColor, new Color(120, 255, 120), 0.6f);
            }

            spriteBatch.Draw(pixel, new Rectangle(slotRect.X, slotRect.Y, slotRect.Width, 2), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(slotRect.X, slotRect.Bottom - 2, slotRect.Width, 2), borderColor * 0.7f);

            //悬停发光效果
            if (hoverProgress > 0.01f) {
                Rectangle glowRect = slotRect;
                glowRect.Inflate(2, 2);
                Color glowColor = new Color(80, 200, 255) * (animation.UIAlpha * 0.15f * hoverProgress);

                if (isHolding && interaction.HoldingPurchaseTimer >= 20) {
                    glowColor = Color.Lerp(glowColor, new Color(120, 255, 120), 0.5f);
                    glowColor *= 1.5f;
                }

                spriteBatch.Draw(pixel, glowRect, glowColor);
            }
        }

        private void DrawHoldProgressBar(SpriteBatch spriteBatch, Rectangle slotRect, Texture2D pixel) {
            float holdProgress = interaction.HoldingPurchaseTimer / 20f;
            int progressWidth = (int)(slotRect.Width * holdProgress);
            Rectangle progressRect = new Rectangle(
                slotRect.X,
                slotRect.Bottom - 4,
                progressWidth,
                4
            );
            Color progressColor = Color.Lerp(Color.Yellow, Color.Lime, holdProgress) * (animation.UIAlpha * 0.8f);
            spriteBatch.Draw(pixel, progressRect, progressColor);

            Rectangle progressGlow = progressRect;
            progressGlow.Inflate(0, 2);
            spriteBatch.Draw(pixel, progressGlow, progressColor * 0.3f);
        }

        private void DrawPurchaseCounter(SpriteBatch spriteBatch, Rectangle slotRect) {
            string countText = $"x{interaction.ConsecutivePurchaseCount + 1}";
            Vector2 countPos = new Vector2(slotRect.Right - 10, slotRect.Y + 5);
            float countPulse = (float)Math.Sin(Main.GameUpdateCount * 0.2f) * 0.5f + 0.5f;
            Color countColor = Color.Lerp(Color.Lime, Color.Yellow, countPulse);
            Utils.DrawBorderString(spriteBatch, countText, countPos, countColor * animation.UIAlpha, 1f, 1f, 0f);
        }

        private void DrawItemIcon(SpriteBatch spriteBatch, ShopItem shopItem, Vector2 position, float hoverProgress) {
            Main.instance.LoadItem(shopItem.itemType);
            Texture2D itemTexture = TextureAssets.Item[shopItem.itemType].Value;
            Rectangle itemFrame = Main.itemAnimations[shopItem.itemType]?.GetFrame(itemTexture) ?? itemTexture.Bounds;
            float itemScale = Math.Min(56f / itemFrame.Width, 56f / itemFrame.Height);
            itemScale *= 1f + hoverProgress * 0.1f;
            Vector2 itemDrawPos = position + new Vector2(42, DraedonShopInteraction.ItemSlotHeight / 2f - 4);

            if (hoverProgress > 0.01f) {
                Color itemGlow = new Color(80, 200, 255) * (animation.UIAlpha * 0.3f * hoverProgress);
                spriteBatch.Draw(itemTexture, itemDrawPos, itemFrame, itemGlow, 0f,
                    itemFrame.Size() / 2f, itemScale * 1.15f, SpriteEffects.None, 0f);
            }
            spriteBatch.Draw(itemTexture, itemDrawPos, itemFrame, Color.White * animation.UIAlpha, 0f,
                itemFrame.Size() / 2f, itemScale, SpriteEffects.None, 0f);
        }

        private void DrawItemName(SpriteBatch spriteBatch, ShopItem shopItem, Vector2 position, float hoverProgress) {
            string itemName = Lang.GetItemNameValue(shopItem.itemType);
            Vector2 namePos = position + new Vector2(90, 18);
            Color nameColor = Color.Lerp(new Color(200, 230, 255), new Color(255, 255, 255), hoverProgress) * animation.UIAlpha;
            Utils.DrawBorderString(spriteBatch, itemName, namePos, nameColor, 0.9f);
        }

        private void DrawPriceDisplay(SpriteBatch spriteBatch, ShopItem shopItem, Vector2 position, float hoverProgress) {
            Vector2 pricePos = position + new Vector2(90, 45);

            int platinum = shopItem.price / 1000000;
            int gold = shopItem.price % 1000000 / 10000;
            int silver = shopItem.price % 10000 / 100;
            int copper = shopItem.price % 100;

            bool canAfford = player.CanAfford(shopItem.price);
            float coinScale = 0.65f;
            float spacing = 6f;

            Vector2 coinPos = pricePos;

            if (platinum > 0) {
                DrawPriceCoin(spriteBatch, coinPos, ItemID.PlatinumCoin, platinum, coinScale, hoverProgress, canAfford);
                coinPos.X += GetCoinDisplayWidth(platinum, coinScale) + spacing;
            }

            if (gold > 0) {
                DrawPriceCoin(spriteBatch, coinPos, ItemID.GoldCoin, gold, coinScale, hoverProgress, canAfford);
                coinPos.X += GetCoinDisplayWidth(gold, coinScale) + spacing;
            }

            if (silver > 0) {
                DrawPriceCoin(spriteBatch, coinPos, ItemID.SilverCoin, silver, coinScale, hoverProgress, canAfford);
                coinPos.X += GetCoinDisplayWidth(silver, coinScale) + spacing;
            }

            if (copper > 0 || shopItem.price == 0) {
                DrawPriceCoin(spriteBatch, coinPos, ItemID.CopperCoin, copper, coinScale, hoverProgress, canAfford);
            }
        }

        private void DrawPriceCoin(SpriteBatch spriteBatch, Vector2 position, int coinType,
            int amount, float scale, float hoverProgress, bool canAfford) {
            Main.instance.LoadItem(coinType);
            Texture2D coinTexture = TextureAssets.Item[coinType].Value;
            Rectangle coinFrame = coinTexture.Bounds;

            float iconScale = Math.Min(20f / coinFrame.Width, 20f / coinFrame.Height) * scale;
            Vector2 iconPos = position + new Vector2(10, 10);

            Color priceColor = canAfford
                ? Color.Lerp(GetCoinColor(coinType), Color.Lerp(GetCoinColor(coinType), Color.White, 0.4f), hoverProgress)
                : Color.Lerp(new Color(255, 100, 100), new Color(255, 150, 150), hoverProgress);
            priceColor *= animation.UIAlpha;

            if (canAfford && hoverProgress > 0.01f) {
                Color glowColor = GetCoinGlowColor(coinType, hoverProgress) * (animation.UIAlpha * 0.5f * hoverProgress);
                for (int i = 0; i < 4; i++) {
                    float angle = MathHelper.TwoPi * i / 4f;
                    Vector2 offset = angle.ToRotationVector2() * (1.0f * scale);
                    spriteBatch.Draw(coinTexture, iconPos + offset, coinFrame, glowColor * 0.4f, 0f,
                        coinFrame.Size() / 2f, iconScale * 1.15f, SpriteEffects.None, 0f);
                }
            }

            spriteBatch.Draw(coinTexture, iconPos, coinFrame, priceColor, 0f,
                coinFrame.Size() / 2f, iconScale, SpriteEffects.None, 0f);

            string amountText = amount.ToString();
            Vector2 textPos = position + new Vector2(22, 2);

            if (canAfford && hoverProgress > 0.01f) {
                Color textGlowColor = GetCoinGlowColor(coinType, hoverProgress) * (animation.UIAlpha * 0.5f * hoverProgress);
                for (int i = 0; i < 4; i++) {
                    float angle = MathHelper.TwoPi * i / 4f;
                    Vector2 offset = angle.ToRotationVector2() * (0.8f * scale);
                    Utils.DrawBorderString(spriteBatch, amountText, textPos + offset, textGlowColor * 0.4f, 0.7f * scale);
                }
            }

            Utils.DrawBorderString(spriteBatch, amountText, textPos, priceColor, scale);
        }

        private void DrawDataStreamEffect(SpriteBatch spriteBatch, Vector2 position, float hoverProgress, Texture2D pixel) {
            float dataShift = (float)Math.Sin(animation.DataStreamTimer * 1.5f + position.Y * 0.02f) * (1f + hoverProgress);
            Vector2 dataLinePos = position + new Vector2(dataShift + 90, DraedonShopInteraction.ItemSlotHeight / 2f + 5);
            Color dataColor = new Color(60, 160, 240) * (animation.UIAlpha * (0.15f + hoverProgress * 0.15f));
            spriteBatch.Draw(pixel, new Rectangle((int)dataLinePos.X, (int)dataLinePos.Y,
                (int)((PanelWidth - 150) * (0.7f + hoverProgress * 0.3f)), 1), dataColor);
        }

        private void DrawScrollHint(SpriteBatch spriteBatch, Vector2 panelPosition) {
            if (shopItems.Count <= DraedonShopInteraction.MaxVisibleItems) return;

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            int maxScroll = Math.Max(0, shopItems.Count - DraedonShopInteraction.MaxVisibleItems);

            //滚动条绘制
            interaction.DrawScrollBar(spriteBatch, panelPosition, animation.UIAlpha, animation.CircuitPulseTimer);

            //滚动提示文字
            if (interaction.ScrollOffset > 0 || interaction.ScrollOffset < maxScroll) {
                string hint = $"▲▼ [{interaction.ScrollOffset + 1}/{shopItems.Count}]";
                Vector2 hintPos = panelPosition + new Vector2(PanelWidth - 35, PanelHeight - 25);
                Utils.DrawBorderString(spriteBatch, hint, hintPos, new Color(100, 180, 230) * (animation.UIAlpha * 0.6f), 0.7f);
            }
        }

        private static void DrawGradientLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color startColor, Color endColor, float thickness) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 edge = end - start;
            float length = edge.Length();
            if (length < 1f) return;

            edge.Normalize();
            float rotation = (float)Math.Atan2(edge.Y, edge.X);
            int segments = Math.Max(1, (int)(length / 10f));

            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                Vector2 segPos = start + edge * (length * t);
                float segLength = length / segments;
                Color color = Color.Lerp(startColor, endColor, t);
                spriteBatch.Draw(pixel, segPos, null, color, rotation, new Vector2(0, 0.5f),
                    new Vector2(segLength, thickness), SpriteEffects.None, 0f);
            }
        }
    }
}
