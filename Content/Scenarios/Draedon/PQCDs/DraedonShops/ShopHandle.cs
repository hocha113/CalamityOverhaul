using CalamityOverhaul.Content.Items.Materials;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Draedon.PQCDs.DraedonShops
{
    internal class ShopHandle
    {
        /// <summary>价格缓存</summary>
        private static readonly Dictionary<int, int> priceCache = new();

        public static void Handle(List<ShopItem> shopItems) {
            HashSet<int> addedItems = new(); //防重复
            List<ShopItem> tempItems = new();

            for (int i = 0; i < Recipe.numRecipes; i++) {
                Recipe recipe = Main.recipe[i];

                if (!ContainsDraedonMaterials(recipe)) {
                    continue;
                }

                int itemType = recipe.createItem.type;

                //跳过终局聚合材料
                if (itemType == ModContent.ItemType<NeutronStarIngot>()) {
                    continue;
                }

                if (addedItems.Contains(itemType)) {
                    continue;
                }

                int price = CalculateReasonablePrice(recipe);

                tempItems.Add(new ShopItem(itemType, 1, price));
                addedItems.Add(itemType);
            }

            tempItems = tempItems
                .OrderBy(item => item.price)
                .ThenBy(item => GetItemRarity(item.itemType))
                .ThenBy(item => GetItemName(item.itemType))
                .ToList();

            shopItems.AddRange(tempItems);
        }

        private static bool ContainsDraedonMaterials(Recipe recipe) {
            return recipe.requiredItem.Any(item =>
                item.type == CWRID.Item_ExoPrism ||
                item.type == CWRID.Item_DubiousPlating ||
                item.type == CWRID.Item_MysteriousCircuitry
            );
        }

        private static int CalculateReasonablePrice(Recipe recipe) {
            int resultType = recipe.createItem.type;

            if (priceCache.TryGetValue(resultType, out int cachedPrice)) {
                return cachedPrice;
            }

            int calculatedPrice = CalculatePriceFromMaterials(recipe);

            Item resultItem = new(resultType);

            int minimumPrice = Item.buyPrice(silver: 1);
            calculatedPrice = Math.Max(calculatedPrice, minimumPrice);

            calculatedPrice = AdjustPriceByRarity(calculatedPrice, resultItem.rare);

            priceCache[resultType] = calculatedPrice;

            return calculatedPrice;
        }

        private static int CalculatePriceFromMaterials(Recipe recipe) {
            int totalMaterialValue = 0;
            int validMaterialCount = 0;

            foreach (Item material in recipe.requiredItem) {
                if (material.type == ItemID.None) {
                    continue;
                }

                Item materialItem = new Item(material.type);
                int materialValue = materialItem.value;

                //价值异常则递归查配方
                if (materialValue <= 0 || materialValue > Item.buyPrice(platinum: 50)) {
                    materialValue = GetMaterialValueRecursive(material.type, 0);
                }

                if (materialValue > 0) {
                    totalMaterialValue += materialValue * material.stack;
                    validMaterialCount++;
                }
            }

            if (validMaterialCount == 0) {
                return Item.buyPrice(gold: 1);
            }

            //材料总价×1.2
            int finalPrice = (int)(totalMaterialValue * 1.2f);

            if (recipe.createItem.stack > 1) {
                finalPrice = (int)(finalPrice / recipe.createItem.stack * 1.1f);
            }

            return finalPrice;
        }

        private static int GetMaterialValueRecursive(int itemType, int depth) {
            const int maxDepth = 3;
            if (depth >= maxDepth) {
                return Item.buyPrice(silver: 10); //递归过深默认价
            }

            if (priceCache.TryGetValue(itemType, out int cachedValue)) {
                return cachedValue;
            }

            Item item = new Item(itemType);

            if (item.value > 0 && item.value < Item.buyPrice(platinum: 50)) {
                return item.value;
            }

            Recipe foundRecipe = null;
            for (int i = 0; i < Recipe.numRecipes; i++) {
                Recipe r = Main.recipe[i];
                if (r.createItem.type == itemType) {
                    foundRecipe = r;
                    break;
                }
            }

            if (foundRecipe == null) {
                return EstimatePriceByRarity(item.rare);
            }

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

        private static int AdjustPriceByRarity(int basePrice, int rarity) {
            float multiplier = rarity switch {
                >= ItemRarityID.Red => 2.0f,      //红+
                ItemRarityID.LightPurple => 1.6f,
                ItemRarityID.Lime => 1.4f,
                ItemRarityID.Yellow => 1.3f,
                ItemRarityID.LightRed => 1.2f,
                ItemRarityID.Pink => 1.15f,
                ItemRarityID.Orange => 1.1f,
                ItemRarityID.Green => 1.05f,
                ItemRarityID.Blue => 1.0f,
                ItemRarityID.White => 0.95f,
                _ => 1.0f
            };

            return (int)(basePrice * multiplier);
        }

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

        private static int GetItemRarity(int itemType) {
            Item item = new Item(itemType);
            return item.rare;
        }

        private static string GetItemName(int itemType) {
            Item item = new Item(itemType);
            return item.Name ?? "";
        }

        /// <summary>世界重载清缓存</summary>
        public static void ClearCache() {
            priceCache.Clear();
        }
    }
}
