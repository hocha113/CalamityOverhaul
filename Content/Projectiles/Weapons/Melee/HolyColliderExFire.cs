using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Particles.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class HolyColliderExFire : BaseHeldProj, IDrawWarp
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle";
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 400;
            Projectile.timeLeft = 20;
            Projectile.aiStyle = -1;
            Projectile.penetrate = -1;
            Projectile.friendly = true;
            Projectile.netImportant = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 2;
            Projectile.DamageType = DamageClass.Melee;

        }

        private void SpwanPRKAndDustEffect() {
            if (Main.dedServ) {
                return;
            }

            Vector2 spanPos = Projectile.Center;

            if (Projectile.ai[0] == 1) {
                for (int i = 0; i < 20; i++) {
                    int dust = Dust.NewDust(spanPos, Projectile.width, Projectile.height, DustID.OrangeTorch, Scale: 6);
                    Main.dust[dust].position = spanPos;
                    Main.dust[dust].velocity = (MathHelper.TwoPi / 20f * i).ToRotationVector2() * 16;
                    Main.dust[dust].noGravity = true;
                }
                for (int i = 0; i < 30; i++) {
                    int dust = Dust.NewDust(spanPos, Projectile.width, Projectile.height, DustID.OrangeTorch, Scale: 6);
                    Main.dust[dust].position = spanPos;
                    Main.dust[dust].velocity = (MathHelper.TwoPi / 30f * i).ToRotationVector2() * 26;
                    Main.dust[dust].noGravity = true;
                }
                for (int i = 0; i < 40; i++) {
                    int dust = Dust.NewDust(spanPos, Projectile.width, Projectile.height, DustID.OrangeTorch, Scale: 6);
                    Main.dust[dust].position = spanPos;
                    Main.dust[dust].velocity = (MathHelper.TwoPi / 40f * i).ToRotationVector2() * 36;
                    Main.dust[dust].noGravity = true;
                }

                for (int i = 0; i < 80; i++) {
                    PRT_LavaFire lavaFire = new PRT_LavaFire {
                        Velocity = CWRUtils.randVr(1, 9),
                        Position = spanPos,

                        Scale = Main.rand.NextFloat(2.9f, 4.6f),
                        Color = Color.White
                    };
                    lavaFire.ai[1] = 2;
                    PRTLoader.AddParticle(lavaFire);
                }
                for (int i = 0; i < 20; i++) {
                    PRT_LavaFire lavaFire = new PRT_LavaFire {
                        Velocity = CWRUtils.randVr(3, 6),
                        Position = spanPos,
                        Scale = Main.rand.NextFloat(0.8f, 1.2f),
                        Color = Color.White
                    };
                    lavaFire.ai[0] = 1;
                    lavaFire.ai[1] = 0;
                    lavaFire.minLifeTime = 60;
                    lavaFire.maxLifeTime = 90;
                    PRTLoader.AddParticle(lavaFire);
                }
                return;
            }

            for (int i = 0; i < 20; i++) {
                int dust = Dust.NewDust(spanPos, Projectile.width, Projectile.height, DustID.OrangeTorch, Scale: 6);
                Main.dust[dust].position = spanPos;
                Main.dust[dust].velocity = (MathHelper.TwoPi / 20f * i).ToRotationVector2() * 16;
                Main.dust[dust].noGravity = true;
            }
            for (int i = 0; i < 80; i++) {
                PRT_LavaFire lavaFire = new PRT_LavaFire {
                    Velocity = CWRUtils.randVr(1, 4),
                    Position = spanPos,

                    Scale = Main.rand.NextFloat(0.9f, 1.6f),
                    Color = Color.White
                };
                lavaFire.ai[1] = 2;
                PRTLoader.AddParticle(lavaFire);
            }
            for (int i = 0; i < 20; i++) {
                PRT_LavaFire lavaFire = new PRT_LavaFire {
                    Velocity = CWRUtils.randVr(3, 4),
                    Position = spanPos,
                    Scale = Main.rand.NextFloat(0.8f, 1.2f),
                    Color = Color.White
                };
                lavaFire.ai[0] = 1;
                lavaFire.ai[1] = 0;
                lavaFire.minLifeTime = 30;
                lavaFire.maxLifeTime = 60;
                PRTLoader.AddParticle(lavaFire);
            }
        }

        public override void AI() {
            if (Projectile.ai[2] == 0) {
                if (Projectile.ai[0] != 1) {
                    SoundEngine.PlaySound(SoundID.Item69, Projectile.Center);
                }
                else {
                    Vector2 origPos = Projectile.Center;
                    Projectile.width = Projectile.height = 1400;
                    Projectile.Center = origPos;
                    SoundEngine.PlaySound(SoundID.Item69 with { Pitch = 1.02f }, Projectile.Center);
                    if (!CWRUtils.isServer) {
                        for (int i = 0; i < 156; i++) {
                            Vector2 pos = Projectile.Center;
                            Vector2 particleSpeed = Main.rand.NextVector2Unit() * Main.rand.Next(13, 34);
                            BaseParticle energyLeak = new PRT_Light(pos, particleSpeed
                                , Main.rand.NextFloat(0.5f, 1.3f), Color.DarkRed, 30, 1, 1.5f, hueShift: 0.0f);
                            PRTLoader.AddParticle(energyLeak);
                        }
                    }
                }
                SpwanPRKAndDustEffect();
            }

            Projectile.ai[0] += 0.25f;
            if (Projectile.timeLeft > 15) {
                Projectile.localAI[0] += 0.25f;
                Projectile.ai[1] += 0.2f;
            }
            else {
                Projectile.localAI[0] -= 0.13f;
                Projectile.ai[1] -= 0.066f;
            }

            Projectile.localAI[1] += 0.07f;
            Projectile.ai[1] = Math.Clamp(Projectile.ai[1], 0f, 1f);
            Projectile.ai[2]++;
            Lighting.AddLight(Projectile.Center, new Vector3(1, 1, 1));
        }

        public override void OnKill(int timeLeft) {

        }

        public override bool ShouldUpdatePosition() => false;

        public override bool PreDraw(ref Color lightColor) => false;

        void IDrawWarp.Warp() {
            Texture2D warpTex = TextureAssets.Projectile[Type].Value;
            Color warpColor = new Color(45, 45, 45) * Projectile.ai[1];
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 drawOrig = warpTex.Size() / 2;
            int num = 13;
            if (Projectile.ai[0] == 1) {
                num = 66;
            }
            for (int i = 0; i < num; i++) {
                Main.spriteBatch.Draw(warpTex, drawPos, null, warpColor, Projectile.ai[0] + i * 115f
                    , drawOrig, Projectile.localAI[0] + i * 0.015f, SpriteEffects.None, 0f);
            }
        }

        bool IDrawWarp.noBlueshift() => true;

        bool IDrawWarp.canDraw() => false;

        void IDrawWarp.costomDraw(SpriteBatch spriteBatch) { }
    }
}
