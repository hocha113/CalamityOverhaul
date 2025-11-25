using CalamityOverhaul.Content.UIs.SupertableUIs.Animation;
using CalamityOverhaul.Content.UIs.SupertableUIs.Crafting;
using CalamityOverhaul.Content.UIs.SupertableUIs.Inventory;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.UIs.SupertableUIs
{
    /// <summary>
    /// 超级工作台主控制器，协调各个模块的工作
    /// 这是重构后的核心类，采用了职责分离的设计
    /// </summary>
    public class SupertableController
    {
        //核心模块
        public ItemSlotManager SlotManager { get; private set; }
        public RecipeMatchingEngine RecipeEngine { get; private set; }
        public CraftingResultManager ResultManager { get; private set; }
        public UIAnimationController AnimationController { get; private set; }

        //状态缓存
        private RecipeData _lastMatchedRecipe;
        private string[] _lastMatchedRecipeNames;
        private int[] _lastMatchedRecipeTypes;

        public SupertableController() {
            SlotManager = new ItemSlotManager();
            RecipeEngine = new RecipeMatchingEngine();
            ResultManager = new CraftingResultManager();
            AnimationController = new UIAnimationController();
        }

        /// <summary>
        /// 初始化配方数据
        /// </summary>
        public void InitializeRecipes(IEnumerable<RecipeData> recipes) {
            RecipeEngine.Clear();
            RecipeEngine.AddRecipes(recipes);
        }

        /// <summary>
        /// 更新配方匹配
        /// </summary>
        public void UpdateRecipeMatching(bool syncNetwork = true) {
            int[] currentTypes = SlotManager.GetAllItemTypes();
            var matchResult = RecipeEngine.MatchRecipe(currentTypes);

            if (matchResult.IsMatch) {
                //计算可合成数量
                int maxCraftable = SlotManager.GetMinimumStackSize();
                ResultManager.SetResult(matchResult.MatchedRecipe, maxCraftable);

                //缓存匹配结果
                _lastMatchedRecipe = matchResult.MatchedRecipe;
                _lastMatchedRecipeTypes = matchResult.MatchedRecipe.MaterialTypesCache;
                _lastMatchedRecipeNames = RecipeUtilities.ConvertTypesToFullNames(_lastMatchedRecipeTypes);

                //更新预览
                SlotManager.SetPreviewFromTypes(matchResult.MatchedRecipe.MaterialTypesCache);
            }
            else {
                ResultManager.ClearResult();
                _lastMatchedRecipe = null;
                _lastMatchedRecipeTypes = null;
                _lastMatchedRecipeNames = null;
            }

            //TODO: 网络同步
            if (syncNetwork) {
                SyncToNetwork();
            }
        }

        /// <summary>
        /// 尝试获取合成结果
        /// </summary>
        public bool TryTakeResult(ref Item mouseItem) {
            if (ResultManager.TryTakeResult(ref mouseItem, out int takenAmount)) {
                //消耗材料
                SlotManager.ConsumeItems(takenAmount);

                //重新匹配配方
                UpdateRecipeMatching();

                return true;
            }

            return false;
        }

        /// <summary>
        /// 清空所有槽位
        /// </summary>
        public void ClearAllSlots() {
            SlotManager.ClearAllSlots();
            ResultManager.ClearResult();
            _lastMatchedRecipe = null;
        }

        /// <summary>
        /// 更新动画
        /// </summary>
        public void UpdateAnimations(bool isActive, int hoveredSlotIndex) {
            AnimationController.UpdateOpenAnimation(isActive);
            AnimationController.UpdateSlotHoverAnimation(hoveredSlotIndex);
        }

        /// <summary>
        /// 获取最后匹配的配方信息
        /// </summary>
        public (RecipeData recipe, string[] names, int[] types) GetLastMatchedRecipeInfo() {
            return (_lastMatchedRecipe, _lastMatchedRecipeNames, _lastMatchedRecipeTypes);
        }

        /// <summary>
        /// 网络同步(待实现)
        /// </summary>
        private void SyncToNetwork() {
            //TODO: 实现网络同步逻辑
        }

        /// <summary>
        /// 重置所有状态
        /// </summary>
        public void Reset() {
            ClearAllSlots();
            AnimationController.Reset();
        }
    }
}
