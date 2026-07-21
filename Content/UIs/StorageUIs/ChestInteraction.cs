using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.UIs.StorageUIs
{
    /// <summary>箱子槽位交互，经 <see cref="IChestStorage"/></summary>
    internal class ChestInteraction
    {
        private readonly Player player;
        private readonly IChestStorage storage;

        private const int SlotSize = 32;
        private const int SlotPadding = 4;

        public int HoveredSlot { get; private set; } = -1;

        public bool IsCloseButtonHovered { get; private set; } = false;
        public const int CloseButtonSize = 32;

        private int soundCooldown = 0;
        private const int SoundCooldownMax = 15;
        private int lastQuickTransferSlot = -1;

        //右键拖动已访问槽，防重复
        private bool isRightDragging;
        private readonly HashSet<int> rightDragVisitedSlots = new();

        public ChestInteraction(Player player, IChestStorage storage) {
            this.player = player;
            this.storage = storage;
        }


        public bool UpdateCloseButton(Point mousePoint, Vector2 panelPosition, int panelWidth, bool mouseLeftRelease) {
            Rectangle buttonRect = new Rectangle(
                (int)(panelPosition.X + panelWidth - CloseButtonSize - 10),
                (int)(panelPosition.Y + 10),
                CloseButtonSize,
                CloseButtonSize
            );

            IsCloseButtonHovered = buttonRect.Contains(mousePoint);
            return IsCloseButtonHovered && mouseLeftRelease;
        }


        public void UpdateSlotInteraction(Point mousePoint, Vector2 storageStartPos,
            bool leftPressed, bool leftHeld, bool rightPressed, bool rightHeld) {
            if (soundCooldown > 0) {
                soundCooldown--;
            }

            HoveredSlot = -1;

            int slotsPerRow = storage.SlotsPerRow;
            int slotRows = storage.SlotRows;

            for (int row = 0; row < slotRows; row++) {
                for (int col = 0; col < slotsPerRow; col++) {
                    int index = row * slotsPerRow + col;
                    Rectangle slotRect = new Rectangle(
                        (int)(storageStartPos.X + col * (SlotSize + SlotPadding)),
                        (int)(storageStartPos.Y + row * (SlotSize + SlotPadding)),
                        SlotSize,
                        SlotSize
                    );

                    if (slotRect.Contains(mousePoint)) {
                        HoveredSlot = index;
                        break;
                    }
                }
                if (HoveredSlot != -1) break;
            }

            if (HoveredSlot == -1) {
                lastQuickTransferSlot = -1;
                return;
            }

            Item slotItem = storage.GetItem(HoveredSlot);
            if (slotItem != null && slotItem.type > ItemID.None && slotItem.stack > 0) {
                Main.HoverItem = slotItem.Clone();
                Main.hoverItemName = slotItem.Name;
            }

            KeyboardState keyboard = Keyboard.GetState();
            bool shiftPressed = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);

            if (shiftPressed && leftPressed) {
                QuickTransferToInventory(HoveredSlot);
                return;
            }

            if (!shiftPressed) {
                lastQuickTransferSlot = -1;
            }

            if (leftPressed) {
                HandleLeftClick(slotItem);
            }

            //右键，pressed/held 互斥避首帧双触
            if (rightPressed) {
                HandleRightClick(slotItem);
                //拖动开始
                if (Main.mouseItem.type != ItemID.None) {
                    isRightDragging = true;
                    rightDragVisitedSlots.Clear();
                    rightDragVisitedSlots.Add(HoveredSlot);
                }
            }
            else if (rightHeld && isRightDragging) {
                //拖动中仅新槽放1
                HandleRightDrag(HoveredSlot);
            }

            //松右键结束拖动
            if (!rightHeld && !rightPressed) {
                if (isRightDragging) {
                    isRightDragging = false;
                    rightDragVisitedSlots.Clear();
                }
            }

            if (shiftPressed && Main.mouseItem.type == ItemID.None) {
                GatherSameItems(HoveredSlot);
            }
        }

        private void HandleLeftClick(Item slotItem) {
            if (Main.mouseItem.type == ItemID.None) {
                if (slotItem != null && slotItem.type > ItemID.None) {
                    Main.mouseItem = slotItem.Clone();
                    storage.SetItem(HoveredSlot, new Item());
                    PlaySound(SoundID.Grab);
                }
            }
            else {
                if (slotItem == null || slotItem.type == ItemID.None) {
                    storage.SetItem(HoveredSlot, Main.mouseItem.Clone());
                    Main.mouseItem.TurnToAir();
                    PlaySound(SoundID.Grab);
                }
                else if (slotItem.type == Main.mouseItem.type && slotItem.stack < slotItem.maxStack) {
                    int spaceLeft = slotItem.maxStack - slotItem.stack;
                    int amountToAdd = Math.Min(spaceLeft, Main.mouseItem.stack);

                    Item updated = slotItem.Clone();
                    updated.stack += amountToAdd;
                    Main.mouseItem.stack -= amountToAdd;

                    if (Main.mouseItem.stack <= 0) {
                        Main.mouseItem.TurnToAir();
                    }

                    storage.SetItem(HoveredSlot, updated);
                    PlaySound(SoundID.Grab);
                }
                else {
                    Item temp = slotItem.Clone();
                    storage.SetItem(HoveredSlot, Main.mouseItem.Clone());
                    Main.mouseItem = temp;
                    PlaySound(SoundID.Grab);
                }
            }
        }

        /// <summary>右键单击，空手拿半组，持物放1</summary>
        private void HandleRightClick(Item slotItem) {
            if (Main.mouseItem.type == ItemID.None) {
                //空手拿半
                if (slotItem != null && slotItem.type > ItemID.None && slotItem.stack > 0) {
                    int halfStack = (slotItem.stack + 1) / 2;
                    Main.mouseItem = slotItem.Clone();
                    Main.mouseItem.stack = halfStack;

                    int remaining = slotItem.stack - halfStack;
                    if (remaining <= 0) {
                        storage.SetItem(HoveredSlot, new Item());
                    }
                    else {
                        Item leftover = slotItem.Clone();
                        leftover.stack = remaining;
                        storage.SetItem(HoveredSlot, leftover);
                    }

                    PlaySound(SoundID.Grab, 0.1f);
                }
            }
            else {
                //持物放1
                PlaceOneItem(HoveredSlot);
            }
        }

        /// <summary>右键拖动，新槽各放1</summary>
        private void HandleRightDrag(int slot) {
            if (Main.mouseItem.type == ItemID.None || Main.mouseItem.stack <= 0) {
                isRightDragging = false;
                rightDragVisitedSlots.Clear();
                return;
            }

            if (rightDragVisitedSlots.Contains(slot)) return;

            if (PlaceOneItem(slot)) {
                rightDragVisitedSlots.Add(slot);
            }
        }

        /// <summary>向槽放入1个光标物</summary>
        private bool PlaceOneItem(int slot) {
            if (Main.mouseItem.type == ItemID.None || Main.mouseItem.stack <= 0) return false;

            Item slotItem = storage.GetItem(slot);

            if (slotItem == null || slotItem.type == ItemID.None || slotItem.IsAir) {
                Item newItem = Main.mouseItem.Clone();
                newItem.stack = 1;
                storage.SetItem(slot, newItem);

                Main.mouseItem.stack--;
                if (Main.mouseItem.stack <= 0) Main.mouseItem.TurnToAir();

                PlaySound(SoundID.Grab, 0.1f);
                return true;
            }
            else if (slotItem.type == Main.mouseItem.type && slotItem.stack < slotItem.maxStack) {
                Item updated = slotItem.Clone();
                updated.stack++;
                storage.SetItem(slot, updated);

                Main.mouseItem.stack--;
                if (Main.mouseItem.stack <= 0) Main.mouseItem.TurnToAir();

                PlaySound(SoundID.Grab, 0.1f);
                return true;
            }

            return false;
        }

        private void QuickTransferToInventory(int slotIndex) {
            int totalSlots = storage.TotalSlots;
            if (slotIndex < 0 || slotIndex >= totalSlots) return;

            Item item = storage.GetItem(slotIndex);
            if (item == null || item.type <= ItemID.None || item.stack <= 0) return;

            Item leftover = player.GetItem(player.whoAmI, item.Clone(),
                GetItemSettings.InventoryUIToInventorySettings);

            bool success = false;
            bool partialSuccess = false;

            if (leftover == null || leftover.stack == 0) {
                storage.SetItem(slotIndex, new Item());
                success = true;
            }
            else if (leftover.stack < item.stack) {
                storage.SetItem(slotIndex, leftover);
                partialSuccess = true;
            }

            if ((success || partialSuccess) && CanPlaySound()) {
                PlayQuickTransferSound(success ? 0f : -0.2f);
            }

            if (success || partialSuccess) {
                lastQuickTransferSlot = slotIndex;
            }
        }

        private void GatherSameItems(int targetSlot) {
            Item targetItem = storage.GetItem(targetSlot);
            if (targetItem == null || targetItem.type == ItemID.None || targetItem.stack >= targetItem.maxStack) {
                return;
            }

            bool gathered = false;
            int totalSlots = storage.TotalSlots;
            Item accumulated = targetItem.Clone();

            for (int i = 0; i < totalSlots; i++) {
                if (i == targetSlot) continue;
                if (accumulated.stack >= accumulated.maxStack) break;

                Item otherItem = storage.GetItem(i);
                if (otherItem != null && otherItem.type == accumulated.type) {
                    int spaceLeft = accumulated.maxStack - accumulated.stack;
                    int amountToTransfer = Math.Min(spaceLeft, otherItem.stack);

                    accumulated.stack += amountToTransfer;

                    if (otherItem.stack - amountToTransfer <= 0) {
                        storage.SetItem(i, new Item());
                    }
                    else {
                        Item remaining = otherItem.Clone();
                        remaining.stack -= amountToTransfer;
                        storage.SetItem(i, remaining);
                    }

                    gathered = true;
                }
            }

            if (gathered) {
                storage.SetItem(targetSlot, accumulated);
                PlaySound(SoundID.Grab);
            }
        }

        private void PlaySound(SoundStyle sound, float pitch = 0f) {
            if (CanPlaySound()) {
                SoundEngine.PlaySound(sound with { Pitch = pitch });
                soundCooldown = SoundCooldownMax;
            }
        }

        private void PlayQuickTransferSound(float pitch = 0f) {
            SoundEngine.PlaySound(SoundID.Grab with { Pitch = pitch });
            soundCooldown = SoundCooldownMax;
        }

        private bool CanPlaySound() => soundCooldown <= 0;


        public void Reset() {
            HoveredSlot = -1;
            IsCloseButtonHovered = false;
            soundCooldown = 0;
            lastQuickTransferSlot = -1;
            isRightDragging = false;
            rightDragVisitedSlots.Clear();
        }
    }
}
