using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

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
            kreloadMaxTime = 90;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 15;
            HandDistanceY = 0;
            GunPressure = 0.2f;
            ControlForce = 0.05f;
            Recoil = 1.6f;
            RepeatedCartridgeChange = true;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 11;
            RecoilOffsetRecoverValue = 0.8f;
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(30, 0, 6);
        }

        public override void FiringShoot() {
            SpawnGunFireDust(GunShootPos, ShootVelocity, dustID1: 76, dustID2: 149, dustID3: 76);
            SpawnGunFireDust(GunShootPos, ShootVelocity, splNum: 2, dustID1: 76, dustID2: 149, dustID3: 76);
            SpawnGunFireDust(GunShootPos, ShootVelocity, splNum: 3, dustID1: 76, dustID2: 149, dustID3: 76);
            for (int i = 0; i < 8; i++) {
                int proj = Projectile.NewProjectile(Source2, GunShootPos, ShootVelocity.RotatedByRandom(0.1f) * Main.rand.NextFloat(0.8f, 1.1f)
                    , AmmoTypes, (int)(WeaponDamage * Main.rand.NextFloat(0.2f, 0.8f)), WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj].scale += Main.rand.NextFloat(0.3f);
            }
        }
    }
}

