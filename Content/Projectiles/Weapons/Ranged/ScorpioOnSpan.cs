using CalamityMod.Projectiles.Ranged;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class ScorpioOnSpan : BaseOnSpanNoDraw
    {
        public override void SetDefaults() {
            base.SetDefaults();
        }
        public override void SpanProj() {
            if (Projectile.timeLeft % 5 == 0 && Owner.PressKey()) {
                Vector2 vr = Projectile.rotation.ToRotationVector2() * 17;
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center + vr.UnitVector() * 53, vr, ModContent.ProjectileType<MiniRocket>()
                        , Owner.GetShootState().WeaponDamage, Owner.GetShootState().WeaponKnockback, Owner.whoAmI, 0);
                Vector2 pos = Projectile.Center - vr * 3 + vr.GetNormalVector() * 10 * Owner.direction;
                for (int i = 0; i < 100; i++) {
                    Vector2 dustVel = (Projectile.rotation + Main.rand.NextFloat(-0.1f, 0.1f)).ToRotationVector2() * -Main.rand.Next(26, 117);
                    float scale = Main.rand.NextFloat(0.5f, 1.5f);
                    Dust.NewDust(pos, 5, 5, DustID.CopperCoin, dustVel.X, dustVel.Y, 0, default, scale);
                }
            }
        }
    }
}
