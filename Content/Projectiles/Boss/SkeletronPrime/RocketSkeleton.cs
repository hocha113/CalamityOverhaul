using CalamityOverhaul.Content.Particles.Core;
using CalamityOverhaul.Content.Particles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using CalamityOverhaul.Content.Buffs;

namespace CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime
{
    internal class RocketSkeleton : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder3;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.timeLeft = 600;
            Projectile.extraUpdates = 6;
            Projectile.tileCollide = false;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            Projectile.scale += 0.012f;
            Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.FireworkFountain_Red, Projectile.velocity);
            dust.noGravity = true;
            dust.scale *= Main.rand.NextFloat(0.3f, 1.2f);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            Vector2 spanPos = Projectile.Center;
            Vector2 vr = Vector2.Zero;
            CWRParticle pulse3 = new DimensionalWave(spanPos, vr, Color.Red
            , new Vector2(0.7f, 1.3f) * 0.8f, vr.ToRotation(), 1.18f, 3.32f, 60);
            CWRParticleHandler.AddParticle(pulse3);
            target.AddBuff(ModContent.BuffType<HellfireExplosion>(), 120);
        }

        public override void OnKill(int timeLeft) {
            Vector2 spanPos = Projectile.Center;
            Vector2 vr = Vector2.Zero;
            CWRParticle pulse3 = new DimensionalWave(spanPos, vr, Color.Red
            , new Vector2(0.7f, 0.73f), vr.ToRotation(), 2.18f, 6.32f, 60);
            CWRParticleHandler.AddParticle(pulse3);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D mainValue = TextureAssets.Projectile[ProjectileID.RocketSkeleton].Value;
            Main.EntitySpriteDraw(mainValue, Projectile.Center - Main.screenPosition, null, Color.White, Projectile.rotation
                , CWRUtils.GetOrig(mainValue), Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
