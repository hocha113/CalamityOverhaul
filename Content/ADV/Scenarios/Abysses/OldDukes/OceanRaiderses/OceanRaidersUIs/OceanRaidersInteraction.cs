using Microsoft.Xna.Framework.Input;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.OceanRaiderses.OceanRaidersUIs
{
    /// <summary>
    /// 海洋吞噬者UI交互逻辑处理器
    /// </summary>
    internal class OceanRaidersInteraction
    {
        private readonly Player player;
        private OceanRaidersTP machine;

        //槽位常量
        private const int SlotsPerRow = 20;
        private const int SlotRows = 18;
        private const int TotalSlots = SlotsPerRow * SlotRows;
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

        public OceanRaidersInteraction(Player player, OceanRaidersTP machine) {
            this.player = player;
            this.machine = machine;
        }

        public void UpdateMachine(OceanRaidersTP newMachine) {
            machine = newMachine;
            Reset();
        }

        /// <summary>
        /// 更新关闭按钮悬停
        /// </summary>
        public bool UpdateCloseButton(Point mousePoint, Vector2 panelPosition, bool mouseLeftRelease) {
            Rectangle buttonRect = new Rectangle(
                (int)(panelPosition.X + 760 - CloseButtonSize - 10),
                (int)(panelPosition.Y + 10),
                CloseButtonSize,
                CloseButtonSize
            );

            IsCloseButtonHovered = buttonRect.Contains(mousePoint);

            if (IsCloseButtonHovered && mouseLeftRelease) {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 更新槽位交互
        /// </summary>
        public void UpdateSlotInteraction(Point mousePoint, Vector2 storageStartPos,
            bool leftPressed, bool leftHeld, bool rightPressed, bool rightHeld) {
            //更新音效冷却
            if (soundCooldown > 0) {
                soundCooldown--;
            }

            HoveredSlot = -1;

            //计算鼠标所在的槽位
            for (int row = 0; row < SlotRows; row++) {
                for (int col = 0; col < SlotsPerRow; col++) {
                    int index = row * SlotsPerRow + col;
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

            //获取当前槽位的物品
            Item slotItem = GetItem(HoveredSlot);
            if (slotItem != null && slotItem.type > ItemID.None && slotItem.stack > 0) {
                Main.HoverItem = slotItem;
                Main.hoverItemName = slotItem.Name;
            }

            //检测Shift键状态
            KeyboardState keyboard = Keyboard.GetState();
            bool shiftPressed = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);

            //Shift+左键：转移到背包
            if (shiftPressed && leftPressed) {
                QuickTransferToInventory(HoveredSlot);
                return;
            }

            //重置快速转移槽位记录
            if (!shiftPressed) {
                lastQuickTransferSlot = -1;
            }

            //处理左键交互
            if (leftPressed) {
                HandleLeftClick(slotItem);
            }

            //右键单次交互
            if (rightPressed) {
                HandleRightClick(slotItem);
            }

            //右键拖拽放置
            if (rightHeld) {
                HandleDragPlace(slotItem);
            }

            //Shift+鼠标悬停+手持空物品时：聚集相同物品
            if (shiftPressed && Main.mouseItem.type == ItemID.None) {
                GatherSameItems(HoveredSlot);
            }
        }

        /// <summary>
        /// 获取槽位物品
        /// </summary>
        private Item GetItem(int slotIndex) {
            if (slotIndex < 0 || slotIndex >= machine.storedItems.Count) {
                return new Item();
            }
            return machine.storedItems[slotIndex];
        }

        /// <summary>
        /// 设置槽位物品
        /// </summary>
        private void SetItem(int slotIndex, Item item) {
            //确保列表足够长
            while (machine.storedItems.Count <= slotIndex) {
                machine.storedItems.Add(new Item());
            }

            machine.storedItems[slotIndex] = item;

            //清理尾部空槽位
            CleanEmptySlots();
            machine.SendData();
        }

        /// <summary>
        /// 处理左键交互
        /// </summary>
        private void HandleLeftClick(Item slotItem) {
            if (Main.mouseItem.type == ItemID.None) {
                //手上没有物品，拾取槽位物品
                if (slotItem != null && slotItem.type > ItemID.None) {
                    Main.mouseItem = slotItem.Clone();
                    SetItem(HoveredSlot, new Item());
                    PlaySound(SoundID.Grab);
                }
            }
            else {
                //手上有物品
                if (slotItem == null || slotItem.type == ItemID.None) {
                    //槽位为空，放下手上的物品
                    SetItem(HoveredSlot, Main.mouseItem.Clone());
                    Main.mouseItem.TurnToAir();
                    PlaySound(SoundID.Grab);
                }
                else if (slotItem.type == Main.mouseItem.type && slotItem.stack < slotItem.maxStack) {
                    //相同物品，自动堆叠
                    int spaceLeft = slotItem.maxStack - slotItem.stack;
                    int amountToAdd = Math.Min(spaceLeft, Main.mouseItem.stack);

                    slotItem.stack += amountToAdd;
                    Main.mouseItem.stack -= amountToAdd;

                    if (Main.mouseItem.stack <= 0) {
                        Main.mouseItem.TurnToAir();
                    }

                    SetItem(HoveredSlot, slotItem);
                    PlaySound(SoundID.Grab);
                }
                else {
                    //不同物品，交换
                    Item temp = slotItem.Clone();
                    SetItem(HoveredSlot, Main.mouseItem.Clone());
                    Main.mouseItem = temp;
                    PlaySound(SoundID.Grab);
                }
            }
        }

        /// <summary>
        /// 右键单次交互
        /// </summary>
        private void HandleRightClick(Item slotItem) {
            if (Main.mouseItem.type == ItemID.None) {
                //手上没有物品，拿取一半
                if (slotItem != null && slotItem.type > ItemID.None) {
                    int halfStack = (slotItem.stack + 1) / 2;
                    Main.mouseItem = slotItem.Clone();
                    Main.mouseItem.stack = halfStack;
                    slotItem.stack -= halfStack;

                    if (slotItem.stack <= 0) {
                        SetItem(HoveredSlot, new Item());
                    }
                    else {
                        SetItem(HoveredSlot, slotItem);
                    }

                    PlaySound(SoundID.Grab, 0.1f);
                }
            }
            else {
                //手上有物品，放下一个
                if (slotItem == null || slotItem.type == ItemID.None) {
                    //槽位为空，放下一个
                    Item newItem = Main.mouseItem.Clone();
                    newItem.stack = 1;
                    SetItem(HoveredSlot, newItem);
                    Main.mouseItem.stack--;

                    if (Main.mouseItem.stack <= 0) {
                        Main.mouseItem.TurnToAir();
                    }

                    PlaySound(SoundID.Grab, 0.1f);
                }
                else if (slotItem.type == Main.mouseItem.type && slotItem.stack < slotItem.maxStack) {
                    //相同物品，放下一个
                    slotItem.stack++;
                    Main.mouseItem.stack--;

                    if (Main.mouseItem.stack <= 0) {
                        Main.mouseItem.TurnToAir();
                    }

                    SetItem(HoveredSlot, slotItem);
                    PlaySound(SoundID.Grab, 0.1f);
                }
            }
        }

        /// <summary>
        /// 右键拖拽放置处理
        /// </summary>
        private void HandleDragPlace(Item slotItem) {
            if (Main.mouseItem.type == ItemID.None) return;

            if (slotItem == null || slotItem.type == ItemID.None) {
                //槽位为空，放下一个
                Item newItem = Main.mouseItem.Clone();
                newItem.stack = 1;
                SetItem(HoveredSlot, newItem);
                Main.mouseItem.stack--;

                if (Main.mouseItem.stack <= 0) {
                    Main.mouseItem.TurnToAir();
                }
            }
            else if (slotItem.type == Main.mouseItem.type && slotItem.stack < slotItem.maxStack) {
                //相同物品，放下一个
                slotItem.stack++;
                Main.mouseItem.stack--;

                if (Main.mouseItem.stack <= 0) {
                    Main.mouseItem.TurnToAir();
                }

                SetItem(HoveredSlot, slotItem);
            }
        }

        /// <summary>
        /// 快速转移到背包
        /// </summary>
        private void QuickTransferToInventory(int slotIndex) {
            if (slotIndex < 0 || slotIndex >= TotalSlots) return;

            Item item = GetItem(slotIndex);
            if (item == null || item.type <= ItemID.None || item.stack <= 0) return;

            //尝试添加到玩家背包
            Item leftover = player.GetItem(player.whoAmI, item.Clone(),
                GetItemSettings.InventoryUIToInventorySettings);

            bool success = false;
            bool partialSuccess = false;

            if (leftover == null || leftover.stack == 0) {
                //完全添加成功
                SetItem(slotIndex, new Item());
                success = true;
            }
            else if (leftover.stack < item.stack) {
                //部分添加
                item.stack = leftover.stack;
                SetItem(slotIndex, item);
                partialSuccess = true;
            }

            //只在不与上次成功的槽位相同且音效冷却结束时才播放音效
            if ((success || partialSuccess) && CanPlaySound()) {
                if (success) {
                    PlayQuickTransferSound();
                }
                else if (partialSuccess) {
                    PlayQuickTransferSound(-0.2f);
                }
            }

            //记录本次快速转移的槽位
            if (success || partialSuccess) {
                lastQuickTransferSlot = slotIndex;
            }
        }

        /// <summary>
        /// 聚集相同物品
        /// </summary>
        private void GatherSameItems(int targetSlot) {
            Item targetItem = GetItem(targetSlot);
            if (targetItem == null || targetItem.type == ItemID.None || targetItem.stack >= targetItem.maxStack) {
                return;
            }

            bool gathered = false;

            //遍历所有槽位收集相同物品
            for (int i = 0; i < TotalSlots; i++) {
                if (i == targetSlot) continue;
                if (targetItem.stack >= targetItem.maxStack) break;

                Item otherItem = GetItem(i);
                if (otherItem != null && otherItem.type == targetItem.type) {
                    int spaceLeft = targetItem.maxStack - targetItem.stack;
                    int amountToTransfer = Math.Min(spaceLeft, otherItem.stack);

                    targetItem.stack += amountToTransfer;
                    otherItem.stack -= amountToTransfer;

                    if (otherItem.stack <= 0) {
                        SetItem(i, new Item());
                    }
                    else {
                        SetItem(i, otherItem);
                    }

                    gathered = true;
                }
            }

            if (gathered) {
                SetItem(targetSlot, targetItem);
                PlaySound(SoundID.Grab);
            }
        }

        /// <summary>
        /// 清理尾部空槽位
        /// </summary>
        private void CleanEmptySlots() {
            //从后往前清理空槽位
            for (int i = machine.storedItems.Count - 1; i >= 0; i--) {
                Item item = machine.storedItems[i];
                if (item == null || item.type <= ItemID.None || item.stack <= 0) {
                    machine.storedItems.RemoveAt(i);
                }
                else {
                    break; //遇到非空槽位就停止
                }
            }
        }

        /// <summary>
        /// 播放音效
        /// </summary>
        private void PlaySound(SoundStyle sound, float pitch = 0f) {
            if (CanPlaySound()) {
                SoundEngine.PlaySound(sound with { Pitch = pitch });
                soundCooldown = SoundCooldownMax;
            }
        }

        /// <summary>
        /// 播放快速转移音效
        /// </summary>
        private void PlayQuickTransferSound(float pitch = 0f) {
            SoundEngine.PlaySound(SoundID.Grab with { Pitch = pitch });
            soundCooldown = SoundCooldownMax;
        }

        /// <summary>
        /// 检查是否可以播放音效
        /// </summary>
        private bool CanPlaySound() {
            return soundCooldown <= 0;
        }

        /// <summary>
        /// 重置交互状态
        /// </summary>
        public void Reset() {
            HoveredSlot = -1;
            IsCloseButtonHovered = false;
            soundCooldown = 0;
            lastQuickTransferSlot = -1;
        }
    }
}
