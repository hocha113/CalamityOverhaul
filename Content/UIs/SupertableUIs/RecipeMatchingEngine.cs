using System.Collections.Generic;
using System.Linq;
using Terraria.ID;

namespace CalamityOverhaul.Content.UIs.SupertableUIs
{
    /// <summary>
    /// 配方匹配引擎，负责配方的匹配和查询
    /// </summary>
    public class RecipeMatchingEngine
    {
        private readonly List<RecipeData> _recipes;
        private Dictionary<int, List<RecipeData>> _targetItemIndex; //按结果物品索引
        private Dictionary<int, HashSet<RecipeData>> _materialIndex; //按材料索引

        public IReadOnlyList<RecipeData> AllRecipes => _recipes.AsReadOnly();
        public int RecipeCount => _recipes.Count;

        public RecipeMatchingEngine() {
            _recipes = new List<RecipeData>();
            _targetItemIndex = new Dictionary<int, List<RecipeData>>();
            _materialIndex = new Dictionary<int, HashSet<RecipeData>>();
        }

        /// <summary>
        /// 添加配方
        /// </summary>
        public void AddRecipe(RecipeData recipe) {
            if (recipe == null) return;

            _recipes.Add(recipe);

            //构建索引
            BuildIndicesForRecipe(recipe);
        }

        /// <summary>
        /// 批量添加配方
        /// </summary>
        public void AddRecipes(IEnumerable<RecipeData> recipes) {
            if (recipes == null) return;

            foreach (var recipe in recipes) {
                AddRecipe(recipe);
            }
        }

        /// <summary>
        /// 重建所有索引
        /// </summary>
        public void RebuildIndices() {
            _targetItemIndex.Clear();
            _materialIndex.Clear();

            foreach (var recipe in _recipes) {
                BuildIndicesForRecipe(recipe);
            }
        }

        /// <summary>
        /// 匹配配方
        /// </summary>
        public RecipeMatchResult MatchRecipe(int[] materialTypes) {
            if (materialTypes == null || materialTypes.Length != SupertableConstants.TOTAL_SLOTS) {
                return RecipeMatchResult.NoMatch();
            }

            foreach (var recipe in _recipes) {
                if (IsRecipeMatch(recipe, materialTypes)) {
                    return RecipeMatchResult.Success(recipe);
                }
            }

            return RecipeMatchResult.NoMatch();
        }

        /// <summary>
        /// 根据结果物品查找配方
        /// </summary>
        public IEnumerable<RecipeData> FindRecipesByTarget(int targetItemType) {
            if (_targetItemIndex.TryGetValue(targetItemType, out var recipes)) {
                return recipes;
            }
            return Enumerable.Empty<RecipeData>();
        }

        /// <summary>
        /// 根据材料查找配方
        /// </summary>
        public IEnumerable<RecipeData> FindRecipesByMaterial(int materialType) {
            if (_materialIndex.TryGetValue(materialType, out var recipes)) {
                return recipes;
            }
            return Enumerable.Empty<RecipeData>();
        }

        /// <summary>
        /// 检查配方是否匹配
        /// </summary>
        private bool IsRecipeMatch(RecipeData recipe, int[] materialTypes) {
            if (recipe.MaterialTypesCache == null || recipe.MaterialTypesCache.Length != SupertableConstants.TOTAL_SLOTS) {
                recipe.BuildMaterialTypesCache();
            }

            for (int i = 0; i < SupertableConstants.TOTAL_SLOTS; i++) {
                if (materialTypes[i] != recipe.MaterialTypesCache[i]) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 为配方构建索引
        /// </summary>
        private void BuildIndicesForRecipe(RecipeData recipe) {
            //按结果物品索引
            if (!_targetItemIndex.ContainsKey(recipe.Target)) {
                _targetItemIndex[recipe.Target] = new List<RecipeData>();
            }
            _targetItemIndex[recipe.Target].Add(recipe);

            //按材料索引
            if (recipe.MaterialTypesCache != null) {
                foreach (var materialType in recipe.MaterialTypesCache) {
                    if (materialType == ItemID.None) continue;

                    if (!_materialIndex.ContainsKey(materialType)) {
                        _materialIndex[materialType] = new HashSet<RecipeData>();
                    }
                    _materialIndex[materialType].Add(recipe);
                }
            }
        }

        /// <summary>
        /// 清除所有配方
        /// </summary>
        public void Clear() {
            _recipes.Clear();
            _targetItemIndex.Clear();
            _materialIndex.Clear();
        }

        /// <summary>
        /// 移除指定范围的配方
        /// </summary>
        public void RemoveRange(int startIndex, int count) {
            if (startIndex < 0 || startIndex >= _recipes.Count) return;
            if (count <= 0) return;

            _recipes.RemoveRange(startIndex, count);
            RebuildIndices();
        }
    }

    /// <summary>
    /// 配方匹配结果
    /// </summary>
    public class RecipeMatchResult
    {
        public bool IsMatch { get; private set; }
        public RecipeData MatchedRecipe { get; private set; }

        private RecipeMatchResult(bool isMatch, RecipeData recipe) {
            IsMatch = isMatch;
            MatchedRecipe = recipe;
        }

        public static RecipeMatchResult Success(RecipeData recipe) => new RecipeMatchResult(true, recipe);
        public static RecipeMatchResult NoMatch() => new RecipeMatchResult(false, null);
    }
}
