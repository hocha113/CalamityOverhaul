using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Items.OceanRaiderses.OceanRaidersUIs
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
        private int draggedSlotIndex = -1;
        private Item draggedItem = null;
        private bool isDragging = false;

        //关闭按钮
        public bool IsCloseButtonHovered { get; private set; } = false;
        public const int CloseButtonSize = 32;

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
        public void UpdateSlotInteraction(Point mousePoint, Vector2 storageStartPos) {
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

            if (HoveredSlot >= 0 && HoveredSlot < machine.storedItems.Count) {
                Main.HoverItem = machine.storedItems[HoveredSlot];
                Main.hoverItemName = machine.storedItems[HoveredSlot].Name;
            }

            //处理拖拽逻辑
            HandleDragAndDrop();
        }

        private void HandleDragAndDrop() {
            //开始拖拽
            if (Main.mouseLeft && !isDragging && HoveredSlot != -1) {
                if (HoveredSlot < machine.storedItems.Count) {
                    Item item = machine.storedItems[HoveredSlot];
                    if (item != null && item.type > ItemID.None && item.stack > 0) {
                        //检查是否按住Shift - 快速取出到背包
                        if (Main.keyState.PressingShift()) {
                            TakeItemToInventory(HoveredSlot);
                            return;
                        }

                        //开始拖拽
                        draggedSlotIndex = HoveredSlot;
                        draggedItem = item.Clone();
                        isDragging = true;
                        SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.2f });
                    }
                }
            }

            //结束拖拽
            if (!Main.mouseLeft && isDragging) {
                if (HoveredSlot != -1 && HoveredSlot != draggedSlotIndex) {
                    //交换物品
                    SwapItems(draggedSlotIndex, HoveredSlot);
                }
                else if (HoveredSlot == -1) {
                    //拖出界面外，掉落物品
                    DropItemToWorld(draggedSlotIndex);
                }

                isDragging = false;
                draggedSlotIndex = -1;
                draggedItem = null;
                SoundEngine.PlaySound(SoundID.Grab);
            }
        }

        private void TakeItemToInventory(int slotIndex) {
            if (slotIndex < 0 || slotIndex >= machine.storedItems.Count) return;

            Item item = machine.storedItems[slotIndex];
            if (item == null || item.type <= ItemID.None || item.stack <= 0) return;

            //尝试添加到玩家背包
            Item leftover = player.GetItem(player.whoAmI, item.Clone(), GetItemSettings.InventoryUIToInventorySettings);
            if (leftover == null || leftover.stack == 0) {
                //完全添加成功，移除槽位物品
                machine.storedItems.RemoveAt(slotIndex);
                SoundEngine.PlaySound(SoundID.Grab);
                machine.SendData();
            }
            else {
                //部分添加，更新剩余数量
                item.stack = leftover.stack;
                SoundEngine.PlaySound(SoundID.Grab with { Pitch = -0.2f });
            }
        }

        private void SwapItems(int fromIndex, int toIndex) {
            if (fromIndex < 0 || fromIndex >= TotalSlots) return;
            if (toIndex < 0 || toIndex >= TotalSlots) return;

            //确保列表足够长
            while (machine.storedItems.Count <= Math.Max(fromIndex, toIndex)) {
                machine.storedItems.Add(new Item());
            }

            //交换物品
            Item temp = machine.storedItems[fromIndex];
            machine.storedItems[fromIndex] = machine.storedItems[toIndex];
            machine.storedItems[toIndex] = temp;

            //清理空物品
            CleanEmptySlots();
            machine.SendData();
        }

        private void DropItemToWorld(int slotIndex) {
            if (slotIndex < 0 || slotIndex >= machine.storedItems.Count) return;

            Item item = machine.storedItems[slotIndex];
            if (item == null || item.type <= ItemID.None || item.stack <= 0) return;

            //在玩家位置生成物品
            player.QuickSpawnItem(player.FromObjectGetParent(), item);
            machine.storedItems.RemoveAt(slotIndex);
            machine.SendData();
        }

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
        /// 重置交互状态
        /// </summary>
        public void Reset() {
            HoveredSlot = -1;
            isDragging = false;
            draggedSlotIndex = -1;
            draggedItem = null;
            IsCloseButtonHovered = false;
        }
    }
}
