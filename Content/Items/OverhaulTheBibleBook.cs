using CalamityMod.Items;
using CalamityMod.Items.Materials;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.UIs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items
{
    internal class OverhaulTheBibleBook : ModItem
    {
        public override string Texture => CWRConstant.Item + "book";
        public override void SetDefaults() {
            Item.width = 58;
            Item.height = 48;
            Item.value = CalamityGlobalItem.RarityHotPinkBuyPrice;
            Item.rare = ItemRarityID.Orange;
            Item.useTime = Item.useAnimation = 30;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.UseSound = SoundID.Item30 with { Volume = SoundID.Item30.Volume * 0.75f };
        }

        public override bool? UseItem(Player player) {
            if (player.whoAmI == Main.myPlayer) {
                OverhaulTheBible.Instance.Active = !OverhaulTheBible.Instance.Active;
            }
            return true;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.Wood, 5)
                .AddIngredient(ItemID.IronBar, 1)
                .Register();
        }
    }
}
