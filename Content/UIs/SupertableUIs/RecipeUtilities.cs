using System;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.UIs.SupertableUIs
{
    /// <summary>
    /// 配方工具类，提供配方相关的辅助方法
    /// </summary>
    public static class RecipeUtilities
    {
        /// <summary>
        /// 从完整名称创建物品
        /// </summary>
        public static Item CreateItemFromFullName(string fullName, bool loadVanillaTexture = false) {
            if (string.IsNullOrEmpty(fullName) || fullName == SupertableConstants.NULL_ITEM_KEY) {
                return new Item();
            }

            //尝试解析为数字ID
            if (int.TryParse(fullName, out int itemId)) {
                if (loadVanillaTexture && !VaultUtils.isServer && itemId > ItemID.None) {
                    Main.instance.LoadItem(itemId);
                }
                return new Item(itemId);
            }

            //解析模组物品
            string[] parts = fullName.Split('/');
            if (parts.Length != 2) {
                return new Item();
            }

            try {
                var mod = ModLoader.GetMod(parts[0]);
                if (mod == null) return new Item();

                var modItem = mod.Find<ModItem>(parts[1]);
                if (modItem == null) return new Item();

                return modItem.Item;
            } catch {
                return new Item();
            }
        }

        /// <summary>
        /// 将完整名称数组转换为物品类型数组
        /// </summary>
        public static int[] ConvertFullNamesToTypes(string[] fullNames) {
            if (fullNames == null) return null;

            int[] types = new int[fullNames.Length];
            for (int i = 0; i < fullNames.Length; i++) {
                types[i] = VaultUtils.GetItemTypeFromFullName(fullNames[i]);
            }
            return types;
        }

        /// <summary>
        /// 从物品类型数组获取完整名称数组
        /// </summary>
        public static string[] ConvertTypesToFullNames(int[] types) {
            if (types == null) return null;

            string[] names = new string[types.Length];
            for (int i = 0; i < types.Length; i++) {
                names[i] = GetItemFullName(types[i]);
            }
            return names;
        }

        /// <summary>
        /// 获取物品的完整名称
        /// </summary>
        public static string GetItemFullName(int itemType) {
            if (itemType == ItemID.None) {
                return SupertableConstants.NULL_ITEM_KEY;
            }

            Item item = new Item(itemType);
            if (item.ModItem == null) {
                return itemType.ToString();
            }

            return item.ModItem.FullName;
        }

        /// <summary>
        /// 验证配方数据的有效性
        /// </summary>
        public static bool ValidateRecipeData(string[] recipeValues) {
            if (recipeValues == null) {
                return false;
            }

            if (recipeValues.Length != SupertableConstants.RECIPE_LENGTH) {
                return false;
            }

            //检查结果物品
            string targetItemFullName = recipeValues[^1];
            int targetItemId = VaultUtils.GetItemTypeFromFullName(targetItemFullName);

            //如果结果不是Null/Null但解析为None，则无效
            if (targetItemFullName != SupertableConstants.NULL_ITEM_KEY && targetItemId == ItemID.None) {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 计算可合成的最大数量
        /// </summary>
        public static int CalculateMaxCraftableAmount(Item[] materials, int resultMaxStack) {
            int minStack = int.MaxValue;

            foreach (var material in materials) {
                if (material.type == ItemID.None) continue;
                if (material.stack < minStack) {
                    minStack = material.stack;
                }
            }

            if (minStack == int.MaxValue) {
                minStack = 1;
            }

            return Math.Min(minStack, resultMaxStack);
        }

        /// <summary>
        /// 检查配方是否包含指定材料
        /// </summary>
        public static bool RecipeContainsMaterial(RecipeData recipe, int materialType) {
            if (recipe?.MaterialTypesCache == null) return false;

            return recipe.MaterialTypesCache.Contains(materialType);
        }

        /// <summary>
        /// 比较两个配方是否相同
        /// </summary>
        public static bool AreRecipesEqual(RecipeData recipe1, RecipeData recipe2) {
            if (recipe1 == null && recipe2 == null) return true;
            if (recipe1 == null || recipe2 == null) return false;

            return recipe1.Equals(recipe2);
        }
    }
}
