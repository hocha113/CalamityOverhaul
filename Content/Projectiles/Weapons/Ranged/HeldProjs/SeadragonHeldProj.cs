using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class SeadragonHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Seadragon";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.Seadragon>();
        public override int targetCWRItem => ModContent.ItemType<Seadragon>();
        public override void SetRangedProperty() {
            ControlForce = 0.06f;
            GunPressure = 0.2f;
            Recoil = 1.2f;
        }
        public override void FiringIncident() {
            base.FiringIncident();
        }

        public override void FiringShoot() {
            Vector2 gundir = Projectile.rotation.ToRotationVector2();

            Projectile.NewProjectile(Owner.parent(), Projectile.Center + gundir * 3, ShootVelocity
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);

            Projectile.NewProjectile(Owner.parent(), Projectile.Center + gundir * 3, ShootVelocity, ModContent.ProjectileType<ArcherfishShot>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);

            Projectile.NewProjectile(Owner.parent(), Projectile.Center + gundir * 3
                , ShootVelocity.RotatedByRandom(MathHelper.ToRadians(5f)) * Main.rand.NextFloat(1.45f, 1.65f)
                , ModContent.ProjectileType<ArcherfishRing>()
                , WeaponDamage, WeaponKnockback, Owner.whoAmI);

            _ = UpdateConsumeAmmo();
            _ = CreateRecoil();
        }
    }
}
