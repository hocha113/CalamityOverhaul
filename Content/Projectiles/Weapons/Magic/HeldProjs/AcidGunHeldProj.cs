using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Magic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs
{
    internal class AcidGunHeldProj : BaseMagicGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "AcidGun";
        public override int targetCayItem => ModContent.ItemType<AcidGun>();
        public override int targetCWRItem => ModContent.ItemType<AcidGunEcType>();

        private int fireIndex;
        public override void SetMagicProperty() {
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 15;
            HandDistanceY = 0;
            HandFireDistance = 15;
            HandFireDistanceY = -5;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
        }

        public override void FiringShoot() {
            Item.useTime = 6;
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedByRandom(0.2f)
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            if (++fireIndex > 6) {
                Item.useTime = 45;
                fireIndex = 0;
            }
        }
    }
}
