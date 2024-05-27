using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class VortexpopperHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Vortexpopper";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.Vortexpopper>();
        public override int targetCWRItem => ModContent.ItemType<VortexpopperEcType>();

        public override void SetRangedProperty() {
            FireTime = 12;
            ControlForce = 0.03f;
            GunPressure = 0.1f;
            Recoil = 1;
            HandDistance = 27;
            HandDistanceY = 5;
            HandFireDistance = 25;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = -5;
            CanRightClick = true;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 8;
            SpwanGunDustMngsData.dustID1 = DustID.Water;
            SpwanGunDustMngsData.dustID2 = DustID.Water;
            SpwanGunDustMngsData.dustID3 = DustID.Water;
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(50, 3, 25);
        }

        public override void FiringShoot() {
            FireTime = 12;
            Recoil = 1;
            GunPressure = 0.2f;
            EnableRecoilRetroEffect = false;
            for (int i = 0; i < 5; i++) {
                Projectile proj = Projectile.NewProjectileDirect(Source, GunShootPos,
                    ShootVelocity.RotatedBy(Main.rand.NextFloat(-1.1f, 1.1f)) * Main.rand.NextFloat(0.7f, 1.5f)
                    , ModContent.ProjectileType<XenopopperProj>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 1);
                proj.localAI[0] = AmmoTypes;
                proj.localAI[1] = 12f;
            }
        }

        public override void FiringShootR() {
            FireTime = 4;
            Recoil = 0.5f;
            GunPressure = 0.1f;
            EnableRecoilRetroEffect = true;
            _ = Projectile.NewProjectileDirect(Source, GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Projectile proj = Projectile.NewProjectileDirect(Source, Main.MouseWorld + CWRUtils.randVr(130, 160), ShootVelocity / 3
                    , ModContent.ProjectileType<XenopopperProj>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            proj.localAI[0] = AmmoTypes;
            proj.localAI[1] = 22;
        }
    }
}
