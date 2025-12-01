using Microsoft.Xna.Framework.Input;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Items.OldDuchests.OldDuchestUIs
{
    /// <summary>
    /// 老箱子UI交互逻辑处理器
    /// </summary>
    internal class OldDuchestInteraction
    {
        private readonly Player player;
        private readonly OldDuchestUI ui;

        //槽位常量
        private const int SlotsPerRow = 20;
        private const int SlotRows = 12;
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

        public OldDuchestInteraction(Player player, OldDuchestUI ui) {
            this.player = player;
            this.ui = ui;
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
            Item slotItem = ui.GetItem(HoveredSlot);
            if (slotItem.Alives()) {
                Main.HoverItem = slotItem;
                Main.hoverItemName = slotItem.Name;
            }

            //检查Shift键状态
            KeyboardState keyboard = Keyboard.GetState();
            bool shiftPressed = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);

            //Shift+左键快速转移到背包
            if (shiftPressed && leftPressed) {
                QuickTransferToInventory(HoveredSlot);
                return;
            }

            //重置快速转移槽位记录
            if (!shiftPressed) {
                lastQuickTransferSlot = -1;
            }

            //左键点击交互
            if (leftPressed) {
                HandleLeftClick(slotItem);
            }

            //右键点击交互
            if (rightPressed) {
                HandleRightClick(slotItem);
            }

            //右键拖拽放置
            if (rightHeld) {
                HandleDragPlace(slotItem);
            }

            //Shift+鼠标悬停+无手持物品时，聚集相同物品
            if (shiftPressed && Main.mouseItem.type == ItemID.None) {
                GatherSameItems(HoveredSlot);
            }
        }

        /// <summary>
        /// 左键点击处理
        /// </summary>
        private void HandleLeftClick(Item slotItem) {
            if (Main.mouseItem.type == ItemID.None) {
                //手上没有物品，拿起槽位物品
                if (slotItem != null && slotItem.type > ItemID.None) {
                    Main.mouseItem = slotItem.Clone();
                    ui.SetItem(HoveredSlot, new Item());
                    PlaySound(SoundID.Grab);
                }
            }
            else {
                //手上有物品
                if (slotItem == null || slotItem.type == ItemID.None) {
                    //槽位为空，放下手上的物品
                    ui.SetItem(HoveredSlot, Main.mouseItem.Clone());
                    Main.mouseItem.TurnToAir();
                    PlaySound(SoundID.Grab);
                }
                else if (slotItem.type == Main.mouseItem.type && slotItem.stack < slotItem.maxStack) {
                    //相同物品，尝试堆叠
                    int spaceLeft = slotItem.maxStack - slotItem.stack;
                    int amountToAdd = Math.Min(spaceLeft, Main.mouseItem.stack);
                    
                    slotItem.stack += amountToAdd;
                    Main.mouseItem.stack -= amountToAdd;
                    
                    if (Main.mouseItem.stack <= 0) {
                        Main.mouseItem.TurnToAir();
                    }
                    
                    ui.SetItem(HoveredSlot, slotItem);
                    PlaySound(SoundID.Grab);
                }
                else {
                    //不同物品，交换
                    Item temp = slotItem.Clone();
                    ui.SetItem(HoveredSlot, Main.mouseItem.Clone());
                    Main.mouseItem = temp;
                    PlaySound(SoundID.Grab);
                }
            }
        }

        /// <summary>
        /// 右键点击处理
        /// </summary>
        private void HandleRightClick(Item slotItem) {
            if (Main.mouseItem.type == ItemID.None) {
                //手上没有物品，拿起一半
                if (slotItem != null && slotItem.type > ItemID.None) {
                    int halfStack = (slotItem.stack + 1) / 2;
                    Main.mouseItem = slotItem.Clone();
                    Main.mouseItem.stack = halfStack;
                    slotItem.stack -= halfStack;
                    
                    if (slotItem.stack <= 0) {
                        ui.SetItem(HoveredSlot, new Item());
                    }
                    else {
                        ui.SetItem(HoveredSlot, slotItem);
                    }
                    
                    PlaySound(SoundID.Grab, 0.1f);
                }
            }
            else {
                //手上有物品，放置一个
                if (slotItem == null || slotItem.type == ItemID.None) {
                    //槽位为空，放下一个
                    ui.SetItem(HoveredSlot, Main.mouseItem.Clone());
                    ui.GetItem(HoveredSlot).stack = 1;
                    Main.mouseItem.stack--;
                    
                    if (Main.mouseItem.stack <= 0) {
                        Main.mouseItem.TurnToAir();
                    }
                    
                    PlaySound(SoundID.Grab, 0.1f);
                }
                else if (slotItem.type == Main.mouseItem.type && slotItem.stack < slotItem.maxStack) {
                    //相同物品，添加一个
                    slotItem.stack++;
                    Main.mouseItem.stack--;
                    
                    if (Main.mouseItem.stack <= 0) {
                        Main.mouseItem.TurnToAir();
                    }
                    
                    ui.SetItem(HoveredSlot, slotItem);
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
                //槽位为空，放置一个
                ui.SetItem(HoveredSlot, Main.mouseItem.Clone());
                ui.GetItem(HoveredSlot).stack = 1;
                Main.mouseItem.stack--;
                
                if (Main.mouseItem.stack <= 0) {
                    Main.mouseItem.TurnToAir();
                }
            }
            else if (slotItem.type == Main.mouseItem.type && slotItem.stack < slotItem.maxStack) {
                //相同物品，添加一个
                slotItem.stack++;
                Main.mouseItem.stack--;
                
                if (Main.mouseItem.stack <= 0) {
                    Main.mouseItem.TurnToAir();
                }
                
                ui.SetItem(HoveredSlot, slotItem);
            }
        }

        /// <summary>
        /// 快速转移到背包
        /// </summary>
        private void QuickTransferToInventory(int slotIndex) {
            if (slotIndex < 0 || slotIndex >= TotalSlots) return;

            Item item = ui.GetItem(slotIndex);
            if (item == null || item.type <= ItemID.None || item.stack <= 0) return;

            //尝试添加到玩家背包
            Item leftover = player.GetItem(player.whoAmI, item.Clone(), 
                GetItemSettings.InventoryUIToInventorySettings);
            
            bool success = false;
            bool partialSuccess = false;

            if (leftover == null || leftover.stack == 0) {
                //完全添加成功
                ui.SetItem(slotIndex, new Item());
                success = true;
            }
            else if (leftover.stack < item.stack) {
                //部分添加
                item.stack = leftover.stack;
                ui.SetItem(slotIndex, item);
                partialSuccess = true;
            }

            //只有在操作成功且音效冷却结束时才播放音效
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
            Item targetItem = ui.GetItem(targetSlot);
            if (targetItem == null || targetItem.type == ItemID.None || targetItem.stack >= targetItem.maxStack) {
                return;
            }

            bool gathered = false;

            //从其他槽位收集相同物品
            for (int i = 0; i < TotalSlots; i++) {
                if (i == targetSlot) continue;
                if (targetItem.stack >= targetItem.maxStack) break;

                Item otherItem = ui.GetItem(i);
                if (otherItem != null && otherItem.type == targetItem.type) {
                    int spaceLeft = targetItem.maxStack - targetItem.stack;
                    int amountToTransfer = Math.Min(spaceLeft, otherItem.stack);

                    targetItem.stack += amountToTransfer;
                    otherItem.stack -= amountToTransfer;

                    if (otherItem.stack <= 0) {
                        ui.SetItem(i, new Item());
                    }
                    else {
                        ui.SetItem(i, otherItem);
                    }

                    gathered = true;
                }
            }

            if (gathered) {
                ui.SetItem(targetSlot, targetItem);
                PlaySound(SoundID.Grab);
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
