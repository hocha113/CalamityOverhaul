using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class ClockworkAssaultRifleHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.ClockworkAssaultRifle].Value;
        public override int targetCayItem => ItemID.ClockworkAssaultRifle;
        public override int targetCWRItem => ItemID.ClockworkAssaultRifle;

        private int thisNeedsTime;
        private int chargeAmmoNum;
        public override void SetRangedProperty() {
            FireTime = 5;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 15;
            HandDistanceY = 0;
            GunPressure = 0.1f;
            ControlForce = 0.05f;
            Recoil = 0.8f;
            RangeOfStress = 48;
            RepeatedCartridgeChange = true;
            kreloadMaxTime = 45;
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(30, 0, 13);
        }

        public override void PostInOwnerUpdate() {
        }

        public override void PostFiringShoot() {
            FireTime = 5;
            chargeAmmoNum++;
            if (chargeAmmoNum >= 3) {
                FireTime = 20;
                chargeAmmoNum = 0;
            }
        }
    }
}
