using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class MolecularManipulatorHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "MolecularManipulator";
        public override int targetCayItem => ModContent.ItemType<MolecularManipulator>();
        public override int targetCWRItem => ModContent.ItemType<MolecularManipulatorEcType>();

        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 15;
            HandIdleDistanceX = 25;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 25;
            HandFireDistanceY = -4;
            ShootPosNorlLengValue = 0;
            ShootPosToMouLengValue = 0;
            RepeatedCartridgeChange = true;
            GunPressure = 0.1f;
            ControlForce = 0.05f;
            Recoil = 1.2f;
            RangeOfStress = 25;
            AmmoTypeAffectedByMagazine = false;
            CanCreateCaseEjection = CanCreateSpawnGunDust = false;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 6;
        }

        public override void FiringShoot() {
            for (int index = 0; index < 2; ++index) {
                Vector2 velocity = ShootVelocity;
                velocity.X += Main.rand.Next(-10, 11) * 0.05f;
                velocity.Y += Main.rand.Next(-10, 11) * 0.05f;
                Projectile.NewProjectile(Source, GunShootPos, velocity, AmmoTypes
                    , WeaponDamage / 2, WeaponKnockback, Owner.whoAmI, 0);
            }
        }
    }
}
