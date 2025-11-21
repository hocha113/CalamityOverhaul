using CalamityMod.Projectiles.Melee;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class BrinyBaronOrb : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private float rotationSpeed; //旋转速度
        private float expansionPhase; //扩散阶段
        private int trailTimer; //拖尾计时器

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
            Projectile.timeLeft = 600;
            Projectile.extraUpdates = 8;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.penetrate = 1;
            rotationSpeed = Main.rand.NextFloat(0.15f, 0.25f);
        }

        public override void AI() {
            trailTimer++;
            expansionPhase = MathHelper.Clamp(Projectile.timeLeft / 600f, 0.3f, 1f);

            //旋转效果
            Projectile.rotation += rotationSpeed;

            //动态气泡生成
            if (trailTimer % 4 == 0) {
                Gore bubble = Gore.NewGorePerfect(
                    Projectile.GetSource_FromAI(),
                    Projectile.position,
                    Projectile.velocity * 0.15f + Main.rand.NextVector2Circular(1.5f, 1.5f),
                    Main.rand.NextBool(3) ? 412 : 411
                );
                bubble.timeLeft = 12 + Main.rand.Next(10);
                bubble.scale = Main.rand.NextFloat(0.8f, 1.4f) * expansionPhase;
                bubble.type = Main.rand.NextBool(3) ? 412 : 411;
            }

            //优化水滴粒子 - 减少频率但增强效果
            if (trailTimer % 3 == 0) {
                for (int i = 0; i < 3; i++) {
                    Dust water = Dust.NewDustDirect(
                        Projectile.position,
                        Projectile.width,
                        Projectile.height,
                        DustID.DungeonWater
                    );
                    water.noGravity = true;
                    water.velocity = Projectile.velocity * 0.4f + Main.rand.NextVector2Circular(1, 1);
                    water.scale = 1.5f * expansionPhase;
                    water.alpha = 100;
                }
            }

            //水流漩涡粒子效果
            if (trailTimer % 6 == 0) {
                Vector2 perpendicular = Projectile.velocity.RotatedBy(MathHelper.PiOver2).SafeNormalize(Vector2.UnitY);
                for (int i = -1; i <= 1; i += 2) {
                    Dust swirl = Dust.NewDustPerfect(
                        Projectile.Center + perpendicular * i * 8,
                        DustID.DungeonWater,
                        perpendicular * i * 2 + Projectile.velocity * 0.2f,
                        0, default, 1.3f
                    );
                    swirl.noGravity = true;
                }
            }

            //光照效果
            Lighting.AddLight(Projectile.Center, 0.2f, 0.4f, 0.6f);
        }

        public override void OnKill(int timeLeft) {
            //增强爆炸音效
            SoundEngine.PlaySound(SoundID.Item96 with { Pitch = -0.1f, Volume = 1.2f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.8f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item54, Projectile.Center);

            GenerateExplosionBubbles();
            GenerateWaterExplosion();
            GenerateWaterParticles();

            if (Projectile.IsOwnedByLocalPlayer()) {
                //生成水泡爆炸
                Projectile.NewProjectile(
                    Projectile.GetSource_FromAI(),
                    Projectile.Center + new Vector2(0, 16),
                    Vector2.Zero,
                    ModContent.ProjectileType<BrinyTyphoonBubble>(),
                    Projectile.damage,
                    Projectile.knockBack,
                    Projectile.owner
                );
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //击中音效
            SoundEngine.PlaySound(SoundID.NPCHit13 with { Pitch = 0.3f, Volume = 0.6f }, target.Center);

            //击中水花
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(5, 5);
                Dust water = Dust.NewDustPerfect(target.Center, DustID.DungeonWater, vel, 0, default, 1.8f);
                water.noGravity = true;
            }

            //击中气泡
            for (int i = 0; i < 3; i++) {
                Gore bubble = Gore.NewGorePerfect(
                    Projectile.GetSource_FromAI(),
                    target.Center,
                    Main.rand.NextVector2Circular(3, 3),
                    Main.rand.NextBool() ? 411 : 412
                );
                bubble.timeLeft = 10;
                bubble.scale = Main.rand.NextFloat(0.6f, 1f);
            }
        }

        //生成爆炸气泡效果
        private void GenerateExplosionBubbles() {
            for (int i = 0; i < 20; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(6, 6);
                Gore bubble = Gore.NewGorePerfect(
                    Projectile.GetSource_FromAI(),
                    Projectile.Center,
                    velocity,
                    Main.rand.NextBool(2) ? 412 : 411
                );
                bubble.timeLeft = 15 + Main.rand.Next(12);
                bubble.scale = Main.rand.NextFloat(0.8f, 1.6f);
            }
        }

        //生成水花爆炸
        private void GenerateWaterExplosion() {
            //水花环
            int ringCount = 3;
            for (int ring = 0; ring < ringCount; ring++) {
                int particleCount = 20 + ring * 8;
                float radius = 8 + ring * 6;

                for (int j = 0; j < particleCount; j++) {
                    float angle = MathHelper.TwoPi / particleCount * j;
                    Vector2 velocity = angle.ToRotationVector2() * (radius + Main.rand.NextFloat(-2, 2));

                    Dust water = Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.DungeonWater,
                        velocity,
                        0, default,
                        1.5f + ring * 0.3f
                    );
                    water.noGravity = true;
                    water.velocity *= 0.8f + ring * 0.2f;
                }
            }
        }

        //生成水流粒子效果
        private void GenerateWaterParticles() {
            //径向水滴效果
            for (int i = 0; i < 4; i++) {
                int particleCount = 30 + i * 8;

                for (int j = 0; j < particleCount; j++) {
                    Vector2 offset = Vector2.Normalize(Projectile.velocity) *
                        new Vector2(Projectile.width / 2f, Projectile.height) * 0.75f;
                    Vector2 rotatedOffset = offset.RotatedBy(
                        (j - (particleCount / 2 - 1)) * MathHelper.TwoPi / particleCount
                    ) + Projectile.Center;
                    Vector2 faceDirection = rotatedOffset - Projectile.Center;

                    int dustIndex = Dust.NewDust(
                        rotatedOffset + faceDirection,
                        0, 0,
                        DustID.DungeonWater,
                        faceDirection.X * 2.5f,
                        faceDirection.Y * 2.5f,
                        100, default,
                        1.6f + i * 0.2f
                    );

                    Dust dust = Main.dust[dustIndex];
                    dust.noGravity = true;
                    dust.velocity = faceDirection * (1 + i * 0.5f);
                    dust.fadeIn = 1.2f;
                }
            }
        }
    }
}
