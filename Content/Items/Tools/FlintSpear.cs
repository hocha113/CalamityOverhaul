using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Tools
{
    internal class FlintSpear : ModItem
    {
        public override string Texture => CWRConstant.Item + "Tools/FlintSpear";
        public override void SetDefaults() {
            Item.damage = 11;
            Item.knockBack = 1f;
            Item.useTime = 22;
            Item.useAnimation = 22;
            Item.DamageType = DamageClass.Melee;
            Item.width = 46;
            Item.height = 46;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.value = Item.buyPrice(0, 0, 0, 25);
            Item.rare = ItemRarityID.White;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<FlintSpearProj>();
            Item.shootSpeed = 7;
        }

        public override void ModifyWeaponCrit(Player player, ref float crit) {
            crit += 1;
        }

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[Item.shoot] <= 0;
        }

        public override void AddRecipes() {
            CreateRecipe().
                AddIngredient<Flint>(3).
                AddIngredient(ItemID.VineRope, 2).
                AddIngredient(ItemID.Wood, 12).
                AddTile(TileID.WorkBenches).
                Register();
        }
    }
}
