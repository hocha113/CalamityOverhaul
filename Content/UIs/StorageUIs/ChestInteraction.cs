using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.UIs.StorageUIs
{
    /// <summary>
    /// 通用箱子UI交互逻辑，所有槽位的鼠标交互
    /// 通过 <see cref="IChestStorage"/> 接口统一不同存储模式
    /// </summary>
    internal class ChestInteraction
    {
        private readonly Player player;
        private readonly IChestStorage storage;

        private const int SlotSize = 32;
        private const int SlotPadding = 4;

        //交互状态
        public int HoveredSlot { get; private set; } = -1;

        //关闭按钮
        public bool IsCloseButtonHovered { get; private set; } = false;
        public const int CloseButtonSize = 32;

        //音效冷却
        private int soundCooldown = 0;
        private const int SoundCooldownMax = 15;
        private int lastQuickTransferSlot = -1;

        //右键拖动状态 — 记录当前拖动操作中已分配过的槽位，避免重复
        private bool isRightDragging;
        private readonly HashSet<int> rightDragVisitedSlots = new();

        public ChestInteraction(Player player, IChestStorage storage) {
            this.player = player;
            this.storage = storage;
        }

        /// <summary>
        /// 更新关闭按钮悬停
        /// </summary>
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

        /// <summary>
        /// 更新槽位交互
        /// </summary>
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

            //左键单击
            if (leftPressed) {
                HandleLeftClick(slotItem);
            }

            //右键交互 — pressed 和 held 互斥处理，避免首帧双重触发
            if (rightPressed) {
                HandleRightClick(slotItem);
                //标记拖动开始，记录首个槽位
                if (Main.mouseItem.type != ItemID.None) {
                    isRightDragging = true;
                    rightDragVisitedSlots.Clear();
                    rightDragVisitedSlots.Add(HoveredSlot);
                }
            }
            else if (rightHeld && isRightDragging) {
                //拖动中 — 仅对新槽位放入1个
                HandleRightDrag(HoveredSlot);
            }

            //松开右键时结束拖动
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
                //拿起整组
                if (slotItem != null && slotItem.type > ItemID.None) {
                    Main.mouseItem = slotItem.Clone();
                    storage.SetItem(HoveredSlot, new Item());
                    PlaySound(SoundID.Grab);
                }
            }
            else {
                if (slotItem == null || slotItem.type == ItemID.None) {
                    //放入空槽
                    storage.SetItem(HoveredSlot, Main.mouseItem.Clone());
                    Main.mouseItem.TurnToAir();
                    PlaySound(SoundID.Grab);
                }
                else if (slotItem.type == Main.mouseItem.type && slotItem.stack < slotItem.maxStack) {
                    //同类堆叠
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
                    //异类交换
                    Item temp = slotItem.Clone();
                    storage.SetItem(HoveredSlot, Main.mouseItem.Clone());
                    Main.mouseItem = temp;
                    PlaySound(SoundID.Grab);
                }
            }
        }

        /// <summary>
        /// 右键单击：空手拿半组，持物放1个
        /// </summary>
        private void HandleRightClick(Item slotItem) {
            if (Main.mouseItem.type == ItemID.None) {
                //空手 → 拿起一半
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
                //持物 → 放入1个
                PlaceOneItem(HoveredSlot);
            }
        }

        /// <summary>
        /// 右键拖动：对每个新经过的槽位放入1个物品
        /// </summary>
        private void HandleRightDrag(int slot) {
            if (Main.mouseItem.type == ItemID.None || Main.mouseItem.stack <= 0) {
                isRightDragging = false;
                rightDragVisitedSlots.Clear();
                return;
            }

            //已经访问过的槽位不再重复放入
            if (rightDragVisitedSlots.Contains(slot)) return;

            if (PlaceOneItem(slot)) {
                rightDragVisitedSlots.Add(slot);
            }
        }

        /// <summary>
        /// 向指定槽位放入1个光标物品，返回是否成功
        /// </summary>
        private bool PlaceOneItem(int slot) {
            if (Main.mouseItem.type == ItemID.None || Main.mouseItem.stack <= 0) return false;

            Item slotItem = storage.GetItem(slot);

            if (slotItem == null || slotItem.type == ItemID.None || slotItem.IsAir) {
                //空槽 → 放入1个
                Item newItem = Main.mouseItem.Clone();
                newItem.stack = 1;
                storage.SetItem(slot, newItem);

                Main.mouseItem.stack--;
                if (Main.mouseItem.stack <= 0) Main.mouseItem.TurnToAir();

                PlaySound(SoundID.Grab, 0.1f);
                return true;
            }
            else if (slotItem.type == Main.mouseItem.type && slotItem.stack < slotItem.maxStack) {
                //同类 → 堆叠1个
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

        /// <summary>
        /// 重置交互状态
        /// </summary>
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
