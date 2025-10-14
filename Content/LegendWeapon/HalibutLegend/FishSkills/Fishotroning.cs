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
    /// <summary>
    /// 骷髅王鱼技能-召唤骷髅王手臂进行强力攻击
    /// </summary>
    internal class Fishotroning : FishSkill
    {
        public override int UnlockFishID => ItemID.Fishotron;
        public override int DefaultCooldown => 180;

        private static readonly List<int> ActiveHands = new();
        private static int MaxHands => 2 + HalibutData.GetDomainLayer() / 3;
        private int shootCounter = 0;
        private const int HandSpawnInterval = 5;

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            shootCounter++;

            if (shootCounter >= HandSpawnInterval && Cooldown <= 0) {
                shootCounter = 0;
                SetCooldown();

                CleanupInactiveHands();

                if (ActiveHands.Count < MaxHands) {
                    SpawnSkeletronHand(player, source, damage, knockback);
                }
            }

            return null;
        }

        private void SpawnSkeletronHand(Player player, IEntitySource source, int damage, float knockback) {
            //手臂直接从玩家中心生成
            Vector2 spawnPos = player.Center;

            int handProj = Projectile.NewProjectile(
                source,
                spawnPos,
                Vector2.Zero,
                ModContent.ProjectileType<SkeletronHandMinion>(),
                (int)(damage * (8 + HalibutData.GetDomainLayer() * 2)),
                knockback * 2f,
                player.whoAmI,
                ActiveHands.Count
            );

            if (handProj >= 0) {
                ActiveHands.Add(handProj);
                SpawnSummonEffect(spawnPos);

                //骨头摩擦音效
                SoundEngine.PlaySound(SoundID.NPCHit2 with {
                    Volume = 0.8f,
                    Pitch = -0.3f
                }, spawnPos);

                //低沉的召唤音
                SoundEngine.PlaySound(SoundID.DD2_SkeletonHurt with {
                    Volume = 0.6f,
                    Pitch = -0.5f
                }, spawnPos);
            }
        }

        private static void CleanupInactiveHands() {
            ActiveHands.RemoveAll(id => {
                if (id < 0 || id >= Main.maxProjectiles) return true;
                Projectile proj = Main.projectile[id];
                return !proj.active || proj.type != ModContent.ProjectileType<SkeletronHandMinion>();
            });
        }

        private void SpawnSummonEffect(Vector2 position) {
            //骨质粒子爆发
            for (int i = 0; i < 30; i++) {
                float angle = MathHelper.TwoPi * i / 30f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 8f);

                Dust dust = Dust.NewDustPerfect(
                    position,
                    DustID.Bone,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                dust.noGravity = true;
                dust.fadeIn = 1.4f;
            }

            //烟雾环
            for (int i = 0; i < 3; i++) {
                for (int j = 0; j < 15; j++) {
                    float angle = MathHelper.TwoPi * j / 15f;
                    float radius = 30f + i * 20f;
                    Vector2 spawnPos = position + angle.ToRotationVector2() * radius;

                    Dust ring = Dust.NewDustPerfect(
                        spawnPos,
                        DustID.Smoke,
                        Vector2.Zero,
                        100,
                        new Color(100, 100, 100),
                        Main.rand.NextFloat(1.2f, 2f)
                    );
                    ring.noGravity = true;
                    ring.velocity = angle.ToRotationVector2() * 3f;
                }
            }
        }
    }

    #region 骷髅王手臂仆从
    /// <summary>
    /// 骷髅王手臂-使用IK系统实现流畅的攻击动作
    /// </summary>
    internal class SkeletronHandMinion : ModProjectile
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.SkeletronHand;

        private enum HandState
        {
            Idle,           //待机漂浮
            Targeting,      //锁定目标
            WindingUp,      //蓄力后拉
            Swinging,       //挥击
            Slamming,       //下砸
            Sweeping,       //横扫
            Throwing,       //投掷骨头
            Recovering      //攻击后恢复
        }

        private ref float HandIndex => ref Projectile.ai[0];
        private ref float StateRaw => ref Projectile.ai[1];
        private ref float StateTimer => ref Projectile.localAI[0];
        private ref float AttackType => ref Projectile.localAI[1];

        private HandState State {
            get => (HandState)StateRaw;
            set => StateRaw = (float)value;
        }

        private int targetNPCID = -1;
        private Vector2 idleOffset = Vector2.Zero;
        private Vector2 attackStartPos = Vector2.Zero;
        private Vector2 attackTargetPos = Vector2.Zero;

        //IK手臂参数
        private readonly List<Vector2> armSegments = new();
        private const int ArmSegmentCount = 6;
        private const float SegmentLength = 45f;
        private Vector2 shoulderPos = Vector2.Zero;
        private Vector2 handPos = Vector2.Zero;
        private float armTension = 0f; //手臂张力,用于IK自然度

        //攻击参数
        private const float SearchRange = 800f;
        private const int IdleDuration = 40;
        private const int WindUpDuration = 30;
        private const int SwingDuration = 18;
        private const int SlamDuration = 22;
        private const int SweepDuration = 35;
        private const int ThrowDuration = 40;
        private const int RecoverDuration = 35;

        //视觉效果
        private float glowIntensity = 0f;
        private float impactShake = 0f;
        private readonly List<Vector2> trailPositions = new();
        private const int MaxTrailLength = 20;
        private float handScale = 1f;
        private float attackWindUpIntensity = 0f;

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 1;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = false;
        }

        public override void SetDefaults() {
            Projectile.width = 80;
            Projectile.height = 80;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;

            //初始化IK手臂段
            for (int i = 0; i < ArmSegmentCount; i++) {
                armSegments.Add(Vector2.Zero);
            }
        }

        public override bool? CanDamage() => State == HandState.Swinging || State == HandState.Slamming || State == HandState.Sweeping;

        public override void AI() {
            Player owner = Main.player[Projectile.owner];

            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            if (!FishSkill.GetT<Fishotroning>().Active(owner)) {
                Projectile.Kill();
                return;
            }

            StateTimer++;
            UpdateIdleOffset();
            UpdateShoulderPosition(owner);

            //状态机
            switch (State) {
                case HandState.Idle:
                    IdleBehavior(owner);
                    break;
                case HandState.Targeting:
                    TargetingBehavior();
                    break;
                case HandState.WindingUp:
                    WindUpBehavior();
                    break;
                case HandState.Swinging:
                    SwingingBehavior();
                    break;
                case HandState.Slamming:
                    SlammingBehavior();
                    break;
                case HandState.Sweeping:
                    SweepingBehavior();
                    break;
                case HandState.Throwing:
                    ThrowingBehavior();
                    break;
                case HandState.Recovering:
                    RecoveringBehavior(owner);
                    break;
            }

            //更新IK手臂
            UpdateArmIK();

            //更新拖尾
            UpdateTrail();

            //发光效果
            float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 5f) * 0.2f + 0.8f;
            Lighting.AddLight(Projectile.Center, 0.5f * pulse, 0.5f * pulse, 0.6f * pulse);

            //冲击震动衰减
            impactShake *= 0.85f;

            //手部缩放回归
            handScale = MathHelper.Lerp(handScale, 1f, 0.1f);

            //蓄力强度衰减
            attackWindUpIntensity *= 0.95f;

            //旋转朝向
            UpdateRotation();
        }

        private void UpdateIdleOffset() {
            //更加明显的漂浮效果
            idleOffset.X = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f + HandIndex) * 50f;
            idleOffset.Y = (float)Math.Cos(Main.GlobalTimeWrappedHourly * 1.5f + HandIndex) * 30f;
        }

        private void UpdateShoulderPosition(Player owner) {
            //肩膀固定在玩家中心
            shoulderPos = owner.Center;
        }

        private void IdleBehavior(Player owner) {
            //在玩家周围较远距离漂浮
            float angle = HandIndex * MathHelper.TwoPi / 3f + Main.GlobalTimeWrappedHourly * 0.5f;
            Vector2 targetPos = shoulderPos + angle.ToRotationVector2() * 150f + idleOffset + new Vector2(0, -80f);
            MoveToPosition(targetPos, 0.15f);

            glowIntensity = 0.3f;
            armTension = 0.3f;

            //搜索敌人
            if (StateTimer > IdleDuration) {
                NPC target = FindNearestEnemy(owner);
                if (target != null) {
                    targetNPCID = target.whoAmI;
                    State = HandState.Targeting;
                    StateTimer = 0;
                }
            }

            //周围骨质粒子
            if (Main.rand.NextBool(10)) {
                SpawnIdleDust();
            }
        }

        private void TargetingBehavior() {
            if (!IsTargetValid()) {
                State = HandState.Idle;
                StateTimer = 0;
                return;
            }

            NPC target = Main.npc[targetNPCID];
            float distanceToTarget = Vector2.Distance(Projectile.Center, target.Center);

            //根据距离决定行为
            if (distanceToTarget > 400f) {
                //距离较远-直接进入投掷模式
                AttackType = 3;
                State = HandState.WindingUp;
                StateTimer = 0;
                attackStartPos = Projectile.Center;
                attackTargetPos = target.Center;
                attackWindUpIntensity = 1f;
                
                //投掷前置音效
                SoundEngine.PlaySound(SoundID.DD2_SkeletonHurt with {
                    Volume = 0.4f,
                    Pitch = -0.3f
                }, Projectile.Center);
            }
            else {
                //距离适中-移动到目标附近
                Vector2 approachPos = target.Center + new Vector2(0, -180f);
                MoveToPosition(approachPos, 0.2f);

                glowIntensity = 0.5f;
                armTension = 0.6f;

                //到达位置后选择近战攻击方式
                if (Vector2.Distance(Projectile.Center, approachPos) < 120f) {
                    ChooseAttackType(target);
                    State = HandState.WindingUp;
                    StateTimer = 0;
                    attackStartPos = Projectile.Center;
                    attackTargetPos = target.Center;
                    attackWindUpIntensity = 1f;
                }

                //锁定音效
                if (StateTimer == 1) {
                    SoundEngine.PlaySound(SoundID.DD2_SkeletonHurt with {
                        Volume = 0.4f,
                        Pitch = -0.3f
                    }, Projectile.Center);
                }
            }

            //如果长时间无法接近目标,切换到投掷
            if (StateTimer > 180 && distanceToTarget > 200f) {
                AttackType = 3;
                State = HandState.WindingUp;
                StateTimer = 0;
                attackStartPos = Projectile.Center;
                attackTargetPos = target.Center;
                attackWindUpIntensity = 1f;
            }
        }

        private void ChooseAttackType(NPC target) {
            //根据相对位置和随机性选择近战攻击方式
            Vector2 toTarget = target.Center - Projectile.Center;
            
            if (Math.Abs(toTarget.Y) > Math.Abs(toTarget.X) * 1.2f && toTarget.Y > 0) {
                //目标在下方-下砸攻击
                AttackType = 1;
            }
            else {
                //横向-随机选择挥击/横扫
                AttackType = Main.rand.NextBool() ? 0 : 2;
            }
        }

        private void WindUpBehavior() {
            if (!IsTargetValid()) {
                State = HandState.Idle;
                StateTimer = 0;
                return;
            }

            float progress = StateTimer / WindUpDuration;
            glowIntensity = 0.5f + progress * 0.4f;
            armTension = 0.9f;

            //根据攻击类型后拉-增大幅度
            Vector2 windUpOffset = AttackType switch {
                0 => new Vector2(-200f, -100f),  //挥击-向后上方拉
                1 => new Vector2(0, -250f),      //下砸-向上拉
                2 => new Vector2(-220f, 0),      //横扫-向侧后方拉
                3 => new Vector2(-180f, -120f),  //投掷-向后上方
                _ => Vector2.Zero
            };

            Vector2 targetPos = attackStartPos + windUpOffset;
            MoveToPosition(targetPos, 0.3f);

            //蓄力时手部放大
            handScale = 1f + progress * 0.3f;

            //蓄力粒子
            if (Main.rand.NextBool(2)) {
                SpawnWindUpDust();
            }

            //蓄力音效
            if (StateTimer % 8 == 0) {
                SoundEngine.PlaySound(SoundID.Item1 with {
                    Volume = 0.3f * progress,
                    Pitch = -0.5f + progress * 0.3f
                }, Projectile.Center);
            }

            if (StateTimer >= WindUpDuration) {
                //切换到对应攻击状态
                State = AttackType switch {
                    0 => HandState.Swinging,
                    1 => HandState.Slamming,
                    2 => HandState.Sweeping,
                    3 => HandState.Throwing,
                    _ => HandState.Idle
                };
                StateTimer = 0;

                //攻击开始音效
                SoundEngine.PlaySound(SoundID.Item1 with {
                    Volume = 0.8f,
                    Pitch = -0.2f
                }, Projectile.Center);

                SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with {
                    Volume = 0.7f
                }, Projectile.Center);
            }
        }

        private void SwingingBehavior() {
            float progress = StateTimer / SwingDuration;
            glowIntensity = 1f;
            armTension = 1f;

            //快速挥击弧线-增大范围
            float swingAngle = MathHelper.Lerp(
                MathHelper.PiOver2 * 1.2f,
                -MathHelper.PiOver4 * 1.5f,
                EaseInOutCubic(progress)
            );

            Vector2 swingOffset = new Vector2(
                (float)Math.Cos(swingAngle) * 250f,
                (float)Math.Sin(swingAngle) * 180f
            );

            Projectile.Center = attackTargetPos + swingOffset;
            Projectile.velocity = (attackTargetPos - Projectile.Center) * 1.2f;

            //挥击时手部缩放效果
            handScale = 1f + (float)Math.Sin(progress * MathHelper.Pi) * 0.4f;

            //挥击特效
            SpawnSwingEffect();

            if (StateTimer >= SwingDuration) {
                State = HandState.Recovering;
                StateTimer = 0;
                CreateImpactEffect(attackTargetPos);
            }
        }

        private void SlammingBehavior() {
            float progress = StateTimer / SlamDuration;
            glowIntensity = 1f;
            armTension = 1f;

            //强力下砸-增强速度曲线
            Vector2 slamStart = attackTargetPos + new Vector2(0, -250f);
            Vector2 slamEnd = attackTargetPos + new Vector2(0, 50f);

            float easeProgress = EaseInCubic(progress);
            Projectile.Center = Vector2.Lerp(slamStart, slamEnd, easeProgress);

            Projectile.velocity = Vector2.Lerp(
                Vector2.Zero,
                new Vector2(0, 60f),
                easeProgress
            );

            //下砸时手部逐渐握紧效果
            handScale = 1f + (1f - progress) * 0.5f;

            //下砸轨迹特效
            SpawnSlamTrail();

            if (StateTimer >= SlamDuration) {
                State = HandState.Recovering;
                StateTimer = 0;
                CreateSlamImpact(slamEnd);
            }
        }

        private void SweepingBehavior() {
            float progress = StateTimer / SweepDuration;
            glowIntensity = 1f;
            armTension = 1f;

            //横扫弧线-增大范围
            float sweepAngle = MathHelper.Lerp(
                -MathHelper.Pi * 1.1f,
                MathHelper.Pi * 1.1f,
                EaseInOutQuad(progress)
            );

            float radius = 220f;
            Vector2 sweepOffset = new Vector2(
                (float)Math.Cos(sweepAngle) * radius,
                (float)Math.Sin(sweepAngle) * radius * 0.4f
            );

            Projectile.Center = attackTargetPos + sweepOffset;
            Projectile.velocity = Vector2.Zero;

            //横扫时手部扩张
            handScale = 1f + (float)Math.Sin(progress * MathHelper.Pi) * 0.35f;

            //横扫特效
            SpawnSweepEffect();

            if (StateTimer >= SweepDuration) {
                State = HandState.Recovering;
                StateTimer = 0;
                CreateImpactEffect(Projectile.Center);
            }
        }

        private void ThrowingBehavior() {
            float progress = StateTimer / ThrowDuration;
            glowIntensity = 1f;
            armTension = 0.8f;

            if (StateTimer < ThrowDuration / 2) {
                //前半段-手臂后拉蓄力
                Vector2 windUpPos = attackStartPos + new Vector2(-180f, -120f);
                MoveToPosition(windUpPos, 0.3f);
                handScale = 1f + progress * 2f * 0.4f;

                //蓄力粒子
                if (Main.rand.NextBool(3)) {
                    SpawnWindUpDust();
                }
            }
            else {
                //后半段-投掷动作
                float throwProgress = (StateTimer - ThrowDuration / 2) / (ThrowDuration / 2);
                Vector2 throwPos = attackTargetPos + new Vector2(-50f, -80f);
                MoveToPosition(throwPos, 0.5f);
                handScale = 1f + (1f - throwProgress) * 0.4f;

                //投掷骨头
                if (StateTimer == ThrowDuration / 2 + 5) {
                    ThrowBones();
                }
            }

            if (StateTimer >= ThrowDuration) {
                State = HandState.Recovering;
                StateTimer = 0;
            }
        }

        private void ThrowBones() {
            if (!IsTargetValid() || Main.myPlayer != Projectile.owner) return;

            NPC target = Main.npc[targetNPCID];
            Vector2 throwDirection = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);

            //投掷5-8根骨头
            int boneCount = 5 + Main.rand.Next(4);
            for (int i = 0; i < boneCount; i++) {
                float spreadAngle = MathHelper.Lerp(-0.4f, 0.4f, i / (float)(boneCount - 1));
                Vector2 velocity = throwDirection.RotatedBy(spreadAngle) * Main.rand.NextFloat(18f, 25f);

                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    velocity,
                    ProjectileID.Bone,
                    Projectile.damage / 2,
                    2f,
                    Projectile.owner
                );
            }

            //投掷特效
            for (int i = 0; i < 20; i++) {
                Vector2 velocity = throwDirection.RotatedByRandom(0.5f) * Main.rand.NextFloat(5f, 10f);
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Bone,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                dust.noGravity = true;
            }

            //投掷音效
            SoundEngine.PlaySound(SoundID.Item1 with {
                Volume = 0.7f,
                Pitch = 0.3f
            }, Projectile.Center);

            SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with {
                Volume = 0.6f,
                Pitch = 0.2f
            }, Projectile.Center);
        }

        private void RecoveringBehavior(Player owner) {
            float progress = StateTimer / RecoverDuration;
            glowIntensity = 1f - progress * 0.7f;
            armTension = 0.5f;

            //返回待机位置
            float angle = HandIndex * MathHelper.TwoPi / 3f + Main.GlobalTimeWrappedHourly * 0.5f;
            Vector2 recoverPos = shoulderPos + angle.ToRotationVector2() * 150f + idleOffset + new Vector2(0, -80f);
            MoveToPosition(recoverPos, 0.2f);

            if (StateTimer >= RecoverDuration) {
                State = HandState.Idle;
                StateTimer = 0;
            }
        }

        private void MoveToPosition(Vector2 target, float speed) {
            Vector2 direction = target - Projectile.Center;
            float distance = direction.Length();

            if (distance > 5f) {
                direction.Normalize();
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, direction * distance * speed, 0.3f);
            }
            else {
                Projectile.velocity *= 0.9f;
            }

            Projectile.Center += Projectile.velocity;
        }

        private void UpdateArmIK() {
            handPos = Projectile.Center;

            //增强的FABRIK算法,考虑手臂张力
            float targetDistance = Vector2.Distance(shoulderPos, handPos);
            float maxReach = SegmentLength * ArmSegmentCount;

            //如果超出最大伸展范围,限制手的位置
            if (targetDistance > maxReach * 0.98f) {
                Vector2 direction = (handPos - shoulderPos).SafeNormalize(Vector2.Zero);
                handPos = shoulderPos + direction * maxReach * 0.98f;
                Projectile.Center = handPos;
            }

            //前向遍历-从手到肩
            armSegments[0] = handPos;
            for (int i = 1; i < ArmSegmentCount; i++) {
                Vector2 direction = (armSegments[i - 1] - (i == ArmSegmentCount - 1 ? shoulderPos : armSegments[i])).SafeNormalize(Vector2.Zero);
                
                //根据张力调整关节位置,增加自然弯曲
                float bendFactor = (float)Math.Sin((i / (float)ArmSegmentCount) * MathHelper.Pi) * armTension;
                Vector2 perpendicular = new Vector2(-direction.Y, direction.X) * bendFactor * 15f;
                
                armSegments[i] = armSegments[i - 1] - direction * SegmentLength + perpendicular;
            }

            //反向遍历-从肩到手
            armSegments[ArmSegmentCount - 1] = shoulderPos;
            for (int i = ArmSegmentCount - 2; i >= 0; i--) {
                Vector2 direction = (armSegments[i] - armSegments[i + 1]).SafeNormalize(Vector2.Zero);
                
                //同样应用弯曲
                float bendFactor = (float)Math.Sin((i / (float)ArmSegmentCount) * MathHelper.Pi) * armTension;
                Vector2 perpendicular = new Vector2(-direction.Y, direction.X) * bendFactor * 15f;
                
                armSegments[i] = armSegments[i + 1] + direction * SegmentLength + perpendicular;
            }

            //最终调整手的位置
            Projectile.Center = armSegments[0];
        }

        private void UpdateTrail() {
            trailPositions.Insert(0, Projectile.Center);
            if (trailPositions.Count > MaxTrailLength) {
                trailPositions.RemoveAt(trailPositions.Count - 1);
            }
        }

        private void UpdateRotation() {
            if (Projectile.velocity.LengthSquared() > 0.1f) {
                Projectile.rotation = MathHelper.Lerp(
                    Projectile.rotation,
                    Projectile.velocity.ToRotation() + MathHelper.PiOver2,
                    0.2f
                );
            }
        }

        private NPC FindNearestEnemy(Player owner) {
            NPC closest = null;
            float closestDist = SearchRange;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.CanBeChasedBy() && !npc.friendly) {
                    float dist = Vector2.Distance(owner.Center, npc.Center);
                    if (dist < closestDist) {
                        closestDist = dist;
                        closest = npc;
                    }
                }
            }

            return closest;
        }

        private bool IsTargetValid() {
            if (targetNPCID < 0 || targetNPCID >= Main.maxNPCs) return false;
            NPC target = Main.npc[targetNPCID];
            return target.active && target.CanBeChasedBy();
        }

        //特效方法
        private void SpawnIdleDust() {
            Dust dust = Dust.NewDustDirect(
                Projectile.position,
                Projectile.width,
                Projectile.height,
                DustID.Bone,
                Scale: Main.rand.NextFloat(0.8f, 1.3f)
            );
            dust.velocity = Main.rand.NextVector2Circular(1f, 1f);
            dust.noGravity = true;
        }

        private void SpawnWindUpDust() {
            for (int i = 0; i < 2; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(3f, 3f);
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(40f, 40f),
                    DustID.Bone,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(1.2f, 1.8f)
                );
                dust.noGravity = true;
            }
        }

        private void SpawnSwingEffect() {
            if (Main.rand.NextBool(2)) {
                Dust dust = Dust.NewDustDirect(
                    Projectile.position,
                    Projectile.width,
                    Projectile.height,
                    DustID.Smoke,
                    Scale: Main.rand.NextFloat(1.5f, 2.5f)
                );
                dust.velocity = Projectile.velocity * 0.3f;
                dust.noGravity = true;
                dust.color = new Color(150, 150, 150);
            }
        }

        private void SpawnSlamTrail() {
            for (int i = 0; i < 3; i++) {
                Dust dust = Dust.NewDustDirect(
                    Projectile.position,
                    Projectile.width,
                    Projectile.height,
                    DustID.Bone,
                    0, 0, 100,
                    default,
                    Main.rand.NextFloat(1.5f, 2.2f)
                );
                dust.velocity = -Projectile.velocity * 0.2f + Main.rand.NextVector2Circular(2f, 2f);
                dust.noGravity = true;
            }
        }

        private void SpawnSweepEffect() {
            if (Main.rand.NextBool()) {
                Vector2 velocity = Projectile.velocity.RotatedByRandom(0.5f) * Main.rand.NextFloat(0.5f, 1.5f);
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Smoke,
                    velocity,
                    100,
                    new Color(180, 180, 180),
                    Main.rand.NextFloat(1.8f, 2.8f)
                );
                dust.noGravity = true;
            }
        }

        private void CreateImpactEffect(Vector2 position) {
            impactShake = 12f;

            //冲击波
            for (int i = 0; i < 40; i++) {
                float angle = MathHelper.TwoPi * i / 40f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(5f, 12f);

                Dust dust = Dust.NewDustPerfect(
                    position,
                    DustID.Bone,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                dust.noGravity = true;
                dust.fadeIn = 1.3f;
            }

            //烟雾
            for (int i = 0; i < 20; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(8f, 8f);
                Dust smoke = Dust.NewDustPerfect(
                    position,
                    DustID.Smoke,
                    velocity,
                    100,
                    new Color(100, 100, 100),
                    Main.rand.NextFloat(2f, 3.5f)
                );
                smoke.noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item14 with {
                Volume = 0.8f,
                Pitch = -0.4f
            }, position);

            SoundEngine.PlaySound(SoundID.NPCHit2 with {
                Volume = 0.7f,
                Pitch = -0.3f
            }, position);
        }

        private void CreateSlamImpact(Vector2 position) {
            impactShake = 18f;

            //强力冲击波
            for (int i = 0; i < 60; i++) {
                float angle = MathHelper.TwoPi * i / 60f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(8f, 18f);

                Dust dust = Dust.NewDustPerfect(
                    position,
                    DustID.Bone,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(2f, 3.5f)
                );
                dust.noGravity = true;
                dust.fadeIn = 1.5f;
            }

            //地面碎片
            for (int i = 0; i < 30; i++) {
                Vector2 velocity = new Vector2(Main.rand.NextFloat(-10f, 10f), Main.rand.NextFloat(-15f, -5f));
                Dust debris = Dust.NewDustPerfect(
                    position,
                    DustID.Stone,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                debris.noGravity = false;
            }

            //烟尘环
            for (int i = 0; i < 3; i++) {
                for (int j = 0; j < 20; j++) {
                    float angle = MathHelper.TwoPi * j / 20f;
                    float radius = 40f + i * 30f;
                    Vector2 spawnPos = position + angle.ToRotationVector2() * radius;

                    Dust smoke = Dust.NewDustPerfect(
                        spawnPos,
                        DustID.Smoke,
                        Vector2.Zero,
                        100,
                        new Color(120, 120, 120),
                        Main.rand.NextFloat(2.5f, 4f)
                    );
                    smoke.velocity = angle.ToRotationVector2() * 4f;
                    smoke.noGravity = true;
                }
            }

            SoundEngine.PlaySound(SoundID.Item14 with {
                Volume = 1f,
                Pitch = -0.5f
            }, position);

            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                Volume = 0.9f
            }, position);
        }

        //缓动函数
        private float EaseInOutCubic(float t) {
            return t < 0.5f
                ? 4f * t * t * t
                : 1f - (float)Math.Pow(-2f * t + 2f, 3) / 2f;
        }

        private float EaseInCubic(float t) {
            return t * t * t;
        }

        private float EaseInOutQuad(float t) {
            return t < 0.5f ? 2f * t * t : 1f - (float)Math.Pow(-2f * t + 2f, 2) / 2f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //击中特效
            for (int i = 0; i < 15; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(8f, 8f);
                Dust dust = Dust.NewDustPerfect(
                    target.Center,
                    DustID.Bone,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                dust.noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.NPCHit2 with {
                Volume = 0.6f,
                Pitch = 0.2f
            }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            //消散特效
            for (int i = 0; i < 30; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(6f, 6f);
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Bone,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                dust.noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.NPCDeath2 with {
                Volume = 0.5f,
                Pitch = -0.3f
            }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D handTexture = TextureAssets.Npc[NPCID.SkeletronHand].Value;
            Vector2 origin = handTexture.Size() / 2f;

            //绘制IK手臂链-使用骨头纹理
            DrawArmChain(sb, lightColor);

            //绘制攻击拖尾
            if (State == HandState.Swinging || State == HandState.Slamming || State == HandState.Sweeping) {
                DrawAttackTrail(sb, handTexture, origin);
            }

            //添加冲击震动偏移
            Vector2 shakeOffset = Main.rand.NextVector2Circular(impactShake, impactShake);
            Vector2 drawPos = Projectile.Center - Main.screenPosition + shakeOffset;

            //蓄力辉光效果
            if (attackWindUpIntensity > 0.3f) {
                for (int i = 0; i < 3; i++) {
                    float windUpScale = handScale * (1.15f + i * 0.1f);
                    float windUpAlpha = (attackWindUpIntensity - 0.3f) * (1f - i * 0.3f) * 0.5f;

                    sb.Draw(
                        handTexture,
                        drawPos,
                        null,
                        new Color(255, 200, 200, 0) * windUpAlpha,
                        Projectile.rotation + MathHelper.Pi,
                        origin,
                        Projectile.scale * windUpScale,
                        SpriteEffects.None,
                        0
                    );
                }
            }

            //发光层
            if (glowIntensity > 0.5f) {
                for (int i = 0; i < 3; i++) {
                    float glowScale = handScale * (1.1f + i * 0.1f);
                    float glowAlpha = (glowIntensity - 0.5f) * (1f - i * 0.3f);

                    sb.Draw(
                        handTexture,
                        drawPos,
                        null,
                        new Color(200, 200, 220, 0) * glowAlpha,
                        Projectile.rotation + MathHelper.Pi,
                        origin,
                        Projectile.scale * glowScale,
                        SpriteEffects.None,
                        0
                    );
                }
            }

            //主体绘制
            sb.Draw(
                handTexture,
                drawPos,
                null,
                lightColor,
                Projectile.rotation + MathHelper.Pi,
                origin,
                Projectile.scale * handScale,
                SpriteEffects.None,
                0
            );

            return false;
        }

        private void DrawArmChain(SpriteBatch sb, Color lightColor) {
            //使用骨头弹幕纹理作为链条
            Texture2D boneTexture = TextureAssets.Projectile[ProjectileID.Bone].Value;
            
            for (int i = 0; i < armSegments.Count - 1; i++) {
                Vector2 start = armSegments[i + 1];
                Vector2 end = armSegments[i];
                Vector2 diff = end - start;
                float length = diff.Length();
                float rotation = diff.ToRotation() + MathHelper.PiOver4;

                //计算需要多少骨头来填充这段
                int boneCount = Math.Max(1, (int)(length / 20f));
                
                for (int j = 0; j < boneCount; j++) {
                    float progress = j / (float)boneCount;
                    Vector2 bonePos = Vector2.Lerp(start, end, progress);
                    
                    //根据位置添加轻微的骨头大小变化
                    float boneScale = Projectile.scale * MathHelper.Lerp(0.6f, 0.8f, (float)Math.Sin(progress * MathHelper.Pi));
                    
                    sb.Draw(
                        boneTexture,
                        bonePos - Main.screenPosition,
                        null,
                        lightColor * 0.9f,
                        rotation + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f + i + j) * 0.1f,
                        boneTexture.Size() / 2f,
                        boneScale * 2f,
                        SpriteEffects.None,
                        0
                    );
                }
            }
        }

        private void DrawAttackTrail(SpriteBatch sb, Texture2D texture, Vector2 origin) {
            for (int i = 0; i < trailPositions.Count; i++) {
                if (i >= trailPositions.Count - 1) continue;

                float fade = (trailPositions.Count - i) / (float)trailPositions.Count;
                Color trailColor = new Color(180, 180, 200, 0) * fade * 0.6f;

                sb.Draw(
                    texture,
                    trailPositions[i] - Main.screenPosition,
                    null,
                    trailColor,
                    Projectile.rotation,
                    origin,
                    Projectile.scale * handScale * (0.7f + fade * 0.3f),
                    SpriteEffects.None,
                    0
                );
            }
        }
    }
    #endregion
}
