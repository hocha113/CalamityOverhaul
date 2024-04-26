using CalamityMod.Items.Materials;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Summon.Whips;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Summon.Extras
{
    internal class BleedingScourge : ModItem
    {
        public override string Texture => CWRConstant.Item_Summon + "BleedingScourge";

        public override void SetDefaults() {
            Item.DefaultToWhip(ModContent.ProjectileType<BleedingScourgeProjectile>(), 191, 3, 13, 40);
            Item.rare = ItemRarityID.Purple;
            Item.useTurn = true;
            Item.autoReuse = true;
        }

        public override bool MeleePrefix() {
            return true;
        }

        public override void AddRecipes() {
            _ = CreateRecipe()
                .AddIngredient(ModContent.ItemType<ElementWhip>())
                .AddIngredient(ModContent.ItemType<BloodstoneCore>(), 15)
                .AddIngredient(ModContent.ItemType<RuinousSoul>(), 5)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }
}
