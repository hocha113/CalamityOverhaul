using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class SlagMagnumHeldProj : BaseFeederGun   
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "SlagMagnum";
        public override int targetCayItem => ModContent.ItemType<SlagMagnum>();
        public override int targetCWRItem => ModContent.ItemType<SlagMagnumEcType>();

        public override void SetRangedProperty() {
            kreloadMaxTime = 90;
            FireTime = 20;
            HandDistance = 15;
            HandDistanceY = 5;
            HandFireDistance = 15;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = -7;
            ShootPosToMouLengValue = 10;
            RepeatedCartridgeChange = true;
            GunPressure = 0.3f;
            ControlForce = 0.05f;
            Recoil = 2.2f;
            RangeOfStress = 25;
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Handgun;
            LoadingAA_Handgun.loadingAmmoStarg_y = -12;
            LoadingAA_Handgun.clipLocked = CWRSound.Gun_Magnum_ClipLocked;
            LoadingAA_Handgun.clipOut = CWRSound.Gun_Magnum_ClipOut;
        }

        public override void FiringShoot() {
            EjectCasing();
            SpawnGunFireDust(dustID1: DustID.GreenTorch, dustID2: DustID.GreenMoss);
            if (AmmoTypes == ProjectileID.Bullet) {
                AmmoTypes = Item.shoot;
            }
            Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
