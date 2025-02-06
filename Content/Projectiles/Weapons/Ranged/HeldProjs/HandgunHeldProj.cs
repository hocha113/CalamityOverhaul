using CalamityOverhaul.Content.RangedModify.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class HandgunHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.Handgun].Value;
        public override int TargetID => ItemID.Handgun;
        public override void SetRangedProperty() {
            FireTime = 10;
            HandIdleDistanceX = 16;
            HandIdleDistanceY = 2;
            HandFireDistanceX = 18;
            HandFireDistanceY = 0;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = -8;
            SpwanGunDustMngsData.splNum = 0.4f;
            GunPressure = 0.2f;
            ControlForce = 0.05f;
            Recoil = 0.8f;
            Onehanded = true;
            InOwner_HandState_AlwaysSetInFireRoding = true;
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Handgun;
        }
    }
}
