using CalamityOverhaul.Content.Projectiles.Weapons.Summon.Whips;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Summon
{
    internal class BleedingScourge : ModItem
    {
        public override string Texture => CWRConstant.Item_Summon + "BleedingScourge";

        public override void SetDefaults() {
            Item.DefaultToWhip(ModContent.ProjectileType<BleedingScourgeProjectile>(), 191, 3, 13, 40);
            Item.rare = ItemRarityID.Purple;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.value = Terraria.Item.buyPrice(0, 6, 15, 0);
            Item.rare = ItemRarityID.Lime;
        }

        public override bool MeleePrefix() {
            return true;
        }

        public override void AddRecipes() {
            _ = CreateRecipe()
                .AddIngredient(ModContent.ItemType<ElementWhip>())
                .AddIngredient(CWRID.Item_BloodstoneCore, 15)
                .AddIngredient(CWRID.Item_RuinousSoul, 5)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }
}
