using CalamityOverhaul.Common;
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
            FireTime = 8;
            HandDistance = 16;
            HandDistanceY = -1;
            HandFireDistance = 18;
            HandFireDistanceY = -5;
            ShootPosToMouLengValue = 16;
            ShootPosNorlLengValue = -13;
            GunPressure = 0.2f;
            ControlForce = 0.05f;
            Recoil = 0.8f;
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Handgun;
        }

        public override void FiringShoot() {
            base.FiringShoot();
            CaseEjection();
        }
    }
}
