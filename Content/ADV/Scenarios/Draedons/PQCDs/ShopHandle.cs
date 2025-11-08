using CalamityOverhaul.Content.Items.Materials;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.ADV.Scenarios.Draedons.PQCDs.DraedonShopUI;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.PQCDs
{
    internal class ShopHandle
    {
        /// <summary>
        /// 价格缓存，避免重复计算
        /// </summary>
        private static readonly Dictionary<int, int> priceCache = new();

        /// <summary>
        /// 处理商店物品列表
        /// </summary>
        public static void Handle(List<ShopItem> shopItems) {
            HashSet<int> addedItems = new(); //防止重复添加
            List<ShopItem> tempItems = new();

            for (int i = 0; i < Recipe.numRecipes; i++) {
                Recipe recipe = Main.recipe[i];
                
                //检查配方是否包含嘉登材料
                if (!ContainsDraedonMaterials(recipe)) {
                    continue;
                }

                if (recipe.createItem.CWR().OmigaSnyContent != null) {
                    continue;//跳过特殊内容物品
                }

                int itemType = recipe.createItem.type;

                if (itemType == ModContent.ItemType<InfinityCatalyst>()) {
                    continue;
                }
                
                //跳过已添加的物品
                if (addedItems.Contains(itemType)) {
                    continue;
                }

                //计算合理的价格
                int price = CalculateReasonablePrice(recipe);
                
                //添加物品
                tempItems.Add(new ShopItem(itemType, 1, price));
                addedItems.Add(itemType);
            }

            //排序：按价格从低到高，相同价格按稀有度排序
            tempItems = tempItems
                .OrderBy(item => item.price)
                .ThenBy(item => GetItemRarity(item.itemType))
                .ThenBy(item => GetItemName(item.itemType))
                .ToList();

            //添加到商店列表
            shopItems.AddRange(tempItems);
        }

        /// <summary>
        /// 检查配方是否包含嘉登材料
        /// </summary>
        private static bool ContainsDraedonMaterials(Recipe recipe) {
            return recipe.requiredItem.Any(item => 
                item.type == CWRID.Item_ExoPrism ||
                item.type == CWRID.Item_DubiousPlating ||
                item.type == CWRID.Item_MysteriousCircuitry
            );
        }

        /// <summary>
        /// 计算合理的价格
        /// </summary>
        private static int CalculateReasonablePrice(Recipe recipe) {
            int resultType = recipe.createItem.type;

            //检查缓存
            if (priceCache.TryGetValue(resultType, out int cachedPrice)) {
                return cachedPrice;
            }

            int calculatedPrice;

            //获取物品基础价值
            Item resultItem = new Item(resultType);
            int baseValue = resultItem.value;

            //如果基础价值合理（大于0且小于100铂金），直接使用并加成
            if (baseValue > 0 && baseValue < Item.buyPrice(platinum: 100)) {
                calculatedPrice = (int)(baseValue * 1.5f); //50%加成作为商店售价
            }
            else {
                //基础价值不合理，通过材料计算
                calculatedPrice = CalculatePriceFromMaterials(recipe);
            }

            //确保最低价格
            int minimumPrice = Item.buyPrice(silver: 1);
            calculatedPrice = Math.Max(calculatedPrice, minimumPrice);

            //根据稀有度调整价格
            calculatedPrice = AdjustPriceByRarity(calculatedPrice, resultItem.rare);

            //缓存结果
            priceCache[resultType] = calculatedPrice;

            return calculatedPrice;
        }

        /// <summary>
        /// 通过材料计算价格
        /// </summary>
        private static int CalculatePriceFromMaterials(Recipe recipe) {
            int totalMaterialValue = 0;
            int validMaterialCount = 0;

            foreach (Item material in recipe.requiredItem) {
                if (material.type == ItemID.None) {
                    continue;
                }

                Item materialItem = new Item(material.type);
                int materialValue = materialItem.value;

                //如果材料本身价值异常，尝试递归查找其配方
                if (materialValue <= 0 || materialValue > Item.buyPrice(platinum: 50)) {
                    materialValue = GetMaterialValueRecursive(material.type, 0);
                }

                if (materialValue > 0) {
                    totalMaterialValue += materialValue * material.stack;
                    validMaterialCount++;
                }
            }

            //如果没有有效材料，返回默认价格
            if (validMaterialCount == 0) {
                return Item.buyPrice(gold: 1);
            }

            //材料总价 * 1.2倍作为成品售价（20%加成）
            int finalPrice = (int)(totalMaterialValue * 1.2f);

            //根据制作数量调整
            if (recipe.createItem.stack > 1) {
                finalPrice = (int)(finalPrice / recipe.createItem.stack * 1.1f);
            }

            return finalPrice;
        }

        /// <summary>
        /// 递归获取材料价值（防止无限递归）
        /// </summary>
        private static int GetMaterialValueRecursive(int itemType, int depth) {
            //最大递归深度限制
            const int maxDepth = 3;
            if (depth >= maxDepth) {
                return Item.buyPrice(silver: 10); //递归过深，返回默认值
            }

            //检查缓存
            if (priceCache.TryGetValue(itemType, out int cachedValue)) {
                return cachedValue;
            }

            Item item = new Item(itemType);
            
            //如果物品本身有合理价值，直接返回
            if (item.value > 0 && item.value < Item.buyPrice(platinum: 50)) {
                return item.value;
            }

            //查找该物品的配方
            Recipe foundRecipe = null;
            for (int i = 0; i < Recipe.numRecipes; i++) {
                Recipe r = Main.recipe[i];
                if (r.createItem.type == itemType) {
                    foundRecipe = r;
                    break;
                }
            }

            if (foundRecipe == null) {
                //没有配方，根据稀有度估算
                return EstimatePriceByRarity(item.rare);
            }

            //通过配方材料递归计算
            int totalValue = 0;
            int validCount = 0;

            foreach (Item material in foundRecipe.requiredItem) {
                if (material.type == ItemID.None) {
                    continue;
                }

                Item mat = new Item(material.type);
                int matValue = mat.value;

                if (matValue <= 0 || matValue > Item.buyPrice(platinum: 50)) {
                    matValue = GetMaterialValueRecursive(material.type, depth + 1);
                }

                if (matValue > 0) {
                    totalValue += matValue * material.stack;
                    validCount++;
                }
            }

            if (validCount == 0) {
                return EstimatePriceByRarity(item.rare);
            }

            int calculatedValue = totalValue / Math.Max(1, foundRecipe.createItem.stack);
            priceCache[itemType] = calculatedValue;
            return calculatedValue;
        }

        /// <summary>
        /// 根据稀有度调整价格
        /// </summary>
        private static int AdjustPriceByRarity(int basePrice, int rarity) {
            float multiplier = rarity switch {
                >= ItemRarityID.Red => 2.0f,      //红色及以上
                ItemRarityID.LightPurple => 1.6f, //亮紫
                ItemRarityID.Lime => 1.4f,        //黄绿
                ItemRarityID.Yellow => 1.3f,      //黄色
                ItemRarityID.LightRed => 1.2f,    //亮红
                ItemRarityID.Pink => 1.15f,       //粉色
                ItemRarityID.Orange => 1.1f,      //橙色
                ItemRarityID.Green => 1.05f,      //绿色
                ItemRarityID.Blue => 1.0f,        //蓝色
                ItemRarityID.White => 0.95f,      //白色
                _ => 1.0f
            };

            return (int)(basePrice * multiplier);
        }

        /// <summary>
        /// 根据稀有度估算价格
        /// </summary>
        private static int EstimatePriceByRarity(int rarity) {
            return rarity switch {
                >= ItemRarityID.Red => Item.buyPrice(platinum: 5),
                ItemRarityID.LightPurple => Item.buyPrice(platinum: 1),
                ItemRarityID.Lime => Item.buyPrice(gold: 50),
                ItemRarityID.Yellow => Item.buyPrice(gold: 20),
                ItemRarityID.LightRed => Item.buyPrice(gold: 10),
                ItemRarityID.Pink => Item.buyPrice(gold: 5),
                ItemRarityID.Orange => Item.buyPrice(gold: 2),
                ItemRarityID.Green => Item.buyPrice(gold: 1),
                ItemRarityID.Blue => Item.buyPrice(silver: 50),
                ItemRarityID.White => Item.buyPrice(silver: 20),
                _ => Item.buyPrice(silver: 10)
            };
        }

        /// <summary>
        /// 获取物品稀有度
        /// </summary>
        private static int GetItemRarity(int itemType) {
            Item item = new Item(itemType);
            return item.rare;
        }

        /// <summary>
        /// 获取物品名称（用于排序）
        /// </summary>
        private static string GetItemName(int itemType) {
            Item item = new Item(itemType);
            return item.Name ?? "";
        }

        /// <summary>
        /// 清除价格缓存（在世界重载时调用）
        /// </summary>
        public static void ClearCache() {
            priceCache.Clear();
        }
    }
}
