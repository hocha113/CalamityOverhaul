using CalamityMod;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class DeathwindHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Deathwind";
        public override int TargetID => ModContent.ItemType<Deathwind>();
        public override void SetRangedProperty() {
            BowArrowDrawNum = 3;
            FiringDefaultSound = false;
            BowstringData.DeductRectangle = new Rectangle(4, 24, 10, 30);
        }

        public override void BowShoot() {
            if (CalamityUtils.CheckWoodenAmmo(AmmoTypes, Owner)) {
                SoundEngine.PlaySound(SoundID.Item12, Projectile.Center);
                for (int i = 0; i < 3; i++) {
                    int ammo = Projectile.NewProjectile(Source, Projectile.Center, (Projectile.rotation + MathHelper.ToRadians(5 - 5 * i)).ToRotationVector2()
                        , ModContent.ProjectileType<DeathLaser>(), WeaponDamage, WeaponKnockback, Projectile.owner);
                    Main.projectile[ammo].ai[1] = Projectile.whoAmI;
                }
            }
            else {
                SoundEngine.PlaySound(SoundID.Item5, Projectile.Center);
                if (AmmoTypes == ModContent.ProjectileType<VanquisherArrowProj>()) {
                    WeaponDamage = (int)(WeaponDamage * 0.65f);
                }
                for (int i = 0; i < 3; i++) {
                    int ammo = Projectile.NewProjectile(Source, Projectile.Center
                        , (Projectile.rotation + MathHelper.ToRadians(5 - 5 * i)).ToRotationVector2() * 17
                        , AmmoTypes, WeaponDamage, WeaponKnockback, Projectile.owner);
                    Main.projectile[ammo].MaxUpdates = 2;
                    Main.projectile[ammo].CWR().SpanTypes = (byte)SpanTypesEnum.DeadWing;
                }
                Projectile.ai[2]++;
                if (Projectile.ai[2] > 5) {
                    for (int i = 0; i < 3; i++) {
                        Vector2 vr = (Projectile.rotation + MathHelper.ToRadians(5 - 5 * i)).ToRotationVector2();
                        int ammo = Projectile.NewProjectile(Source, Projectile.Center + vr * 150, vr * 15,
                                ModContent.ProjectileType<DeadArrow>(), WeaponDamage, Projectile.knockBack, Projectile.owner);
                        Main.projectile[ammo].scale = 1.5f;
                    }
                    Projectile.ai[2] = 0;
                }
            }
        }
    }
}
