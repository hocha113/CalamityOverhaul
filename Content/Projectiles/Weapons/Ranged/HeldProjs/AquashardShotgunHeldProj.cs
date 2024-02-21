using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Terraria.ID;
using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class AquashardShotgunHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "AquashardShotgun";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.AquashardShotgun>();
        public override int targetCWRItem => ModContent.ItemType<AquashardShotgun>();
        public override void SetRangedProperty() {
            ControlForce = 0.1f;
            GunPressure = 0.52f;
            Recoil = 4;
        }

        public override void FiringIncident() {
            base.FiringIncident();
        }

        public override void FiringShoot() {
            if (AmmoTypes == ProjectileID.Bullet) {
                AmmoTypes = ModContent.ProjectileType<Aquashard>();
            }
            for (int i = 0; i < 4; i++) {
                Projectile.NewProjectile(Owner.parent(), Projectile.Center, ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.1f, 0.1f))
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }

            _ = UpdateConsumeAmmo();
            _ = CreateRecoil();
        }
    }
}
