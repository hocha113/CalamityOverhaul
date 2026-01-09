using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Incinerators
{
    /// <summary>
    /// 熔炼配方数据
    /// </summary>
    public struct SmeltRecipeData
    {
        /// <summary>
        /// 输入物品类型
        /// </summary>
        public int InputType;
        /// <summary>
        /// 输入物品数量
        /// </summary>
        public int InputStack;
        /// <summary>
        /// 输出物品类型
        /// </summary>
        public int OutputType;
        /// <summary>
        /// 输出物品数量(原始数量，会被倍率影响)
        /// </summary>
        public int OutputStack;

        public SmeltRecipeData(int inputType, int inputStack, int outputType, int outputStack) {
            InputType = inputType;
            InputStack = inputStack;
            OutputType = outputType;
            OutputStack = outputStack;
        }
    }

    /// <summary>
    /// 焚烧炉配方管理器
    /// 自动从游戏配方中读取单材料熔炼配方
    /// </summary>
    internal static class IncineratorRecipes
    {
        /// <summary>
        /// 熔炼配方表：输入物品类型 -> 配方数据
        /// </summary>
        public static Dictionary<int, SmeltRecipeData> SmeltRecipes { get; private set; }

        /// <summary>
        /// 允许的制作台类型(熔炉类)
        /// </summary>
        private static HashSet<int> _validFurnaceTiles;

        /// <summary>
        /// 是否已初始化
        /// </summary>
        private static bool _initialized = false;

        /// <summary>
        /// 产出倍率(电炉倍矿机制)
        /// </summary>
        public const int OutputMultiplier = 2;

        /// <summary>
        /// 初始化熔炼配方
        /// </summary>
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

        /// <summary>
        /// 初始化有效的熔炉类制作台
        /// </summary>
        private static void InitializeValidFurnaceTiles() {
            _validFurnaceTiles = [
                TileID.Furnaces,
                TileID.Hellforge,
                TileID.AdamantiteForge,
                TileID.GlassKiln,
            ];
        }

        /// <summary>
        /// 扫描所有配方，提取单材料熔炼配方
        /// </summary>
        private static void ScanRecipes() {
            foreach (Recipe recipe in Main.recipe) {
                if (recipe == null || recipe.createItem == null || recipe.createItem.IsAir) {
                    continue;
                }

                //检查是否是有效的熔炼配方
                if (!IsValidSmeltRecipe(recipe)) {
                    continue;
                }

                int inputType = recipe.requiredItem[0].type;
                int inputStack = recipe.requiredItem[0].stack;
                int outputType = recipe.createItem.type;
                int outputStack = recipe.createItem.stack;

                //避免重复添加，优先保留已有的配方
                if (!SmeltRecipes.ContainsKey(inputType)) {
                    SmeltRecipes[inputType] = new SmeltRecipeData(inputType, inputStack, outputType, outputStack);
                }
            }
        }

        /// <summary>
        /// 检查配方是否是有效的熔炼配方
        /// </summary>
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

        /// <summary>
        /// 添加额外的手动配方(游戏配方中没有但逻辑上应该支持的)
        /// </summary>
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

        /// <summary>
        /// 尝试添加配方(不覆盖已有的)
        /// </summary>
        private static void TryAddRecipe(int inputType, int inputStack, int outputType, int outputStack) {
            if (!SmeltRecipes.ContainsKey(inputType)) {
                SmeltRecipes[inputType] = new SmeltRecipeData(inputType, inputStack, outputType, outputStack);
            }
        }

        /// <summary>
        /// 检查物品是否可以被焚烧
        /// </summary>
        public static bool CanSmelt(Item item) {
            if (item == null || item.IsAir) {
                return false;
            }
            Initialize();
            return SmeltRecipes.ContainsKey(item.type);
        }

        /// <summary>
        /// 获取熔炼配方数据
        /// </summary>
        public static bool TryGetRecipe(int inputType, out SmeltRecipeData recipe) {
            Initialize();
            return SmeltRecipes.TryGetValue(inputType, out recipe);
        }

        /// <summary>
        /// 获取焚烧后的输出物品类型
        /// </summary>
        public static int GetSmeltResult(int inputType) {
            Initialize();
            return SmeltRecipes.TryGetValue(inputType, out var recipe) ? recipe.OutputType : ItemID.None;
        }

        /// <summary>
        /// 获取配方所需的输入数量
        /// </summary>
        public static int GetRequiredInputStack(int inputType) {
            Initialize();
            return SmeltRecipes.TryGetValue(inputType, out var recipe) ? recipe.InputStack : 1;
        }

        /// <summary>
        /// 获取配方的输出数量(已应用倍率)
        /// </summary>
        public static int GetOutputStack(int inputType) {
            Initialize();
            if (SmeltRecipes.TryGetValue(inputType, out var recipe)) {
                return recipe.OutputStack * OutputMultiplier;
            }
            return 1;
        }

        /// <summary>
        /// 手动添加熔炼配方(供其他模组或扩展使用)
        /// </summary>
        public static void AddSmeltRecipe(int inputType, int inputStack, int outputType, int outputStack) {
            Initialize();
            SmeltRecipes[inputType] = new SmeltRecipeData(inputType, inputStack, outputType, outputStack);
        }

        /// <summary>
        /// 添加有效的熔炉制作台类型
        /// </summary>
        public static void AddValidFurnaceTile(int tileType) {
            Initialize();
            _validFurnaceTiles.Add(tileType);
        }

        /// <summary>
        /// 重置配方(用于重新加载)
        /// </summary>
        public static void Reset() {
            SmeltRecipes?.Clear();
            _validFurnaceTiles?.Clear();
            _initialized = false;
        }

        /// <summary>
        /// 获取所有熔炼配方数量(调试用)
        /// </summary>
        public static int GetRecipeCount() {
            Initialize();
            return SmeltRecipes?.Count ?? 0;
        }
    }
}
