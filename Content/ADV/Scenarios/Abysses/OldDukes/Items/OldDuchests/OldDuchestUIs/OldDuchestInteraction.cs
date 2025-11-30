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
        private int draggedSlotIndex = -1;
        private Item draggedItem = null;
        private bool isDragging = false;

        //关闭按钮
        public bool IsCloseButtonHovered { get; private set; } = false;
        public const int CloseButtonSize = 32;

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

            //处理拖拽逻辑
            HandleDragAndDrop();
        }

        private void HandleDragAndDrop() {
            //开始拖拽
            if (Main.mouseLeft && !isDragging && HoveredSlot != -1) {
                Item item = ui.GetItem(HoveredSlot);
                if (item != null && item.type > ItemID.None && item.stack > 0) {
                    //检查是否按住Shift快速取出到背包
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
            if (slotIndex < 0 || slotIndex >= TotalSlots) return;

            Item item = ui.GetItem(slotIndex);
            if (item == null || item.type <= ItemID.None || item.stack <= 0) return;

            //尝试添加到玩家背包
            Item leftover = player.GetItem(player.whoAmI, item.Clone(), 
                GetItemSettings.InventoryUIToInventorySettings);
            
            if (leftover == null || leftover.stack == 0) {
                //完全添加成功，移除槽位物品
                ui.SetItem(slotIndex, new Item());
                SoundEngine.PlaySound(SoundID.Grab);
            }
            else {
                //部分添加，更新剩余数量
                item.stack = leftover.stack;
                ui.SetItem(slotIndex, item);
                SoundEngine.PlaySound(SoundID.Grab with { Pitch = -0.2f });
            }
        }

        private void SwapItems(int fromIndex, int toIndex) {
            if (fromIndex < 0 || fromIndex >= TotalSlots) return;
            if (toIndex < 0 || toIndex >= TotalSlots) return;

            //交换物品
            Item fromItem = ui.GetItem(fromIndex);
            Item toItem = ui.GetItem(toIndex);
            
            ui.SetItem(fromIndex, toItem);
            ui.SetItem(toIndex, fromItem);
        }

        private void DropItemToWorld(int slotIndex) {
            if (slotIndex < 0 || slotIndex >= TotalSlots) return;

            Item item = ui.GetItem(slotIndex);
            if (item == null || item.type <= ItemID.None || item.stack <= 0) return;

            //在玩家位置生成物品
            player.QuickSpawnItem(player.FromObjectGetParent(), item);
            ui.SetItem(slotIndex, new Item());
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
