using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class PhoenixBlasterHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.PhoenixBlaster].Value;
        public override int targetCayItem => ItemID.PhoenixBlaster;
        public override int targetCWRItem => ItemID.PhoenixBlaster;
        public override void SetRangedProperty() {
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = -6;
            HandDistance = 15;
            HandDistanceY = 0;
            GunPressure = 0.2f;
            ControlForce = 0.05f;
            Recoil = 1.1f;
            RangeOfStress = 8;
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Handgun;
            SpwanGunDustMngsData.splNum = 0.5f;
            SpwanGunDustMngsData.dustID1 = DustID.FireworkFountain_Red;
        }
    }
}
