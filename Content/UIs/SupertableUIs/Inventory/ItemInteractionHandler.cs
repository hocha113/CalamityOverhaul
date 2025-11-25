using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.UIs.SupertableUIs.Inventory
{
    /// <summary>
    /// 物品交互处理器，负责处理物品的点击、拖拽等交互
    /// </summary>
    public static class ItemInteractionHandler
    {
        /// <summary>
        /// 处理左键点击物品
        /// </summary>
        public static void HandleLeftClick(ref Item slotItem, ref Item mouseItem) {
            //两个都为空，无需处理
            if (slotItem.type == ItemID.None && mouseItem.type == ItemID.None)
                return;

            //捡起物品
            if (slotItem.type != ItemID.None && mouseItem.type == ItemID.None) {
                PlayGrabSound();
                mouseItem = slotItem;
                slotItem = new Item();
                return;
            }

            //同种物品堆叠
            if (slotItem.type == mouseItem.type && mouseItem.type != ItemID.None) {
                PlayGrabSound();
                StackItems(ref slotItem, ref mouseItem);
                return;
            }

            //放置或交换物品
            if (slotItem.type == ItemID.None || slotItem.type != mouseItem.type) {
                PlayGrabSound();
                SwapItems(ref slotItem, ref mouseItem);
            }
        }

        /// <summary>
        /// 处理右键点击物品
        /// </summary>
        public static void HandleRightClick(ref Item slotItem, ref Item mouseItem) {
            //两个都为空，无需处理
            if (slotItem.type == ItemID.None && mouseItem.type == ItemID.None)
                return;

            //槽位有物品，鼠标为空，拿取一个
            if (slotItem.type != ItemID.None && mouseItem.type == ItemID.None && slotItem.stack > 1) {
                PlayGrabSound();
                TakeOneItem(ref slotItem, ref mouseItem);
                return;
            }

            //同种物品，放置一个
            if (slotItem.type == mouseItem.type && mouseItem.type != ItemID.None) {
                if (slotItem.maxStack == 1) return;

                PlayGrabSound();
                PlaceOneItem(ref slotItem, ref mouseItem);
                return;
            }

            //不同种物品，交换
            if (slotItem.type != mouseItem.type && slotItem.type != ItemID.None && mouseItem.type != ItemID.None) {
                PlayGrabSound();
                SwapItems(ref slotItem, ref mouseItem);
                return;
            }

            //鼠标有物品，槽位为空，放置一个
            if (slotItem.type == ItemID.None && mouseItem.type != ItemID.None) {
                PlayGrabSound();
                PlaceOneItem(ref slotItem, ref mouseItem);
            }
        }

        /// <summary>
        /// 处理拖拽放置(持续右键)
        /// </summary>
        public static void HandleDragPlace(ref Item slotItem, ref Item mouseItem) {
            if (slotItem.type == ItemID.None && mouseItem.type != ItemID.None && mouseItem.stack > 0) {
                mouseItem.stack--;
                Item newItem = mouseItem.Clone();
                newItem.stack = 1;
                slotItem = newItem;

                if (mouseItem.stack == 0) {
                    mouseItem.TurnToAir();
                }
            }
        }

        /// <summary>
        /// 收集同类物品到一个槽位
        /// </summary>
        public static void GatherSameItems(Item[] slots, int targetIndex) {
            if (slots[targetIndex].type == ItemID.None) return;

            int targetType = slots[targetIndex].type;

            for (int i = 0; i < slots.Length; i++) {
                if (i == targetIndex) continue;
                if (slots[i].type != targetType) continue;

                slots[targetIndex].stack += slots[i].stack;
                slots[i].TurnToAir();
            }
        }

        /// <summary>
        /// Shift+左键快速转移到玩家背包
        /// </summary>
        public static void QuickTransferToInventory(Item slotItem, Player player) {
            if (slotItem.type == ItemID.None) return;

            PlayGrabSound();
            Item itemClone = slotItem.Clone();
            player.QuickSpawnItem(player.FromObjectGetParent(), itemClone, itemClone.stack);
            slotItem.TurnToAir();
        }

        /// <summary>
        /// 一键放置配方材料
        /// </summary>
        public static bool TryQuickPlaceRecipe(Item[] slots, Item[] previewSlots, ref Item mouseItem, Player player) {
            if (previewSlots == null || previewSlots.Length != slots.Length)
                return false;

            bool placedAny = false;

            for (int i = 0; i < previewSlots.Length; i++) {
                Item previewItem = previewSlots[i];
                if (previewItem == null || previewItem.type == ItemID.None)
                    continue;

                //先检查鼠标物品
                if (mouseItem.type == previewItem.type && mouseItem.type != ItemID.None) {
                    if (PlaceItemIntoSlot(ref slots[i], ref mouseItem)) {
                        placedAny = true;
                        if (mouseItem.stack == 0) return placedAny;
                        continue;
                    }
                }

                //再检查背包
                foreach (var backpackItem in player.inventory) {
                    if (backpackItem.type == previewItem.type && backpackItem.type != ItemID.None) {
                        if (PlaceItemFromInventory(ref slots[i], backpackItem)) {
                            placedAny = true;
                            break;
                        }
                    }
                }
            }

            if (placedAny) {
                PlayGrabSound();
            }

            return placedAny;
        }

        #region 私有辅助方法

        private static void StackItems(ref Item slotItem, ref Item mouseItem) {
            int totalStack = slotItem.stack + mouseItem.stack;

            if (totalStack <= slotItem.maxStack) {
                slotItem.stack = totalStack;
                mouseItem = new Item();
            }
            else {
                slotItem.stack = slotItem.maxStack;
                mouseItem.stack = totalStack - slotItem.maxStack;
            }
        }

        private static void SwapItems(ref Item slotItem, ref Item mouseItem) {
            (mouseItem, slotItem) = (slotItem, mouseItem);
        }

        private static void TakeOneItem(ref Item slotItem, ref Item mouseItem) {
            Item takenItem = slotItem.Clone();
            takenItem.stack = 1;
            mouseItem = takenItem;

            slotItem.stack--;
            if (slotItem.stack <= 0) {
                slotItem.TurnToAir();
            }
        }

        private static void PlaceOneItem(ref Item slotItem, ref Item mouseItem) {
            if (slotItem.type == ItemID.None) {
                Item placedItem = mouseItem.Clone();
                placedItem.stack = 1;
                slotItem = placedItem;
            }
            else {
                slotItem.stack++;
            }

            mouseItem.stack--;
            if (mouseItem.stack == 0) {
                mouseItem.TurnToAir();
            }
        }

        private static bool PlaceItemIntoSlot(ref Item slot, ref Item sourceItem) {
            if (slot.type == ItemID.None) {
                Item targetItem = sourceItem.Clone();
                targetItem.stack = 1;
                slot = targetItem;
                sourceItem.stack--;
                if (sourceItem.stack == 0) {
                    sourceItem.TurnToAir();
                }
                return true;
            }
            else if (slot.type == sourceItem.type && slot.stack < slot.maxStack) {
                slot.stack++;
                sourceItem.stack--;
                if (sourceItem.stack == 0) {
                    sourceItem.TurnToAir();
                }
                return true;
            }

            return false;
        }

        private static bool PlaceItemFromInventory(ref Item slot, Item inventoryItem) {
            if (slot.type == ItemID.None) {
                Item targetItem = inventoryItem.Clone();
                targetItem.stack = 1;
                slot = targetItem;
            }
            else if (slot.type == inventoryItem.type) {
                slot.stack++;
                if (slot.stack > slot.maxStack) {
                    slot.stack = slot.maxStack;
                    return false;
                }
            }
            else {
                return false;
            }

            inventoryItem.stack--;
            if (inventoryItem.stack == 0) {
                inventoryItem.TurnToAir();
            }

            return true;
        }

        private static void PlayGrabSound() {
            SoundEngine.PlaySound(SoundID.Grab);
        }

        #endregion
    }
}
