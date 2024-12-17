using CalamityMod.Items.Weapons.Magic;
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
            ShootPosToMouLengValue = -20;
            ShootPosNorlLengValue = 0;
            HandFireDistanceX = 16;
            HandFireDistanceY = -4;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
            Onehanded = true;
            InOwner_HandState_AlwaysSetInFireRoding = true;
        }

        public override void SetShootAttribute() {
            Item.useTime = 6;
            if (++fireIndex > 6) {
                Item.useTime = 45;
                fireIndex = 0;
            }
        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedByRandom(0.2f)
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
