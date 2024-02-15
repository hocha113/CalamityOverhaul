using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Rarities;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    /// <summary>
    /// 死亡之风
    /// </summary>
    internal class Deathwind : EctypeItem
    {
        public new string LocalizationCategory => "Items.Weapons.Ranged";

        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Deathwind";

        public override void SetDefaults() {
            Item.damage = 248;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 40;
            Item.height = 82;
            Item.useTime = 14;
            Item.useAnimation = 14;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.knockBack = 5f;
            Item.value = CalamityGlobalItem.Rarity14BuyPrice;
            Item.rare = ModContent.RarityType<DarkBlue>();
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<DeathwindHeldProj>();
            Item.shootSpeed = 20f;
            Item.useAmmo = AmmoID.Arrow;
            Item.Calamity().canFirePointBlankShots = true;
            
        }

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, float rotation, float scale, int whoAmI) {
            Item.DrawItemGlowmaskSingleFrame(spriteBatch, rotation, ModContent.Request<Texture2D>("CalamityMod/Items/Weapons/Ranged/DeathwindGlow").Value);
        }

        public override void HoldItem(Player player) {
            Item.initialize();
            Projectile heldProj = CWRUtils.GetProjectileInstance((int)Item.CWR().ai[0]);
            if (heldProj != null && heldProj.type == ModContent.ProjectileType<DeathwindHeldProj>()) {
                heldProj.localAI[1] = Item.CWR().ai[1];
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Item.initialize();
            Item.CWR().ai[1] = type;
            if (player.ownedProjectileCounts[ModContent.ProjectileType<DeathwindHeldProj>()] <= 0) {
                Item.CWR().ai[0] = Projectile.NewProjectile(source, position, Vector2.Zero
                , ModContent.ProjectileType<DeathwindHeldProj>()
                , damage, knockback, player.whoAmI);
            }
            return false;
        }
    }
}
