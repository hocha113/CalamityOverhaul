using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.UIs.StorageUIs
{
    /// <summary>
    /// 通用箱子UI渲染器基类，提供通用的槽位绘制和布局计算
    /// 子类覆写主题相关方法以不同的视觉风格
    /// </summary>
    internal abstract class BaseChestRenderer
    {
        protected readonly Player player;
        protected readonly IChestStorage storage;
        protected readonly BaseChestAnimation animation;
        protected readonly ChestInteraction interaction;

        protected const int SlotSize = 32;
        protected const int SlotPadding = 4;

        protected abstract int PanelWidth { get; }
        protected abstract int PanelHeight { get; }
        protected abstract int StorageStartX { get; }
        protected abstract int StorageStartY { get; }

        protected BaseChestRenderer(Player player, IChestStorage storage,
            BaseChestAnimation animation, ChestInteraction interaction) {
            this.player = player;
            this.storage = storage;
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

        public void Draw(SpriteBatch spriteBatch, Vector2 panelPosition, IChestEffects effects) {
            if (animation.UIAlpha <= 0f) return;

            DrawBackgroundEffects(spriteBatch, effects);
            DrawMainPanel(spriteBatch, panelPosition);
            DrawHeader(spriteBatch, panelPosition);
            DrawCloseButton(spriteBatch, panelPosition);
            DrawHeaderDivider(spriteBatch, panelPosition);
            DrawStorageSlots(spriteBatch, panelPosition);
            DrawFooter(spriteBatch, panelPosition);
            DrawForegroundEffects(spriteBatch, effects);
        }

        //--- 主题相关的抽象/虚方法 ---

        /// <summary>绘制背景层特效</summary>
        protected virtual void DrawBackgroundEffects(SpriteBatch spriteBatch, IChestEffects effects) {
            effects.DrawEffects(spriteBatch, animation.UIAlpha * 0.4f);
        }

        /// <summary>绘制前景层特效</summary>
        protected virtual void DrawForegroundEffects(SpriteBatch spriteBatch, IChestEffects effects) {
            effects.DrawEffects(spriteBatch, animation.UIAlpha * 0.2f);
        }

        /// <summary>绘制主面板背景和边框</summary>
        protected abstract void DrawMainPanel(SpriteBatch spriteBatch, Vector2 panelPosition);

        /// <summary>绘制标题</summary>
        protected abstract void DrawHeader(SpriteBatch spriteBatch, Vector2 panelPosition);

        /// <summary>绘制分隔线</summary>
        protected abstract void DrawHeaderDivider(SpriteBatch spriteBatch, Vector2 panelPosition);

        protected abstract Color GetFooterColor();
        protected abstract string GetTitleText();
        protected abstract string GetStorageText();
        protected abstract Color GetSlotBackgroundColor();
        protected abstract Color GetSlotHoverBackgroundColor();
        protected abstract Color GetSlotBorderColor();
        protected abstract Color GetSlotHoverBorderColor();
        protected abstract Color GetCloseButtonHoverColor();
        protected abstract Color GetCloseButtonNormalColor();

        //--- 通用绘制方法 ---

        protected void DrawCloseButton(SpriteBatch spriteBatch, Vector2 panelPosition) {
            int buttonSize = ChestInteraction.CloseButtonSize;
            Rectangle buttonRect = new((int)(panelPosition.X + PanelWidth - buttonSize - 10),
                (int)(panelPosition.Y + 10), buttonSize, buttonSize);

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Color buttonColor = interaction.IsCloseButtonHovered
                ? GetCloseButtonHoverColor() * animation.UIAlpha
                : GetCloseButtonNormalColor() * (animation.UIAlpha * 0.7f);

            spriteBatch.Draw(pixel, buttonRect, buttonColor * 0.5f);

            float crossSize = buttonSize * 0.5f;
            Vector2 center = buttonRect.Center.ToVector2();
            DrawLine(spriteBatch, center - new Vector2(crossSize / 2, crossSize / 2),
                center + new Vector2(crossSize / 2, crossSize / 2), buttonColor, 2f);
            DrawLine(spriteBatch, center - new Vector2(crossSize / 2, -crossSize / 2),
                center + new Vector2(crossSize / 2, -crossSize / 2), buttonColor, 2f);
        }

        protected void DrawStorageSlots(SpriteBatch spriteBatch, Vector2 panelPosition) {
            Vector2 storageStartPos = panelPosition + new Vector2(StorageStartX, StorageStartY);
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            int slotsPerRow = storage.SlotsPerRow;
            int slotRows = storage.SlotRows;

            for (int row = 0; row < slotRows; row++) {
                for (int col = 0; col < slotsPerRow; col++) {
                    int index = row * slotsPerRow + col;
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

        protected virtual void DrawStorageSlot(SpriteBatch spriteBatch, Vector2 position, int index,
            bool isHovered, float hoverProgress, DynamicSpriteFont font) {
            Rectangle slotRect = new((int)position.X, (int)position.Y, SlotSize, SlotSize);
            Texture2D pixel = VaultAsset.placeholder2.Value;

            Color bgColor = GetSlotBackgroundColor() * (animation.UIAlpha * 0.8f);
            if (isHovered) {
                bgColor = Color.Lerp(bgColor, GetSlotHoverBackgroundColor(), hoverProgress * 0.6f);
            }
            spriteBatch.Draw(pixel, slotRect, bgColor);

            Color borderColor = GetSlotBorderColor() * (animation.UIAlpha * 0.7f);
            if (isHovered) {
                borderColor = Color.Lerp(borderColor, GetSlotHoverBorderColor(), hoverProgress);
            }
            DrawSlotBorder(spriteBatch, slotRect, borderColor);

            Item item = storage.GetItem(index);
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

        protected void DrawFooter(SpriteBatch spriteBatch, Vector2 panelPosition) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            int usedSlots = storage.UsedSlotCount;
            int totalSlots = storage.TotalSlots;
            string infoText = $"{GetStorageText()}: {usedSlots}/{totalSlots}";
            Vector2 infoSize = font.MeasureString(infoText) * 0.8f;
            Vector2 infoPos = panelPosition + new Vector2(PanelWidth / 2 - infoSize.X / 2, PanelHeight - 30);

            Utils.DrawBorderString(spriteBatch, infoText, infoPos,
                GetFooterColor() * animation.UIAlpha, 0.8f);
        }

        //--- 通用绘制工具 ---

        protected static void DrawLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color, float thickness) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 edge = end - start;
            float length = edge.Length();
            if (length < 1f) return;
            spriteBatch.Draw(pixel, start, null, color, edge.ToRotation(),
                Vector2.Zero, new Vector2(length, thickness), SpriteEffects.None, 0);
        }

        protected static void DrawPanelBorder(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
        }

        protected static void DrawSlotBorder(SpriteBatch spriteBatch, Rectangle rect, Color color) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), color);
        }
    }
}
