using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.UIs.SupertableUIs.Inventory
{
    /// <summary>
    /// 物品槽位管理器，负责管理81个材料槽位
    /// </summary>
    public class ItemSlotManager
    {
        private Item[] _slots;
        private Item[] _previewSlots;

        public int SlotCount => _slots.Length;

        public ItemSlotManager(int slotCount = SupertableConstants.TOTAL_SLOTS) {
            _slots = new Item[slotCount];
            _previewSlots = new Item[slotCount];
            InitializeSlots();
        }

        private void InitializeSlots() {
            for (int i = 0; i < _slots.Length; i++) {
                _slots[i] = new Item();
                _previewSlots[i] = new Item();
            }
        }

        /// <summary>
        /// 获取指定槽位的物品
        /// </summary>
        public Item GetSlot(int index) {
            if (!IsValidIndex(index)) return null;
            return _slots[index];
        }

        /// <summary>
        /// 设置指定槽位的物品
        /// </summary>
        public void SetSlot(int index, Item item) {
            if (!IsValidIndex(index)) return;
            _slots[index] = item ?? new Item();
        }

        /// <summary>
        /// 获取预览槽位
        /// </summary>
        public Item GetPreviewSlot(int index) {
            if (!IsValidIndex(index)) return null;
            return _previewSlots[index];
        }

        /// <summary>
        /// 设置预览槽位
        /// </summary>
        public void SetPreviewSlot(int index, Item item) {
            if (!IsValidIndex(index)) return;
            _previewSlots[index] = item ?? new Item();
        }

        /// <summary>
        /// 清空所有槽位
        /// </summary>
        public void ClearAllSlots() {
            for (int i = 0; i < _slots.Length; i++) {
                _slots[i].TurnToAir();
            }
        }

        /// <summary>
        /// 清空指定槽位
        /// </summary>
        public void ClearSlot(int index) {
            if (!IsValidIndex(index)) return;
            _slots[index].TurnToAir();
        }

        /// <summary>
        /// 获取所有非空槽位
        /// </summary>
        public IEnumerable<(int index, Item item)> GetNonEmptySlots() {
            for (int i = 0; i < _slots.Length; i++) {
                if (_slots[i].type != ItemID.None) {
                    yield return (i, _slots[i]);
                }
            }
        }

        /// <summary>
        /// 检查是否有任何物品
        /// </summary>
        public bool HasAnyItems() {
            return _slots.Any(item => item.type != ItemID.None);
        }

        /// <summary>
        /// 获取所有物品类型
        /// </summary>
        public int[] GetAllItemTypes() {
            int[] types = new int[_slots.Length];
            for (int i = 0; i < _slots.Length; i++) {
                types[i] = _slots[i].type;
            }
            return types;
        }

        /// <summary>
        /// 根据物品类型数组设置预览槽位
        /// </summary>
        public void SetPreviewFromTypes(int[] types) {
            if (types == null || types.Length != _slots.Length) return;

            for (int i = 0; i < types.Length; i++) {
                _previewSlots[i] = new Item(types[i]);
            }
        }

        /// <summary>
        /// 获取最小堆叠数量(用于计算合成数量)
        /// </summary>
        public int GetMinimumStackSize() {
            int minStack = int.MaxValue;

            foreach (var item in _slots) {
                if (item.type == ItemID.None) continue;
                if (item.stack < minStack) {
                    minStack = item.stack;
                }
            }

            return minStack == int.MaxValue ? 1 : minStack;
        }

        /// <summary>
        /// 消耗材料
        /// </summary>
        public void ConsumeItems(int amount) {
            foreach (var item in _slots) {
                if (item.type == ItemID.None) continue;

                item.stack -= amount;
                if (item.stack <= 0) {
                    item.TurnToAir();
                }
            }
        }

        private bool IsValidIndex(int index) => index >= 0 && index < _slots.Length;

        /// <summary>
        /// 直接访问槽位数组(用于兼容性)
        /// </summary>
        public ref Item[] Slots => ref _slots;

        /// <summary>
        /// 直接访问预览槽位数组(用于兼容性)
        /// </summary>
        public Item[] PreviewSlots => _previewSlots;
    }
}
