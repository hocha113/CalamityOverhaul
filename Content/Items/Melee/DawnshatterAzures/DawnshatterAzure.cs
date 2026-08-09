using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.DawnshatterAzures
{
    /// 苍穹破晓,五拍连段长枪,右键举枪突进;连段状态住在 DawnshatterHeld,物品只做路由
    internal class DawnshatterAzure : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "DawnshatterAzure";

        public override void SetStaticDefaults() {
            Main.RegisterItemAnimation(Item.type, new DrawAnimationVertical(5, 4));
            ItemID.Sets.AnimatesAsSoul[Type] = true;
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        }

        public override void SetDefaults() {
            Item.height = Item.width = 54;
            Item.damage = 11200;
            Item.DamageType = DamageClass.Melee;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.useTime = Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 7.5f;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.value = Item.buyPrice(6, 23, 75, 0);
            Item.rare = CWRID.Rarity_DarkOrange;
            Item.shoot = ModContent.ProjectileType<DawnshatterHeld>();
            Item.shootSpeed = 1f;
        }

        public override void AddRecipes() {
            if (!CWRID.AllValid(CWRID.Item_ShadowspecBar, CWRID.Item_RedSun, CWRID.Item_DraconicDestruction
                , CWRID.Item_DragonPow, CWRID.Item_DragonRage, CWRID.Item_Rock)) {
                return;
            }
            CreateRecipe()
                .AddIngredient(ItemID.DayBreak)
                .AddIngredient(ItemID.FragmentSolar, 16)
                .AddIngredient(CWRID.Item_ShadowspecBar, 3)
                .AddIngredient(CWRID.Item_RedSun)
                .AddIngredient(CWRID.Item_DraconicDestruction)
                .AddIngredient(CWRID.Item_DragonPow)
                .AddIngredient(CWRID.Item_DragonRage)
                .AddIngredient(CWRID.Item_Rock)
                .AddEndgameStation()
                .DisableDecraft()
                .Register();
        }

        public override bool AltFunctionUse(Player player) => true;

        public override void ModifyWeaponCrit(Player player, ref float crit) => crit += 20;

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[ModContent.ProjectileType<DawnshatterHeld>()] == 0
                && player.ownedProjectileCounts[ModContent.ProjectileType<DawnshatterDash>()] == 0;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<DawnshatterDash>()
                    , (int)(damage * 2.2f), knockback * 1.5f, player.whoAmI);
                return false;
            }
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }
}
