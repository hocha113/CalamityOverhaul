using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ID;
using Terraria;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RSpaceGun : BaseRItem
    {
        public override int TargetID => ItemID.SpaceGun;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => item.SetHeldProj<SpaceGunHeld>();
    }

    internal class SpaceGunHeld : BaseMagicGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.SpaceGun].Value;
        public override int targetCayItem => ItemID.SpaceGun;
        public override int targetCWRItem => ItemID.SpaceGun;
        public override void SetMagicProperty() {
            ShootPosToMouLengValue = -14;
            ShootPosNorlLengValue = -2;
            HandFireDistanceX = 18;
            HandFireDistanceY = -4;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
            Onehanded = true;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 6;
            InOwner_HandState_AlwaysSetInFireRoding = true;
        }
    }
}
