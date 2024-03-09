using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class GaleforceHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Galeforce";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.Galeforce>();
        public override int targetCWRItem => ModContent.ItemType<Galeforce>();
        public override void SetRangedProperty() {
            CanRightClick = true;
        }

        public override void HandEvent() {
            base.HandEvent();
            if (onFireR) {
                Item.useTime = 5;
            }
            else {
                Item.useTime = 20;
            }
        }

        public override void BowShootR() {
            AmmoTypes = ModContent.ProjectileType<FeatherLarge>();
            base.BowShootR();
        }

        public override void BowShoot() {
            base.BowShoot();
            for (int i = -8; i <= 8; i += 8) {
                Vector2 perturbedSpeed = ShootVelocity.RotatedBy(MathHelper.ToRadians(i));
                Projectile.NewProjectile(Source, Projectile.Center, perturbedSpeed, ModContent.ProjectileType<FeatherLarge>(), WeaponDamage, 0f, Owner.whoAmI);
            }
        }
    }
}
