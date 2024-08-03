using CalamityMod.Buffs.StatDebuffs;
using CalamityMod.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class IceOrb : ModProjectile
    {
        public override string Texture => CWRConstant.Cay_Proj + "Turret/IceShot";
        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 24;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 180;
            Projectile.usesIDStaticNPCImmunity = true;
            Projectile.idStaticNPCHitCooldown = 10;
        }

        public override bool PreAI() {
            if (Projectile.knockBack == 0f)
                Projectile.hostile = true;
            else Projectile.friendly = true;
            return true;
        }

        public override void AI() {
            float fallSpeedCap = 15f;
            float downwardsAccel = 0.3f;
            if (Projectile.localAI[0] == 0f) {
                Projectile.velocity.Y -= 3f;
                SoundEngine.PlaySound(SoundID.Item89 with { Volume = 0.4f }, Projectile.position);
            }
            Projectile.localAI[0]++;
            if (Projectile.velocity.Y < fallSpeedCap)
                Projectile.velocity.Y += downwardsAccel;
            if (Projectile.velocity.Y > fallSpeedCap)
                Projectile.velocity.Y = fallSpeedCap;
            Projectile.velocity.X *= 0.985f;
            Projectile.rotation += 0.1f * Projectile.velocity.X;

            Vector2 bloodSpawnPosition = Projectile.Center + (Vector2.UnitY * -13f).RotatedBy(Projectile.rotation);
            Vector2 splatterDirection = -(Projectile.Center - bloodSpawnPosition).SafeNormalize(Vector2.UnitY);
            int bloodLifetime = Main.rand.Next(5, 8);
            float bloodScale = Main.rand.NextFloat(0.4f, 0.6f);
            Color bloodColor = Color.Lerp(Color.Cyan, Color.LightCyan, Main.rand.NextFloat());
            bloodColor = Color.Lerp(bloodColor, new Color(11, 64, 128), Main.rand.NextFloat(0.65f));

            if (Main.rand.NextBool(20))
                bloodScale *= 1.7f;

            Vector2 bloodVelocity = splatterDirection.RotatedByRandom(0.81f) * Main.rand.NextFloat(3f, 6f);
            bloodVelocity.Y -= 1f;
            BloodParticle blood = new BloodParticle(bloodSpawnPosition, bloodVelocity, bloodLifetime, bloodScale, bloodColor);
            GeneralParticleHandler.SpawnParticle(blood);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<GlacialState>(), 30);
            target.AddBuff(BuffID.Frostburn2, 180);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(ModContent.BuffType<GlacialState>(), 30);
            target.AddBuff(BuffID.Frostburn2, 180);
            if (Projectile.hostile && Main.netMode == NetmodeID.MultiplayerClient)
                return;
            Projectile.Kill();
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Projectile.oldVelocity.Y > 0f && Projectile.velocity.X != 0f) {
                Projectile.velocity.Y = -0.6f * Projectile.oldVelocity.Y;
                Projectile.velocity.X *= 0.975f;
            }
            else if (Projectile.velocity.X == 0f) {
                Projectile.velocity.X = -0.6f * Projectile.oldVelocity.X;
            }
            return false;
        }

        public override Color? GetAlpha(Color drawColor) {
            return Projectile.timeLeft < 30 && Projectile.timeLeft % 10 < 5 ? Color.Orange : Color.White;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(new SoundStyle("CalamityMod/Sounds/NPCHit/CryogenHit", 3) with { Volume = 0.55f }, Projectile.Center);
        }
    }
}
