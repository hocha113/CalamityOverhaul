using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.Projectiles.Weapons.Summon;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Summon.Extras
{
    internal class TheSpiritFlint : ModItem
    {
        public override string Texture => CWRConstant.Item_Summon + "TheSpiritFlint";
        public override void SetDefaults() {
            Item.damage = 8;
            Item.knockBack = 1f;
            Item.useTime = 40;
            Item.useAnimation = 40;
            Item.DamageType = DamageClass.Summon;
            Item.width = 46;
            Item.height = 46;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.value = Terraria.Item.buyPrice(0, 0, 1, 45);
            Item.rare = ItemRarityID.Green;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shootSpeed = 7;
            Item.mana = 20;
            Item.shoot = ModContent.ProjectileType<TheSpiritFlintProj>();
        }

        public override void AddRecipes() {
            CreateRecipe().
                AddIngredient<Flint>(6).
                AddIngredient(ItemID.FallenStar, 3).
                AddTile(TileID.WorkBenches).
                Register();
        }
    }
}
