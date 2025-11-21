using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
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
    internal class HolyColliderExFire : BaseHeldProj, IWarpDrawable
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle";
        private int Time;
        private float sengs;
        private float expansionSpeed; //扩散速度
        private bool hasPlayedSound; //是否已播放音效

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 400;
            Projectile.timeLeft = 30;
            Projectile.aiStyle = -1;
            Projectile.penetrate = -1;
            Projectile.friendly = true;
            Projectile.netImportant = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 6;
            Projectile.DamageType = DamageClass.Melee;
            sengs = Time = 0;
            expansionSpeed = 0.25f;
        }

        private void SpwanPRTAndDustEffect_ShootFireShowFromProjEX() {
            if (VaultUtils.isServer) {
                return;
            }

            Vector2 origPos = new Vector2(Projectile.ai[1], Projectile.ai[2]);
            Vector2 ToMouse = origPos.To(InMousePos);
            float lengs = ToMouse.Length();
            if (lengs < 145 * Projectile.scale) {
                lengs = 145 * Projectile.scale;
            }

            //增强的火焰路径
            for (int i = 0; i < lengs / 8; i++) {
                Vector2 currentPos = origPos + Projectile.velocity * (1 + i) * 8;
                
                //主火焰粒子
                PRT_LavaFire lavaFire = new PRT_LavaFire {
                    Velocity = ToMouse.UnitVector() * Main.rand.NextFloat(1.5f, 3f),
                    Position = currentPos + Main.rand.NextVector2Circular(15, 15),
                    Scale = Main.rand.NextFloat(2.5f, 4.5f),
                    Color = Color.White
                };
                lavaFire.ai[0] = 1;
                lavaFire.ai[1] = 0;
                lavaFire.minLifeTime = 28;
                lavaFire.maxLifeTime = 40;
                lavaFire.colors = new Color[3];
                lavaFire.colors[0] = new Color(255, 200, 80, 255);
                lavaFire.colors[1] = new Color(240, 140, 50, 255);
                lavaFire.colors[2] = new Color(200, 90, 30, 255);
                PRTLoader.AddParticle(lavaFire);

                //边缘火花
                if (i % 3 == 0) {
                    Vector2 perpendicular = ToMouse.UnitVector().RotatedBy(MathHelper.PiOver2);
                    for (int j = -1; j <= 1; j += 2) {
                        Dust spark = Dust.NewDustPerfect(currentPos + perpendicular * j * 20, DustID.Torch, 
                            perpendicular * j * Main.rand.NextFloat(2, 4), 0, Color.OrangeRed, 2.5f);
                        spark.noGravity = true;
                    }
                }
            }

            //强化的爆发粒子
            for (int i = 0; i < 200; i++) {
                Vector2 pos = Projectile.Center;
                Vector2 particleSpeed = Main.rand.NextVector2Unit() * Main.rand.Next(15, 45);
                Color particleColor = Main.rand.NextBool() ? Color.Gold : Color.OrangeRed;
                BasePRT energyLeak = new PRT_Light(pos, particleSpeed
                    , Main.rand.NextFloat(0.8f, 1.8f), particleColor, 40, 1, 1.8f, hueShift: 0.0f);
                PRTLoader.AddParticle(energyLeak);
            }

            //冲击波环
            for (int i = 0; i < 3; i++) {
                float ringRadius = 100 + i * 80;
                int dustCount = (int)(ringRadius / 5);
                for (int j = 0; j < dustCount; j++) {
                    float angle = MathHelper.TwoPi / dustCount * j;
                    Vector2 ringPos = Projectile.Center + angle.ToRotationVector2() * ringRadius;
                    Dust ring = Dust.NewDustPerfect(ringPos, DustID.Torch, angle.ToRotationVector2() * 5, 0, Color.Gold, 2f);
                    ring.noGravity = true;
                }
            }
        }

        private void SpwanPRTAndDustEffect_ShootFireShowFromProj() {
            if (VaultUtils.isServer) {
                return;
            }

            float lengs = ToMouse.Length();
            if (lengs < 145 * Projectile.scale) {
                lengs = 145 * Projectile.scale;
            }

            //快速火焰斩击路径
            for (int i = 0; i < lengs / 10; i++) {
                PRT_LavaFire lavaFire = new PRT_LavaFire {
                    Velocity = ToMouse.UnitVector() * Main.rand.NextFloat(1f, 2.5f),
                    Position = Owner.GetPlayerStabilityCenter() + Projectile.velocity * (1 + i) * 10 + Main.rand.NextVector2Circular(8, 8),
                    Scale = Main.rand.NextFloat(1.2f, 2f),
                    Color = Color.White
                };
                lavaFire.ai[0] = 1;
                lavaFire.ai[1] = 0;
                lavaFire.minLifeTime = 20;
                lavaFire.maxLifeTime = 32;
                lavaFire.colors = new Color[3];
                lavaFire.colors[0] = new Color(255, 190, 70, 255);
                lavaFire.colors[1] = new Color(230, 130, 45, 255);
                lavaFire.colors[2] = new Color(200, 85, 30, 255);
                PRTLoader.AddParticle(lavaFire);
            }
        }

        private void SpwanPRKAndDustEffect() {
            if (Main.dedServ) {
                return;
            }

            Vector2 spanPos = Projectile.Center;

            if (Projectile.ai[0] == 1) {
                //蓄力重击的爆炸效果
                int[] dustRings = new int[] { 25, 35, 45 };
                float[] speeds = new float[] { 18, 28, 38 };

                for (int ring = 0; ring < dustRings.Length; ring++) {
                    for (int i = 0; i < dustRings[ring]; i++) {
                        float angle = MathHelper.TwoPi / dustRings[ring] * i;
                        Vector2 velocity = angle.ToRotationVector2() * speeds[ring];
                        
                        int dust = Dust.NewDust(spanPos, Projectile.width, Projectile.height, DustID.Torch, Scale: 7);
                        Main.dust[dust].position = spanPos;
                        Main.dust[dust].velocity = velocity;
                        Main.dust[dust].noGravity = true;
                        Main.dust[dust].fadeIn = 1.5f;
                    }
                }

                //大量爆炸火焰粒子
                for (int i = 0; i < 120; i++) {
                    PRT_LavaFire lavaFire = new PRT_LavaFire {
                        Velocity = VaultUtils.RandVr(2, 12),
                        Position = spanPos,
                        Scale = Main.rand.NextFloat(3.5f, 5.5f),
                        Color = Color.White
                    };
                    lavaFire.ai[1] = 2;
                    lavaFire.colors = new Color[3];
                    lavaFire.colors[0] = new Color(255, 200, 80, 255);
                    lavaFire.colors[1] = new Color(240, 140, 50, 255);
                    lavaFire.colors[2] = new Color(200, 90, 30, 255);
                    PRTLoader.AddParticle(lavaFire);
                }

                //持续燃烧粒子
                for (int i = 0; i < 35; i++) {
                    PRT_LavaFire lavaFire = new PRT_LavaFire {
                        Velocity = VaultUtils.RandVr(4, 8),
                        Position = spanPos,
                        Scale = Main.rand.NextFloat(1.2f, 1.8f),
                        Color = Color.White
                    };
                    lavaFire.ai[0] = 1;
                    lavaFire.ai[1] = 0;
                    lavaFire.minLifeTime = 70;
                    lavaFire.maxLifeTime = 100;
                    lavaFire.colors = new Color[3];
                    lavaFire.colors[0] = new Color(255, 200, 80, 255);
                    lavaFire.colors[1] = new Color(240, 140, 50, 255);
                    lavaFire.colors[2] = new Color(200, 90, 30, 255);
                    PRTLoader.AddParticle(lavaFire);
                }
                
                expansionSpeed = 0.35f;
                return;
            }

            //普通攻击效果
            for (int i = 0; i < 25; i++) {
                float angle = MathHelper.TwoPi / 25 * i;
                int dust = Dust.NewDust(spanPos, Projectile.width, Projectile.height, DustID.Torch, Scale: 6);
                Main.dust[dust].position = spanPos;
                Main.dust[dust].velocity = angle.ToRotationVector2() * 18;
                Main.dust[dust].noGravity = true;
            }

            for (int i = 0; i < 100; i++) {
                PRT_LavaFire lavaFire = new PRT_LavaFire {
                    Velocity = VaultUtils.RandVr(2, 6),
                    Position = spanPos,
                    Scale = Main.rand.NextFloat(1.2f, 2.2f),
                    Color = Color.White
                };
                lavaFire.ai[1] = 2;
                lavaFire.colors = new Color[3];
                lavaFire.colors[0] = new Color(255, 190, 70, 255);
                lavaFire.colors[1] = new Color(230, 130, 45, 255);
                lavaFire.colors[2] = new Color(200, 85, 30, 255);
                PRTLoader.AddParticle(lavaFire);
            }

            for (int i = 0; i < 25; i++) {
                PRT_LavaFire lavaFire = new PRT_LavaFire {
                    Velocity = VaultUtils.RandVr(3, 5),
                    Position = spanPos,
                    Scale = Main.rand.NextFloat(1f, 1.5f),
                    Color = Color.White
                };
                lavaFire.ai[0] = 1;
                lavaFire.ai[1] = 0;
                lavaFire.minLifeTime = 35;
                lavaFire.maxLifeTime = 65;
                lavaFire.colors = new Color[3];
                lavaFire.colors[0] = new Color(255, 190, 70, 255);
                lavaFire.colors[1] = new Color(230, 130, 45, 255);
                lavaFire.colors[2] = new Color(200, 85, 30, 255);
                PRTLoader.AddParticle(lavaFire);
            }
        }

        public override void AI() {
            if (Time == 0) {
                if (Projectile.ai[0] != 1) {
                    SoundEngine.PlaySound(SoundID.Item69 with { Pitch = 0.2f, Volume = 1.2f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.DD2_BetsyFireballShot with { Pitch = -0.3f }, Projectile.Center);
                    SpwanPRTAndDustEffect_ShootFireShowFromProj();
                }
                else {
                    Projectile.timeLeft = 45;
                    SoundEngine.PlaySound(SoundID.Item69 with { Pitch = 1.2f, Volume = 1.5f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Pitch = -0.4f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Item14, Projectile.Center);
                    SpwanPRTAndDustEffect_ShootFireShowFromProjEX();
                }

                SpwanPRKAndDustEffect();
            }

            //动态扩散速度
            sengs += expansionSpeed;

            if (Projectile.timeLeft > 18) {
                Projectile.localAI[0] += expansionSpeed;
                Projectile.ai[1] += 0.25f;
            }
            else {
                Projectile.localAI[0] -= 0.15f;
                Projectile.ai[1] -= 0.08f;
            }

            //燃烧持续粒子效果
            if (Time % 2 == 0 && Projectile.timeLeft > 10) {
                Vector2 randomPos = Projectile.Center + Main.rand.NextVector2Circular(
                    Projectile.ai[0] == 1 ? 250 : 100, 
                    Projectile.ai[0] == 1 ? 250 : 100
                );
                Dust burn = Dust.NewDustPerfect(randomPos, DustID.Torch, Vector2.Zero, 0, Color.Orange, 2f);
                burn.noGravity = true;
            }

            Projectile.localAI[1] += 0.09f;
            Projectile.ai[1] = Math.Clamp(Projectile.ai[1], 0f, 1f);
            
            //强烈的光照效果
            float lightIntensity = Projectile.ai[0] == 1 ? 1.5f : 1f;
            Lighting.AddLight(Projectile.Center, new Vector3(1, 0.8f, 0.3f) * lightIntensity);

            Time++;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //击中音效
            if (!hasPlayedSound) {
                SoundEngine.PlaySound(SoundID.DD2_BetsyFireballImpact with { Pitch = Projectile.ai[0] == 1 ? -0.2f : 0.1f }, target.Center);
                hasPlayedSound = true;
            }

            //击中粒子
            for (int i = 0; i < (Projectile.ai[0] == 1 ? 12 : 6); i++) {
                Vector2 vel = Main.rand.NextVector2Circular(6, 6);
                Dust hitDust = Dust.NewDustPerfect(target.Center, DustID.Torch, vel, 0, Color.OrangeRed, 2.5f);
                hitDust.noGravity = true;
            }

            //添加燃烧效果
            target.AddBuff(BuffID.OnFire3, Projectile.ai[0] == 1 ? 360 : 180);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (target.IsWormBody()) {
                modifiers.DisableCrit();
                modifiers.FinalDamage /= 16;
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float radius = Projectile.ai[0] == 1 ? 320 : 130;
            return VaultUtils.CircleIntersectsRectangle(Projectile.Center, radius * Projectile.localAI[0], targetHitbox);
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool PreDraw(ref Color lightColor) => false;

        void IWarpDrawable.Warp() {
            Texture2D warpTex = TextureAssets.Projectile[Type].Value;
            Color warpColor = new Color(55, 40, 25) * Projectile.ai[1];
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 drawOrig = warpTex.Size() / 2;
            
            int num = Projectile.ai[0] == 1 ? 32 : 12;
            float scaleMultiplier = Projectile.ai[0] == 1 ? 0.018f : 0.012f;

            for (int i = 0; i < num; i++) {
                float rotation = sengs + i * (Projectile.ai[0] == 1 ? 95f : 115f);
                float scale = Projectile.localAI[0] + i * scaleMultiplier;
                Main.spriteBatch.Draw(warpTex, drawPos, null, warpColor, rotation
                    , drawOrig, scale, SpriteEffects.None, 0f);
            }
        }

        bool IWarpDrawable.DontUseBlueshiftEffect() => true;

        bool IWarpDrawable.CanDrawCustom() => false;

        void IWarpDrawable.DrawCustom(SpriteBatch spriteBatch) { }
    }
}
