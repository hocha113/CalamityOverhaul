using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.OtherMods.InfernumMode;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Projectiles
{
    internal class RocketSkeleton : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder3;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.timeLeft = 600;
            Projectile.extraUpdates = 4;
            if (InfernumRef.InfernumModeOpenState) {
                Projectile.extraUpdates += 1;
            }
            if (CWRRef.GetBossRushActive() || Main.getGoodWorld || Main.zenithWorld || NPC.downedMoonlord) {
                Projectile.extraUpdates += 1;
            }

            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            Projectile.scale += 0.012f;

            if (PRTLoader.NumberUsablePRT() > 10) {
                PRTLoader.NewParticle<PRT_SparkAlpha>(Projectile.Center, Projectile.velocity * 0.7f, Color.DarkRed, 1.4f).Configure(false, 6);
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, Projectile.velocity * 0.7f, Color.LightGoldenrodYellow, 1f).Configure(false, 10);
            }
            else {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.FireworkFountain_Red, Projectile.velocity);
                dust.noGravity = true;
                dust.scale *= Main.rand.NextFloat(0.3f, 1.2f);
            }
            Projectile.localAI[0]++;
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Projectile.localAI[0] <= 0) {
                Projectile.tileCollide = false;
                return false;
            }
            return base.OnTileCollide(oldVelocity);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            SpawnBossRocketBurst();
            target.AddBuff(ModContent.BuffType<HellburnBuff>(), 60);
            Projectile.numHits++;
        }

        public override void OnKill(int timeLeft) {
            if (Projectile.numHits <= 0) {
                SpawnBossRocketBurst();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D mainValue = TextureAssets.Projectile[ProjectileID.RocketSkeleton].Value;
            Main.EntitySpriteDraw(mainValue, Projectile.Center - Main.screenPosition, null, Color.White, Projectile.rotation
                , mainValue.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }

        private void SpawnBossRocketBurst() {
            if (Main.dedServ) {
                return;
            }

            Vector2 pos = Projectile.Center;
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.7f, Pitch = -0.1f }, pos);

            for (int i = 0; i < 28; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(6f, 6f);
                int dustIndex = Dust.NewDust(pos - Vector2.One * 8f, 16, 16, DustID.Torch, vel.X, vel.Y, 100, default, Main.rand.NextFloat(1.5f, 2.5f));
                Main.dust[dustIndex].noGravity = true;
            }

            for (int i = 0; i < 16; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4f, 4f) + Vector2.UnitY * -1.5f;
                int dustIndex = Dust.NewDust(pos - Vector2.One * 6f, 12, 12, DustID.Smoke, vel.X, vel.Y, 120, Color.DarkRed, 1.4f);
                Main.dust[dustIndex].fadeIn = 0.8f + Main.rand.NextFloat(0.4f);
            }

            for (int i = 0; i < 4; i++) {
                float scale = Main.rand.NextFloat(0.5f, 0.9f);
                int goreIndex = Gore.NewGore(Projectile.GetSource_Death(), pos, Main.rand.NextVector2Circular(3f, 3f), Main.rand.Next(61, 64), scale);
                Main.gore[goreIndex].velocity *= 1.2f;
            }
        }
    }
}
