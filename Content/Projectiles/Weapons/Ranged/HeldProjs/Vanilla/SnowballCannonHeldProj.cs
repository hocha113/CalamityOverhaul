using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    /// <summary>
    /// 雪球炮
    /// </summary>
    internal class SnowballCannonHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.SnowballCannon].Value;
        public override int targetCayItem => ItemID.SnowballCannon;
        public override int targetCWRItem => ItemID.SnowballCannon;
        public override void SetRangedProperty() {
            FireTime = 40;
            kreloadMaxTime = 35;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandIdleDistanceX = 15;
            HandIdleDistanceY = 0;
            GunPressure = 0.2f;
            ControlForce = 0.05f;
            Recoil = 1.6f;
            RepeatedCartridgeChange = true;
            EnableRecoilRetroEffect = true;
            CanCreateCaseEjection = false;
            RecoilRetroForceMagnitude = 11;
            RecoilOffsetRecoverValue = 0.8f;
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Revolver;
            if (!MagazineSystem) {
                FireTime += 25;
            }
        }

        public override void HanderSpwanDust() {
            SpawnGunFireDust(GunShootPos, ShootVelocity, dustID1: 76, dustID2: 149, dustID3: 76);
            SpawnGunFireDust(GunShootPos, ShootVelocity, splNum: 2, dustID1: 76, dustID2: 149, dustID3: 76);
            SpawnGunFireDust(GunShootPos, ShootVelocity, splNum: 3, dustID1: 76, dustID2: 149, dustID3: 76);
        }

        public override void FiringShoot() {
            for (int i = 0; i < 5; i++) {
                int proj = Projectile.NewProjectile(Source2, GunShootPos, ShootVelocity.RotatedByRandom(0.1f) * Main.rand.NextFloat(0.8f, 1.1f)
                    , AmmoTypes, (int)(WeaponDamage * Main.rand.NextFloat(0.2f, 0.8f)), WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj].scale += Main.rand.NextFloat(0.3f);
            }
        }
    }
}

