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
    internal class FishBone : FishSkill
    {
        public override int UnlockFishID => ItemID.Bonefish;

        public override int DefaultCooldown => 60 - HalibutData.GetDomainLayer() * 4;
        public override int ResearchDuration => 60 * 18;
        //骨头管理系统
        private static readonly List<int> ActiveBones = new();
        private static int MaxBones => 3 + HalibutData.GetDomainLayer() / 2; //最多3-8根骨头

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
                        (int)(damage * (3 + HalibutData.GetDomainLayer() * 1.75)),
                        knockback * 0.25f,
                        player.whoAmI,
                        ai0: ActiveBones.Count //传递索引用于错开动画
                    );

                    if (boneProj >= 0 && boneProj < Main.maxProjectiles) {
                        ActiveBones.Add(boneProj);

                        //生成召唤粒子
                        SpawnSummonEffect(player.Center);

                        //骨质召唤音效
                        SoundEngine.PlaySound(SoundID.Item1 with {
                            Volume = 0.5f,
                            Pitch = -0.4f + ActiveBones.Count * 0.05f
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
            for (int i = 0; i < 15; i++) {
                float angle = MathHelper.TwoPi * i / 15f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(2f, 5f);

                Dust bone = Dust.NewDustPerfect(
                    position,
                    DustID.Bone,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(1.2f, 2f)
                );
                bone.noGravity = true;
                bone.fadeIn = 1.2f;
            }

            // 额外的骨质碎片
            for (int i = 0; i < 8; i++) {
                Dust shard = Dust.NewDustDirect(
                    position - new Vector2(10),
                    20, 20,
                    DustID.Bone,
                    Scale: Main.rand.NextFloat(1f, 1.5f)
                );
                shard.velocity = Main.rand.NextVector2Circular(3f, 3f);
                shard.noGravity = true;
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
        private enum BoneState
        {
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
        private static int GatherDuration => 20 - HalibutData.GetDomainLayer();      //聚集时间
        private static int OrbitDuration => 60 - HalibutData.GetDomainLayer() * 3;       //环绕时间
        private static int ChargeDuration => 40 - HalibutData.GetDomainLayer() * 2;      //蓄力时间
        private const float LaunchSpeed = 28f;      //发射速度

        //视觉效果
        private float glowIntensity = 0f;
        private float trailIntensity = 0f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 10086;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 3;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];

            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            if (!FishSkill.GetT<FishBone>().Active(owner)) {
                Projectile.Kill();
                return;
            }

            //检查玩家是否受伤
            if (FishBone.IsPlayerHurt(owner) && State != BoneState.Scattering) {
                EnterScatterState();
            }

            StateTimer++;

            if (Projectile.scale < 1.5f) {
                Projectile.scale += 0.01f;
            }

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

            //旋转
            Projectile.rotation += MathHelper.Lerp(0.15f, 0.8f, orbitSpeed / MaxOrbitSpeed);

            //骨质照明（冷白色）
            float lightIntensity = glowIntensity * 0.6f;
            Lighting.AddLight(Projectile.Center,
                0.7f * lightIntensity,
                0.7f * lightIntensity,
                0.8f * lightIntensity);
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

            //聚集骨质粒子
            if (Main.rand.NextBool(4)) {
                SpawnGatherParticle();
            }

            //转入环绕阶段
            if (StateTimer >= GatherDuration) {
                State = BoneState.Orbiting;
                StateTimer = 0;

                //骨头碰撞音效
                SoundEngine.PlaySound(SoundID.Dig with {
                    Volume = 0.4f,
                    Pitch = 0.3f
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

            //骨质粒子环绕
            if (Main.rand.NextBool(3)) {
                SpawnOrbitParticle(owner.Center, progress);
            }

            //周期性骨头摩擦音效
            if (StateTimer % (int)MathHelper.Lerp(25, 8, progress) == 0) {
                SoundEngine.PlaySound(SoundID.Item1 with {
                    Volume = 0.25f * progress,
                    Pitch = -0.5f + progress * 0.3f
                }, Projectile.Center);
            }

            //转入蓄力阶段
            if (StateTimer >= OrbitDuration) {
                State = BoneState.Charging;
                StateTimer = 0;

                //骨质蓄力音效
                SoundEngine.PlaySound(SoundID.Item67 with {
                    Volume = 0.6f,
                    Pitch = -0.4f
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

            //密集骨质粒子
            if (Main.rand.NextBool()) {
                SpawnChargeParticle(owner.Center, progress);
            }

            //蓄力骨质脉冲
            if (StateTimer % 10 == 0) {
                SpawnChargePulse(owner.Center);
            }

            //高频骨头碰撞音效
            if (StateTimer % 6 == 0) {
                SoundEngine.PlaySound(SoundID.Dig with {
                    Volume = 0.15f + progress * 0.25f,
                    Pitch = 0.3f + progress * 0.4f
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
            if (!Framing.GetTileSafely(Projectile.Center.ToTileCoordinates16()).HasTile) {
                Projectile.tileCollide = true;
            }

            //爆发式骨质粒子
            SpawnLaunchBurst();

            //强力发射音效（骨头破碎）
            SoundEngine.PlaySound(SoundID.Item14 with {
                Volume = 0.6f,
                Pitch = 0.5f
            }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.NPCHit2 with {
                Volume = 0.5f,
                Pitch = -0.2f
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

            //飞行骨质粒子
            if (Main.rand.NextBool(3)) {
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

            //骨头破碎音效
            SoundEngine.PlaySound(SoundID.NPCHit2 with {
                Volume = 0.5f,
                Pitch = 0.2f
            }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Dig with {
                Volume = 0.4f,
                Pitch = 0.5f
            }, Projectile.Center);
        }

        //===== 粒子效果方法 =====

        private void SpawnGatherParticle() {
            Dust gather = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(15f, 15f),
                DustID.Bone,
                (Main.player[Projectile.owner].Center - Projectile.Center).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(1f, 3f),
                100,
                default,
                Main.rand.NextFloat(0.8f, 1.3f)
            );
            gather.noGravity = true;
            gather.fadeIn = 1.1f;
        }

        private void SpawnOrbitParticle(Vector2 ownerCenter, float progress) {
            Vector2 toCenter = (ownerCenter - Projectile.Center).SafeNormalize(Vector2.Zero);
            Vector2 velocity = toCenter * Main.rand.NextFloat(0.5f, 2f) * progress;

            Dust orbit = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(12f, 12f),
                DustID.Bone,
                velocity,
                100,
                default,
                Main.rand.NextFloat(0.9f, 1.4f)
            );
            orbit.noGravity = true;
            orbit.fadeIn = 1.1f;
        }

        private void SpawnChargeParticle(Vector2 ownerCenter, float progress) {
            Vector2 toCenter = (ownerCenter - Projectile.Center).SafeNormalize(Vector2.Zero);
            Vector2 velocity = toCenter * Main.rand.NextFloat(2f, 5f) * progress;

            //骨质粒子
            Dust charge = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                DustID.Bone,
                velocity,
                100,
                default,
                Main.rand.NextFloat(1.2f, 1.9f)
            );
            charge.noGravity = true;
            charge.fadeIn = 1.2f;

            //额外骨质碎片
            if (Main.rand.NextBool(3)) {
                Dust shard = Dust.NewDustDirect(
                    Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    2, 2,
                    DustID.Bone,
                    Scale: Main.rand.NextFloat(1f, 1.5f)
                );
                shard.velocity = velocity * 0.6f;
                shard.noGravity = true;
            }
        }

        private void SpawnChargePulse(Vector2 ownerCenter) {
            //环形骨质脉冲
            for (int i = 0; i < 10; i++) {
                float angle = MathHelper.TwoPi * i / 10f;
                Vector2 velocity = angle.ToRotationVector2() * 2.5f;

                Dust pulse = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Bone,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(1.3f, 1.8f)
                );
                pulse.noGravity = true;
                pulse.fadeIn = 1.2f;
            }
        }

        private void SpawnLaunchBurst() {
            //爆发式骨质粒子
            for (int i = 0; i < 25; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 12f);

                Dust burst = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Bone,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(1.4f, 2.2f)
                );
                burst.noGravity = true;
                burst.fadeIn = 1.3f;
            }

            //骨质碎片
            for (int i = 0; i < 15; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(7f, 7f);
                Dust bone = Dust.NewDustDirect(
                    Projectile.Center,
                    4, 4,
                    DustID.Bone,
                    Scale: Main.rand.NextFloat(1.2f, 2f)
                );
                bone.velocity = velocity;
                bone.noGravity = true;
            }
        }

        private void SpawnLaunchTrailParticle() {
            Dust trail = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                DustID.Bone,
                -Projectile.velocity * Main.rand.NextFloat(0.1f, 0.25f),
                100,
                default,
                Main.rand.NextFloat(0.9f, 1.4f)
            );
            trail.noGravity = true;
            trail.fadeIn = 1.1f;
        }

        private void SpawnScatterEffect() {
            for (int i = 0; i < 20; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(9f, 9f);

                Dust scatter = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Bone,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(1.3f, 2.1f)
                );
                scatter.noGravity = true;
                scatter.fadeIn = 1.2f;
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

            //骨头碰撞音效
            SoundEngine.PlaySound(SoundID.Dig with {
                Volume = 0.5f,
                Pitch = 0.4f
            }, Projectile.Center);

            //碰撞骨质碎片
            for (int i = 0; i < 5; i++) {
                Dust.NewDust(
                    Projectile.position,
                    Projectile.width,
                    Projectile.height,
                    DustID.Bone,
                    Scale: Main.rand.NextFloat(1f, 1.5f)
                );
            }

            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //击中骨质粒子效果
            for (int i = 0; i < 12; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(6f, 6f);
                Dust hitDust = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Bone,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(1.3f, 2f)
                );
                hitDust.noGravity = true;
                hitDust.fadeIn = 1.2f;
            }

            //击中音效
            SoundEngine.PlaySound(SoundID.NPCHit2 with {
                Volume = 0.4f,
                Pitch = 0.2f
            }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D boneTex = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Rectangle sourceRect = boneTex.Frame(1, 1);
            Vector2 origin = sourceRect.Size() / 2f;

            Color baseColor = lightColor;
            float alpha = (255f - Projectile.alpha) / 255f;

            //===== 绘制骨头残影拖尾 =====
            if (State == BoneState.Orbiting || State == BoneState.Charging || State == BoneState.Launching) {
                DrawBoneAfterimages(sb, boneTex, sourceRect, origin, baseColor, alpha);
            }

            //===== 绘制主体骨头 =====
            // 基础绘制
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

            //蓄力/发射时的骨质辉光覆盖层
            if ((State == BoneState.Charging || State == BoneState.Launching) && glowIntensity > 0.5f) {
                float glowAlpha = (glowIntensity - 0.5f) * 2f * alpha * 0.4f;
                Color boneGlow = new Color(220, 220, 240); // 冷白色骨质辉光

                sb.Draw(
                    boneTex,
                    drawPos,
                    sourceRect,
                    boneGlow * glowAlpha,
                    Projectile.rotation,
                    origin,
                    Projectile.scale * 1.05f,
                    SpriteEffects.None,
                    0
                );
            }

            return false;
        }

        ///<summary>
        ///绘制骨头残影拖尾
        ///</summary>
        private void DrawBoneAfterimages(SpriteBatch sb, Texture2D boneTex, Rectangle sourceRect,
            Vector2 origin, Color baseColor, float alpha) {

            int afterimageCount = State == BoneState.Launching ? 12 : 8;

            for (int i = 0; i < afterimageCount; i++) {
                if (i >= Projectile.oldPos.Length || Projectile.oldPos[i] == Vector2.Zero) continue;

                float afterimageProgress = 1f - i / (float)afterimageCount;
                float afterimageAlpha = afterimageProgress * trailIntensity * alpha;

                //残影颜色：环绕时淡白，蓄力时增强，发射时最强
                Color afterimageColor;
                if (State == BoneState.Launching) {
                    afterimageColor = Color.Lerp(
                        new Color(200, 200, 220),  // 冷白
                        new Color(240, 240, 255),  // 亮白
                        afterimageProgress
                    ) * (afterimageAlpha * 0.7f);
                }
                else if (State == BoneState.Charging) {
                    afterimageColor = new Color(210, 210, 230) * (afterimageAlpha * 0.6f);
                }
                else {
                    afterimageColor = baseColor * (afterimageAlpha * 0.5f);
                }

                Vector2 afterimagePos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float afterimageScale = Projectile.scale * MathHelper.Lerp(0.85f, 1f, afterimageProgress);

                sb.Draw(
                    boneTex,
                    afterimagePos,
                    sourceRect,
                    afterimageColor,
                    Projectile.rotation - i * 0.1f * (orbitSpeed / MaxOrbitSpeed), // 轻微旋转错位
                    origin,
                    afterimageScale,
                    SpriteEffects.None,
                    0
                );
            }
        }
    }
}
