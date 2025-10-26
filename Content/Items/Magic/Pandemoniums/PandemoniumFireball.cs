using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Pandemoniums
{
    ///<summary>
    ///混沌魔能火球 - 改进为集束爆炸火球
    ///</summary>
    internal class PandemoniumFireball : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        private bool exploded = false;
        private ref float DelayTimer => ref Projectile.ai[0];
        private ref float ClusterMode => ref Projectile.ai[1]; //0=普通 1=集束模式
        private bool initialized = false;
        private Vector2 targetVelocity;
        private Vector2 targetPosition;

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.hostile = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            if (!initialized && DelayTimer > 0) {
                Projectile.velocity = Vector2.Zero;
                DelayTimer--;

                //延迟期间的充能效果 - 集束模式更强
                int chargeFreq = ClusterMode == 1 ? 1 : 2;
                if (Main.rand.NextBool(chargeFreq)) {
                    Color chargeColor = ClusterMode == 1 ? Color.Gold : Color.OrangeRed;
                    Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(30f, 30f),
                        DustID.Torch, Vector2.Zero, 100, chargeColor, 1.2f);
                    d.noGravity = true;
                }

                //集束模式：充能期间向目标位置移动
                if (ClusterMode == 1 && DelayTimer < 40) {
                    Vector2 toTarget = (targetPosition - Projectile.Center).SafeNormalize(Vector2.Zero);
                    Projectile.Center += toTarget * 2f;
                }

                return;
            }

            if (!initialized) {
                initialized = true;
                targetVelocity = Projectile.velocity;
                
                //集束模式：记录目标位置
                if (ClusterMode == 1) {
                    targetPosition = Projectile.Center;
                }
            }

            //加速发射
            float accelSpeed = ClusterMode == 1 ? 18f : 15f;
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, targetVelocity.SafeNormalize(Vector2.Zero) * accelSpeed, 0.1f);
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //增强拖尾效果
            for (int i = 0; i < 3; i++) {
                Vector2 offset = Projectile.velocity.SafeNormalize(Vector2.Zero) * -i * 8f;
                Color dustColor = ClusterMode == 1 ? 
                    Color.Lerp(Color.Gold, Color.OrangeRed, i / 3f) :
                    Color.Lerp(Color.Red, Color.Orange, i / 3f);
                
                var d = Dust.NewDustPerfect(Projectile.Center + offset, DustID.AncientLight, Vector2.Zero, 100, dustColor, 2.0f);
                d.noGravity = true;
                d.velocity = Main.rand.NextVector2Circular(1.5f, 1.5f);
            }

            //火焰粒子
            if (Main.rand.NextBool(1)) {
                Color flameColor = ClusterMode == 1 ? Color.Gold : Color.OrangeRed;
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                    Main.rand.NextVector2Circular(2f, 2f), 100, flameColor, 1.5f);
                d.noGravity = true;
            }

            //集束模式：到达目标位置后等待一起爆炸
            if (ClusterMode == 1 && Projectile.Distance(targetPosition) < 50f) {
                Projectile.velocity *= 0.9f;
                Projectile.tileCollide = false;
                
                //等待其他火球到位
                if (Projectile.timeLeft > 60) {
                    Projectile.timeLeft = 60;
                }
            }

            float lightMult = ClusterMode == 1 ? 2.0f : 1.5f;
            Lighting.AddLight(Projectile.Center, lightMult, 0.8f * lightMult, 0.4f * lightMult);
        }

        public override void OnKill(int timeLeft) {
            if (exploded) return;
            Explode();
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Explode();
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (exploded) return;
            Explode();
        }

        private void Explode() {
            if (exploded) return;
            exploded = true;

            //集束模式：更大的爆炸范围
            int explosionSize = ClusterMode == 1 ? 400 : 300;
            Projectile.position = Projectile.Center;
            Projectile.width = Projectile.height = explosionSize;
            Projectile.Center = Projectile.position;
            Projectile.Damage();

            SoundEngine.PlaySound(SoundID.Item74 with { Volume = 1.2f, Pitch = -0.5f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = 0.8f, Pitch = -0.3f }, Projectile.Center);

            //强化爆炸粒子
            int particleCount = ClusterMode == 1 ? 120 : 80;
            for (int i = 0; i < particleCount; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(15f, 15f);
                Color particleColor = ClusterMode == 1 ? 
                    new Color(255, Main.rand.Next(150, 220), 40) :
                    new Color(255, Main.rand.Next(80, 150), 40);
                
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, vel, 100,
                    particleColor, Main.rand.NextFloat(2.0f, 3.5f));
                d.noGravity = true;
            }

            for (int i = 0; i < 50; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(10f, 10f);
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, vel, 150,
                    Color.DarkGray, Main.rand.NextFloat(2.0f, 3.0f));
                d.noGravity = true;
            }

            //火环效果
            int ringCount = ClusterMode == 1 ? 45 : 30;
            for (int i = 0; i < ringCount; i++) {
                float angle = MathHelper.TwoPi * i / ringCount;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(8f, 12f);
                Color ringColor = ClusterMode == 1 ? Color.Gold : Color.OrangeRed;
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, vel, 100, ringColor, 2.0f);
                d.noGravity = true;
            }

            //集束模式：爆炸后产生二次爆炸
            if (ClusterMode == 1 && Projectile.owner == Main.myPlayer) {
                for (int i = 0; i < 6; i++) {
                    float angle = MathHelper.TwoPi * i / 6f;
                    Vector2 secondaryPos = Projectile.Center + angle.ToRotationVector2() * 120f;
                    Vector2 secondaryVel = angle.ToRotationVector2() * 2f;
                    
                    Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(),
                        secondaryPos,
                        secondaryVel,
                        ModContent.ProjectileType<PandemoniumFireball>(),
                        (int)(Projectile.damage * 0.5f),
                        Projectile.knockBack * 0.5f,
                        Projectile.owner,
                        0, //无延迟
                        0  //普通模式
                    );
                }
            }

            Projectile.active = false;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (!initialized && DelayTimer > 0) return false;

            Texture2D glow = CWRAsset.SoftGlow.Value;
            float time = Main.GlobalTimeWrappedHourly;
            float pulse = (float)Math.Sin(time * 25f) * 0.5f + 0.5f;

            //集束模式：金色调
            Color c1, c2, c3, c4;
            if (ClusterMode == 1) {
                c1 = new Color(255, 250, 200, 0);
                c2 = new Color(255, 200, 100, 0);
                c3 = new Color(255, 150, 50, 0);
                c4 = new Color(200, 100, 30, 0);
            }
            else {
                c1 = new Color(255, 240, 200, 0);
                c2 = new Color(255, 150, 60, 0);
                c3 = new Color(255, 80, 30, 0);
                c4 = new Color(180, 30, 20, 0);
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float scaleMult = ClusterMode == 1 ? 1.2f : 1f;

            Main.spriteBatch.Draw(glow, drawPos, null, c4 * 0.6f, 0, glow.Size() / 2, Projectile.scale * 2.5f * scaleMult, 0, 0);
            Main.spriteBatch.Draw(glow, drawPos, null, c3 * 0.8f, time * 2f, glow.Size() / 2, Projectile.scale * 2.0f * scaleMult, 0, 0);
            Main.spriteBatch.Draw(glow, drawPos, null, c2, -time * 3f, glow.Size() / 2, Projectile.scale * (1.5f + pulse * 0.3f) * scaleMult, 0, 0);
            Main.spriteBatch.Draw(glow, drawPos, null, c1, time * 4f, glow.Size() / 2, Projectile.scale * (1.0f + pulse * 0.5f) * scaleMult, 0, 0);

            return false;
        }
    }
}
