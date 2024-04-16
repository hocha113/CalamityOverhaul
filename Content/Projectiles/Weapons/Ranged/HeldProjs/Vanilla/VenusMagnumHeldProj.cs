using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    /// <summary>
    /// 维纳斯万能枪
    /// </summary>
    internal class VenusMagnumHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.VenusMagnum].Value;
        public override int targetCayItem => ItemID.VenusMagnum;
        public override int targetCWRItem => ItemID.VenusMagnum;
        public override void SetRangedProperty() {
            FireTime = 7;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = -3;
            HandDistance = 18;
            HandFireDistanceY = -5;
            HandDistanceY = 0;
            GunPressure = 0.12f;
            ControlForce = 0.05f;
            Recoil = 1.1f;
            RepeatedCartridgeChange = true;
            kreloadMaxTime = 45;
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(30, 0, 13);
        }

        public override void FiringShoot() {
            if (AmmoTypes == ProjectileID.Bullet) {
                AmmoTypes = ProjectileID.BulletHighVelocity;
            }
            base.FiringShoot();
        }
    }
}
