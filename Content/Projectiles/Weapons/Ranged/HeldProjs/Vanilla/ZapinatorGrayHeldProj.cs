using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class ZapinatorGrayHeldProj : BaseMagicGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.ZapinatorGray].Value;
        public override int targetCayItem => ItemID.ZapinatorGray;
        public override int targetCWRItem => ItemID.ZapinatorGray;
        public override void SetMagicProperty() {
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 15;
            HandDistanceY = 0;
            GunPressure = 0.8f;
            ControlForce = 0.05f;           
            RangeOfStress = 48;
            Recoil = 4.8f;
        }

        public override int Shoot() {
            return base.Shoot();
        }
    }
}
