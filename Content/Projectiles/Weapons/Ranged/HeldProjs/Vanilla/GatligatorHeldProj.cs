using CalamityOverhaul.Common;
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
        public override int targetCayItem => ItemID.Gatligator;
        public override int targetCWRItem => ItemID.Gatligator;
        public override void SetRangedProperty() {
            kreloadMaxTime = 100;
            FireTime = 4;
            HandDistance = 18;
            HandDistanceY = 0;
            HandFireDistance = 18;
            HandFireDistanceY = -10;
            ShootPosNorlLengValue = -2;
            ShootPosToMouLengValue = 10;
            RepeatedCartridgeChange = true;
            EnableRecoilRetroEffect = true;
            GunPressure = 0.1f;
            ControlForce = 0.05f;
            Recoil = 0.6f;
            RangeOfStress = 25;
            SpwanGunDustMngsData.splNum = 0.35f;
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(50, 3, 15);
        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Source, GunShootPos
                    , ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.12f, 0.12f)) * Main.rand.NextFloat(0.6f, 1.52f)
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
