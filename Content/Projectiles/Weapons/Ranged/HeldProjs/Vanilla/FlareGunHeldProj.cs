using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class FlareGunHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.FlareGun].Value;
        public override int targetCayItem => ItemID.FlareGun;
        public override int targetCWRItem => ItemID.FlareGun;
        public override void SetRangedProperty() {
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 17;
            HandDistanceY = 0;
            HandFireDistance = 17;
            GunPressure = 0.8f;
            ControlForce = 0.05f;
            Recoil = 4.8f;
            RangeOfStress = 48;
        }
    }
}
