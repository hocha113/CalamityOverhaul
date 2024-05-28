using CalamityMod.Items.Weapons.Magic;
using CalamityMod.Projectiles.Magic;
using CalamityMod.Sounds;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Magic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs
{
    internal class SHPCHeldProj : BaseMagicGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "SHPC";
        public override int targetCayItem => ModContent.ItemType<SHPC>();
        public override int targetCWRItem => ModContent.ItemType<SHPCEcType>();
        public override void SetMagicProperty() {
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 35;
            HandDistanceY = 5;
            HandFireDistance = 35;
            HandFireDistanceY = 0;
            GunPressure = 0.3f;
            ControlForce = 0.02f;
            Recoil = 0;
            CanRightClick = true;
            EnableRecoilRetroEffect = true;
        }

        public override void HanderPlaySound() {
            if (onFire) {
                SoundEngine.PlaySound(SoundID.Item92, Projectile.Center);
            }
            else if (onFireR) {
                SoundEngine.PlaySound(CommonCalamitySounds.LaserCannonSound, Projectile.Center);
            }
        }

        public override void SetShootAttribute() {
            if (onFire) {
                Item.useTime = 45;
                GunPressure = 0.3f;
                RecoilRetroForceMagnitude = 0;
            }
            else if (onFireR) {
                Item.useTime = 7;
                GunPressure = 0f;
                RecoilRetroForceMagnitude = 6;
            }
        }

        public override void FiringShoot() {
            for (int i = 0; i < 3; i++) {
                Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedByRandom(0.2f) * Main.rand.NextFloat(0.8f, 1f)
                    , ModContent.ProjectileType<SHPB>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
        }

        public override void FiringShootR() {
            for (int i = 0; i < 3; i++) {
                Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedByRandom(0.2f) * Main.rand.NextFloat(0.8f, 1.1f)
                    , ModContent.ProjectileType<SHPL>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
        }
    }
}
