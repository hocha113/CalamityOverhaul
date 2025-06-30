using CalamityMod.Items.Materials;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Tiles.Furniture.CraftingStations;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Rapiers;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class FadingGlory : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "FadingGlory";
        public override void SetDefaults() {
            Item.width = Item.height = 35;
            Item.damage = 282;
            Item.DamageType = DamageClass.Melee;
            Item.rare = ItemRarityID.Red;
            Item.value = Item.buyPrice(2, 53, 75, 0);
            Item.shoot = ModContent.ProjectileType<FadingGloryRapier>();
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.autoReuse = true;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 3.5f;
            Item.shootSpeed = 5f;
            Item.UseSound = null;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.channel = true;
            Item.CWR().IsShootCountCorlUse = true;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<GrandGuardian>()
                .AddIngredient<AshesofAnnihilation>(5)
                .AddTile(ModContent.TileType<DraedonsForge>())
                .Register();
        }
    }
}
