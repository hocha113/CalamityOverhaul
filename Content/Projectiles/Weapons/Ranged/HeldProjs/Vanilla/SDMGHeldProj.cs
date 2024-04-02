using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class SDMGHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.SDMG].Value;
        public override int targetCayItem => ItemID.SDMG;
        public override int targetCWRItem => ItemID.SDMG;
        public override void SetRangedProperty() {
            FireTime = 5;
            ShootPosToMouLengValue = 15;
            ShootPosNorlLengValue = 10;
            HandDistance = 15;
            HandDistanceY = 0;
            GunPressure = 0.05f;
            ControlForce = 0.05f;
            Recoil = 1f;
            RangeOfStress = 48;
            RepeatedCartridgeChange = true;
            kreloadMaxTime = 45;
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(30, 0, 13);
        }
        public override bool KreLoadFulfill() {
            return base.KreLoadFulfill();
        }

        public override void PostFiringShoot() {
        }

        public override void FiringShoot() {
            base.FiringShoot();
        }
    }
}
