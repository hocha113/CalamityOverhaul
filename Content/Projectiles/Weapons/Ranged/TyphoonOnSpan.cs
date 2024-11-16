using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class TyphoonOnSpan : BaseOnSpanProj
    {
        protected override Color[] colors => new Color[] { Color.LightSkyBlue, Color.Blue, Color.AliceBlue, Color.LightBlue };
        protected override float halfSpreadAngleRate => 1.15f;
        protected override float edgeBlendLength => 0.17f;
        protected override float edgeBlendStrength => 9;
        public override float MaxCharge => 180;

        public override void SpanProjFunc() {
            ShootState shootState = Owner.GetShootState();
            if (shootState.HasAmmo && !Owner.IsWoodenAmmo(shootState.AmmoTypes)) {
                float speedMode = 17f;
                ModProjectile targetProj = ProjectileLoader.GetProjectile(shootState.AmmoTypes);
                if (targetProj != null && targetProj.Projectile.extraUpdates > 3) {
                    speedMode /= (targetProj.Projectile.extraUpdates - 2);
                }
                for (int i = 0; i < 64; i++) {
                    float rot = MathHelper.TwoPi / 64 * i;
                    Vector2 velocity = rot.ToRotationVector2() * (19 + (-11f + rot % MathHelper.PiOver4) * speedMode);
                    Projectile.NewProjectile(shootState.Source, Projectile.Center, velocity
                        , shootState.AmmoTypes, shootState.WeaponDamage, shootState.WeaponKnockback, Owner.whoAmI);
                }
                return;
            }

            for (int i = 0; i < 94; i++) {
                float rot = MathHelper.TwoPi / 94 * i;
                Vector2 velocity = rot.ToRotationVector2() * (1 + (-6f + rot % MathHelper.PiOver4) * 2);
                Projectile.NewProjectile(Owner.parent(), Projectile.Center, velocity
                    , ModContent.ProjectileType<TorrentialArrow>()
                    , shootState.WeaponDamage, shootState.WeaponKnockback, Owner.whoAmI);
            }
        }
    }
}
