using CalamityOverhaul.Content.Projectiles.Weapons.Summon.Whips;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Summon
{
    internal class WhiplashGalactica : ModItem
    {
        public override string Texture => CWRConstant.Item_Summon + "WhiplashGalactica";

        public override void SetDefaults() {
            Item.DefaultToWhip(ModContent.ProjectileType<WhiplashGalacticaProjectile>(), 302, 0, 12, 45);
            Item.rare = ItemRarityID.Green;
            Item.channel = false;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.value = Item.buyPrice(0, 36, 5, 75);
            Item.rare = ItemRarityID.Red;
        }

        public override bool MeleePrefix() {
            return true;
        }

        public override void AddRecipes() {
            _ = CreateRecipe()
                .AddIngredient(ModContent.ItemType<ElementWhip>())
                .AddIngredient(CWRID.Item_CosmiliteBar, 5)
                .AddTile(CWRID.Tile_CosmicAnvil)
                .Register();
        }
    }
}
