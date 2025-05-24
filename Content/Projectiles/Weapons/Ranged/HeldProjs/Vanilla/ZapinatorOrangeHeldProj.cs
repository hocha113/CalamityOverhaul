using CalamityOverhaul.Content.Projectiles.Weapons.Magic.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class ZapinatorOrangeHeldProj : BaseMagicGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.ZapinatorOrange].Value;
        public override int TargetID => ItemID.ZapinatorOrange;
        public override void SetMagicProperty() {
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandIdleDistanceX = 15;
            HandIdleDistanceY = 0;
            GunPressure = 0.25f;
            ControlForce = 0.05f;
            Recoil = 0;
            RangeOfStress = 48;
            Onehanded = true;
            InOwner_HandState_AlwaysSetInFireRoding = true;
        }
    }
}
