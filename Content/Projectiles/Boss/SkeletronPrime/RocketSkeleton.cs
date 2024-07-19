using CalamityMod.Events;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Particles.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

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
            Projectile.extraUpdates = 4;
            if (ModGanged.InfernumModeOpenState) {
                Projectile.extraUpdates += 1;
            }
            if (BossRushEvent.BossRushActive || Main.getGoodWorld || Main.zenithWorld) {
                Projectile.extraUpdates += 1;
            }
            Projectile.tileCollide = false;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            Projectile.scale += 0.012f;

            if (Projectile.DistanceSQ(Main.LocalPlayer.Center) < 1600 * 1600) {
                if (CWRParticleHandler.FreeSpacesAvailable() > 10) {
                    CWRParticle spark = new HeavenfallStarParticle(Projectile.Center, Projectile.velocity * 0.7f, false, 20, 1.2f, Color.Gold);
                    CWRParticleHandler.AddParticle(spark);
                }
                else {
                    Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.FireworkFountain_Red, Projectile.velocity);
                    dust.noGravity = true;
                    dust.scale *= Main.rand.NextFloat(0.3f, 1.2f);
                }
            }

        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            Vector2 spanPos = Projectile.Center;
            Vector2 vr = Vector2.Zero;
            CWRParticle pulse3 = new DimensionalWave(spanPos, vr, Color.Red
            , new Vector2(0.7f, 1.3f) * 0.8f, vr.ToRotation(), 1.18f, 3.32f, 60);
            CWRParticleHandler.AddParticle(pulse3);
            target.AddBuff(ModContent.BuffType<HellfireExplosion>(), 60);
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
