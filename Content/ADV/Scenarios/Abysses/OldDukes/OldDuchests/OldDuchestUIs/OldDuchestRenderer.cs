using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Items.OldDuchests.OldDuchestUIs
{
    /// <summary>
    /// ÀÏÏä×ÓUIäÖÈ¾Æ÷
    /// </summary>
    internal class OldDuchestRenderer
    {
        private readonly Player player;
        private readonly OldDuchestUI ui;
        private readonly OldDuchestAnimation animation;
        private readonly OldDuchestInteraction interaction;

        private const int PanelWidth = 760;
        private const int PanelHeight = 520;
        private const int SlotSize = 32;
        private const int SlotPadding = 4;
        private const int SlotsPerRow = 20;
        private const int SlotRows = 12;

        public OldDuchestRenderer(Player player, OldDuchestUI ui,
            OldDuchestAnimation animation, OldDuchestInteraction interaction) {
            this.player = player;
            this.ui = ui;
            this.animation = animation;
            this.interaction = interaction;
        }

        public Vector2 CalculatePanelPosition() {
            float slideOffset = (1f - animation.PanelSlideProgress) * 100f;
            return new Vector2(
                Main.screenWidth - PanelWidth,
                Main.screenHeight / 2 - PanelHeight / 2 + slideOffset
            );
        }

        public void Draw(SpriteBatch spriteBatch, Vector2 panelPosition, OldDuchestEffects effects) {
            if (animation.UIAlpha <= 0f) return;

            effects.DrawEffects(spriteBatch, animation.UIAlpha * 0.4f);

            DrawMainPanel(spriteBatch, panelPosition);
            DrawHeader(spriteBatch, panelPosition);
            DrawCloseButton(spriteBatch, panelPosition);
            DrawHeaderDivider(spriteBatch, panelPosition);
            DrawStorageSlots(spriteBatch, panelPosition);
            DrawFooter(spriteBatch, panelPosition);

            effects.DrawEffects(spriteBatch, animation.UIAlpha * 0.2f);
        }

        private void DrawMainPanel(SpriteBatch spriteBatch, Vector2 panelPosition) {
            Rectangle panelRect = new((int)panelPosition.X, (int)panelPosition.Y, PanelWidth, PanelHeight + 30);
            Texture2D pixel = VaultAsset.placeholder2.Value;

            spriteBatch.Draw(pixel, panelRect, new Rectangle(0, 0, 1, 1),
                new Color(20, 15, 10) * (animation.UIAlpha * 0.95f));

            Color borderColor = new Color(139, 87, 42) * animation.UIAlpha;
            DrawPanelBorder(spriteBatch, panelRect, borderColor, 2);
        }

        private void DrawPanelBorder(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
        }

        private void DrawHeader(SpriteBatch spriteBatch, Vector2 panelPosition) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            string title = OldDuchestUI.TitleText.Value;
            Vector2 titleSize = font.MeasureString(title);
            Vector2 titlePos = panelPosition + new Vector2(PanelWidth / 2 - titleSize.X / 2 * 1.1f, 15);

            Utils.DrawBorderString(spriteBatch, title, titlePos, Color.White * animation.UIAlpha, 1.1f);
        }

        private void DrawCloseButton(SpriteBatch spriteBatch, Vector2 panelPosition) {
            int buttonSize = OldDuchestInteraction.CloseButtonSize;
            Rectangle buttonRect = new((int)(panelPosition.X + PanelWidth - buttonSize - 10),
                (int)(panelPosition.Y + 10), buttonSize, buttonSize);

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Color buttonColor = interaction.IsCloseButtonHovered
                ? new Color(180, 80, 60) * animation.UIAlpha
                : new Color(120, 80, 40) * (animation.UIAlpha * 0.7f);

            spriteBatch.Draw(pixel, buttonRect, buttonColor * 0.5f);

            float crossSize = buttonSize * 0.5f;
            Vector2 center = buttonRect.Center.ToVector2();
            DrawLine(spriteBatch, center - new Vector2(crossSize / 2, crossSize / 2),
                center + new Vector2(crossSize / 2, crossSize / 2), buttonColor, 2f);
            DrawLine(spriteBatch, center - new Vector2(crossSize / 2, -crossSize / 2),
                center + new Vector2(crossSize / 2, -crossSize / 2), buttonColor, 2f);
        }

        private void DrawLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color, float thickness) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 edge = end - start;
            float length = edge.Length();
            if (length < 1f) return;
            spriteBatch.Draw(pixel, start, null, color, edge.ToRotation(),
                Vector2.Zero, new Vector2(length, thickness), SpriteEffects.None, 0);
        }

        private void DrawHeaderDivider(SpriteBatch spriteBatch, Vector2 panelPosition) {
            Vector2 divStart = panelPosition + new Vector2(20, 55);
            Vector2 divEnd = divStart + new Vector2(PanelWidth - 40, 0);
            DrawLine(spriteBatch, divStart, divEnd, new Color(139, 87, 42) * animation.UIAlpha, 1.5f);
        }

        private void DrawStorageSlots(SpriteBatch spriteBatch, Vector2 panelPosition) {
            Vector2 storageStartPos = panelPosition + new Vector2(20, 90);
            DynamicSpriteFont font = FontAssets.MouseText.Value;

            for (int row = 0; row < SlotRows; row++) {
                for (int col = 0; col < SlotsPerRow; col++) {
                    int index = row * SlotsPerRow + col;
                    Vector2 slotPos = storageStartPos + new Vector2(
                        col * (SlotSize + SlotPadding),
                        row * (SlotSize + SlotPadding)
                    );

                    bool isHovered = interaction.HoveredSlot == index;
                    float hoverProgress = animation.SlotHoverProgress[index];

                    DrawStorageSlot(spriteBatch, slotPos, index, isHovered, hoverProgress, font);
                }
            }
        }

        private void DrawStorageSlot(SpriteBatch spriteBatch, Vector2 position, int index,
            bool isHovered, float hoverProgress, DynamicSpriteFont font) {
            Rectangle slotRect = new((int)position.X, (int)position.Y, SlotSize, SlotSize);
            Texture2D pixel = VaultAsset.placeholder2.Value;

            Color bgColor = new Color(40, 30, 20) * (animation.UIAlpha * 0.8f);
            if (isHovered) {
                bgColor = Color.Lerp(bgColor, new Color(80, 60, 40), hoverProgress * 0.6f);
            }
            spriteBatch.Draw(pixel, slotRect, bgColor);

            Color borderColor = new Color(139, 87, 42) * (animation.UIAlpha * 0.7f);
            if (isHovered) {
                borderColor = Color.Lerp(borderColor, new Color(200, 150, 100), hoverProgress);
            }
            DrawPanelBorder(spriteBatch, slotRect, borderColor, 1);

            Item item = ui.GetItem(index);
            if (item != null && item.type > ItemID.None && item.stack > 0) {
                Main.instance.LoadItem(item.type);
                float scale = SlotSize * 0.9f / 32f;
                Vector2 itemPos = position + new Vector2(SlotSize / 2);

                if (isHovered) {
                    scale *= 1f + hoverProgress * 0.15f;
                }

                VaultUtils.SimpleDrawItem(spriteBatch, item.type, itemPos, itemWidth: 32, size: scale);

                if (item.stack > 1) {
                    string stackText = item.stack.ToString();
                    Vector2 stackSize = font.MeasureString(stackText) * 0.7f;
                    Vector2 stackPos = position + new Vector2(SlotSize - stackSize.X - 2, SlotSize - stackSize.Y);
                    Utils.DrawBorderString(spriteBatch, stackText, stackPos, Color.White * animation.UIAlpha, 0.7f);
                }
            }
        }

        private void DrawFooter(SpriteBatch spriteBatch, Vector2 panelPosition) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            int usedSlots = 0;
            for (int i = 0; i < SlotsPerRow * SlotRows; i++) {
                Item item = ui.GetItem(i);
                if (item != null && !item.IsAir) {
                    usedSlots++;
                }
            }

            string infoText = $"{OldDuchestUI.StorageText.Value}: {usedSlots}/{SlotsPerRow * SlotRows}";
            Vector2 infoSize = font.MeasureString(infoText) * 0.8f;
            Vector2 infoPos = panelPosition + new Vector2(PanelWidth / 2 - infoSize.X / 2, PanelHeight);

            Utils.DrawBorderString(spriteBatch, infoText, infoPos,
                new Color(200, 150, 100) * animation.UIAlpha, 0.8f);
        }
    }
}
