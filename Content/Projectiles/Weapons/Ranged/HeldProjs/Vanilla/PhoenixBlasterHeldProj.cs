using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class PhoenixBlasterHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.PhoenixBlaster].Value;
        public override int TargetID => ItemID.PhoenixBlaster;
        public override void SetRangedProperty() {
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = -6;
            HandIdleDistanceX = 15;
            HandIdleDistanceY = 0;
            GunPressure = 0.2f;
            ControlForce = 0.05f;
            Recoil = 1.1f;
            RangeOfStress = 8;
            Onehanded = true;
            InOwner_HandState_AlwaysSetInFireRoding = true;
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Handgun;
            SpwanGunDustMngsData.splNum = 0.5f;
            SpwanGunDustMngsData.dustID1 = DustID.FireworkFountain_Red;
        }
    }
}
