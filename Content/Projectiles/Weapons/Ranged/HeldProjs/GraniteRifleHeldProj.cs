using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged.Extras;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class GraniteRifleHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Item_Ranged + "GraniteRifle";
        public override int targetCayItem => ModContent.ItemType<GraniteRifle>();
        public override int targetCWRItem => ModContent.ItemType<GraniteRifle>();
        private float oldSetRoting;
        public override void SetRangedProperty() {
            ForcedConversionTargetAmmoFunc = () => AmmoTypes == ProjectileID.Bullet;
            ToTargetAmmo = ModContent.ProjectileType<GraniteBullet>();
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Handgun;
            FireTime = 25;
            HandDistance = 22;
            HandFireDistance = 22;
            Recoil = 0.6f;
        }

        public override void PostInOwnerUpdate() {
            if (ShootCoolingValue <= 0) {
                oldSetRoting = GunOnFireRot;
            }
            if (ShootCoolingValue == FireTime / 2) {
                SoundEngine.PlaySound(CWRSound.Case with { Volume = 0.5f, PitchRange = (-0.05f, 0.05f) }, Projectile.Center);
                CaseEjection();
            }
        }

        public override float GetGunInFireRot() {
            return kreloadTimeValue == 0 ? oldSetRoting : GetGunBodyRotation();
        }
    }
}
