using CalamityOverhaul.Content.RangedModify.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class GatligatorHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.Gatligator].Value;
        public override int TargetID => ItemID.Gatligator;
        public override void SetRangedProperty() {
            KreloadMaxTime = 100;
            FireTime = 4;
            HandIdleDistanceX = 18;
            HandIdleDistanceY = 0;
            HandFireDistanceX = 18;
            HandFireDistanceY = -4;
            ShootPosNorlLengValue = -2;
            ShootPosToMouLengValue = 10;
            RepeatedCartridgeChange = true;
            EnableRecoilRetroEffect = true;
            GunPressure = 0.1f;
            ControlForce = 0.05f;
            Recoil = 0.6f;
            RangeOfStress = 25;
            SpwanGunDustMngsData.splNum = 0.35f;
            LoadingAA_None.Roting = 50;
            LoadingAA_None.gunBodyX = 3;
            LoadingAA_None.gunBodyY = 15;
            if (!MagazineSystem) {
                FireTime += 1;
            }
        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Source, ShootPos
                    , ShootVelocity.RotatedByRandom(0.12f) * Main.rand.NextFloat(0.6f, 1.52f)
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
