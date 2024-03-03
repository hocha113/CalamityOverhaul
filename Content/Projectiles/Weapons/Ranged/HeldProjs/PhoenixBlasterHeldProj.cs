using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class PhoenixBlasterHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.PhoenixBlaster].Value;
        public override int targetCayItem => ItemID.PhoenixBlaster;
        public override int targetCWRItem => ItemID.PhoenixBlaster;
        public override void SetRangedProperty() {
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 15;
            HandDistanceY = 0;
            GunPressure = 0.2f;
            ControlForce = 0.05f;
            Recoil = 1.2f;
            RangeOfStress = 8;
            CanRightClick = true;
        }

        public override void FiringIncident() {
            base.FiringIncident();
            if (onFireR) {
                heldItem.useTime = 36;
            }
            else {
                heldItem.useTime = 12;
            }
        }

        public override void FiringShoot() {
            base.FiringShoot();
            SpawnGunDust(GunShootPos, ShootVelocity, dustID1: 174, dustID2: 213, dustID3: 213);
        }

        public override void FiringShootR() {
            for (int i = 0; i < 3; i++) {
                SpawnGunDust(GunShootPos, ShootVelocity, dustID1: 174, dustID2: 213, dustID3: 213);
                Projectile.NewProjectile(Owner.parent(), GunShootPos, ShootVelocity.RotatedBy(MathHelper.Lerp(-0.05f, 0.05f, i / 2f)) * 2f, ModContent.ProjectileType<HellfireBullet>(), WeaponDamage / 2, WeaponKnockback, Owner.whoAmI, 0);
                _ = UpdateConsumeAmmo();
                _ = CreateRecoil();
            }
        }
    }
}
