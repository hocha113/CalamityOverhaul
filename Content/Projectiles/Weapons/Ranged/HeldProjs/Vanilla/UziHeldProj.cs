using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class UziHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.Uzi].Value;
        public override int targetCayItem => ItemID.Uzi;
        public override int targetCWRItem => ItemID.Uzi;
        public override void SetRangedProperty() {
            FireTime = 6;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = -8;
            HandDistance = 20;
            HandDistanceY = 5;
            GunPressure = 0.15f;
            ControlForce = 0.05f;
            Recoil = 0.22f;
            RepeatedCartridgeChange = true;
            kreloadMaxTime = 45;
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(30, 0, 13);
        }

        public override bool KreLoadFulfill() {
            return true;
        }

        public override void FiringShoot() {
            if(AmmoTypes == ProjectileID.Bullet) {
                AmmoTypes = ProjectileID.BulletHighVelocity;
            }
            base.FiringShoot();
        }
    }
}
