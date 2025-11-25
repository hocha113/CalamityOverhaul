using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.AriaofTheCosmoses
{
    /// <summary>
    /// 寰宇咏叹调Q技能，星环守护
    /// 召唤多个小型吸积盘环绕玩家，自动攻击敌人
    /// </summary>
    internal class AriaQSkill : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private const int MaxDiskCount = 6;
        private const int SkillDuration = 600; //10秒
        private float orbitRadius = 150f;
        private float orbitSpeed = 0.05f;
        private int[] diskIndices = new int[MaxDiskCount];

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = SkillDuration;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
        }

        public override void AI() {
            Player player = Main.player[Projectile.owner];
            
            if (!player.active || player.dead) {
                Projectile.Kill();
                return;
            }

            //跟随玩家
            Projectile.Center = player.Center;

            //初始化吸积盘
            if (Projectile.ai[0] == 0) {
                InitializeDisks(player);
                Projectile.ai[0] = 1;

                //播放激活音效
                SoundEngine.PlaySound(SoundID.Item109 with { 
                    Volume = 0.8f, 
                    Pitch = 0.3f 
                }, Projectile.Center);

                //生成激活特效
                SpawnActivationEffect();
            }

            //更新所有吸积盘
            UpdateOrbitingDisks(player);

            //生成环绕粒子
            if (Projectile.timeLeft % 3 == 0) {
                SpawnOrbitParticles();
            }

            //淡出效果
            if (Projectile.timeLeft < 60) {
                float fadeProgress = Projectile.timeLeft / 60f;
                for (int i = 0; i < MaxDiskCount; i++) {
                    if (diskIndices[i] >= 0 && Main.projectile[diskIndices[i]].active) {
                        Main.projectile[diskIndices[i]].alpha = (int)(255 * (1f - fadeProgress));
                    }
                }
            }

            //发光效果
            float pulseIntensity = (float)Math.Sin(Projectile.timeLeft * 0.1f) * 0.5f + 0.5f;
            Lighting.AddLight(Projectile.Center, 
                new Vector3(1f, 0.8f, 0.3f) * pulseIntensity * 0.6f);
        }

        private void InitializeDisks(Player player) {
            for (int i = 0; i < MaxDiskCount; i++) {
                float angle = MathHelper.TwoPi * i / MaxDiskCount;
                Vector2 offset = angle.ToRotationVector2() * orbitRadius;
                
                int diskIndex = Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    player.Center + offset,
                    Vector2.Zero,
                    ModContent.ProjectileType<AriaQSkillDisk>(),
                    (int)(Projectile.damage * 0.6f),
                    Projectile.knockBack * 0.5f,
                    Projectile.owner,
                    angle,
                    Projectile.whoAmI
                );
                
                diskIndices[i] = diskIndex;
            }
        }

        private void UpdateOrbitingDisks(Player player) {
            for (int i = 0; i < MaxDiskCount; i++) {
                if (diskIndices[i] < 0 || !Main.projectile[diskIndices[i]].active) {
                    continue;
                }

                Projectile disk = Main.projectile[diskIndices[i]];
                
                //更新轨道角度
                float currentAngle = disk.ai[0] + orbitSpeed;
                disk.ai[0] = currentAngle;

                //计算轨道位置
                Vector2 targetPos = player.Center + currentAngle.ToRotationVector2() * orbitRadius;
                
                //平滑移动
                disk.Center = Vector2.Lerp(disk.Center, targetPos, 0.15f);
                disk.timeLeft = 10; //保持存活
            }
        }

        private void SpawnOrbitParticles() {
            if (VaultUtils.isServer) {
                return;
            }

            for (int i = 0; i < MaxDiskCount; i++) {
                if (diskIndices[i] < 0 || !Main.projectile[diskIndices[i]].active) {
                    continue;
                }

                Projectile disk = Main.projectile[diskIndices[i]];
                
                //在吸积盘轨迹上生成粒子
                BasePRT particle = new PRT_AccretionDiskImpact(
                    disk.Center + Main.rand.NextVector2Circular(15, 15),
                    Main.rand.NextVector2Circular(1f, 1f),
                    Color.Lerp(Color.Gold, Color.Orange, Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.2f, 0.4f),
                    Main.rand.Next(10, 20),
                    Main.rand.NextFloat(-0.1f, 0.1f),
                    false,
                    Main.rand.NextFloat(0.1f, 0.15f)
                );
                PRTLoader.AddParticle(particle);
            }
        }

        private void SpawnActivationEffect() {
            if (VaultUtils.isServer) {
                return;
            }

            //环形激活波
            for (int ring = 0; ring < 3; ring++) {
                int segments = 32;
                float radius = 50f + ring * 60f;

                for (int i = 0; i < segments; i++) {
                    float angle = MathHelper.TwoPi * i / segments;
                    Vector2 offset = angle.ToRotationVector2() * radius;
                    Vector2 particlePos = Projectile.Center + offset;
                    Vector2 particleVel = offset.SafeNormalize(Vector2.Zero) * 3f;

                    BasePRT particle = new PRT_AccretionDiskImpact(
                        particlePos,
                        particleVel,
                        Color.Lerp(Color.Gold, Color.Orange, ring / 3f),
                        Main.rand.NextFloat(0.5f, 0.9f),
                        Main.rand.Next(30, 45),
                        Main.rand.NextFloat(-0.3f, 0.3f),
                        false,
                        Main.rand.NextFloat(0.2f, 0.3f)
                    );
                    PRTLoader.AddParticle(particle);
                }
            }
        }

        public override void OnKill(int timeLeft) {
            //清理所有吸积盘
            for (int i = 0; i < MaxDiskCount; i++) {
                if (diskIndices[i] >= 0 && Main.projectile[diskIndices[i]].active) {
                    Main.projectile[diskIndices[i]].Kill();
                }
            }

            //播放消失音效
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item62 with { 
                    Volume = 0.6f, 
                    Pitch = 0.2f 
                }, Projectile.Center);

                //消失特效
                for (int i = 0; i < 30; i++) {
                    Vector2 velocity = Main.rand.NextVector2Circular(8f, 8f);
                    BasePRT particle = new PRT_AccretionDiskImpact(
                        Projectile.Center,
                        velocity,
                        Color.Lerp(Color.Gold, Color.Orange, Main.rand.NextFloat()),
                        Main.rand.NextFloat(0.4f, 0.8f),
                        Main.rand.Next(20, 35),
                        Main.rand.NextFloat(-0.4f, 0.4f),
                        true,
                        Main.rand.NextFloat(0.15f, 0.25f)
                    );
                    PRTLoader.AddParticle(particle);
                }
            }
        }
    }

    /// <summary>
    /// Q技能的环绕小吸积盘
    /// </summary>
    internal class AriaQSkillDisk : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        public ref float OrbitAngle => ref Projectile.ai[0];
        public ref float ParentIndex => ref Projectile.ai[1];

        private float time;
        private float brightness = 1f;
        private Color diskColor = new Color(255, 200, 100);
        private float attackCooldown;
        private const float MaxAttackCooldown = 30f;

        public override void SetDefaults() {
            Projectile.width = 280;
            Projectile.height = 280;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 10;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 0;
            Projectile.scale = 0.5f;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            time += 0.016f;
            Projectile.rotation += 0.08f;

            //脉动效果
            float pulse = (float)Math.Sin(time * 3f) * 0.1f + 0.9f;
            brightness = pulse;

            //攻击冷却
            if (attackCooldown > 0) {
                attackCooldown--;
            }

            //寻找并攻击敌人
            if (attackCooldown <= 0) {
                NPC target = FindNearestEnemy();
                if (target != null) {
                    AttackEnemy(target);
                    attackCooldown = MaxAttackCooldown;
                }
            }

            //生成粒子
            if (Main.rand.NextBool(5)) {
                SpawnDiskParticle();
            }

            //发光
            Lighting.AddLight(Projectile.Center, diskColor.ToVector3() * brightness * 0.5f);
        }

        private NPC FindNearestEnemy() {
            float maxDetectDistance = 400f;
            NPC closestNPC = null;
            float minDistance = maxDetectDistance;

            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy(Projectile)) {
                    continue;
                }

                float distance = Vector2.Distance(Projectile.Center, npc.Center);
                if (distance < minDistance) {
                    minDistance = distance;
                    closestNPC = npc;
                }
            }

            return closestNPC;
        }

        private void AttackEnemy(NPC target) {
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }

            //发射小型能量弹
            Vector2 velocity = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * 15f;
            
            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                Projectile.Center,
                velocity,
                ModContent.ProjectileType<AriaQSkillBolt>(),
                (int)(Projectile.damage * 0.8f),
                Projectile.knockBack,
                Projectile.owner
            );

            //攻击音效
            SoundEngine.PlaySound(SoundID.Item9 with { 
                Volume = 0.4f, 
                Pitch = 0.6f 
            }, Projectile.Center);

            //攻击特效
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 5; i++) {
                    Vector2 particleVel = velocity.RotatedByRandom(0.3f) * Main.rand.NextFloat(0.3f, 0.6f);
                    BasePRT particle = new PRT_AccretionDiskImpact(
                        Projectile.Center,
                        particleVel,
                        diskColor,
                        Main.rand.NextFloat(0.3f, 0.6f),
                        Main.rand.Next(15, 25),
                        Main.rand.NextFloat(-0.2f, 0.2f),
                        false,
                        Main.rand.NextFloat(0.15f, 0.25f)
                    );
                    PRTLoader.AddParticle(particle);
                }
            }
        }

        private void SpawnDiskParticle() {
            if (VaultUtils.isServer) {
                return;
            }

            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 offset = angle.ToRotationVector2() * Main.rand.NextFloat(15, 25);
            
            BasePRT particle = new PRT_AccretionDiskImpact(
                Projectile.Center + offset,
                offset.RotatedBy(MathHelper.PiOver2).SafeNormalize(Vector2.Zero) * 0.5f,
                diskColor * 0.8f,
                Main.rand.NextFloat(0.15f, 0.3f),
                Main.rand.Next(10, 18),
                Main.rand.NextFloat(-0.1f, 0.1f),
                false,
                Main.rand.NextFloat(0.08f, 0.12f)
            );
            PRTLoader.AddParticle(particle);
        }

        public void DrawPrimitives() {
            if (VaultUtils.isServer) {
                return;
            }

            DrawMiniAccretionDisk();
        }

        [VaultLoaden(CWRConstant.Masking)]
        private static Texture2D TransverseTwill;

        private void DrawMiniAccretionDisk() {
            SpriteBatch spriteBatch = Main.spriteBatch;

            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Effect shader = EffectLoader.AccretionDisk.Value;

            float actualWidth = Projectile.width * Projectile.scale;
            float actualHeight = Projectile.height * Projectile.scale;

            Matrix world = Matrix.Identity;
            Matrix view = Main.GameViewMatrix.TransformationMatrix;
            Matrix projection = Matrix.CreateOrthographicOffCenter(
                0, Main.screenWidth,
                Main.screenHeight, 0,
                -1, 1);

            Matrix finalMatrix = world * view * projection;

            shader.Parameters["transformMatrix"]?.SetValue(finalMatrix);
            shader.Parameters["uTime"]?.SetValue(time);
            shader.Parameters["rotationSpeed"]?.SetValue(2f);
            shader.Parameters["innerRadius"]?.SetValue(0.2f);
            shader.Parameters["outerRadius"]?.SetValue(0.8f);
            shader.Parameters["brightness"]?.SetValue(brightness);
            shader.Parameters["distortionStrength"]?.SetValue(0.1f);
            shader.Parameters["noiseTexture"]?.SetValue(TransverseTwill);

            Vector2 screenCenter = Projectile.Center - Main.screenPosition;
            shader.Parameters["centerPos"]?.SetValue(screenCenter);

            Color innerColor = new Color(255, 220, 120);
            Color midColor = new Color(255, 160, 80);
            Color outerColor = new Color(200, 100, 50);

            shader.Parameters["innerColor"]?.SetValue(innerColor.ToVector4());
            shader.Parameters["midColor"]?.SetValue(midColor.ToVector4());
            shader.Parameters["outerColor"]?.SetValue(outerColor.ToVector4());

            Main.graphics.GraphicsDevice.Textures[1] = TransverseTwill;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;

            shader.CurrentTechnique.Passes["AccretionDiskPass"].Apply();

            Vector2 drawPosition = Projectile.Center - Main.screenPosition;

            for (int i = 0; i < 6; i++) {
                spriteBatch.Draw(
                TransverseTwill,
                drawPosition,
                null,
                Color.White * (1f - Projectile.alpha / 255f),
                Projectile.rotation,
                TransverseTwill.Size() * 0.5f,
                new Vector2(actualWidth / TransverseTwill.Width, actualHeight / TransverseTwill.Height) * (1 + i * 0.2f),
                SpriteEffects.None,
                0
            );
            }
            

            spriteBatch.End();
        }
    }

    /// <summary>
    /// Q技能发射的能量弹
    /// </summary>
    internal class AriaQSkillBolt : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.alpha = 0;
            Projectile.scale = 0.8f;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();

            //轻微的追踪效果
            NPC target = Projectile.Center.FindClosestNPC(300f);
            if (target != null) {
                Vector2 desiredVelocity = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * Projectile.velocity.Length();
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVelocity, 0.05f);
            }

            //生成拖尾粒子
            if (Main.rand.NextBool(3)) {
                BasePRT particle = new PRT_AccretionDiskImpact(
                    Projectile.Center,
                    -Projectile.velocity * 0.2f,
                    Color.Gold,
                    Main.rand.NextFloat(0.2f, 0.4f),
                    Main.rand.Next(8, 15),
                    0f,
                    false,
                    Main.rand.NextFloat(0.1f, 0.15f)
                );
                PRTLoader.AddParticle(particle);
            }

            //发光
            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.8f, 0.3f) * 0.5f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //击中特效
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 8; i++) {
                    Vector2 velocity = Main.rand.NextVector2Circular(5f, 5f);
                    BasePRT particle = new PRT_AccretionDiskImpact(
                        target.Center,
                        velocity,
                        Color.Gold,
                        Main.rand.NextFloat(0.3f, 0.6f),
                        Main.rand.Next(15, 25),
                        Main.rand.NextFloat(-0.3f, 0.3f),
                        true,
                        Main.rand.NextFloat(0.15f, 0.25f)
                    );
                    PRTLoader.AddParticle(particle);
                }
            }

            Projectile.damage = (int)(Projectile.damage * 0.85f);
        }

        public override bool PreDraw(ref Color lightColor) {
            //绘制拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }

                float progress = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;
                Color trailColor = Color.Lerp(Color.Orange, Color.Gold, progress) * progress * 0.6f;

                Main.EntitySpriteDraw(
                    VaultAsset.placeholder2.Value,
                    drawPos,
                    null,
                    trailColor,
                    Projectile.rotation,
                    VaultAsset.placeholder2.Value.Size() / 2,
                    Projectile.scale * progress * 0.5f,
                    SpriteEffects.None,
                    0
                );
            }

            //绘制主体
            Main.EntitySpriteDraw(
                VaultAsset.placeholder2.Value,
                Projectile.Center - Main.screenPosition,
                null,
                Color.White,
                Projectile.rotation,
                VaultAsset.placeholder2.Value.Size() / 2,
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            return false;
        }
    }
}
