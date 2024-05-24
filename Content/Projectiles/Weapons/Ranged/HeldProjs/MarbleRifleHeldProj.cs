using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged.Extras;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class MarbleRifleHeldProj : GraniteRifleHeldProj
    {
        public override string Texture => CWRConstant.Item_Ranged + "MarbleRifle";
        public override int targetCayItem => ModContent.ItemType<MarbleRifle>();
        public override int targetCWRItem => ModContent.ItemType<MarbleRifle>();
        public override void SetRangedProperty() {
            ForcedConversionTargetAmmoFunc = () => AmmoTypes == ProjectileID.Bullet;
            ToTargetAmmo = ModContent.ProjectileType<MarbleBullet>();
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Handgun;
            FireTime = 25;
            HandDistance = 22;
            HandFireDistance = 22;
            Recoil = 0.6f;
        }
    }
}
