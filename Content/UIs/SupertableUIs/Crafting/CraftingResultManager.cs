using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.UIs.SupertableUIs.Crafting
{
    /// <summary>
    /// 合成结果管理器，负责管理合成结果物品
    /// </summary>
    public class CraftingResultManager
    {
        private Item _resultItem;
        private RecipeData _currentRecipe;

        public Item ResultItem => _resultItem ?? (_resultItem = new Item());
        public bool HasResult => _resultItem != null && _resultItem.type != ItemID.None;
        public RecipeData CurrentRecipe => _currentRecipe;

        public CraftingResultManager() {
            _resultItem = new Item();
        }

        /// <summary>
        /// 设置合成结果
        /// </summary>
        public void SetResult(RecipeData recipe, int craftAmount) {
            if (recipe == null || recipe.Target == ItemID.None) {
                ClearResult();
                return;
            }

            _currentRecipe = recipe;
            _resultItem = new Item(recipe.Target);
            _resultItem.stack = Math.Min(craftAmount, _resultItem.maxStack);
        }

        /// <summary>
        /// 清除合成结果
        /// </summary>
        public void ClearResult() {
            _resultItem = new Item();
            _currentRecipe = null;
        }

        /// <summary>
        /// 尝试取出合成结果
        /// </summary>
        public bool TryTakeResult(ref Item mouseItem, out int takenAmount) {
            takenAmount = 0;

            if (!HasResult) {
                return false;
            }

            // 鼠标为空，直接取出
            if (mouseItem.type == ItemID.None) {
                mouseItem = _resultItem;
                takenAmount = _resultItem.stack;
                ClearResult();
                return true;
            }

            // 同类物品且可堆叠
            if (mouseItem.type == _resultItem.type && mouseItem.stack < mouseItem.maxStack) {
                int availableSpace = mouseItem.maxStack - mouseItem.stack;
                int amountToTake = Math.Min(_resultItem.stack, availableSpace);

                mouseItem.stack += amountToTake;
                takenAmount = amountToTake;
                ClearResult();
                return true;
            }

            return false;
        }

        /// <summary>
        /// 检查是否可以取出结果
        /// </summary>
        public bool CanTakeResult(Item mouseItem) {
            if (!HasResult) return false;

            if (mouseItem.type == ItemID.None) return true;

            return mouseItem.type == _resultItem.type && mouseItem.stack < mouseItem.maxStack;
        }

        /// <summary>
        /// 更新合成数量
        /// </summary>
        public void UpdateCraftAmount(int newAmount) {
            if (!HasResult) return;

            _resultItem.stack = Math.Min(newAmount, _resultItem.maxStack);
        }
    }
}
