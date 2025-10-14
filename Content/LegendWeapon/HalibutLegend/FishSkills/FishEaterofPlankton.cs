using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishEaterofPlankton : FishSkill
    {
        public override int UnlockFishID => ItemID.EaterofPlankton;
        public override int DefaultCooldown => 60;

        /// <summary>每次射击生成的噬魂虫数量</summary>
        private const int BitesPerShot = 3;

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            // 检查技能是否在冷却中
            if (Cooldown > 0) {
                return null;
            }

            // 每次射击生成多条噬魂虫
            for (int i = 0; i < BitesPerShot; i++) {
                // 计算随机偏移角度
                float angleOffset = Main.rand.NextFloat(-0.3f, 0.3f);
                Vector2 biteVelocity = velocity.RotatedBy(angleOffset) * Main.rand.NextFloat(0.8f, 1.2f);

                // 生成噬魂虫
                int proj = Projectile.NewProjectile(
                    source,
                    position + Main.rand.NextVector2Circular(20f, 20f),
                    biteVelocity,
                    ModContent.ProjectileType<SoulEaterBite>(),
                    (int)(damage * 0.6f),
                    knockback * 0.5f,
                    player.whoAmI,
                    ai0: i // 用于区分不同虫子
                );
            }

            // 播放音效
            SoundEngine.PlaySound(SoundID.NPCHit18 with { Volume = 0.6f, Pitch = -0.3f }, position);

            return null;
        }
    }

    /// <summary>
    /// 噬魂虫弹幕 - 模仿EatersBite但具有更强的生物感和演出效果
    /// </summary>
    internal class SoulEaterBite : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private ref float BiteID => ref Projectile.ai[0];
        private ref float AIState => ref Projectile.ai[1];
        private ref float AITimer => ref Projectile.localAI[0];

        // 身体段数（模拟蠕虫）
        private const int BodySegments = 8;
        private List<Vector2> bodyPositions = new();
        private List<float> bodyRotations = new();
        
        // 蠕动参数
        private float wrigglePhase;
        private float wriggleSpeed = 0.15f;
        private float wriggleAmplitude = 8f;
        
        // 追踪目标
        private int targetNPC = -1;
        private float homingStrength = 0f;

        // 状态枚举
        private enum State {
            Launching,    // 发射阶段
            Seeking,      // 寻找目标
            Homing,       // 追踪目标
            Biting        // 咬击
        }

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = BodySegments;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 3; // 可穿透3个敌人
            Projectile.timeLeft = 300;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override void AI() {
            AITimer++;
            
            // 初始化身体段
            if (bodyPositions.Count == 0) {
                for (int i = 0; i < BodySegments; i++) {
                    bodyPositions.Add(Projectile.Center);
                    bodyRotations.Add(Projectile.velocity.ToRotation());
                }
                wrigglePhase = BiteID * MathHelper.TwoPi / 3f; // 不同虫子相位不同
            }

            // 状态机
            State currentState = (State)AIState;
            switch (currentState) {
                case State.Launching:
                    LaunchingAI();
                    break;
                case State.Seeking:
                    SeekingAI();
                    break;
                case State.Homing:
                    HomingAI();
                    break;
                case State.Biting:
                    BitingAI();
                    break;
            }

            // 更新身体段位置（蠕动效果）
            UpdateBodySegments();
            
            // 旋转朝向速度方向
            Projectile.rotation = Projectile.velocity.ToRotation();
            
            // 生成粒子效果
            if (Main.rand.NextBool(5)) {
                SpawnParticles();
            }

            // 淡出效果
            if (Projectile.timeLeft < 30) {
                Projectile.alpha = (int)((1f - Projectile.timeLeft / 30f) * 255);
            }
        }

        private void LaunchingAI() {
            // 发射阶段：直线加速
            if (AITimer < 15) {
                Projectile.velocity *= 1.03f;
                
                // 发射15帧后进入寻找阶段
                if (AITimer >= 15) {
                    AIState = (float)State.Seeking;
                    AITimer = 0;
                }
            }
        }

        private void SeekingAI() {
            // 寻找目标阶段：寻找最近的敌人
            targetNPC = -1;
            var npc = Projectile.Center.FindClosestNPC(800f);
            if (npc != null) {
                targetNPC = npc.whoAmI;
            }


            if (targetNPC != -1) {
                AIState = (float)State.Homing;
                AITimer = 0;
                homingStrength = 0.02f;
                
                // 播放锁定音效
                SoundEngine.PlaySound(SoundID.NPCHit18 with { Volume = 0.3f, Pitch = 0.5f }, Projectile.Center);
            }
            else {
                // 没有目标时蠕动前进
                ApplyWriggleMotion();
            }
        }

        private void HomingAI() {
            // 追踪目标阶段
            if (targetNPC < 0 || !Main.npc[targetNPC].active) {
                // 目标丢失，返回寻找状态
                AIState = (float)State.Seeking;
                targetNPC = -1;
                return;
            }

            NPC target = Main.npc[targetNPC];
            
            // 逐渐增强追踪强度
            if (homingStrength < 0.15f) {
                homingStrength += 0.005f;
            }

            // 计算追踪方向
            Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * Projectile.velocity.Length(), homingStrength);
            
            // 速度逐渐加快
            if (Projectile.velocity.Length() < 25f) {
                Projectile.velocity *= 1.02f;
            }

            // 应用蠕动效果
            ApplyWriggleMotion();
            
            // 接近目标时进入咬击状态
            float distanceToTarget = Vector2.Distance(Projectile.Center, target.Center);
            if (distanceToTarget < 80f) {
                AIState = (float)State.Biting;
                AITimer = 0;
            }
        }

        private void BitingAI() {
            // 咬击阶段：短暂的爆发加速
            if (AITimer < 10) {
                Projectile.velocity *= 1.1f;
                wriggleAmplitude = 15f; // 咬击时蠕动幅度加大
                
                // 咬击粒子爆发
                if (AITimer % 2 == 0) {
                    for (int i = 0; i < 3; i++) {
                        Dust bite = Dust.NewDustDirect(
                            Projectile.position,
                            Projectile.width,
                            Projectile.height,
                            DustID.Blood,
                            Projectile.velocity.X * 0.3f,
                            Projectile.velocity.Y * 0.3f,
                            100,
                            default,
                            1.5f
                        );
                        bite.noGravity = true;
                    }
                }
            }
            else {
                // 咬击后回到寻找状态
                AIState = (float)State.Seeking;
                AITimer = 0;
                targetNPC = -1;
                wriggleAmplitude = 8f;
            }
        }

        /// <summary>
        /// 应用蠕动运动
        /// </summary>
        private void ApplyWriggleMotion() {
            wrigglePhase += wriggleSpeed;
            
            // 计算蠕动的垂直偏移
            float wriggleOffset = (float)Math.Sin(wrigglePhase) * wriggleAmplitude;
            
            // 将偏移应用到速度的垂直方向
            Vector2 perpendicular = new Vector2(-Projectile.velocity.Y, Projectile.velocity.X).SafeNormalize(Vector2.Zero);
            Vector2 wriggleVelocity = perpendicular * wriggleOffset;
            
            Projectile.velocity += wriggleVelocity;
        }

        /// <summary>
        /// 更新身体段位置
        /// </summary>
        private void UpdateBodySegments() {
            // 头部位置
            bodyPositions[0] = Projectile.Center;
            bodyRotations[0] = Projectile.rotation;
            
            // 每个身体段跟随前一段
            for (int i = 1; i < BodySegments; i++) {
                Vector2 targetPos = bodyPositions[i - 1];
                Vector2 currentPos = bodyPositions[i];
                
                // 保持固定距离
                float segmentDistance = 8f;
                Vector2 toTarget = targetPos - currentPos;
                float distance = toTarget.Length();
                
                if (distance > segmentDistance) {
                    Vector2 moveDir = toTarget.SafeNormalize(Vector2.Zero);
                    bodyPositions[i] = currentPos + moveDir * (distance - segmentDistance);
                }
                else if (distance < segmentDistance) {
                    Vector2 moveDir = toTarget.SafeNormalize(Vector2.Zero);
                    bodyPositions[i] = currentPos + moveDir * (distance - segmentDistance);
                }
                
                // 计算当前段和前一段的差值
                Vector2 segmentDiff = bodyPositions[i - 1] - bodyPositions[i];
                
                // 更新旋转
                if (i > 0) {
                    bodyRotations[i] = segmentDiff.ToRotation();
                }
                
                // 蠕动波动
                float segmentWriggle = (float)Math.Sin(wrigglePhase - i * 0.3f) * wriggleAmplitude * 0.5f;
                Vector2 perpendicular = new Vector2(-segmentDiff.Y, segmentDiff.X).SafeNormalize(Vector2.Zero);
                bodyPositions[i] += perpendicular * segmentWriggle * 0.1f;
            }
        }

        /// <summary>
        /// 生成粒子效果
        /// </summary>
        private void SpawnParticles() {
            // 随机选择身体段生成粒子
            int segmentIndex = Main.rand.Next(BodySegments);
            Vector2 particlePos = bodyPositions[segmentIndex];
            
            Dust particle = Dust.NewDustPerfect(
                particlePos,
                DustID.Shadowflame,
                -Projectile.velocity * 0.2f,
                100,
                default,
                Main.rand.NextFloat(0.8f, 1.2f)
            );
            particle.noGravity = true;
            particle.fadeIn = 1f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 击中效果：咬击音效和粒子爆发
            SoundEngine.PlaySound(SoundID.NPCHit18 with { Pitch = -0.2f }, Projectile.Center);
            
            // 血液粒子
            for (int i = 0; i < 8; i++) {
                Vector2 particleVel = Main.rand.NextVector2Circular(4f, 4f);
                Dust blood = Dust.NewDustDirect(
                    target.position,
                    target.width,
                    target.height,
                    DustID.Blood,
                    particleVel.X,
                    particleVel.Y,
                    100,
                    default,
                    1.5f
                );
                blood.noGravity = true;
            }
            
            // 噬魂效果
            for (int i = 0; i < 5; i++) {
                Dust soul = Dust.NewDustDirect(
                    target.position,
                    target.width,
                    target.height,
                    DustID.Shadowflame,
                    0, -2f,
                    100,
                    default,
                    1.3f
                );
                soul.noGravity = true;
            }
            
            // 击中后穿透继续寻找下一个目标
            if (Projectile.penetrate > 1) {
                AIState = (float)State.Seeking;
                targetNPC = -1;
                AITimer = 0;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            // 加载噬魂怪纹理
            Main.instance.LoadProjectile(ProjectileID.EatersBite);
            Texture2D texture = TextureAssets.Projectile[ProjectileID.EatersBite].Value;
            
            // 计算纹理参数
            int frameHeight = texture.Height / Main.projFrames[ProjectileID.EatersBite];
            Rectangle sourceRect = new Rectangle(0, 0, texture.Width, frameHeight);
            Vector2 origin = sourceRect.Size() / 2f;
            
            // 绘制所有身体段（从尾到头）
            for (int i = BodySegments - 1; i >= 0; i--) {
                Vector2 drawPos = bodyPositions[i] - Main.screenPosition;
                float rotation = bodyRotations[i] + MathHelper.PiOver4;
                
                // 尾部更小，头部更大
                float scale = MathHelper.Lerp(0.6f, 1.0f, i / (float)BodySegments);
                
                // 透明度渐变
                float segmentAlpha = MathHelper.Lerp(0.6f, 1.0f, i / (float)BodySegments);
                Color drawColor = lightColor * segmentAlpha * (1f - Projectile.alpha / 255f);
                
                // 发光效果
                if ((State)AIState == State.Biting) {
                    drawColor = Color.Lerp(drawColor, Color.Red, 0.5f);
                }
                
                Main.EntitySpriteDraw(
                    texture,
                    drawPos,
                    sourceRect,
                    drawColor,
                    rotation,
                    origin,
                    scale * 0.8f,
                    SpriteEffects.None,
                    0
                );
                
                // 头部额外发光
                if (i == 0) {
                    Color glowColor = Color.Purple * 0.5f * (1f - Projectile.alpha / 255f);
                    Main.EntitySpriteDraw(
                        texture,
                        drawPos,
                        sourceRect,
                        glowColor,
                        rotation,
                        origin,
                        scale * 1.2f,
                        SpriteEffects.None,
                        0
                    );
                }
            }
            
            return false;
        }

        public override Color? GetAlpha(Color lightColor) {
            return new Color(255, 255, 255, 200) * (1f - Projectile.alpha / 255f);
        }
    }
}
