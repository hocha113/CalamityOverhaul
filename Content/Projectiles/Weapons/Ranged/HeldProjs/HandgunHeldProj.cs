using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class HandgunHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.Handgun].Value;
        public override int targetCayItem => ItemID.Handgun;
        public override int targetCWRItem => ItemID.Handgun;
        public override void SetRangedProperty() {
            FireTime = 10;
            HandDistance = 16;
            HandDistanceY = -1;
            HandFireDistance = 18;
            HandFireDistanceY = -4;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = -8;
            SpwanGunDustMngsData.splNum = 0.4f;
            GunPressure = 0.2f;
            ControlForce = 0.05f;
            Recoil = 0.8f;
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Handgun;
        }
    }
}
