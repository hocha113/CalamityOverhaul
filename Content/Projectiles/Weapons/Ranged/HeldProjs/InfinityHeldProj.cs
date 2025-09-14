using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.RangedModify.Core;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class InfinityHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Infinity";
        public override int TargetID => ModContent.ItemType<Infinity>();
        public override void SetRangedProperty() {
            KreloadMaxTime = 95;
            FireTime = 2;
            HandIdleDistanceX = 25;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 25;
            HandFireDistanceY = -10;
            ShootPosNorlLengValue = 0;
            ShootPosToMouLengValue = 14;
            SpwanGunDustData.dustID1 = DustID.AncientLight;
            SpwanGunDustData.dustID2 = DustID.AncientLight;
            SpwanGunDustData.dustID3 = DustID.AncientLight;
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
