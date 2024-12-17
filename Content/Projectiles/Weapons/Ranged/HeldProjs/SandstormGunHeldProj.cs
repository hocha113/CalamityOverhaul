using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class SandstormGunHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "SandstormGun";
        public override int targetCayItem => ModContent.ItemType<SandstormGun>();
        public override int targetCWRItem => ModContent.ItemType<SandstormGunEcType>();

        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
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
            SpwanGunDustMngsData.dustID1 = Dust.SandStormCount;
            SpwanGunDustMngsData.dustID2 = Dust.SandStormCount;
            SpwanGunDustMngsData.dustID3 = Dust.SandStormCount;
        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Source, new Vector2(GunShootPos.X, Main.MouseWorld.Y), new Vector2(ShootVelocity.X, 0)
                , ModContent.ProjectileType<SandnadoOnSpan>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
