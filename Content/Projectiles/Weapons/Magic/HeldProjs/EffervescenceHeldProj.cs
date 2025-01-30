using CalamityMod.Items.Weapons.Magic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs
{
    internal class EffervescenceHeldProj : BaseMagicGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "Effervescence";
        public override int TargetID => ModContent.ItemType<Effervescence>();
        public override void SetMagicProperty() {
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandIdleDistanceX = 20;
            HandIdleDistanceY = 3;
            HandFireDistanceX = 20;
            HandFireDistanceY = -5;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 6;
        }

        public override void FiringShoot() {
            for (int i = 0; i < 3; i++) {
                int type = Projectile.NewProjectile(Source, ShootPos, ShootVelocity, AmmoTypes
                    , WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[type].velocity = Main.projectile[type].velocity.RotatedByRandom(0.3f);
            }
        }
    }
}
