using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
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
    internal class FishBone : FishSkill
    {
        public override int UnlockFishID => ItemID.Bonefish;
        public override int DefaultCooldown => 30;

        //骨头管理系统
        private static readonly List<int> ActiveBones = new();
        private const int MaxBones = 8; //最多8根骨头
        
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            
            if (Cooldown <= 0) {
                SetCooldown();
                //检查当前活跃的骨头数量
                CleanupInactiveBones();

                if (ActiveBones.Count < MaxBones) {
                    //生成新的骨头弹幕
                    int boneProj = Projectile.NewProjectile(
                        source,
                        player.Center,
                        Vector2.Zero, //初始速度为0
                        ModContent.ProjectileType<BonefishOrbit>(),
                        (int)(damage * 0.8f),
                        knockback * 0.5f,
                        player.whoAmI,
                        ai0: ActiveBones.Count //传递索引用于错开动画
                    );

                    if (boneProj >= 0 && boneProj < Main.maxProjectiles) {
                        ActiveBones.Add(boneProj);

                        //生成召唤粒子
                        SpawnSummonEffect(player.Center);

                        //音效
                        SoundEngine.PlaySound(SoundID.Item71 with {
                            Volume = 0.6f,
                            Pitch = 0.3f + ActiveBones.Count * 0.05f
                        }, player.Center);
                    }
                }
            }

            return null;
        }

        private static void CleanupInactiveBones() {
            ActiveBones.RemoveAll(id => id < 0 || id >= Main.maxProjectiles || !Main.projectile[id].active);
        }

        private void SpawnSummonEffect(Vector2 position) {
            //召唤时的骨质粒子效果
            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(2f, 5f);
                
                Dust bone = Dust.NewDustPerfect(
                    position,
                    DustID.Bone,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(1.2f, 1.8f)
                );
                bone.noGravity = true;
            }
        }

        //检查玩家是否受伤（通过Hit弹幕数量）
        public static bool IsPlayerHurt(Player player) {
            int hitCount = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                if (Main.projectile[i].active && 
                    Main.projectile[i].owner == player.whoAmI &&
                    Main.projectile[i].type == ModContent.ProjectileType<Content.Projectiles.Others.Hit>()) {
                    hitCount++;
                }
            }
            return hitCount > 0;
        }
    }

    /// <summary>
    /// 旋转骨头弹幕 - 环绕蓄力后发射
    /// </summary>
    internal class BonefishOrbit : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Bone;
        
        //状态机
        private enum BoneState {
            Gathering,    //聚集阶段：骨头飞向玩家
            Orbiting,     //环绕阶段：环绕玩家加速旋转
            Charging,     //蓄力阶段：继续加速，准备发射
            Launching,    //发射阶段：向目标冲刺
            Scattering    //散射阶段：玩家受伤，向四周飞散
        }
        
        private BoneState State {
            get => (BoneState)Projectile.ai[1];
            set => Projectile.ai[1] = (float)value;
        }
        
        private ref float BoneIndex => ref Projectile.ai[0];
        private ref float StateTimer => ref Projectile.ai[2];
        
        //环绕参数
        private float orbitRadius = 120f;
        private float orbitAngle = 0f;
        private float orbitSpeed = 0.05f;
        private const float MaxOrbitSpeed = 0.5f;

        //蓄力参数
        private const int GatherDuration = 20;      //聚集时间
        private const int OrbitDuration = 60;       //环绕时间
        private const int ChargeDuration = 40;      //蓄力时间
        private const float LaunchSpeed = 28f;      //发射速度
        
        //视觉效果
        private float glowIntensity = 0f;
        private float trailIntensity = 0f;
        private List<Vector2> trailPositions = new();
        private const int MaxTrailLength = 20;
        
        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> SoftGlow = null;
        
        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> StarTexture = null;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 15;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 600;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
            
            //初始化轨迹
            for (int i = 0; i < MaxTrailLength; i++) {
                trailPositions.Add(Projectile.Center);
            }
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }
            
            //检查玩家是否受伤
            if (FishBone.IsPlayerHurt(owner) && State != BoneState.Scattering) {
                EnterScatterState();
            }
            
            StateTimer++;
            
            //状态机
            switch (State) {
                case BoneState.Gathering:
                    GatheringPhaseAI(owner);
                    break;
                    
                case BoneState.Orbiting:
                    OrbitingPhaseAI(owner);
                    break;
                    
                case BoneState.Charging:
                    ChargingPhaseAI(owner);
                    break;
                    
                case BoneState.Launching:
                    LaunchingPhaseAI(owner);
                    break;
                    
                case BoneState.Scattering:
                    ScatteringPhaseAI();
                    break;
            }
            
            //更新轨迹
            UpdateTrail();
            
            //旋转
            Projectile.rotation += MathHelper.Lerp(0.1f, 0.6f, orbitSpeed / MaxOrbitSpeed);
            
            //照明
            float lightIntensity = glowIntensity * 0.8f;
            Lighting.AddLight(Projectile.Center, 0.8f * lightIntensity, 0.8f * lightIntensity, 0.9f * lightIntensity);
        }

        ///<summary>
        ///聚集阶段：骨头快速飞向玩家附近
        ///</summary>
        private void GatheringPhaseAI(Player owner) {
            float progress = StateTimer / GatherDuration;
            
            //计算初始环绕位置
            float targetAngle = MathHelper.TwoPi * BoneIndex / 8f;
            Vector2 targetPos = owner.Center + targetAngle.ToRotationVector2() * orbitRadius;
            
            //使用EaseOutCubic缓动，创造有力的冲刺感
            float easeProgress = EaseOutCubic(progress);
            Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos, easeProgress * 0.4f);
            
            //提前开始旋转
            orbitAngle = targetAngle;
            
            glowIntensity = MathHelper.Lerp(0f, 0.5f, progress);
            
            //聚集粒子效果
            if (Main.rand.NextBool(3)) {
                SpawnGatherParticle();
            }
            
            //转入环绕阶段
            if (StateTimer >= GatherDuration) {
                State = BoneState.Orbiting;
                StateTimer = 0;
                
                //环绕开始音效
                SoundEngine.PlaySound(SoundID.Item8 with { 
                    Volume = 0.4f, 
                    Pitch = 0.2f 
                }, Projectile.Center);
            }
        }

        ///<summary>
        ///环绕阶段：环绕玩家并逐渐加速
        ///</summary>
        private void OrbitingPhaseAI(Player owner) {
            float progress = StateTimer / OrbitDuration;
            
            //加速旋转（使用EaseInQuad）
            float speedProgress = EaseInQuad(progress);
            orbitSpeed = MathHelper.Lerp(0.05f, MaxOrbitSpeed * 0.6f, speedProgress);
            
            //半径脉冲（营造能量聚集感）
            float radiusPulse = (float)Math.Sin(StateTimer * 0.3f) * 10f;
            float currentRadius = orbitRadius + radiusPulse * progress;
            
            //更新环绕角度
            orbitAngle += orbitSpeed;
            
            //计算环绕位置
            Vector2 orbitOffset = orbitAngle.ToRotationVector2() * currentRadius;
            Vector2 targetPos = owner.Center + orbitOffset;
            
            //平滑跟随
            Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos, 0.3f);
            
            //辉光强度增加
            glowIntensity = MathHelper.Lerp(0.5f, 0.8f, progress);
            trailIntensity = progress;
            
            //能量聚集粒子
            if (Main.rand.NextBool(2)) {
                SpawnOrbitParticle(owner.Center, progress);
            }
            
            //周期性音效（加速感）
            if (StateTimer % (int)MathHelper.Lerp(20, 5, progress) == 0) {
                SoundEngine.PlaySound(SoundID.Item9 with { 
                    Volume = 0.3f * progress, 
                    Pitch = progress 
                }, Projectile.Center);
            }
            
            //转入蓄力阶段
            if (StateTimer >= OrbitDuration) {
                State = BoneState.Charging;
                StateTimer = 0;
                
                //蓄力音效
                SoundEngine.PlaySound(SoundID.DD2_WitherBeastAuraPulse with { 
                    Volume = 0.6f, 
                    Pitch = 0.3f 
                }, Projectile.Center);
            }
        }

        ///<summary>
        ///蓄力阶段：最高速旋转，准备发射
        ///</summary>
        private void ChargingPhaseAI(Player owner) {
            float progress = StateTimer / ChargeDuration;
            
            //达到最高旋转速度
            orbitSpeed = MathHelper.Lerp(MaxOrbitSpeed * 0.6f, MaxOrbitSpeed, EaseInOutQuad(progress));
            
            //半径脉动（蓄力震荡感）
            float radiusOscillation = (float)Math.Sin(StateTimer * 0.5f) * 15f * progress;
            float currentRadius = orbitRadius - 20f * progress + radiusOscillation;
            
            //更新环绕
            orbitAngle += orbitSpeed;
            Vector2 orbitOffset = orbitAngle.ToRotationVector2() * currentRadius;
            Vector2 targetPos = owner.Center + orbitOffset;
            Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos, 0.4f);
            
            //最大辉光
            glowIntensity = 0.8f + (float)Math.Sin(StateTimer * 0.8f) * 0.2f;
            trailIntensity = 1f;
            
            //密集蓄力粒子
            if (Main.rand.NextBool()) {
                SpawnChargeParticle(owner.Center, progress);
            }
            
            //蓄力闪光环
            if (StateTimer % 10 == 0) {
                SpawnChargePulse(owner.Center);
            }
            
            //高频音效（极限蓄力）
            if (StateTimer % 5 == 0) {
                SoundEngine.PlaySound(SoundID.MaxMana with { 
                    Volume = 0.2f + progress * 0.3f, 
                    Pitch = 0.5f + progress * 0.5f 
                }, Projectile.Center);
            }
            
            //转入发射阶段
            if (StateTimer >= ChargeDuration) {
                State = BoneState.Launching;
                StateTimer = 0;
                LaunchToTarget(owner);
            }
        }

        ///<summary>
        ///发射向目标
        ///</summary>
        private void LaunchToTarget(Player owner) {
            Vector2 toMouse = (Main.MouseWorld - Projectile.Center).SafeNormalize(Vector2.Zero);
            
            //计算发射速度（基于当前旋转速度增加动量感）
            float momentumBonus = orbitSpeed / MaxOrbitSpeed;
            float finalSpeed = LaunchSpeed * (1f + momentumBonus * 0.5f);
            
            Projectile.velocity = toMouse * finalSpeed;
            Projectile.tileCollide = true;
            
            //爆发式粒子效果
            SpawnLaunchBurst();
            
            //强力发射音效
            SoundEngine.PlaySound(SoundID.Item92 with { 
                Volume = 0.8f, 
                Pitch = 0.2f 
            }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { 
                Volume = 0.6f, 
                Pitch = -0.3f 
            }, Projectile.Center);
        }

        ///<summary>
        ///发射阶段：向目标飞行
        ///</summary>
        private void LaunchingPhaseAI(Player owner) {
            //速度衰减
            Projectile.velocity *= 0.99f;
            
            //轨迹强度保持
            trailIntensity = 1f;
            glowIntensity = 0.9f;
            
            //飞行粒子
            if (Main.rand.NextBool(2)) {
                SpawnLaunchTrailParticle();
            }
            
            //超时返回环绕
            if (StateTimer > 180) {
                State = BoneState.Orbiting;
                StateTimer = 0;
                Projectile.tileCollide = false;
            }
        }

        ///<summary>
        ///散射阶段：玩家受伤，骨头向四周飞散
        ///</summary>
        private void ScatteringPhaseAI() {
            //速度衰减
            Projectile.velocity *= 0.96f;
            
            //淡出
            Projectile.alpha += 5;
            if (Projectile.alpha >= 255) {
                Projectile.Kill();
            }
            
            trailIntensity *= 0.95f;
            glowIntensity *= 0.95f;
        }

        ///<summary>
        ///进入散射状态
        ///</summary>
        private void EnterScatterState() {
            State = BoneState.Scattering;
            StateTimer = 0;
            Projectile.tileCollide = false;
            Projectile.friendly = false;
            
            //随机散射方向
            float randomAngle = Main.rand.NextFloat(MathHelper.TwoPi);
            Projectile.velocity = randomAngle.ToRotationVector2() * Main.rand.NextFloat(12f, 20f);
            
            //散射粒子效果
            SpawnScatterEffect();
            
            //散射音效
            SoundEngine.PlaySound(SoundID.NPCDeath2 with { 
                Volume = 0.5f, 
                Pitch = 0.4f 
            }, Projectile.Center);
        }

        private void UpdateTrail() {
            trailPositions.RemoveAt(0);
            trailPositions.Add(Projectile.Center);
        }

        //===== 粒子效果方法 =====
        
        private void SpawnGatherParticle() {
            Dust gather = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(15f, 15f),
                DustID.Bone,
                (Main.player[Projectile.owner].Center - Projectile.Center).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(1f, 3f),
                100,
                default,
                Main.rand.NextFloat(0.8f, 1.2f)
            );
            gather.noGravity = true;
        }

        private void SpawnOrbitParticle(Vector2 ownerCenter, float progress) {
            Vector2 toCenter = (ownerCenter - Projectile.Center).SafeNormalize(Vector2.Zero);
            Vector2 velocity = toCenter * Main.rand.NextFloat(1f, 3f) * progress;
            
            Dust orbit = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                DustID.BlueFairy,
                velocity,
                100,
                new Color(200, 200, 255),
                Main.rand.NextFloat(1f, 1.5f)
            );
            orbit.noGravity = true;
        }

        private void SpawnChargeParticle(Vector2 ownerCenter, float progress) {
            Vector2 toCenter = (ownerCenter - Projectile.Center).SafeNormalize(Vector2.Zero);
            Vector2 velocity = toCenter * Main.rand.NextFloat(3f, 6f) * progress;
            
            //能量粒子
            Dust charge = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                DustID.Electric,
                velocity,
                100,
                new Color(150, 200, 255),
                Main.rand.NextFloat(1.2f, 1.8f)
            );
            charge.noGravity = true;
            
            //骨质粒子混合
            if (Main.rand.NextBool(2)) {
                Dust bone = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Bone,
                    velocity * 0.5f,
                    100,
                    default,
                    Main.rand.NextFloat(1f, 1.5f)
                );
                bone.noGravity = true;
            }
        }

        private void SpawnChargePulse(Vector2 ownerCenter) {
            //环形脉冲
            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12f;
                Vector2 velocity = angle.ToRotationVector2() * 3f;
                
                Dust pulse = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.BlueFairy,
                    velocity,
                    100,
                    new Color(180, 220, 255),
                    Main.rand.NextFloat(1.5f, 2f)
                );
                pulse.noGravity = true;
            }
        }

        private void SpawnLaunchBurst() {
            //爆发式粒子
            for (int i = 0; i < 30; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(5f, 15f);
                
                Dust burst = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Electric,
                    velocity,
                    100,
                    new Color(200, 230, 255),
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                burst.noGravity = true;
            }
            
            //骨质碎片
            for (int i = 0; i < 20; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(8f, 8f);
                Dust bone = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Bone,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(1.2f, 2f)
                );
                bone.noGravity = true;
            }
        }

        private void SpawnLaunchTrailParticle() {
            Dust trail = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                DustID.BlueFairy,
                -Projectile.velocity * Main.rand.NextFloat(0.1f, 0.3f),
                100,
                new Color(180, 210, 255),
                Main.rand.NextFloat(1f, 1.5f)
            );
            trail.noGravity = true;
        }

        private void SpawnScatterEffect() {
            for (int i = 0; i < 20; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(8f, 8f);
                
                Dust scatter = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Bone,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(1.2f, 2f)
                );
                scatter.noGravity = true;
            }
        }

        //===== 缓动函数 =====
        
        private float EaseOutCubic(float t) {
            return 1f - (float)Math.Pow(1f - t, 3);
        }

        private float EaseInQuad(float t) {
            return t * t;
        }

        private float EaseInOutQuad(float t) {
            return t < 0.5f ? 2f * t * t : 1f - (float)Math.Pow(-2f * t + 2f, 2) / 2f;
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //碰撞后反弹
            if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > float.Epsilon) {
                Projectile.velocity.X = -oldVelocity.X * 0.8f;
            }
            if (Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > float.Epsilon) {
                Projectile.velocity.Y = -oldVelocity.Y * 0.8f;
            }
            
            //碰撞音效
            SoundEngine.PlaySound(SoundID.Dig with { 
                Volume = 0.5f, 
                Pitch = 0.5f 
            }, Projectile.Center);
            
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //击中粒子效果
            for (int i = 0; i < 10; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(5f, 5f);
                Dust hitDust = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Bone,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(1.2f, 1.8f)
                );
                hitDust.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D boneTex = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Rectangle sourceRect = boneTex.Frame(1, 1);
            Vector2 origin = sourceRect.Size() / 2f;
            
            Color baseColor = lightColor;
            float alpha = (255f - Projectile.alpha) / 255f;
            
            //===== 绘制能量轨迹 =====
            if (trailIntensity > 0f && State != BoneState.Gathering) {
                DrawEnergyTrail(sb, baseColor, alpha);
            }
            
            //===== 绘制标准拖尾（发射阶段） =====
            if (State == BoneState.Launching) {
                for (int i = 0; i < Projectile.oldPos.Length; i++) {
                    if (Projectile.oldPos[i] == Vector2.Zero) continue;
                    
                    float trailAlpha = (1f - i / (float)Projectile.oldPos.Length) * alpha * 0.6f;
                    Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                    
                    sb.Draw(
                        boneTex,
                        trailPos,
                        sourceRect,
                        new Color(150, 200, 255) * trailAlpha,
                        Projectile.rotation,
                        origin,
                        Projectile.scale * (1f - i * 0.03f),
                        SpriteEffects.None,
                        0
                    );
                }
            }
            
            //===== 绘制外层辉光 =====
            if (glowIntensity > 0f && SoftGlow?.Value != null) {
                Texture2D glow = SoftGlow.Value;
                float glowScale = Projectile.scale * (1.2f + glowIntensity * 0.5f);
                float glowAlpha = glowIntensity * alpha * 0.6f;
                
                //蓝白辉光
                sb.Draw(
                    glow,
                    drawPos,
                    null,
                    new Color(180, 220, 255, 0) * glowAlpha,
                    Projectile.rotation * 0.5f,
                    glow.Size() / 2f,
                    glowScale,
                    SpriteEffects.None,
                    0f
                );
                
                //蓄力阶段额外闪光
                if (State == BoneState.Charging) {
                    float chargePulse = (float)Math.Sin(StateTimer * 0.8f) * 0.5f + 0.5f;
                    sb.Draw(
                        glow,
                        drawPos,
                        null,
                        new Color(200, 240, 255, 0) * (glowAlpha * chargePulse),
                        -Projectile.rotation * 0.8f,
                        glow.Size() / 2f,
                        glowScale * (1f + chargePulse * 0.3f),
                        SpriteEffects.None,
                        0f
                    );
                }
            }
            
            //===== 绘制主体骨头 =====
            //基础绘制
            sb.Draw(
                boneTex,
                drawPos,
                sourceRect,
                baseColor * alpha,
                Projectile.rotation,
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );
            
            //能量覆盖层（蓄力时）
            if (State == BoneState.Charging || State == BoneState.Launching) {
                float energyAlpha = (State == BoneState.Charging ? 0.6f : 0.4f) * alpha;
                sb.Draw(
                    boneTex,
                    drawPos,
                    sourceRect,
                    new Color(180, 220, 255) * energyAlpha,
                    Projectile.rotation,
                    origin,
                    Projectile.scale,
                    SpriteEffects.None,
                    0
                );
            }
            
            //===== 绘制星形闪光（发射瞬间） =====
            if (State == BoneState.Launching && StateTimer < 10 && StarTexture?.Value != null) {
                Texture2D star = StarTexture.Value;
                float starProgress = StateTimer / 10f;
                float starAlpha = (1f - starProgress) * alpha;
                float starScale = Projectile.scale * (0.5f + starProgress * 0.5f);
                
                sb.Draw(
                    star,
                    drawPos,
                    null,
                    new Color(220, 240, 255, 0) * starAlpha,
                    Projectile.rotation,
                    star.Size() / 2f,
                    starScale,
                    SpriteEffects.None,
                    0f
                );
            }
            
            return false;
        }

        ///<summary>
        ///绘制能量轨迹（螺旋残影）
        ///</summary>
        private void DrawEnergyTrail(SpriteBatch sb, Color baseColor, float alpha) {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            
            for (int i = 0; i < trailPositions.Count - 1; i++) {
                if (trailPositions[i] == Vector2.Zero || trailPositions[i + 1] == Vector2.Zero) continue;
                
                float trailProgress = 1f - i / (float)trailPositions.Count;
                float trailAlpha = trailProgress * trailIntensity * alpha * 0.5f;
                float trailWidth = 4f * trailProgress * Projectile.scale;
                
                Vector2 start = trailPositions[i] - Main.screenPosition;
                Vector2 end = trailPositions[i + 1] - Main.screenPosition;
                Vector2 diff = end - start;
                float length = diff.Length();
                float rotation = diff.ToRotation();
                
                Color trailColor = Color.Lerp(
                    new Color(150, 200, 255),
                    new Color(200, 230, 255),
                    trailProgress
                );
                
                sb.Draw(
                    pixel,
                    start,
                    new Rectangle(0, 0, 1, 1),
                    trailColor * trailAlpha,
                    rotation,
                    Vector2.Zero,
                    new Vector2(length, trailWidth),
                    SpriteEffects.None,
                    0f
                );
            }
        }
    }
}
