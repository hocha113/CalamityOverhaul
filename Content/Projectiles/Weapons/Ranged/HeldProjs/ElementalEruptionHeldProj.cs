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
    internal class ElementalEruptionHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "ElementalEruption";
        public override int targetCayItem => ModContent.ItemType<ElementalEruption>();
        public override int targetCWRItem => ModContent.ItemType<ElementalEruptionEcType>();

        private int fireIndex;
        public override void SetRangedProperty() {
            FireTime = 9;
            HandDistance = 25;
            HandDistanceY = 4;
            HandFireDistance = 25;
            HandFireDistanceY = -5;
            ShootPosNorlLengValue = -3;
            ShootPosToMouLengValue = 30;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
            RangeOfStress = 28;
            RepeatedCartridgeChange = true;
            FiringDefaultSound = false;
            kreloadMaxTime = 90;
            loadTheRounds = CWRSound.Liquids_Fill_0 with { Pitch = -0.8f };
            ForcedConversionTargetAmmoFunc = () => true;
            ToTargetAmmo = ModContent.ProjectileType<ElementalFlare>();
        }

        public override void PreInOwnerUpdate() {
            LoadingAnimation(30, 0, 13);
            if (IsKreload) {
                var effectcolor = Main.rand.Next(4) switch {
                    0 => Color.DeepSkyBlue,
                    1 => Color.MediumSpringGreen,
                    2 => Color.DarkOrange,
                    _ => Color.Violet,
                };
                for (int i = 0; i < 2; i++) {
                    int dustType = Main.rand.NextBool() ? 66 : 247;
                    float rotMulti = Main.rand.NextFloat(0.3f, 1f);
                    Dust dust = Dust.NewDustPerfect(GunShootPos, dustType);
                    dust.scale = Main.rand.NextFloat(1.2f, 1.8f) - rotMulti * 0.1f;
                    dust.noGravity = true;
                    dust.velocity = new Vector2(0, -2).RotatedByRandom(rotMulti * 0.3f) * (Main.rand.NextFloat(1f, 3.2f) - rotMulti) + Owner.velocity;
                    dust.alpha = Main.rand.Next(90, 150);
                    dust.color = effectcolor;
                }
            }
        }

        public override void FiringShoot() {
            SoundEngine.PlaySound(SoundID.Item34, Projectile.Center);
            Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedByRandom(0.08f), ModContent.ProjectileType<ElementalFire>(), WeaponDamage, WeaponKnockback, Projectile.owner);
            if (++fireIndex >= 3) {
                Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedBy(0.3f), AmmoTypes, WeaponDamage, WeaponKnockback, Projectile.owner, ShootVelocity.Length(), 1);
                Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedBy(-0.3f), AmmoTypes, WeaponDamage, WeaponKnockback, Projectile.owner, ShootVelocity.Length(), -1);
                fireIndex = 0;
            }
        }
    }
}
