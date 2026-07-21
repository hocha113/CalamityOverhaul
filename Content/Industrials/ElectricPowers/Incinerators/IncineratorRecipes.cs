using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Incinerators
{
    /// <summary>熔炼配方</summary>
    public struct SmeltRecipeData
    {
        public int InputType;
        public int InputStack;
        public int OutputType;
        /// <summary>产出数量(应用倍率前)</summary>
        public int OutputStack;

        public SmeltRecipeData(int inputType, int inputStack, int outputType, int outputStack) {
            InputType = inputType;
            InputStack = inputStack;
            OutputType = outputType;
            OutputStack = outputStack;
        }
    }

    /// <summary>电炉配方，扫单材料熔炼</summary>
    internal static class IncineratorRecipes
    {
        /// <summary>输入type→配方</summary>
        public static Dictionary<int, SmeltRecipeData> SmeltRecipes { get; private set; }

        private static HashSet<int> _validFurnaceTiles;

        private static bool _initialized = false;

        /// <summary>产出倍率</summary>
        public const int OutputMultiplier = 2;

        public static void Initialize() {
            if (_initialized) {
                return;
            }

            SmeltRecipes = new Dictionary<int, SmeltRecipeData>();
            InitializeValidFurnaceTiles();
            ScanRecipes();
            AddExtraRecipes();

            _initialized = true;
        }

        private static void InitializeValidFurnaceTiles() {
            _validFurnaceTiles = [
                TileID.Furnaces,
                TileID.Hellforge,
                TileID.AdamantiteForge,
                TileID.GlassKiln,
            ];
        }

        private static void ScanRecipes() {
            foreach (Recipe recipe in Main.recipe) {
                if (recipe == null || recipe.createItem == null || recipe.createItem.IsAir) {
                    continue;
                }

                if (!IsValidSmeltRecipe(recipe)) {
                    continue;
                }

                int inputType = recipe.requiredItem[0].type;
                int inputStack = recipe.requiredItem[0].stack;
                int outputType = recipe.createItem.type;
                int outputStack = recipe.createItem.stack;

                //已有则跳过
                if (!SmeltRecipes.ContainsKey(inputType)) {
                    SmeltRecipes[inputType] = new SmeltRecipeData(inputType, inputStack, outputType, outputStack);
                }
            }
        }

        private static bool IsValidSmeltRecipe(Recipe recipe) {
            //必须只有一种材料
            if (recipe.requiredItem.Count != 1) {
                return false;
            }

            //材料不能是空气
            Item ingredient = recipe.requiredItem[0];
            if (ingredient == null || ingredient.IsAir) {
                return false;
            }

            //检查制作台是否是熔炉类
            bool hasFurnaceTile = false;
            foreach (int tileId in recipe.requiredTile) {
                if (_validFurnaceTiles.Contains(tileId)) {
                    hasFurnaceTile = true;
                    break;
                }
            }

            if (!hasFurnaceTile) {
                return false;
            }

            //排除需要特殊条件的配方(比如需要水、岩浆、蜂蜜等)
            if (recipe.Conditions.Count > 0) {
                return false;
            }

            //排除输出物品与输入物品相同的情况
            if (ingredient.type == recipe.createItem.type) {
                return false;
            }

            return true;
        }

        /// <summary>补手动配方(原版表没有的)</summary>
        private static void AddExtraRecipes() {
            //沙子烧制成玻璃
            TryAddRecipe(ItemID.SandBlock, 2, ItemID.Glass, 1);

            //木材烧制成煤炭(5个木头烧1个煤炭)
            TryAddRecipe(ItemID.Wood, 5, ItemID.Coal, 1);
            TryAddRecipe(ItemID.Ebonwood, 5, ItemID.Coal, 1);
            TryAddRecipe(ItemID.Shadewood, 5, ItemID.Coal, 1);
            TryAddRecipe(ItemID.RichMahogany, 5, ItemID.Coal, 1);
            TryAddRecipe(ItemID.BorealWood, 5, ItemID.Coal, 1);
            TryAddRecipe(ItemID.PalmWood, 5, ItemID.Coal, 1);
            TryAddRecipe(ItemID.Pearlwood, 5, ItemID.Coal, 1);
            TryAddRecipe(ItemID.SpookyWood, 5, ItemID.Coal, 1);
            TryAddRecipe(ItemID.DynastyWood, 5, ItemID.Coal, 1);
            TryAddRecipe(ItemID.AshWood, 5, ItemID.Coal, 1);

            //粘土烧制成砖块
            TryAddRecipe(ItemID.ClayBlock, 2, ItemID.RedBrick, 1);
        }

        private static void TryAddRecipe(int inputType, int inputStack, int outputType, int outputStack) {
            if (!SmeltRecipes.ContainsKey(inputType)) {
                SmeltRecipes[inputType] = new SmeltRecipeData(inputType, inputStack, outputType, outputStack);
            }
        }

        public static bool CanSmelt(Item item) {
            if (item == null || item.IsAir) {
                return false;
            }
            Initialize();
            return SmeltRecipes.ContainsKey(item.type);
        }

        public static bool TryGetRecipe(int inputType, out SmeltRecipeData recipe) {
            Initialize();
            return SmeltRecipes.TryGetValue(inputType, out recipe);
        }

        public static int GetSmeltResult(int inputType) {
            Initialize();
            return SmeltRecipes.TryGetValue(inputType, out var recipe) ? recipe.OutputType : ItemID.None;
        }

        public static int GetRequiredInputStack(int inputType) {
            Initialize();
            return SmeltRecipes.TryGetValue(inputType, out var recipe) ? recipe.InputStack : 1;
        }

        /// <summary>产出数量(已乘倍率)</summary>
        public static int GetOutputStack(int inputType) {
            Initialize();
            if (SmeltRecipes.TryGetValue(inputType, out var recipe)) {
                return recipe.OutputStack * OutputMultiplier;
            }
            return 1;
        }

        public static void AddSmeltRecipe(int inputType, int inputStack, int outputType, int outputStack) {
            Initialize();
            SmeltRecipes[inputType] = new SmeltRecipeData(inputType, inputStack, outputType, outputStack);
        }

        public static void AddValidFurnaceTile(int tileType) {
            Initialize();
            _validFurnaceTiles.Add(tileType);
        }

        public static void Reset() {
            SmeltRecipes?.Clear();
            _validFurnaceTiles?.Clear();
            _initialized = false;
        }

        public static int GetRecipeCount() {
            Initialize();
            return SmeltRecipes?.Count ?? 0;
        }
    }
}
