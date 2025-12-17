using CalamityOverhaul.Content.Projectiles.Weapons.Summon.Whips;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Summon
{
    internal class AzureDragonRage : ModItem
    {
        public override string Texture => CWRConstant.Item_Summon + "AzureDragonRage";

        public override void SetDefaults() {
            Item.DefaultToWhip(ModContent.ProjectileType<AzureDragonRageProjectile>(), 172, 2.5f, 13, 35);
            Item.rare = ItemRarityID.Purple;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.value = Terraria.Item.buyPrice(0, 5, 5, 0);
            Item.rare = ItemRarityID.LightPurple;
        }

        public override bool MeleePrefix() {
            return true;
        }

        public override void AddRecipes() {
            if (!CWRRef.Has) {
                CreateRecipe()
                .AddIngredient(ItemID.LunarBar, 5)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
                return;
            }
            _ = CreateRecipe()
                .AddIngredient(ModContent.ItemType<ElementWhip>())
                .AddIngredient(CWRID.Item_UelibloomBar, 5)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }
}
