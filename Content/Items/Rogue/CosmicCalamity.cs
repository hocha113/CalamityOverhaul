using CalamityOverhaul.Content.Projectiles.Weapons.Rogue;
using InnoVault.GameSystem;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using static Terraria.ID.ContentSamples.CreativeHelper;

namespace CalamityOverhaul.Content.Items.Rogue
{
    internal class CosmicCalamity : ModItem
    {
        public override string Texture => CWRConstant.Item + "Rogue/CosmicCalamity";
        public override void SetDefaults() {
            Item.width = 44;
            Item.damage = 482;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.useAnimation = Item.useTime = 15;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 4f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.height = 44;
            Item.value = Item.buyPrice(0, 25, 5, 5);
            Item.rare = ItemRarityID.Pink;
            Item.shoot = ModContent.ProjectileType<CosmicCalamityProjectile>();
            Item.shootSpeed = 12f;
            Item.DamageType = CWRRef.GetRogueDamageClass();
            ItemOverride.ItemMeleePrefixDic[Type] = true;
            ItemOverride.ItemRangedPrefixDic[Type] = false;
        }

        public override void ModifyResearchSorting(ref ItemGroup itemGroup) => itemGroup = (ItemGroup)CWRID.ItemGroup_RogueWeapon;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.GetPlayerStealthStrikeAvailable()) {
                int proj = Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<CosmicCalamityProjectile>(), damage * 2, knockback, player.whoAmI);
                Main.projectile[proj].SetProjStealthStrike(true);
                Main.projectile[proj].timeLeft = 600;
                Main.projectile[proj].scale = 1.2f;
                return false;
            }
            return base.Shoot(player, source, position, velocity, type, damage, knockback);
        }

        public override void AddRecipes() {
            CreateRecipe().
                AddIngredient(CWRID.Item_CosmiliteBar, 12).
                AddIngredient(CWRID.Item_WaveSkipper).
                AddTile(CWRID.Tile_CosmicAnvil).
                Register();
        }
    }
}
