using CalamityMod;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Particles.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class AegisBladeProj : ModProjectile
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "AegisBlade";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 13;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
        }

        public override void AI() {
            if (Projectile.ai[1] != 1)
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
            if (Projectile.ai[0] == 0) {
                Projectile.velocity = new Vector2(0, -12);
            }
            if (Projectile.ai[1] == 0) {
                Projectile.scale += 0.01f;
                Projectile.velocity *= 0.97f;
                Projectile.position += Main.player[Projectile.owner].velocity;
                if (Projectile.velocity.LengthSquared() < 9) {
                    Projectile.ai[1] = 1;
                    Projectile.ai[0] = 1;
                    Projectile.netUpdate = true;
                }
            }
            if (Projectile.ai[1] == 1) {
                if (Projectile.scale < 2.5f) {
                    Projectile.scale += 0.02f;
                    if (!CWRUtils.isServer) {
                        for (int i = 0; i < 6; i++) {
                            Vector2 pos = Projectile.Center + Main.rand.NextVector2Unit() * Main.rand.Next(133, 140) * Projectile.scale;
                            Vector2 particleSpeed = pos.To(Projectile.Center).UnitVector() * 17;
                            CWRParticle energyLeak = new LightParticle(pos, particleSpeed
                                , Main.rand.NextFloat(0.3f, 0.5f), Color.Gold, 16, 1, 1.5f, hueShift: 0.0f, _entity: Projectile);
                            CWRParticleHandler.AddParticle(energyLeak);
                        }
                    }
                }
                else {
                    if (!CWRUtils.isServer) {
                        for (int i = 0; i < 6; i++) {
                            Vector2 randdom = Main.rand.NextVector2Unit();
                            Vector2 pos = Projectile.Center + randdom * Main.rand.Next(3, 14) * Projectile.scale;
                            Vector2 particleSpeed = randdom * 17;
                            CWRParticle energyLeak = new LightParticle(pos, particleSpeed
                                , Main.rand.NextFloat(0.1f, 0.6f), Color.DarkGoldenrod, 16, 1, 1.5f, hueShift: 0.0f);
                            CWRParticleHandler.AddParticle(energyLeak);
                        }
                    }
                }
                if (Projectile.damage < Projectile.originalDamage * 45)
                    Projectile.damage += 35;
                Projectile.velocity = Vector2.Zero;
                Projectile.rotation += 0.2f;
                Projectile.position += Main.player[Projectile.owner].velocity;
                if (Main.player[Projectile.owner].PressKey(false)) {
                    Projectile.timeLeft = 300;
                    if (Projectile.ai[0] > 55) {
                        Projectile.ai[0] = 55;
                    }
                }
                if (Projectile.ai[0] > 60 || !Main.player[Projectile.owner].PressKey(false)) {
                    Projectile.ai[1] = 2;
                    Projectile.netUpdate = true;
                }
            }
            if (Projectile.ai[1] == 2) {
                NPC npc = Projectile.Center.FindClosestNPC(6000, true, true);
                if (npc != null) {
                    Projectile.ChasingBehavior(npc.Center, 56);
                    Projectile.penetrate = 1;
                    Projectile.netUpdate = true;
                }
                else {
                    Projectile.Kill();
                }
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
            }
            Projectile.ai[0]++;
        }

        public override bool? CanDamage() {
            if (Projectile.ai[1] != 2)
                return false;
            return base.CanDamage();
        }

        public override void OnKill(int timeLeft) {
            Projectile.damage = Projectile.originalDamage;
            Projectile.Explode(1200);
            if (!CWRUtils.isServer) {
                for (int i = 0; i < 156; i++) {
                    Vector2 pos = Projectile.Center;
                    Vector2 particleSpeed = Main.rand.NextVector2Unit() * Main.rand.Next(13, 34);
                    CWRParticle energyLeak = new LightParticle(pos, particleSpeed
                        , Main.rand.NextFloat(0.5f, 1.3f), Color.Gold, 30, 1, 1.5f, hueShift: 0.0f);
                    CWRParticleHandler.AddParticle(energyLeak);
                }
            }
            if (Projectile.IsOwnedByLocalPlayer()) {
                for (int i = 0; i < 12; i++) {
                    Vector2 velocity = CalamityUtils.RandomVelocity(100f, 70f, 100f);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center + velocity.UnitVector() * 13, velocity, ModContent.ProjectileType<AegisFlame>(), (int)(Projectile.damage * 0.5), 0f, Projectile.owner, 0f, 0f);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = CWRUtils.GetT2DValue(Texture);
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, Color.White
                , Projectile.rotation, value.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            if (Projectile.ai[1] == 2) {
                for (int i = 0; i < Projectile.oldPos.Length; i++) {
                    Main.EntitySpriteDraw(value, Projectile.oldPos[i] - Main.screenPosition + Projectile.Size / 2, null, Color.White * (1 - i * 0.1f)
                    , Projectile.rotation, value.Size() / 2, Projectile.scale - i * 0.1f, SpriteEffects.None, 0);
                }
            }
            return false;
        }
    }
}
