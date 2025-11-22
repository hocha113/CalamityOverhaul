using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class MegalodonHeld : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Megalodon";
        public override void SetRangedProperty() {
            KreloadMaxTime = 90;
            FireTime = 4;
            HandIdleDistanceX = 22;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 22;
            HandFireDistanceY = -8;
            ShootPosNorlLengValue = -5;
            ShootPosToMouLengValue = 20;
            RepeatedCartridgeChange = true;
            MustConsumeAmmunition = false;
            GunPressure = 0.06f;
            ControlForce = 0.05f;
            Recoil = 0.22f;
            RangeOfStress = 25;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 4;
            SpwanGunDustData.splNum = 0.6f;
        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Source, ShootPos, ShootVelocity
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }

        public override void PostFiringShoot() {
            if (++fireIndex > 3) {
                int proj = Projectile.NewProjectile(Source, ShootPos, ShootVelocity
                    , CWRID.Proj_MiniSharkron
                    , WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj].MaxUpdates *= 2;
                Main.projectile[proj].SetAllProjectilesHome(true);
                fireIndex = 0;
            }
        }
    }
}
