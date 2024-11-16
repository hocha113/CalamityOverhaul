using CalamityOverhaul.Content.Particles;
using InnoVault.PRT;
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
        private int Time;
        private float sengs;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 400;
            Projectile.timeLeft = 30;
            Projectile.aiStyle = -1;
            Projectile.penetrate = -1;
            Projectile.friendly = true;
            Projectile.netImportant = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 2;
            Projectile.DamageType = DamageClass.Melee;
            sengs = Time = 0;
        }

        private void SpwanPRTAndDustEffect_ShootFireShowFromProjEX() {
            if (VaultUtils.isServer) {
                return;
            }

            Vector2 origPos = new Vector2(Projectile.ai[1], Projectile.ai[2]);
            Vector2 ToMouse = origPos.To(InMousePos);
            float lengs = ToMouse.Length();
            if (lengs < 140 * Projectile.scale) {
                lengs = 140 * Projectile.scale;
            }
            for (int i = 0; i < lengs / 12; i++) {
                PRT_LavaFire lavaFire = new PRT_LavaFire {
                    Velocity = ToMouse.UnitVector() * 2,
                    Position = origPos + Projectile.velocity * (1 + i) * 12,
                    Scale = Main.rand.NextFloat(1.8f, 3.2f),
                    Color = Color.White
                };
                lavaFire.ai[0] = 1;
                lavaFire.ai[1] = 0;
                lavaFire.minLifeTime = 22;
                lavaFire.maxLifeTime = 30;
                lavaFire.colors = new Color[3];
                lavaFire.colors[0] = new Color(255, 180, 60, 255);// 明亮的金红色
                lavaFire.colors[1] = new Color(220, 120, 40, 255);// 红金色过渡
                lavaFire.colors[2] = new Color(190, 80, 30, 255);// 深红金色，渐变目标
                PRTLoader.AddParticle(lavaFire);
            }

            for (int i = 0; i < 156; i++) {
                Vector2 pos = Projectile.Center;
                Vector2 particleSpeed = Main.rand.NextVector2Unit() * Main.rand.Next(13, 34);
                BasePRT energyLeak = new PRT_Light(pos, particleSpeed
                    , Main.rand.NextFloat(0.5f, 1.3f), Color.DarkRed, 30, 1, 1.5f, hueShift: 0.0f);
                PRTLoader.AddParticle(energyLeak);
            }
        }

        private void SpwanPRTAndDustEffect_ShootFireShowFromProj() {
            if (VaultUtils.isServer) {
                return;
            }

            float lengs = ToMouse.Length();
            if (lengs < 140 * Projectile.scale) {
                lengs = 140 * Projectile.scale;
            }
            for (int i = 0; i < lengs / 12; i++) {
                PRT_LavaFire lavaFire = new PRT_LavaFire {
                    Velocity = ToMouse.UnitVector() * 2,
                    Position = Owner.GetPlayerStabilityCenter() + Projectile.velocity * (1 + i) * 12,
                    Scale = Main.rand.NextFloat(0.8f, 1.2f),
                    Color = Color.White
                };
                lavaFire.ai[0] = 1;
                lavaFire.ai[1] = 0;
                lavaFire.minLifeTime = 22;
                lavaFire.maxLifeTime = 30;
                lavaFire.colors = new Color[3];
                lavaFire.colors[0] = new Color(255, 180, 60, 255);// 明亮的金红色
                lavaFire.colors[1] = new Color(220, 120, 40, 255);// 红金色过渡
                lavaFire.colors[2] = new Color(190, 80, 30, 255);// 深红金色，渐变目标
                PRTLoader.AddParticle(lavaFire);
            }
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
                    lavaFire.colors = new Color[3];
                    lavaFire.colors[0] = new Color(255, 180, 60, 255);// 明亮的金红色
                    lavaFire.colors[1] = new Color(220, 120, 40, 255);// 红金色过渡
                    lavaFire.colors[2] = new Color(190, 80, 30, 255);// 深红金色，渐变目标
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
                    lavaFire.colors = new Color[3];
                    lavaFire.colors[0] = new Color(255, 180, 60, 255);// 明亮的金红色
                    lavaFire.colors[1] = new Color(220, 120, 40, 255);// 红金色过渡
                    lavaFire.colors[2] = new Color(190, 80, 30, 255);// 深红金色，渐变目标
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
                lavaFire.colors = new Color[3];
                lavaFire.colors[0] = new Color(255, 180, 60, 255);// 明亮的金红色
                lavaFire.colors[1] = new Color(220, 120, 40, 255);// 红金色过渡
                lavaFire.colors[2] = new Color(190, 80, 30, 255);// 深红金色，渐变目标
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
                lavaFire.colors = new Color[3];
                lavaFire.colors[0] = new Color(255, 180, 60, 255);// 明亮的金红色
                lavaFire.colors[1] = new Color(220, 120, 40, 255);// 红金色过渡
                lavaFire.colors[2] = new Color(190, 80, 30, 255);// 深红金色，渐变目标
                PRTLoader.AddParticle(lavaFire);
            }
        }

        public override void AI() {
            if (Time == 0) {
                if (Projectile.ai[0] != 1) {
                    SoundEngine.PlaySound(SoundID.Item69, Projectile.Center);
                    SpwanPRTAndDustEffect_ShootFireShowFromProj();
                }
                else {
                    Projectile.timeLeft = 40;
                    SoundEngine.PlaySound(SoundID.Item69 with { Pitch = 1.02f }, Projectile.Center);
                    SpwanPRTAndDustEffect_ShootFireShowFromProjEX();
                }

                SpwanPRKAndDustEffect();
            }

            sengs += 0.25f;

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
            Lighting.AddLight(Projectile.Center, new Vector3(1, 1, 1));

            Time++;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            return CWRUtils.CircularHitboxCollision(Projectile.Center, Projectile.ai[0] == 1 ? 300 : 120, targetHitbox);
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool PreDraw(ref Color lightColor) => false;

        void IDrawWarp.Warp() {
            Texture2D warpTex = TextureAssets.Projectile[Type].Value;
            Color warpColor = new Color(45, 45, 45) * Projectile.ai[1];
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 drawOrig = warpTex.Size() / 2;
            int num = 10;
            if (Projectile.ai[0] == 1) {
                num = 26;
            }
            for (int i = 0; i < num; i++) {
                Main.spriteBatch.Draw(warpTex, drawPos, null, warpColor, sengs + i * 115f
                    , drawOrig, Projectile.localAI[0] + i * 0.015f, SpriteEffects.None, 0f);
            }
        }

        bool IDrawWarp.noBlueshift() => true;

        bool IDrawWarp.canDraw() => false;

        void IDrawWarp.costomDraw(SpriteBatch spriteBatch) { }
    }
}
