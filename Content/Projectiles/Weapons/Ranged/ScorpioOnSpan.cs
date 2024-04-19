using CalamityMod;
using CalamityMod.Projectiles.Ranged;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
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
            if (Projectile.timeLeft % 6 == 0 && Owner.PressKey()) {
                Vector2 vr = Projectile.rotation.ToRotationVector2() * 17;
                if (Projectile.IsOwnedByLocalPlayer()) {
                    Projectile.NewProjectile(Owner.GetShootState("CWRGunShoot").Source, Projectile.Center + vr.UnitVector() * 53 + vr.GetNormalVector() * 11 * (Projectile.rotation.ToRotationVector2().X > 0 ? 1 : -1)
                        , vr, ModContent.ProjectileType<MiniRocket>(), Owner.GetShootState().WeaponDamage, Owner.GetShootState().WeaponKnockback, Owner.whoAmI, 0);
                }
                Vector2 pos = Projectile.Center - vr * 3 + vr.GetNormalVector() * 10 * Owner.direction;
                if (Projectile.rotation != 0) {
                    for (int i = 0; i < 100; i++) {
                        Vector2 dustVel = (Projectile.rotation + Main.rand.NextFloat(-0.1f, 0.1f)).ToRotationVector2() * -Main.rand.Next(26, 117);
                        float scale = Main.rand.NextFloat(0.5f, 1.5f);
                        int type = DustID.CopperCoin;
                        Vector2 pos2 = pos;
                        if (Main.rand.NextBool()) {
                            type = DustID.FireworkFountain_Yellow;
                            pos2 += dustVel * 6;
                        }
                        Dust.NewDust(pos2, 5, 5, type, dustVel.X, dustVel.Y, 0, default, scale);
                    }
                }
            }
        }
    }
}
