using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class StormDragoonHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "StormDragoon";
        public override int targetCayItem => ModContent.ItemType<StormDragoon>();
        public override int targetCWRItem => ModContent.ItemType<StormDragoonEcType>();

        public override void SetRangedProperty() {
            kreloadMaxTime = 95;
            FireTime = 6;
            HandDistance = 15;
            HandDistanceY = 3;
            HandFireDistance = 15;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = -2;
            ShootPosToMouLengValue = 0;
            RepeatedCartridgeChange = true;
            GunPressure = 0.05f;
            ControlForce = 0.05f;
            Recoil = 1.2f;
            RangeOfStress = 25;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 3;
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(50, 3, 25);
        }

        public override void FiringShoot() {
            SpawnGunFireDust();
            for (int i = 0; i < 4; i++) {
                Projectile.NewProjectile(Source, GunShootPos
                    , ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.1f, 0.1f)) * Main.rand.NextFloat(0.9f, 1.32f)
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
        }
    }
}
