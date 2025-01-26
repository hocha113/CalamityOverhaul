using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Content.Items.Magic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs
{
    internal class LazharHeldProj : BaseMagicGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "Lazhar";
        public override int targetCayItem => ModContent.ItemType<Lazhar>();
        public override int targetCWRItem => ModContent.ItemType<LazharEcType>();
        public override void SetMagicProperty() {
            ShootPosToMouLengValue = -30;
            ShootPosNorlLengValue = 0;
            HandFireDistanceX = 16;
            HandFireDistanceY = -4;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
            Onehanded = true;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 6;
            InOwner_HandState_AlwaysSetInFireRoding = true;
        }

        public override void FiringShoot() {
            int type = Projectile.NewProjectile(Source, ShootPos, ShootVelocity
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            if (Main.rand.NextBool(6)) {
                Main.projectile[type].penetrate = -1;
                Main.projectile[type].damage /= 2;
            }
        }
    }
}
