using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class ButcherHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Butcher";
        public override int targetCayItem => ModContent.ItemType<Butcher>();
        public override int targetCWRItem => ModContent.ItemType<ButcherEcType>();
        public override void SetRangedProperty() {
            FireTime = 30;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 17;
            HandDistanceY = 4;
            HandFireDistance = 15;
            ShootPosNorlLengValue = -18;
            ShootPosToMouLengValue = 15;
            GunPressure = 0.1f;
            ControlForce = 0.05f;
            Recoil = 0.5f;
            RangeOfStress = 28;
            RepeatedCartridgeChange = true;
            kreloadMaxTime = 60;
        }

        public override bool KreLoadFulfill() {
            FireTime = 30;
            return true;
        }

        public override void FiringShoot() {
            float randomMode = FireTime * 0.006f;
            for (int i = 0; i < 3; i++) {
                Vector2 ver = ShootVelocity.RotatedBy(Main.rand.NextFloat(-randomMode, randomMode)) * Main.rand.NextFloat(0.6f, 1.52f) * 0.3f;
                Projectile.NewProjectile(Source, GunShootPos, ver, AmmoTypes, WeaponDamage, WeaponKnockback * 1.5f, Owner.whoAmI, 0);
            } 
        }

        public override void PostFiringShoot() {
            FireTime--;
            if (FireTime < 12) {
                FireTime = 12;
            }
        }
    }
}
