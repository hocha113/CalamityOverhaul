using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Items.Materials;
using CalamityMod.Rarities;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Tools;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.AnnihilatingUniverseProj;
using CalamityOverhaul.Content.Tiles;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.Extras
{
    internal class AnnihilatingUniverse : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "AnnihilatingUniverse";
        public new string LocalizationCategory => "Items.Weapons.Ranged";
        public override bool IsLoadingEnabled(Mod mod) {
            if (!CWRServerConfig.Instance.AddExtrasContent) {
                return false;
            }
            return base.IsLoadingEnabled(mod);
        }

        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        }

        public override bool AltFunctionUse(Player player) {
            return true;
        }

        public override void SetDefaults() {
            Item.damage = 1050;
            Item.width = 62;
            Item.height = 128;
            Item.useTime = 12;
            Item.useAnimation = 12;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 4f;
            Item.UseSound = SoundID.Item5;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.DamageType = DamageClass.Ranged;
            Item.channel = true;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<AnnihilatingUniverseHeldProj>();
            Item.shootSpeed = 20f;
            Item.useAmmo = AmmoID.Arrow;
            Item.value = CalamityGlobalItem.RarityPureGreenBuyPrice;
            Item.rare = ModContent.RarityType<PureGreen>();
            Item.Calamity().canFirePointBlankShots = true;
            Item.CWR().OmigaSnyContent = SupertableRecipeDate.FullItems4;
        }

        public override void ModifyWeaponCrit(Player player, ref float crit) {
            crit += 32;
        }

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[Item.shoot] <= 0;
        }

        public override bool CanConsumeAmmo(Item ammo, Player player) {
            return Main.rand.NextBool(3) && player.ownedProjectileCounts[Item.shoot] > 0;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            _ = Projectile.NewProjectile(source, position.X, position.Y, velocity.X, velocity.Y
                , ModContent.ProjectileType<AnnihilatingUniverseHeldProj>(), damage, knockback, player.whoAmI, ai2: player.altFunctionUse == 0 ? 0 : 1);
            return false;
        }

        public override void AddRecipes() {
            _ = CreateRecipe()
                .AddIngredient<CalamityMod.Items.Weapons.Ranged.Deathwind>()
                .AddIngredient<CalamityMod.Items.Weapons.Ranged.Alluvion>()
                .AddIngredient<CalamityMod.Items.Weapons.Magic.Apotheosis>()
                .AddIngredient<Rock>()
                .AddIngredient<CosmiliteBar>(11)//宇宙锭
                .AddIngredient<ShadowspecBar>(16)
                .AddIngredient<DarkMatterBall>(11)
                .AddConsumeItemCallback((Recipe recipe, int type, ref int amount) => {
                    amount = 0;
                })
                .AddOnCraftCallback(CWRRecipes.SpawnAction)
                .AddTile(ModContent.TileType<TransmutationOfMatter>())
                .Register();
        }
    }
}
