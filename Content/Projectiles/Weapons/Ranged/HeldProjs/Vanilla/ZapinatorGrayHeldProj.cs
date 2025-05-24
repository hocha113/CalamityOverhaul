using CalamityOverhaul.Content.Projectiles.Weapons.Magic.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class ZapinatorGrayHeldProj : BaseMagicGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.ZapinatorGray].Value;
        public override int TargetID => ItemID.ZapinatorGray;
        public override void SetMagicProperty() {
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandIdleDistanceX = 15;
            HandIdleDistanceY = 0;
            GunPressure = 0.3f;
            ControlForce = 0.05f;
            RangeOfStress = 48;
            Recoil = 0;
            Onehanded = true;
            InOwner_HandState_AlwaysSetInFireRoding = true;
        }
    }
}
