using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Rarities;
using CalamityMod.Sounds;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 巨龙之怒
    /// </summary>
    internal class DragonRageEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "DragonRage";

        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        }

        public override void SetDefaults() {
            Item.damage = 1075;
            Item.knockBack = 7.5f;
            Item.useAnimation = (Item.useTime = 25);
            Item.DamageType = ModContent.GetInstance<TrueMeleeNoSpeedDamageClass>();
            Item.noMelee = true;
            Item.channel = true;
            Item.autoReuse = true;
            Item.shootSpeed = 14f;
            Item.shoot = ModContent.ProjectileType<RDragonRageStaff>();
            Item.width = 128;
            Item.height = 140;
            Item.noUseGraphic = true;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.UseSound = SoundID.DD2_SkyDragonsFurySwing;
            Item.value = CalamityGlobalItem.RarityVioletBuyPrice;
            Item.rare = ModContent.RarityType<Violet>();

        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            int proj = Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            if (player.altFunctionUse == 2) {
                SoundEngine.PlaySound(in CommonCalamitySounds.MeatySlashSound, player.Center);
                Main.projectile[proj].ai[1] = 1;
                Main.projectile[proj].scale = 0.5f;
            }
            return false;
        }

        public override bool AltFunctionUse(Player player) {
            return true;
        }

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[Item.shoot] <= 0;
        }
    }
}
