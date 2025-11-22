using CalamityOverhaul.Content.Projectiles.Weapons.Magic;
using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class DaemonsFlameHeld : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "DaemonsFlame";
        public override void SetRangedProperty() {
            DrawArrowMode = -30;
            BowstringData.TopBowOffset = new Vector2(8, 4);
            BowstringData.DeductRectangle = new Rectangle(4, 4, 4, 114);
        }

        public override void BowShoot() {
            if (Owner.IsWoodenAmmo(AmmoTypes)) {
                int types = ModContent.ProjectileType<FateCluster>();
                for (int i = 0; i < 4; i++) {
                    Vector2 velocity = ShootVelocity.UnitVector().RotatedByRandom(0.1f) * Main.rand.Next(8, 18);
                    int doms = Projectile.NewProjectile(Source, ShootPos, velocity, types, WeaponDamage, WeaponKnockback, Owner.whoAmI);
                    Projectile newDoms = Main.projectile[doms];
                    newDoms.DamageType = DamageClass.Ranged;
                    newDoms.timeLeft = 120;
                    newDoms.ai[0] = 1;
                }
            }
            else {
                for (int i = 0; i < 4; i++) {
                    Projectile.NewProjectile(Source2, ShootPos, ShootVelocity.UnitVector() * Main.rand.Next(20, 30), AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI);
                }
                Projectile.NewProjectile(Source, ShootPos, ShootVelocity
                    , CWRID.Proj_DaemonsFlameArrow, WeaponDamage, WeaponKnockback, Owner.whoAmI);
            }
        }
    }
}
