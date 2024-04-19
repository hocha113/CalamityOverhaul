using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class TerraFlamebursterHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "TerraFlameburster";
        public override int targetCayItem => ModContent.ItemType<TerraFlameburster>();
        public override int targetCWRItem => ModContent.ItemType<TerraFlamebursterEcType>();
        int fireIndex;
        public override void SetRangedProperty() {
            FireTime = 9;
            HandDistance = 25;
            HandDistanceY = 4;
            HandFireDistance = 25;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = -3;
            ShootPosToMouLengValue = 25;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
            RangeOfStress = 28;
            RepeatedCartridgeChange = true;
            FiringDefaultSound = false;
            kreloadMaxTime = 90;
            loadTheRounds = CWRSound.Liquids_Fill_0 with { Pitch = -0.8f };
            ForcedConversionTargetAmmoFunc = () => true;
            ToTargetAmmo = ModContent.ProjectileType<TerraFlare>();
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(30, 0, 13);
            if (IsKreload) {
                for (int i = 0; i < 2; i++) {
                    float rotMulti = Main.rand.NextFloat(0.3f, 1f);
                    Dust dust2 = Dust.NewDustPerfect(GunShootPos, Main.rand.NextBool(5) ? 135 : 107);
                    dust2.noGravity = true;
                    dust2.velocity = new Vector2(0, -2).RotatedByRandom(rotMulti * 0.3f) * (Main.rand.NextFloat(1f, 2.9f) - rotMulti) + Owner.velocity;
                    dust2.scale = Main.rand.NextFloat(1.2f, 1.8f);
                }
            }
        }

        public override void FiringShoot() {
            SoundEngine.PlaySound(SoundID.Item34, Projectile.Center);
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedByRandom(0.08f), ModContent.ProjectileType<TerraFire>(), WeaponDamage, WeaponKnockback, Projectile.owner);
            if (++fireIndex >= 3) {
                Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedBy(0.3f), AmmoTypes, WeaponDamage, WeaponKnockback, Projectile.owner);
                Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedBy(-0.3f), AmmoTypes, WeaponDamage, WeaponKnockback, Projectile.owner);
                fireIndex = 0;
            }
        }
    }
}
