using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
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
        private int bulletNum
        {
            get => heldItem.CWR().NumberBullets;
            set => heldItem.CWR().NumberBullets = value;
        }
        public override void SetRangedProperty()
        {
            kreloadMaxTime = 40;
            fireTime = 20;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 15;
            HandDistanceY = 0;
            RepeatedCartridgeChange = true;
            GunPressure = 0.8f;
            ControlForce = 0.05f;
            Recoil = 1.2f;
        }

        public override bool WhetherStartChangingAmmunition()
        {
            return base.WhetherStartChangingAmmunition() && bulletNum < heldItem.CWR().AmmoCapacity && !onFire;
        }

        public override void OnKreLoad()
        {
            bulletNum = heldItem.CWR().AmmoCapacity;
            if (heldItem.CWR().AmmoCapacityInFire)
            {
                heldItem.CWR().AmmoCapacityInFire = false;
            }
        }

        public override void PostFiringShoot()
        {
            bulletNum--;
        }
    }
}
