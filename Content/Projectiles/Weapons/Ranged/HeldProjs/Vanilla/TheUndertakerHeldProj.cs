using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class TheUndertakerHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.TheUndertaker].Value;
        public override int targetCayItem => ItemID.TheUndertaker;
        public override int targetCWRItem => ItemID.TheUndertaker;
        private int bulletNum {
            get => Item.CWR().NumberBullets;
            set => Item.CWR().NumberBullets = value;
        }
        public override void SetRangedProperty() {
            kreloadMaxTime = 40;
            FireTime = 20;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 15;
            HandDistanceY = 0;
            RepeatedCartridgeChange = true;
            GunPressure = 0.8f;
            ControlForce = 0.05f;
            Recoil = 1.2f;
        }

        public override bool WhetherStartChangingAmmunition() {
            return base.WhetherStartChangingAmmunition() && bulletNum < Item.CWR().AmmoCapacity && !onFire;
        }

        public override bool KreLoadFulfill() {
            bulletNum = Item.CWR().AmmoCapacity;
            if (Item.CWR().AmmoCapacityInFire) {
                Item.CWR().AmmoCapacityInFire = false;
            }
            return true;
        }

        public override void PostFiringShoot() {
            bulletNum--;
        }
    }
}
