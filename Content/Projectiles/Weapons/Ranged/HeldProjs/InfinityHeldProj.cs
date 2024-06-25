using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class InfinityHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Infinity";
        public override int targetCayItem => ModContent.ItemType<Infinity>();
        public override int targetCWRItem => ModContent.ItemType<InfinityEcType>();

        public override void SetRangedProperty() {
            kreloadMaxTime = 95;
            FireTime = 2;
            HandDistance = 25;
            HandDistanceY = 5;
            HandFireDistance = 25;
            HandFireDistanceY = -10;
            ShootPosNorlLengValue = 0;
            ShootPosToMouLengValue = 14;
            SpwanGunDustMngsData.dustID1 = DustID.AncientLight;
            SpwanGunDustMngsData.dustID2 = DustID.AncientLight;
            SpwanGunDustMngsData.dustID3 = DustID.AncientLight;
            RepeatedCartridgeChange = true;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0.1f;
            RangeOfStress = 25;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 3;
        }

        public override void FiringShoot() {
            float sengs = MathF.Sin(Time * 0.1f) * 0.2f;
            int value = 1;
            if (AmmoTypes == ProjectileID.Bullet) {
                AmmoTypes = ModContent.ProjectileType<ChargedBlast>();
            }
            else {
                Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                value = 2;
            }
            Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity.RotatedBy(sengs), AmmoTypes, WeaponDamage / value, WeaponKnockback, Owner.whoAmI, 0);
            Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity.RotatedBy(-sengs), AmmoTypes, WeaponDamage / value, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
