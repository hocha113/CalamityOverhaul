using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class MusketHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.Musket].Value;
        public override int targetCayItem => ItemID.Musket;
        public override int targetCWRItem => ItemID.Musket;
        public override void SetRangedProperty() {
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandIdleDistanceX = 20;
            HandIdleDistanceY = 0;
            HandFireDistanceX = 16;
            GunPressure = 0.6f;
            ControlForce = 0.1f;
            Recoil = 2.8f;
            RangeOfStress = 48;
            CanCreateCaseEjection = false;
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Handgun;
            LoadingAA_Handgun.clipOut = CWRSound.Gun_Musket_ClipOut with { Volume = 0.65f };
            LoadingAA_Handgun.slideInShoot = CWRSound.Gun_HandGun_SlideInShoot with { Pitch = -0.3f, Volume = 0.55f };
            InOwner_HandState_AlwaysSetInFireRoding = true;
            LoadingAmmoAnimation_AlwaysSetInFireRoding = true;
            if (!MagazineSystem) {
                FireTime += 36;
            }
        }
    }
}
