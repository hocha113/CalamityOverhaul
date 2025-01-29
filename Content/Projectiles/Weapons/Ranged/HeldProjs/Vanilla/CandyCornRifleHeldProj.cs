using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    /// <summary>
    /// 玉米糖步枪
    /// </summary>
    internal class CandyCornRifleHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.CandyCornRifle].Value;
        public override int TargetID => ItemID.CandyCornRifle;
        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 5;
            HandIdleDistanceX = 15;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 15;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = -2;
            ShootPosToMouLengValue = 5;
            RepeatedCartridgeChange = true;
            GunPressure = 0.1f;
            ControlForce = 0.05f;
            Recoil = 0.2f;
            RangeOfStress = 25;
            EnableRecoilRetroEffect = true;
            CanCreateCaseEjection = false;
            SpwanGunDustMngsData.dustID1 = DustID.YellowTorch;
            SpwanGunDustMngsData.dustID2 = DustID.YellowTorch;
            SpwanGunDustMngsData.dustID3 = DustID.YellowTorch;
            RecoilRetroForceMagnitude = 4;
            SpwanGunDustMngsData.splNum = 0.6f;
            LoadingAA_None.Roting = 30;
            LoadingAA_None.gunBodyX = 3;
            LoadingAA_None.gunBodyY = 10;
            if (!MagazineSystem) {
                FireTime += 1;
            }
        }

        public override void FiringShoot() {
            int proj = Projectile.NewProjectile(Source, ShootPos, ShootVelocity
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].extraUpdates += 1;
            Main.projectile[proj].timeLeft += 30;
            Main.projectile[proj].netUpdate = true;
        }
    }
}
