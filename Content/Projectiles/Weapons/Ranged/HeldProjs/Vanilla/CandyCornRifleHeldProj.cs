using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class CandyCornRifleHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.CandyCornRifle].Value;
        public override int targetCayItem => ItemID.CandyCornRifle;
        public override int targetCWRItem => ItemID.CandyCornRifle;
        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 5;
            HandDistance = 15;
            HandDistanceY = 5;
            HandFireDistance = 15;
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
            LoadingAA_None.loadingAA_None_Roting = 30;
            LoadingAA_None.loadingAA_None_X = 3;
            LoadingAA_None.loadingAA_None_Y = 10;
        }

        public override void FiringShoot() {
            int proj = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].extraUpdates += 1;
            Main.projectile[proj].timeLeft += 30;
            Main.projectile[proj].netUpdate = true;
        }
    }
}
