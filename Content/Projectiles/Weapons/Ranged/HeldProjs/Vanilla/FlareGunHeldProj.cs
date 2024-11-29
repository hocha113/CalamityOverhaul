using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    /// <summary>
    /// 信号枪
    /// </summary>
    internal class FlareGunHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.FlareGun].Value;
        public override int targetCayItem => ItemID.FlareGun;
        public override int targetCWRItem => ItemID.FlareGun;
        public override void SetRangedProperty() {
            kreloadMaxTime = 32;
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = -4;
            HandDistance = 17;
            HandDistanceY = 0;
            HandFireDistance = 17;
            GunPressure = 0.3f;
            ControlForce = 0.03f;
            Recoil = 1.8f;
            RangeOfStress = 48;
            CanCreateCaseEjection = false;
            SpwanGunDustMngsData.splNum = 0.3f;
            LoadingAmmoAnimation_AlwaysSetInFireRoding = true;
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Handgun;
            if (!MagazineSystem) {
                LazyRotationUpdate = true;
                FireTime += 12;
            }
        }
    }
}
