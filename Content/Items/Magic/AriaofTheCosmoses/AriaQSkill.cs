using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
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
    /// 寰宇咏叹调Q技能，星环护卫
    /// 召唤多个小型吸积盘环绕玩家，自动攻击敌人
    /// </summary>
    internal class AriaQSkill : BaseHeldProj
    {
        public override string Texture => CWRConstant.Placeholder;

        private const int MaxDiskCount = 6;
        private float orbitRadius = 180f;
        private float orbitSpeed = 0.06f;
        private int[] diskIndices = new int[MaxDiskCount];

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead || Item.type != ModContent.ItemType<AriaofTheCosmos>()) {
                Projectile.Kill();
                return;
            }

            //跟随玩家
            Projectile.Center = Owner.Center;

            //初始化吸积盘
            if (Projectile.ai[0] == 0) {
                InitializeDisks(Owner);
                Projectile.ai[0] = 1;

                //播放激活音效
                SoundEngine.PlaySound(SoundID.Item109 with {
                    Volume = 0.9f,
                    Pitch = 0.3f
                }, Projectile.Center);

                SoundEngine.PlaySound(SoundID.DD2_WitherBeastAuraPulse with {
                    Volume = 0.8f,
                    Pitch = -0.2f
                }, Projectile.Center);

                //生成激活特效
                SpawnActivationEffect();
            }

            //更新所有吸积盘
            UpdateOrbitingDisks(Owner);

            //生成环绕粒子
            if (Projectile.timeLeft % 2 == 0) {
                SpawnOrbitParticles();
            }

            //生成连接线粒子
            if (Projectile.timeLeft % 5 == 0) {
                SpawnConnectionParticles(Owner);
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
            float pulseIntensity = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 5f) * 0.3f + 0.7f;
            Lighting.AddLight(Projectile.Center,
                new Vector3(1f, 0.8f, 0.3f) * pulseIntensity * 0.8f);
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
                    (int)(Projectile.damage * 0.7f),
                    Projectile.knockBack * 0.6f,
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

                //添加轻微的波动效果
                float wave = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f + i * (MathHelper.Pi / 3f)) * 20f;
                float currentRadius = orbitRadius + wave;

                //计算轨道位置
                Vector2 targetPos = player.Center + currentAngle.ToRotationVector2() * currentRadius;

                //平滑移动
                disk.Center = Vector2.Lerp(disk.Center, targetPos, 0.18f);
                disk.timeLeft = 10; //保持存活
            }
        }

        private void SpawnOrbitParticles() {
            if (VaultUtils.isServer) {
                return;
            }

            //每个吸积盘生成轨迹粒子
            for (int i = 0; i < MaxDiskCount; i++) {
                if (diskIndices[i] < 0 || !Main.projectile[diskIndices[i]].active) {
                    continue;
                }

                Projectile disk = Main.projectile[diskIndices[i]];

                //在吸积盘轨迹上生成粒子
                if (Main.rand.NextBool(2)) {
                    BasePRT particle = new PRT_AccretionDiskImpact(
                        disk.Center + Main.rand.NextVector2Circular(20, 20),
                        Main.rand.NextVector2Circular(1.5f, 1.5f),
                        Color.Lerp(Color.Gold, Color.Orange, Main.rand.NextFloat()),
                        Main.rand.NextFloat(0.25f, 0.45f),
                        Main.rand.Next(12, 22),
                        Main.rand.NextFloat(-0.15f, 0.15f),
                        false,
                        Main.rand.NextFloat(0.12f, 0.18f)
                    );
                    PRTLoader.AddParticle(particle);
                }
            }
        }

        private void SpawnConnectionParticles(Player player) {
            if (VaultUtils.isServer) {
                return;
            }

            //生成从玩家到吸积盘的能量连接线
            for (int i = 0; i < MaxDiskCount; i++) {
                if (diskIndices[i] < 0 || !Main.projectile[diskIndices[i]].active) {
                    continue;
                }

                Projectile disk = Main.projectile[diskIndices[i]];
                Vector2 direction = disk.Center - player.Center;
                float distance = direction.Length();

                //在连接线上生成粒子
                int particleCount = (int)(distance / 40f);
                for (int j = 0; j < particleCount; j++) {
                    float progress = j / (float)particleCount;
                    Vector2 particlePos = player.Center + direction * progress;

                    BasePRT particle = new PRT_AccretionDiskImpact(
                        particlePos,
                        Vector2.Zero,
                        Color.Lerp(Color.Gold, Color.Orange, progress) * 0.6f,
                        Main.rand.NextFloat(0.15f, 0.25f),
                        Main.rand.Next(8, 15),
                        0f,
                        false,
                        Main.rand.NextFloat(0.08f, 0.12f)
                    );
                    PRTLoader.AddParticle(particle);
                }
            }
        }

        private void SpawnActivationEffect() {
            if (VaultUtils.isServer) {
                return;
            }

            //环形激活波
            for (int ring = 0; ring < 3; ring++) {
                int segments = 48;
                float radius = 60f + ring * 80f;

                for (int i = 0; i < segments; i++) {
                    float angle = MathHelper.TwoPi * i / segments;
                    Vector2 offset = angle.ToRotationVector2() * radius;
                    Vector2 particlePos = Projectile.Center + offset;
                    Vector2 particleVel = offset.SafeNormalize(Vector2.Zero) * 4f;

                    BasePRT particle = new PRT_AccretionDiskImpact(
                        particlePos,
                        particleVel,
                        Color.Lerp(Color.Gold, Color.Orange, ring / 3f),
                        Main.rand.NextFloat(0.6f, 1.1f),
                        Main.rand.Next(35, 50),
                        Main.rand.NextFloat(-0.3f, 0.3f),
                        false,
                        Main.rand.NextFloat(0.25f, 0.35f)
                    );
                    PRTLoader.AddParticle(particle);
                }
            }

            //爆发粒子
            for (int i = 0; i < 50; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(10f, 10f);
                BasePRT particle = new PRT_GammaImpact(
                    Projectile.Center,
                    velocity,
                    Color.Lerp(Color.Gold, Color.Orange, Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.7f, 1.2f),
                    Main.rand.Next(30, 45),
                    Main.rand.NextFloat(-0.4f, 0.4f),
                    true,
                    0.3f
                );
                PRTLoader.AddParticle(particle);
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
                    Volume = 0.7f,
                    Pitch = 0.2f
                }, Projectile.Center);

                //消失特效
                for (int i = 0; i < 40; i++) {
                    Vector2 spawnPos = Projectile.Center + Main.rand.NextVector2Circular(150f, 150f);
                    Vector2 velocity = (Projectile.Center - spawnPos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(8f, 15f);

                    BasePRT particle = new PRT_AccretionDiskImpact(
                        spawnPos,
                        velocity,
                        Color.Lerp(Color.Gold, Color.Orange, Main.rand.NextFloat()),
                        Main.rand.NextFloat(0.5f, 0.9f),
                        Main.rand.Next(25, 40),
                        Main.rand.NextFloat(-0.4f, 0.4f),
                        true,
                        Main.rand.NextFloat(0.2f, 0.3f)
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
        private float distortionStrength = 0.12f;

        //颜色配置
        private Color innerColor = new Color(255, 230, 150);
        private Color midColor = new Color(255, 180, 100);
        private Color outerColor = new Color(220, 130, 60);

        private float attackCooldown;
        private const float MaxAttackCooldown = 45f; //1.5秒攻击间隔
        private float rotationSpeed = 2.5f;

        public override void SetDefaults() {
            Projectile.width = 120;
            Projectile.height = 120;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 10;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 0;
            Projectile.scale = 0.35f; //稍小一些
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void AI() {
            time += 0.02f;
            Projectile.rotation += 0.12f * rotationSpeed;

            //脉动效果
            float pulse = (float)Math.Sin(time * 4f) * 0.12f + 0.88f;
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
            if (Main.rand.NextBool(4)) {
                SpawnDiskParticle();
            }

            //发光
            Lighting.AddLight(Projectile.Center, innerColor.ToVector3() * brightness * 0.6f * Projectile.scale);
        }

        private NPC FindNearestEnemy() {
            float maxDetectDistance = 1500f;
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

            //发射迷你追踪吸积盘
            Vector2 velocity = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * 12f;

            int miniDisk = Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                Projectile.Center,
                velocity,
                ModContent.ProjectileType<AriaQSkillMiniDisk>(),
                Projectile.damage,
                Projectile.knockBack,
                Projectile.owner,
                target.whoAmI
            );

            //攻击音效
            SoundEngine.PlaySound(SoundID.Item9 with {
                Volume = 0.5f,
                Pitch = 0.7f
            }, Projectile.Center);

            //攻击特效
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 8; i++) {
                    Vector2 particleVel = velocity.RotatedByRandom(0.4f) * Main.rand.NextFloat(0.4f, 0.7f);
                    BasePRT particle = new PRT_AccretionDiskImpact(
                        Projectile.Center,
                        particleVel,
                        Color.Lerp(innerColor, outerColor, Main.rand.NextFloat()),
                        Main.rand.NextFloat(0.35f, 0.65f),
                        Main.rand.Next(18, 28),
                        Main.rand.NextFloat(-0.25f, 0.25f),
                        false,
                        Main.rand.NextFloat(0.18f, 0.28f)
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
            float distance = Main.rand.NextFloat(0.3f, 0.7f) * Projectile.width * 0.5f * Projectile.scale;
            Vector2 offset = angle.ToRotationVector2() * distance;
            Vector2 particleVel = offset.RotatedBy(MathHelper.PiOver2).SafeNormalize(Vector2.Zero) * 0.8f;

            BasePRT particle = new PRT_AccretionDiskImpact(
                Projectile.Center + offset,
                particleVel,
                Color.Lerp(innerColor, outerColor, distance / (Projectile.width * 0.5f * Projectile.scale)),
                Main.rand.NextFloat(0.2f, 0.35f),
                Main.rand.Next(12, 20),
                Main.rand.NextFloat(-0.12f, 0.12f),
                false,
                Main.rand.NextFloat(0.1f, 0.15f)
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

            //第一层绘制块
            {
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
                shader.Parameters["rotationSpeed"]?.SetValue(rotationSpeed);
                shader.Parameters["innerRadius"]?.SetValue(0.18f);
                shader.Parameters["outerRadius"]?.SetValue(0.82f);
                shader.Parameters["brightness"]?.SetValue(brightness);
                shader.Parameters["distortionStrength"]?.SetValue(distortionStrength);
                shader.Parameters["noiseTexture"]?.SetValue(VaultAsset.placeholder2.Value);

                Vector2 screenCenter = Projectile.Center - Main.screenPosition;
                shader.Parameters["centerPos"]?.SetValue(screenCenter);

                shader.Parameters["innerColor"]?.SetValue(innerColor.ToVector4());
                shader.Parameters["midColor"]?.SetValue(midColor.ToVector4());
                shader.Parameters["outerColor"]?.SetValue(outerColor.ToVector4());

                Main.graphics.GraphicsDevice.Textures[1] = TransverseTwill;
                Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;

                shader.CurrentTechnique.Passes["AccretionDiskPass"].Apply();

                Vector2 drawPosition = Projectile.Center - Main.screenPosition;

                //绘制多层以增强效果
                for (int i = 0; i < 6; i++) {
                    float layerScale = 10.6f + i * 0.2f;
                    spriteBatch.Draw(
                        TransverseTwill,
                        drawPosition,
                        null,
                        Color.White * (1f - Projectile.alpha / 255f),
                        Projectile.rotation + i * 0.1f,
                        TransverseTwill.Size() * 0.5f,
                        new Vector2(actualWidth / TransverseTwill.Width, actualHeight / TransverseTwill.Height) * layerScale,
                        SpriteEffects.None,
                        0
                    );
                }

                spriteBatch.End();
            }

            //第二层绘制块
            {
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
                shader.Parameters["rotationSpeed"]?.SetValue(rotationSpeed);
                shader.Parameters["innerRadius"]?.SetValue(0.18f);
                shader.Parameters["outerRadius"]?.SetValue(0.82f);
                shader.Parameters["brightness"]?.SetValue(brightness);
                shader.Parameters["distortionStrength"]?.SetValue(distortionStrength);
                shader.Parameters["noiseTexture"]?.SetValue(TransverseTwill);

                Vector2 screenCenter = Projectile.Center - Main.screenPosition;
                shader.Parameters["centerPos"]?.SetValue(screenCenter);

                shader.Parameters["innerColor"]?.SetValue(innerColor.ToVector4());
                shader.Parameters["midColor"]?.SetValue(midColor.ToVector4());
                shader.Parameters["outerColor"]?.SetValue(outerColor.ToVector4());

                Main.graphics.GraphicsDevice.Textures[1] = TransverseTwill;
                Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;

                shader.CurrentTechnique.Passes["AccretionDiskPass"].Apply();

                Vector2 drawPosition = Projectile.Center - Main.screenPosition;

                //绘制多层以增强效果
                for (int i = 0; i < 6; i++) {
                    float layerScale = 10.8f + i * 0.15f;
                    spriteBatch.Draw(
                        TransverseTwill,
                        drawPosition,
                        null,
                        Color.White * (1f - Projectile.alpha / 255f),
                        Projectile.rotation + i * 0.08f,
                        TransverseTwill.Size() * 0.5f,
                        new Vector2(actualWidth / TransverseTwill.Width, actualHeight / TransverseTwill.Height) * layerScale,
                        SpriteEffects.None,
                        0
                    );
                }

                spriteBatch.End();
            }
        }
    }

    /// <summary>
    /// Q技能发射的迷你追踪吸积盘
    /// </summary>
    internal class AriaQSkillMiniDisk : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        public ref float TargetNPCIndex => ref Projectile.ai[0];

        private float time;
        private float brightness = 1f;
        private float rotationSpeed = 3f;

        private Color innerColor = new Color(255, 230, 150);
        private Color midColor = new Color(255, 180, 100);
        private Color outerColor = new Color(220, 130, 60);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 0;
            Projectile.scale = 0.25f;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override void AI() {
            time += 0.025f;
            Projectile.rotation += 0.15f * rotationSpeed;

            //脉动效果
            float pulse = (float)Math.Sin(time * 5f) * 0.15f + 0.85f;
            brightness = pulse;

            //强力追踪
            if (TargetNPCIndex >= 0 && TargetNPCIndex < Main.maxNPCs) {
                NPC target = Main.npc[(int)TargetNPCIndex];
                if (target.active && target.CanBeChasedBy(Projectile)) {
                    Vector2 desiredVelocity = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * 20f;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVelocity, 0.12f);
                }
                else {
                    //目标丢失，寻找新目标
                    NPC newTarget = Projectile.Center.FindClosestNPC(400f);
                    if (newTarget != null) {
                        TargetNPCIndex = newTarget.whoAmI;
                    }
                }
            }
            else {
                //没有目标，寻找最近的敌人
                NPC newTarget = Projectile.Center.FindClosestNPC(400f);
                if (newTarget != null) {
                    TargetNPCIndex = newTarget.whoAmI;
                }
            }

            //生成拖尾粒子
            if (Main.rand.NextBool(2)) {
                SpawnTrailParticle();
            }

            //发光
            Lighting.AddLight(Projectile.Center, innerColor.ToVector3() * brightness * 0.5f * Projectile.scale);
        }

        private void SpawnTrailParticle() {
            if (VaultUtils.isServer) {
                return;
            }

            BasePRT particle = new PRT_AccretionDiskImpact(
                Projectile.Center,
                -Projectile.velocity * Main.rand.NextFloat(0.2f, 0.4f),
                Color.Lerp(innerColor, outerColor, Main.rand.NextFloat()),
                Main.rand.NextFloat(0.25f, 0.45f),
                Main.rand.Next(10, 18),
                Main.rand.NextFloat(-0.2f, 0.2f),
                false,
                Main.rand.NextFloat(0.12f, 0.18f)
            );
            PRTLoader.AddParticle(particle);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //击中特效
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 12; i++) {
                    Vector2 velocity = Main.rand.NextVector2Circular(6f, 6f);
                    BasePRT particle = new PRT_AccretionDiskImpact(
                        target.Center,
                        velocity,
                        Color.Lerp(innerColor, outerColor, Main.rand.NextFloat()),
                        Main.rand.NextFloat(0.4f, 0.7f),
                        Main.rand.Next(18, 28),
                        Main.rand.NextFloat(-0.35f, 0.35f),
                        true,
                        Main.rand.NextFloat(0.2f, 0.3f)
                    );
                    PRTLoader.AddParticle(particle);
                }
            }

            //击中音效
            SoundEngine.PlaySound(SoundID.Item14 with {
                Volume = 0.4f,
                Pitch = 0.5f
            }, target.Center);

            //穿透伤害衰减
            Projectile.damage = (int)(Projectile.damage * 0.8f);
        }

        public void DrawPrimitives() {
            if (VaultUtils.isServer) {
                return;
            }

            DrawMiniDisk();
        }

        [VaultLoaden(CWRConstant.Masking)]
        private static Texture2D TransverseTwill;

        private void DrawMiniDisk() {
            SpriteBatch spriteBatch = Main.spriteBatch;

            //第二层绘制块
            {
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
                shader.Parameters["rotationSpeed"]?.SetValue(rotationSpeed);
                shader.Parameters["innerRadius"]?.SetValue(0.2f);
                shader.Parameters["outerRadius"]?.SetValue(0.85f);
                shader.Parameters["brightness"]?.SetValue(brightness);
                shader.Parameters["distortionStrength"]?.SetValue(0.15f);
                shader.Parameters["noiseTexture"]?.SetValue(TransverseTwill);

                Vector2 screenCenter = Projectile.Center - Main.screenPosition;
                shader.Parameters["centerPos"]?.SetValue(screenCenter);

                shader.Parameters["innerColor"]?.SetValue(innerColor.ToVector4());
                shader.Parameters["midColor"]?.SetValue(midColor.ToVector4());
                shader.Parameters["outerColor"]?.SetValue(outerColor.ToVector4());

                Main.graphics.GraphicsDevice.Textures[1] = TransverseTwill;
                Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;

                shader.CurrentTechnique.Passes["AccretionDiskPass"].Apply();

                Vector2 drawPosition = Projectile.Center - Main.screenPosition;

                //第二组绘制层
                for (int i = 0; i < 6; i++) {
                    float layerScale = 110.7f + i * 2.68f;
                    spriteBatch.Draw(
                        TransverseTwill,
                        drawPosition,
                        null,
                        Color.White * (1f - Projectile.alpha / 255f),
                        Projectile.rotation + i * 0.1f,
                        TransverseTwill.Size() * 0.5f,
                        new Vector2(actualWidth / TransverseTwill.Width, actualHeight / TransverseTwill.Height) * layerScale,
                        SpriteEffects.None,
                        0
                    );
                }

                spriteBatch.End();
            }
        }
    }
}
