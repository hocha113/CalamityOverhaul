using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class VortexpopperHeld : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Vortexpopper";
        public override void SetRangedProperty() {
            FireTime = 12;
            ControlForce = 0.03f;
            GunPressure = 0.1f;
            Recoil = 1;
            HandIdleDistanceX = 27;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 25;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = -5;
            CanRightClick = true;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 8;
            SpwanGunDustData.dustID1 = DustID.Water;
            SpwanGunDustData.dustID2 = DustID.Water;
            SpwanGunDustData.dustID3 = DustID.Water;
        }

        public override void SetShootAttribute() {
            if (onFire) {
                FireTime = 12;
                Recoil = 1;
                GunPressure = 0.2f;
                EnableRecoilRetroEffect = false;
            }
            else if (onFireR) {
                FireTime = 4;
                Recoil = 0.5f;
                GunPressure = 0.1f;
                EnableRecoilRetroEffect = true;
            }
        }

        public override void FiringShoot() {

            for (int i = 0; i < 5; i++) {
                Projectile proj = Projectile.NewProjectileDirect(Source, ShootPos,
                    ShootVelocity.RotatedBy(Main.rand.NextFloat(-1.1f, 1.1f)) * Main.rand.NextFloat(0.7f, 1.5f)
                    , ModContent.ProjectileType<XenopopperProj>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 1);
                proj.localAI[0] = AmmoTypes;
                proj.localAI[1] = 12f;
            }
        }

        public override void FiringShootR() {

            _ = Projectile.NewProjectileDirect(Source, ShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Projectile proj = Projectile.NewProjectileDirect(Source, Main.MouseWorld + VaultUtils.RandVr(130, 160), ShootVelocity / 3
                    , ModContent.ProjectileType<XenopopperProj>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            proj.localAI[0] = AmmoTypes;
            proj.localAI[1] = 22;
        }
    }
}
