using CalamityMod.Items.Materials;
using CalamityMod.Tiles.Furniture.CraftingStations;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Summon.Whips;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Summon.Extras
{
    internal class AllhallowsGoldWhip : ModItem
    {
        public override string Texture => CWRConstant.Item_Summon + "AllhallowsGoldWhip";

        public override bool IsLoadingEnabled(Mod mod) {
            return false;
        }

        public override void SetDefaults()
        {
            Item.DefaultToWhip(ModContent.ProjectileType<AllhallowsGoldWhipProjectile>(), 902, 0, 12, 45);
            Item.rare = ItemRarityID.Green;
            Item.channel = true;
        }

        public override bool MeleePrefix()
        {
            return true;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<WhiplashGalactica>())
                .AddIngredient(ModContent.ItemType<AuricBar>(), 5)
                .AddTile(ModContent.TileType<CosmicAnvil>())
                .Register();
        }
    }
}
