using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class DeathwindHeld : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Deathwind";
        public override void SetRangedProperty() {
            BowArrowDrawNum = 3;
            FiringDefaultSound = false;
            BowstringData.DeductRectangle = new Rectangle(4, 24, 10, 30);
        }

        public override void BowShoot() {
            if (Owner.IsWoodenAmmo(AmmoTypes)) {
                SoundEngine.PlaySound(SoundID.Item12, Projectile.Center);
                for (int i = 0; i < 3; i++) {
                    int ammo = Projectile.NewProjectile(Source, Projectile.Center, (Projectile.rotation + MathHelper.ToRadians(5 - 5 * i)).ToRotationVector2()
                        , ModContent.ProjectileType<DeathLaser>(), WeaponDamage, WeaponKnockback, Projectile.owner);
                    Main.projectile[ammo].ai[1] = Projectile.whoAmI;
                }
            }
            else {
                SoundEngine.PlaySound(SoundID.Item5, Projectile.Center);
                if (AmmoTypes == CWRID.Proj_VanquisherArrowProj) {
                    WeaponDamage = (int)(WeaponDamage * 0.65f);
                }
                for (int i = 0; i < 3; i++) {
                    int ammo = Projectile.NewProjectile(Source, Projectile.Center
                        , (Projectile.rotation + MathHelper.ToRadians(5 - 5 * i)).ToRotationVector2() * 17
                        , AmmoTypes, WeaponDamage, WeaponKnockback, Projectile.owner);
                    Main.projectile[ammo].MaxUpdates = 2;
                    Main.projectile[ammo].CWR().SpanTypes = (byte)SpanTypesEnum.DeadWing;
                }
            }
        }
    }
}
