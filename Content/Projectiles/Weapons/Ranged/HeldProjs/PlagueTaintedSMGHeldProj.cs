using CalamityMod;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class PlagueTaintedSMGHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "PlagueTaintedSMG";
        public override int TargetID => ModContent.ItemType<PlagueTaintedSMG>();
        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 6;
            HandIdleDistanceX = 25;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 25;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = -8;
            ShootPosToMouLengValue = 30;
            GunPressure = 0.06f;
            ControlForce = 0.03f;
            Recoil = 0.2f;
            RangeOfStress = 25;
            CanRightClick = true;
        }

        public override void PreInOwner() {
            if (MagazineSystem) {
                CanRightClick = BulletNum >= 6;
                if (DownRight && BulletNum < 6) {
                    SetAutomaticCartridgeChange(true);
                }
            }
            else {
                CanRightClick = true;
            }
        }

        public override void SetShootAttribute() {
            if (onFire) {
                FireTime = 6;
                GunPressure = 0.1f;
                Recoil = 0.2f;
                Item.UseSound = CWRSound.Gun_SMG_Shoot with { Pitch = -0.2f, Volume = 0.35f };
            }
            else if (onFireR) {
                FireTime = 55;
                GunPressure = 0.5f;
                Recoil = 1.2f;
                Item.UseSound = SoundID.Item61;
            }
        }

        public override void FiringShoot() {
            OffsetPos -= ShootVelocity.UnitVector() * 4;
            Projectile.NewProjectile(Source, ShootPos, ShootVelocity
                , ModContent.ProjectileType<PlagueTaintedProjectile>()
                , WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }

        public override void FiringShootR() {
            OffsetPos -= ShootVelocity.UnitVector() * 6;
            for (int i = 0; i < 3; i++) {
                Projectile.NewProjectile(Source2, ShootPos, ShootVelocity.RotatedBy(-0.15f * (i + 1))
                    , ModContent.ProjectileType<PlagueTaintedDrone>(), WeaponDamage, WeaponKnockback
                    , Owner.whoAmI, 1f, Owner.Calamity().alchFlask || Owner.Calamity().spiritOrigin ? 1f : 0f);
                Projectile.NewProjectile(Source2, ShootPos, ShootVelocity.RotatedBy(0.15f * (i + 1))
                    , ModContent.ProjectileType<PlagueTaintedDrone>(), WeaponDamage, WeaponKnockback
                    , Owner.whoAmI, 1f, Owner.Calamity().alchFlask || Owner.Calamity().spiritOrigin ? 1f : 0f);
            }
            for (int i = 0; i < 5; i++) {
                UpdateMagazineContents();
            }
        }
    }
}
