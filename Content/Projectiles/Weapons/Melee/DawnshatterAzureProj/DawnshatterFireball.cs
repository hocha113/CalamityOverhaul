using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.DawnshatterAzureProj
{
    internal class DawnshatterFireball : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private float scale = 1f;
        private int trailCounter;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 6;
            Projectile.timeLeft = 180;
            Projectile.alpha = 0;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void AI() {
            //轻微重力和空气阻力
            Projectile.velocity.Y += 0.12f;
            Projectile.velocity *= 0.995f;

            //旋转
            Projectile.rotation += Projectile.velocity.Length() * 0.05f;

            //脉冲缩放效果
            scale = 1f + (float)System.Math.Sin(Projectile.timeLeft * 0.2f) * 0.15f;

            //华丽的火焰拖尾
            if (Main.rand.NextBool()) {
                SpawnTrailEffect();
            }

            //寻找敌人追踪
            if (Projectile.timeLeft > 40 && Projectile.timeLeft % 10 == 0) {
                NPC target = Projectile.Center.FindClosestNPC(300f);
                if (target != null) {
                    Vector2 toTarget = target.Center - Projectile.Center;
                    float targetAngle = toTarget.ToRotation();
                    float currentAngle = Projectile.velocity.ToRotation();

                    //平滑转向
                    float angleDiff = MathHelper.WrapAngle(targetAngle - currentAngle);
                    Projectile.velocity = Projectile.velocity.RotatedBy(angleDiff * 0.1f);
                    Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * MathHelper.Lerp(Projectile.velocity.Length(), 14f, 0.05f);
                }
            }

            //淡出效果
            if (Projectile.timeLeft < 40) {
                Projectile.alpha += 6;
            }

            //添加光照
            Lighting.AddLight(Projectile.Center, new Vector3(1.2f, 0.8f, 0.3f) * scale);

            //环绕粒子
            trailCounter++;
            if (trailCounter % 3 == 0) {
                SpawnOrbitParticle();
            }
        }

        //拖尾特效
        private void SpawnTrailEffect() {
            Vector2 trailPos = Projectile.Center - Projectile.velocity * 0.5f + Main.rand.NextVector2Circular(5f, 5f);

            BasePRT trail = new PRT_Light(trailPos, -Projectile.velocity * 0.3f, Main.rand.NextFloat(0.8f, 1.5f)
                , VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.Red, Color.Orange, Color.Yellow)
                , 15, 0.4f, 1.2f);
            PRTLoader.AddParticle(trail);

            int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.FireworkFountain_Yellow;
            int dust = Dust.NewDust(trailPos, 1, 1, dustType, -Projectile.velocity.X * 0.3f, -Projectile.velocity.Y * 0.3f, 100, default, 1.5f);
            Main.dust[dust].noGravity = true;
        }

        //环绕粒子
        private void SpawnOrbitParticle() {
            float angle = trailCounter * 0.3f;
            Vector2 offset = angle.ToRotationVector2() * 15f * scale;
            Vector2 particlePos = Projectile.Center + offset;

            int dust = Dust.NewDust(particlePos, 1, 1, DustID.GoldCoin, 0, 0, 100, default, 1.2f);
            Main.dust[dust].noGravity = true;
            Main.dust[dust].velocity = Projectile.velocity * 0.2f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //强力debuff
            target.AddBuff(BuffID.OnFire3, 300);
            target.AddBuff(BuffID.Daybreak, 240);

            Projectile.penetrate--;

            if (Projectile.penetrate <= 0) {
                Projectile.Kill();
                return;
            }

            //命中爆发
            SpawnHitBurst(target.Center);

            //命中音效
            SoundEngine.PlaySound("CalamityMod/Sounds/Custom/Yharon/YharonFireOrb".GetSound() with { Volume = 0.4f, Pitch = 0.5f }, target.Center);

            //反弹并减速
            Projectile.velocity *= -0.7f;
            Projectile.velocity = Projectile.velocity.RotatedByRandom(0.5f);

            //如果穿透次数还剩很多，尝试寻找新目标
            if (Projectile.penetrate > 2) {
                NPC newTarget = Projectile.Center.FindClosestNPC(400f, false, true, new System.Collections.Generic.HashSet<NPC> { target });
                if (newTarget != null) {
                    Vector2 toNewTarget = (newTarget.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * 12f;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, toNewTarget, 0.3f);
                }
            }
        }

        //命中爆发
        private void SpawnHitBurst(Vector2 position) {
            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(8f, 8f);

                BasePRT burst = new PRT_Light(position, vel, Main.rand.NextFloat(1f, 1.8f)
                    , VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.OrangeRed, Color.Orange, Color.Yellow)
                    , 20, 0.5f, 1.3f);
                PRTLoader.AddParticle(burst);
            }

            for (int i = 0; i < 10; i++) {
                int dust = Dust.NewDust(position, 1, 1, DustID.Torch, 0, 0, 100, default, Main.rand.NextFloat(1.5f, 2.5f));
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(6f, 6f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //碰撞时弹跳
            if (System.Math.Abs(Projectile.velocity.X - oldVelocity.X) > float.Epsilon) {
                Projectile.velocity.X = -oldVelocity.X * 0.7f;
            }
            if (System.Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > float.Epsilon) {
                Projectile.velocity.Y = -oldVelocity.Y * 0.7f;
            }

            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.4f, Pitch = 0.4f }, Projectile.Center);

            //生成碰撞粒子
            for (int i = 0; i < 8; i++) {
                int dust = Dust.NewDust(Projectile.Center, 1, 1, DustID.Torch, 0, 0, 100, default, 1.5f);
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(4f, 4f);
                Main.dust[dust].noGravity = true;
            }

            Projectile.penetrate--;
            return Projectile.penetrate <= 0;
        }

        public override void OnKill(int timeLeft) {
            //爆炸音效
            SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = 0.5f, Pitch = 0.4f }, Projectile.Center);

            //爆炸粒子
            for (int i = 0; i < 35; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(12f, 12f);

                BasePRT explosion = new PRT_Light(Projectile.Center, vel, Main.rand.NextFloat(1.5f, 2.5f)
                    , VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.Red, Color.Orange, Color.Yellow, Color.White)
                    , 30, 0.6f, 1.8f);
                PRTLoader.AddParticle(explosion);
            }

            //火焰尘埃
            for (int i = 0; i < 40; i++) {
                int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.FireworkFountain_Red;
                int dust = Dust.NewDust(Projectile.Center, 1, 1, dustType, 0, 0, 100, default, Main.rand.NextFloat(2f, 3.5f));
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(10f, 10f);
                Main.dust[dust].noGravity = true;
            }

            //金色爆炸光效
            for (int i = 0; i < 20; i++) {
                int dust = Dust.NewDust(Projectile.Center, 1, 1, DustID.GoldCoin, 0, 0, 100, default, 2.5f);
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(8f, 8f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].fadeIn = 1.5f;
            }

            //冲击波
            for (int i = 0; i < 16; i++) {
                float angle = MathHelper.TwoPi * i / 16f;
                Vector2 shockPos = Projectile.Center + angle.ToRotationVector2() * 40f;

                int dust = Dust.NewDust(shockPos, 1, 1, DustID.FireworkFountain_Yellow, 0, 0, 100, default, 2f);
                Main.dust[dust].velocity = angle.ToRotationVector2() * 6f;
                Main.dust[dust].noGravity = true;
            }
        }

        public override Color? GetAlpha(Color lightColor) {
            //火球自发光
            float intensity = 1f - Projectile.alpha / 255f;
            return VaultUtils.MultiStepColorLerp(0.5f, Color.OrangeRed, Color.Orange, Color.Yellow) * intensity;
        }
    }
}
