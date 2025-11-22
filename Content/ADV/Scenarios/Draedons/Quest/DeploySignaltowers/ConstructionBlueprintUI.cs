using CalamityOverhaul.Content.ADV.Common;
using InnoVault.UIHandles;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.Quest.DeploySignaltowers
{
    /// <summary>
    /// 量子塔构建蓝图UI
    /// </summary>
    internal class ConstructionBlueprintUI : BaseRecipeDisplayUI
    {
        public static ConstructionBlueprintUI Instance => UIHandleLoader.GetUIHandleOfType<ConstructionBlueprintUI>();

        public override void SetStaticDefaults() {
            UITitle = this.GetLocalization(nameof(UITitle), () => "量子塔自我构建器");
            MaterialsRequired = this.GetLocalization(nameof(MaterialsRequired), () => "所需材料:");
            CloseHint = this.GetLocalization(nameof(CloseHint), () => "点击关闭");
            CraftingStation = this.GetLocalization(nameof(CraftingStation), () => "合成站点:");
        }

        protected override Recipe GetDisplayRecipe() {
            int itemType = ModContent.ItemType<CQETConstructor>();

            foreach (Recipe recipe in Main.recipe) {
                if (recipe.createItem.type == itemType) {
                    return recipe;
                }
            }

            return null;
        }

        protected override string GetUITitle() {
            return UITitle.Value;
        }
    }
}
