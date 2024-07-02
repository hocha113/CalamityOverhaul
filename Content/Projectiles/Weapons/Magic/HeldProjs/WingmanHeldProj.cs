using CalamityMod.Items.Weapons.Magic;
using CalamityMod.Projectiles.Magic;
using CalamityOverhaul.Content.Items.Magic;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs
{
    internal class WingmanHeldProj : BaseMagicGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "Wingman";
        public override int targetCayItem => ModContent.ItemType<Wingman>();
        public override int targetCWRItem => ModContent.ItemType<WingmanEcType>();

        private int fireIndex;
        public override void SetMagicProperty() {
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 15;
            HandDistanceY = 9;
            HandFireDistance = 15;
            HandFireDistanceY = -5;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
            ArmRotSengsBackNoFireOffset = 113;
        }

        public override void FiringShoot() {
            SoundStyle style = new SoundStyle("CalamityMod/Sounds/Item/MagnaCannonShot");
            float rand = Main.rand.NextFloat(0.3f, 1.1f);
            AmmoTypes = ModContent.ProjectileType<WingmanShot>();
            float newdamage = WeaponDamage * 0.75f;
            if (++fireIndex > 2) {
                style = new SoundStyle("CalamityMod/Sounds/Item/DeadSunExplosion");
                AmmoTypes = ModContent.ProjectileType<WingmanGrenade>();
                newdamage *= 1.75f;
                fireIndex = 0;
            }
            SoundEngine.PlaySound(style with { Volume = 0.6f }, Projectile.Center);
            for (int i = 0; i < 3; i++) {
                Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedBy((-1 + i) * 0.1f * rand)
                    , AmmoTypes, (int)newdamage, WeaponKnockback, Owner.whoAmI, 0);
            }
        }
    }
}
