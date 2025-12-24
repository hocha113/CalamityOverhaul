using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class FrostNovaOrb : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Ranged + "Crystal";
        private float glowIntensity;
        private float rotationSpeed;
        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 4;
        }
        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 28;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 200;
            Projectile.usesIDStaticNPCImmunity = true;
            Projectile.idStaticNPCHitCooldown = 12;
            Projectile.MaxUpdates = 2;
            Projectile.friendly = true;
            rotationSpeed = Main.rand.NextFloat(-0.3f, 0.3f);
        }

        public override void AI() {
            VaultUtils.ClockFrame(ref Projectile.frame, 5, 4);
            Projectile.rotation += rotationSpeed;
            glowIntensity = 0.5f + (float)Math.Sin(Projectile.ai[0] * 0.15f) * 0.5f;

            if (Projectile.ai[1] > 0) {
                NPC target = Projectile.Center.FindClosestNPC(800, false, true);
                if (target != null) {
                    float distance = target.Center.Distance(Projectile.Center);
                    if (distance > 150) {
                        Projectile.SmoothHomingBehavior(target.Center, 1.2f, 0.28f);
                    }
                    else {
                        Projectile.ChasingBehavior(target.Center, Projectile.velocity.Length());
                    }
                }
            }

            if (Main.rand.NextBool(3)) {
                Vector2 dspeed = -Projectile.velocity * 0.5f;
                int dust = Dust.NewDust(Projectile.Center, 1, 1, DustID.BlueCrystalShard, dspeed.X, dspeed.Y, 100, default, 1.8f);
                Main.dust[dust].noGravity = true;
            }

            if (Main.rand.NextBool(5)) {
                Dust snow = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height
                    , DustID.SnowflakeIce, 0, 0, 100, default, Main.rand.NextFloat(1.5f, 2.5f));
                snow.velocity = -Projectile.velocity * 0.3f;
                snow.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 0.5f * glowIntensity, 0.8f * glowIntensity, 1.2f * glowIntensity);

            Projectile.ai[0]++;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn2, 240);
            Projectile.penetrate--;
            if (Projectile.penetrate <= 0) {
                ExplodeEffect();
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Projectile.ai[1] == 0) {
                Collision.HitTiles(Projectile.position, Projectile.velocity, Projectile.width, Projectile.height);
                SoundEngine.PlaySound(SoundID.Item27 with { Pitch = -0.2f, Volume = 0.5f }, Projectile.position);
                
                if (Projectile.velocity.X != oldVelocity.X) {
                    Projectile.velocity.X = -oldVelocity.X * 1.8f;
                }
                if (Projectile.velocity.Y != oldVelocity.Y) {
                    Projectile.velocity.Y = -oldVelocity.Y * 1.8f;
                }
                
                for (int i = 0; i < 4; i++) {
                    Vector2 velocity = new Vector2(Main.rand.NextFloat(-4, 4), -4);
                    Projectile proj = Projectile.NewProjectileDirect(Main.player[Projectile.owner].GetShootState().Source
                    , Projectile.Bottom + new Vector2(Main.rand.Next(-20, 20), 0), velocity
                    , ProjectileID.DeerclopsIceSpike, 28, 0f, Main.myPlayer, 0f, Main.rand.NextFloat(0.85f, 1.15f));
                    proj.rotation = velocity.ToRotation();
                    proj.hostile = false;
                    proj.friendly = true;
                    proj.penetrate = -1;
                    proj.usesLocalNPCImmunity = true;
                    proj.localNPCHitCooldown = 25;
                    proj.light = 0.8f;
                }
            }
            Projectile.ai[1]++;
            return false;
        }

        private void ExplodeEffect() {
            SoundEngine.PlaySound(SoundID.Item27 with { Pitch = 0.2f, Volume = 0.7f }, Projectile.Center);
            
            for (int i = 0; i < 60; i++) {
                Vector2 velocity = Main.rand.NextVector2CircularEdge(12f, 12f);
                Dust frost = Dust.NewDustPerfect(Projectile.Center, DustID.BlueCrystalShard, velocity, 0, default, Main.rand.NextFloat(2f, 3.5f));
                frost.noGravity = true;
                frost.fadeIn = 1.5f;
            }

            for (int i = 0; i < 40; i++) {
                Dust snow = Dust.NewDustPerfect(Projectile.Center, DustID.SnowflakeIce
                    , Main.rand.NextVector2Circular(10f, 10f), 0, default, Main.rand.NextFloat(2.5f, 4f));
                snow.noGravity = true;
            }

            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8f;
                Vector2 velocity = angle.ToRotationVector2() * 8f;
                Projectile proj = Projectile.NewProjectileDirect(Main.player[Projectile.owner].GetShootState().Source
                    , Projectile.Center, velocity, ProjectileID.FrostBeam, Projectile.damage, 2f, Projectile.owner);
                proj.hostile = false;
                proj.friendly = true;
                proj.DamageType = DamageClass.Ranged;
                proj.usesLocalNPCImmunity = true;
                proj.localNPCHitCooldown = -1;
                proj.ArmorPenetration = 30;
            }
        }

        public override void OnKill(int timeLeft) {
            if (timeLeft > 0) {
                ExplodeEffect();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;
            Rectangle frame = value.GetRectangle(Projectile.frame, 4);
            Vector2 origin = frame.Size() / 2f;

            for (int i = 0; i < 3; i++) {
                float glowScale = Projectile.scale * (1.15f + i * 0.2f);
                float glowAlpha = glowIntensity * (1f - i * 0.3f) * 0.8f;
                Main.EntitySpriteDraw(value, drawPosition, frame, new Color(100, 200, 255, 0) * glowAlpha
                    , Projectile.rotation, origin, glowScale, SpriteEffects.None, 0);
            }

            Color mainColor = Color.Lerp(lightColor, new Color(200, 230, 255), glowIntensity * 0.8f);
            Main.EntitySpriteDraw(value, drawPosition, frame, mainColor
                , Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

            Main.EntitySpriteDraw(value, drawPosition, frame, new Color(220, 240, 255, 0) * glowIntensity * 0.9f
                , Projectile.rotation, origin, Projectile.scale * 1.2f, SpriteEffects.None, 0);

            if (glowIntensity > 0.7f) {
                Main.EntitySpriteDraw(value, drawPosition, frame, Color.White * glowIntensity * 0.7f
                    , Projectile.rotation, origin, Projectile.scale * 0.9f, SpriteEffects.None, 0);
            }

            return false;
        }
    }
}
