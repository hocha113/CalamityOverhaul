using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class SandstormGunHeld : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Sandblaster";
        public override void SetRangedProperty() {
            KreloadMaxTime = 90;
            FireTime = 15;
            HandIdleDistanceX = 25;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 25;
            HandFireDistanceY = -10;
            ShootPosNorlLengValue = -8;
            ShootPosToMouLengValue = 0;
            RepeatedCartridgeChange = true;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
            RangeOfStress = 0;
            AmmoTypeAffectedByMagazine = false;
            EnableRecoilRetroEffect = false;
            CanCreateCaseEjection = false;
            SpwanGunDustData.dustID1 = Dust.SandStormCount;
            SpwanGunDustData.dustID2 = Dust.SandStormCount;
            SpwanGunDustData.dustID3 = Dust.SandStormCount;
        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Source, new Vector2(ShootPos.X, Main.MouseWorld.Y), new Vector2(ShootVelocity.X, 0)
                , ModContent.ProjectileType<SandnadoOnSpan>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
