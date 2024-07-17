using CalamityOverhaul.Content.Items.Ranged.Extras;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class GraniteRifleHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Item_Ranged + "GraniteRifle";
        public override int targetCayItem => ModContent.ItemType<GraniteRifle>();
        public override int targetCWRItem => ModContent.ItemType<GraniteRifle>();
        public override void SetRangedProperty() {
            ForcedConversionTargetAmmoFunc = () => AmmoTypes == ProjectileID.Bullet;
            ToTargetAmmo = ModContent.ProjectileType<GraniteBullet>();
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Handgun;
            LoadingAA_Handgun.loadingAmmoStarg_y = -10;
            LazyRotationUpdate = true;
            AutomaticPolishingEffect = true;
            FireTime = 25;
            HandDistance = 22;
            HandFireDistance = 22;
            Recoil = 0.6f;
            GunPressure = 0.2f;
            ControlForce = 0.01f;
        }
    }
}
