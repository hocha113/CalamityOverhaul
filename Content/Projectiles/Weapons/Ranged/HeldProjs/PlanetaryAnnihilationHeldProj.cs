using CalamityMod;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class PlanetaryAnnihilationHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "PlanetaryAnnihilation";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.PlanetaryAnnihilation>();
        public override int targetCWRItem => ModContent.ItemType<PlanetaryAnnihilationEcType>();
        public override void PostInOwner() {
            if (onFire) {
                BowArrowDrawBool = false;
                LimitingAngle();
            }
        }

        public override void HanderPlaySound() {
            if (CalamityUtils.CheckWoodenAmmo(AmmoTypes, Owner)) {
                SoundEngine.PlaySound(Item.UseSound, Projectile.Center);
            }
            else {
                SoundEngine.PlaySound(SoundID.Item5, Projectile.Center);
            }
        }

        public override void BowShoot() {
            if (CalamityUtils.CheckWoodenAmmo(AmmoTypes, Owner)) {
                SoundEngine.PlaySound(Item.UseSound, Projectile.Center);
                for (int i = 0; i < 5; i++) {
                    Vector2 spanPos = Projectile.Center + new Vector2(Main.rand.Next(-520, 520), Main.rand.Next(-732, -623));
                    Vector2 vr = spanPos.To(Main.MouseWorld).UnitVector().RotatedBy(Main.rand.NextFloat(-0.12f, 0.12f)) * Main.rand.NextFloat(0.6f, 1.52f) * 6;
                    Projectile.NewProjectile(Source, spanPos, vr, ModContent.ProjectileType<PlanetaryArrow>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                }
            }
            else {
                SoundEngine.PlaySound(SoundID.Item5, Projectile.Center);
                for (int i = 0; i < 5; i++) {
                    Vector2 spanPos = Projectile.Center + new Vector2(Main.rand.Next(-520, 520), Main.rand.Next(-732, -623));
                    Vector2 vr = spanPos.To(Main.MouseWorld).UnitVector().RotatedBy(Main.rand.NextFloat(-0.12f, 0.12f)) * Main.rand.NextFloat(0.6f, 1.52f) * 16;
                    Projectile.NewProjectile(Source, spanPos, vr, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                }
            }
        }
    }
}
