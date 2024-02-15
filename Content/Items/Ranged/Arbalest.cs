using CalamityMod;
using CalamityMod.Items;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    /// <summary>
    /// 劲弩
    /// </summary>
    internal class Arbalest : EctypeItem
    {
        private int totalProjectiles = 1;

        private float arrowScale = 0.5f;

        public new string LocalizationCategory => "Items.Weapons.Ranged";

        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Arbalest";

        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        }

        public override void SetDefaults() {
            Item.damage = 28;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 82;
            Item.height = 34;
            Item.useTime = 7;
            Item.useAnimation = 7;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.knockBack = 4f;
            Item.value = CalamityGlobalItem.Rarity5BuyPrice;
            Item.rare = ItemRarityID.Pink;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<ArbalestHeldProj>();
            Item.shootSpeed = 12f;
            Item.useAmmo = AmmoID.Arrow;
            Item.Calamity().canFirePointBlankShots = true;
            
        }

        public override void ModifyWeaponCrit(Player player, ref float crit) {
            crit += 20f;
        }

        public override void HoldItem(Player player) {
            Item.initialize();
            Projectile heldProj = CWRUtils.GetProjectileInstance((int)Item.CWR().ai[0]);
            if (heldProj != null && heldProj.type == ModContent.ProjectileType<ArbalestHeldProj>()) {
                heldProj.localAI[1] = Item.CWR().ai[1];
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Item.initialize();
            Item.CWR().ai[1] = type;
            if (player.ownedProjectileCounts[ModContent.ProjectileType<ArbalestHeldProj>()] <= 0) {
                Item.CWR().ai[0] = Projectile.NewProjectile(source, position, Vector2.Zero
                , ModContent.ProjectileType<ArbalestHeldProj>()
                , damage, knockback, player.whoAmI);
                if (player.altFunctionUse == 2) {
                    Main.projectile[(int)Item.CWR().ai[0]].ai[0] = 1;
                }
            }
            return false;
        }

        public override bool AltFunctionUse(Player player) {
            return true;
        }
    }
}
