using CalamityOverhaul.Content.Scenarios.OldDuke.Quest;
using CalamityOverhaul.OtherMods.ImproveGame;
using InnoVault.UIHandles;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldDuke.OldDukeShops
{
    /// <summary>老公爵商店交互逻辑</summary>
    internal class OldDukeShopInteraction
    {
        private readonly Player player;
        private readonly List<OldDukeShopItem> shopItems;
        private readonly OldDukeShopAnimation animation;

        public int SelectedIndex { get; set; } = -1;
        public int HoveredIndex { get; private set; } = -1;
        public int ScrollOffset { get; set; }

        private int holdingPurchaseIndex = -1;
        private int holdingPurchaseTimer;
        private int purchaseCooldown = InitialPurchaseCooldown;
        public const int HoldThreshold = 14;
        private const int InitialPurchaseCooldown = 18;
        private const int MinPurchaseCooldown = 2;
        public int ConsecutivePurchaseCount { get; private set; }

        public const int MaxVisibleItems = 7;
        public const int ItemSlotHeight = 78;

        private readonly OldDukeScrollBar scrollBar = new();

        public bool IsCloseButtonHovered { get; private set; }
        public const int CloseButtonSize = 32;

        public int HoldingPurchaseIndex => holdingPurchaseIndex;
        public int HoldingPurchaseTimer => holdingPurchaseTimer;
        public bool IsScrollBarDragging => scrollBar.IsDragging;

        public OldDukeShopInteraction(Player player, List<OldDukeShopItem> shopItems, OldDukeShopAnimation animation) {
            this.player = player;
            this.shopItems = shopItems;
            this.animation = animation;
        }

        public void HandleScroll() {
            if (scrollBar.IsDragging) {
                return;
            }

            int scrollDelta = PlayerInput.ScrollWheelDeltaForUI;
            if (scrollDelta == 0) {
                return;
            }

            int maxScroll = Math.Max(0, shopItems.Count - MaxVisibleItems);
            int steps = Math.Max(1, Math.Abs(scrollDelta) / 120);
            int oldOffset = ScrollOffset;
            ScrollOffset = Math.Clamp(ScrollOffset - Math.Sign(scrollDelta) * steps, 0, maxScroll);
            if (oldOffset != ScrollOffset) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.28f, Pitch = -0.35f + ScrollOffset * 0.01f });
            }
        }

        public void UpdateScrollBar(Vector2 panelPosition, Point mousePosition,
            bool mouseLeftDown, bool mouseLeftRelease) {
            if (shopItems.Count <= MaxVisibleItems) {
                return;
            }

            int barHeight = MaxVisibleItems * ItemSlotHeight - 20;
            int maxScroll = Math.Max(0, shopItems.Count - MaxVisibleItems);

            scrollBar.Update(panelPosition, barHeight, ScrollOffset, maxScroll,
                shopItems.Count, MaxVisibleItems, mousePosition, mouseLeftDown,
                mouseLeftRelease, out int newScrollOffset);

            if (newScrollOffset != ScrollOffset) {
                ScrollOffset = newScrollOffset;
            }
        }

        public void DrawScrollBar(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch,
            Vector2 panelPosition, float uiAlpha, float sulfurPulseTimer) {
            if (shopItems.Count <= MaxVisibleItems) {
                return;
            }

            int barHeight = MaxVisibleItems * ItemSlotHeight - 20;
            int maxScroll = Math.Max(0, shopItems.Count - MaxVisibleItems);

            scrollBar.Draw(spriteBatch, panelPosition, barHeight, ScrollOffset, maxScroll,
                shopItems.Count, MaxVisibleItems, uiAlpha, sulfurPulseTimer);
        }

        public bool UpdateCloseButton(Point mousePoint, Vector2 panelPosition, bool mouseLeftRelease) {
            Rectangle closeButtonRect = new(
                (int)(panelPosition.X + 580 - CloseButtonSize - 15),
                (int)(panelPosition.Y + 15),
                CloseButtonSize,
                CloseButtonSize
            );

            bool wasHovered = IsCloseButtonHovered;
            IsCloseButtonHovered = closeButtonRect.Contains(mousePoint);

            if (IsCloseButtonHovered && !wasHovered) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.22f, Pitch = 0.35f });
            }

            if (IsCloseButtonHovered && mouseLeftRelease) {
                SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.3f });
                return true;
            }

            return false;
        }

        public void UpdateItemSelection(Point mousePoint, Vector2 itemListPos, int panelWidth, KeyPressState leftKeyState) {
            int itemListY = (int)itemListPos.Y;
            int itemListX = (int)itemListPos.X;
            int oldHoveredIndex = HoveredIndex;
            HoveredIndex = -1;

            for (int i = 0; i < Math.Min(MaxVisibleItems, shopItems.Count - ScrollOffset); i++) {
                int index = i + ScrollOffset;
                Rectangle itemRect = new(
                    itemListX,
                    itemListY + i * ItemSlotHeight,
                    panelWidth - 65,
                    ItemSlotHeight - 6
                );

                if (!itemRect.Contains(mousePoint)) {
                    continue;
                }

                HoveredIndex = index;

                if (oldHoveredIndex != HoveredIndex) {
                    SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.18f, Pitch = 0.15f + i * 0.03f });
                }

                HandlePurchaseInput(index, i, leftKeyState);
                return;
            }

            if (leftKeyState == KeyPressState.None || leftKeyState == KeyPressState.Released) {
                ResetHoldingState();
            }
        }

        private void HandlePurchaseInput(int index, int visibleSlotIndex, KeyPressState leftKeyState) {
            switch (leftKeyState) {
                case KeyPressState.Pressed:
                    SelectedIndex = index;
                    holdingPurchaseIndex = index;
                    holdingPurchaseTimer = 0;
                    ConsecutivePurchaseCount = 0;
                    purchaseCooldown = InitialPurchaseCooldown;
                    if (!TryPurchaseItem(index)) {
                        animation.TriggerFailFlash(visibleSlotIndex);
                    }
                    break;

                case KeyPressState.Held when holdingPurchaseIndex == index:
                    holdingPurchaseTimer++;
                    if (holdingPurchaseTimer >= HoldThreshold && holdingPurchaseTimer % purchaseCooldown == 0) {
                        if (TryPurchaseItem(index)) {
                            ConsecutivePurchaseCount++;
                            if (ConsecutivePurchaseCount % 5 == 0) {
                                purchaseCooldown = Math.Max(MinPurchaseCooldown, (int)(purchaseCooldown * 0.78f));
                            }
                        }
                        else {
                            animation.TriggerFailFlash(visibleSlotIndex);
                            ResetHoldingState();
                        }
                    }
                    break;

                case KeyPressState.Held:
                    holdingPurchaseIndex = index;
                    holdingPurchaseTimer = 0;
                    ConsecutivePurchaseCount = 0;
                    purchaseCooldown = InitialPurchaseCooldown;
                    break;

                case KeyPressState.Released:
                case KeyPressState.None:
                    if (holdingPurchaseIndex == index) {
                        ResetHoldingState();
                    }
                    break;
            }
        }

        private bool TryPurchaseItem(int index) {
            if (index < 0 || index >= shopItems.Count) {
                return false;
            }

            OldDukeShopItem shopItem = shopItems[index];
            int oceanFragmentCount = FindFragmentQuestEntry.GetFragmentCount();

            if (oceanFragmentCount < shopItem.price) {
                SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.55f, Volume = 0.65f });
                return false;
            }

            int remainingToConsume = shopItem.price;
            var bigBags = player.GetBigBagItems() ?? [];
            Item[][] inventories = [
                player.inventory,
                player.bank.item,
                player.bank2.item,
                player.bank3.item,
                player.bank4.item,
                [.. bigBags],
            ];

            foreach (Item[] inventory in inventories) {
                if (remainingToConsume <= 0) {
                    break;
                }
                if (inventory == null) {
                    continue;
                }

                for (int i = 0; i < inventory.Length && remainingToConsume > 0; i++) {
                    Item invItem = inventory[i];
                    if (invItem.type != ModContent.ItemType<Oceanfragments>()) {
                        continue;
                    }

                    int consumeAmount = Math.Min(invItem.stack, remainingToConsume);
                    invItem.stack -= consumeAmount;
                    remainingToConsume -= consumeAmount;

                    if (invItem.stack <= 0) {
                        invItem.TurnToAir();
                    }
                }
            }

            player.QuickSpawnItem(player.GetSource_OpenItem(shopItem.itemType), shopItem.itemType, shopItem.stack);
            SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.72f, Pitch = -0.05f - ConsecutivePurchaseCount * 0.02f });
            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.48f, Pitch = 0.15f + ConsecutivePurchaseCount * 0.03f });
            return true;
        }

        public void ResetHoldingState() {
            holdingPurchaseIndex = -1;
            holdingPurchaseTimer = 0;
            ConsecutivePurchaseCount = 0;
            purchaseCooldown = InitialPurchaseCooldown;
        }

        public void Reset() {
            HoveredIndex = -1;
            SelectedIndex = -1;
            ScrollOffset = 0;
            IsCloseButtonHovered = false;
            ResetHoldingState();
            scrollBar.Reset();
        }
    }
}
