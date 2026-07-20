using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>
    /// 骷髅王鱼技能，召唤骷髅王手臂进行攻击
    /// </summary>
    internal class Fishotroning : FishSkill
    {
        public override int UnlockFishID => ItemID.Fishotron;
        public override int DefaultCooldown => 60 * (10 - HalibutData.GetDomainLayer() / 2);
        public override int ResearchDuration => 60 * 12;
        private static readonly List<int> ActiveHands = new();
        private static int MaxHands => 2 + HalibutData.GetDomainLayer() / 3;
        private int shootCounter = 0;
        private static int HandSpawnInterval = 1;
        private int justHitCooldown;

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

        public override bool UpdateCooldown(HalibutPlayer halibutPlayer, Player player) {
            if (justHitCooldown > 0) {
                justHitCooldown--;
            }
            if (justHitCooldown <= 0 && ActiveHands.Count > 0 && player.immuneTime > 0) {
                int index = ActiveHands[^1];
                if (index.TryGetProjectile(out var hand)) {
                    hand.Kill();
                    ActiveHands.RemoveAt(ActiveHands.Count - 1);
                    justHitCooldown = 2;
                }
            }
            return true;
        }

        private static void SpawnSkeletronHand(Player player, IEntitySource source, int damage, float knockback) {
            //手臂直接从玩家中心生成
            Vector2 spawnPos = player.Center;

            int handProj = Projectile.NewProjectile(
                source,
                spawnPos,
                Vector2.Zero,
                ModContent.ProjectileType<SkeletronHandMinion>(),
                (int)(damage * (3.5f + HalibutData.GetDomainLayer() * 0.6f)),
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

        private static void SpawnSummonEffect(Vector2 position) {
            //召唤点小股骨尘扬起，化形入场读感交给 materialize 长成动画
            for (int i = 0; i < 10; i++) {
                Dust dust = Dust.NewDustPerfect(position + Main.rand.NextVector2Circular(12f, 12f)
                    , DustID.Bone
                    , new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-3f, -0.5f))
                    , 130, default, Main.rand.NextFloat(1f, 1.6f));
                dust.noGravity = false;
            }

            //诅咒魔力点燃：极少量幽绿逸散（骷髅王语系点缀，量与亮度都收着）
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_Spark>(position + Main.rand.NextVector2Circular(10f, 10f)
                    , -Vector2.UnitY * Main.rand.NextFloat(1f, 2.2f)
                    , new Color(96, 178, 110) * 0.55f, 0.32f)
                    ?.Configure(false, Main.rand.Next(14, 20));
            }
        }
    }

    #region 骷髅王手臂仆从
    internal class SkeletronHandExplode : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 200;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 4;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = false;
        }
    }

    /// <summary>骷髅王手臂，IK 驱动</summary>
    internal class SkeletronHandMinion : BaseHeldProj
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
        private float armTension = 0f; //手臂张力，IK自然度
        private int ownerDirection = 1; //玩家朝向 (-1左, 1右)

        //攻击参数
        private const float SearchRange = 800f;
        private const int IdleDuration = 40;
        private const int WindUpDuration = 30;
        private const int SwingDuration = 18;
        private const int SlamDuration = 22;
        private const int SweepDuration = 35;
        private const int ThrowDuration = 50;
        private const int RecoverDuration = 35;

        //视觉效果
        private float glowIntensity = 0f;
        private float impactShake = 0f;
        private readonly List<(Vector2 pos, float rot)> trailPoints = new();
        private const int MaxTrailLength = 20;
        private float handScale = 1f;

        //化形入场：从玩家中心长成，禁 pop-in
        private const int MaterializeDuration = 14;
        private int materializeTimer = MaterializeDuration;

        //下砸落点定帧
        private int impactHoldFrames;
        private Vector2 impactHoldPos = Vector2.Zero;

        //投掷动作相关
        private bool throwActionActive = false;
        private Vector2 throwStartPos = Vector2.Zero;
        private Vector2 throwEndPos = Vector2.Zero;

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 1;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = false;
        }

        public override void SetDefaults() {
            Projectile.width = 80;
            Projectile.height = 80;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
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
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            if (!FishSkill.GetT<Fishotroning>().Active(Owner)) {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 60;

            //化形入场期：长成动画接管，状态机搁置
            if (materializeTimer > 0) {
                MaterializeBehavior();
                return;
            }

            StateTimer++;
            UpdateIdleOffset();
            UpdateShoulderPosition(Owner);

            //状态机
            switch (State) {
                case HandState.Idle:
                    IdleBehavior(Owner);
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
                    RecoveringBehavior(Owner);
                    break;
            }

            //更新IK手臂
            UpdateArmIK();

            //更新拖尾
            UpdateTrail();

            //昏暗骨白微光，亮度跟随攻击强度而非常驻高亮
            float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f + HandIndex) * 0.15f + 0.85f;
            float li = (0.16f + glowIntensity * 0.14f) * pulse;
            Lighting.AddLight(Projectile.Center, li, li * 0.96f, li * 0.8f);
            if (State == HandState.WindingUp) {
                //蓄力期渗出的诅咒魔力微光（极暗幽绿）
                Lighting.AddLight(Projectile.Center, 0.03f, 0.09f, 0.04f);
            }

            //冲击震动衰减
            impactShake *= 0.85f;

            //手部缩放回归
            handScale = MathHelper.Lerp(handScale, 1f, 0.1f);

            //旋转朝向
            UpdateRotation();
        }

        /// <summary>化形入场：easeOutBack 过冲长成 + 上浮至首个待机位 + 钙尘剥落</summary>
        private void MaterializeBehavior() {
            materializeTimer--;
            ownerDirection = Owner.direction;
            UpdateIdleOffset();
            UpdateShoulderPosition(Owner);

            float t = 1f - materializeTimer / (float)MaterializeDuration;
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float xm = t - 1f;
            float ease = 1f + c3 * xm * xm * xm + c1 * xm * xm;
            handScale = MathHelper.Lerp(0.25f, 1f, ease);

            float angle = HandIndex * MathHelper.TwoPi / 3f + Main.GlobalTimeWrappedHourly * 0.5f;
            Vector2 circleOffset = angle.ToRotationVector2() * 150f;
            circleOffset.X *= ownerDirection;
            Vector2 target = shoulderPos + circleOffset + idleOffset + new Vector2(0, -80f);
            Projectile.Center = Vector2.Lerp(Owner.Center, target, VaultUtils.EaseOutCubic(t));
            Projectile.velocity = Vector2.Zero;

            //长成中骨屑坠落：骨骸在凝聚而非凭空出现
            if (!VaultUtils.isServer && materializeTimer % 2 == 0) {
                PRTLoader.NewParticle<PRT_FishOtronShard>(
                    Projectile.Center + Main.rand.NextVector2Circular(24f, 24f) * handScale
                    , new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(0.3f, 1.2f))
                    , default, Main.rand.NextFloat(0.4f, 0.7f))
                    ?.Configure(Main.rand.Next(16, 26));
            }

            glowIntensity = 0.25f;
            armTension = 0.35f;
            UpdateArmIK();
        }

        private void UpdateIdleOffset() {
            //漂浮偏移
            idleOffset.X = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f + HandIndex) * 50f;
            idleOffset.Y = (float)Math.Cos(Main.GlobalTimeWrappedHourly * 1.5f + HandIndex) * 30f;
        }

        private void UpdateShoulderPosition(Player owner) {
            //待机状态下更新玩家朝向
            if (State == HandState.Idle) {
                ownerDirection = owner.direction;
            }

            //肩膀位置需要根据玩家朝向偏移
            Vector2 shoulderOffset = new Vector2(8f * ownerDirection, -4f);
            shoulderPos = owner.Center + shoulderOffset;
        }

        private void IdleBehavior(Player owner) {
            //在玩家周围较远距离漂浮 - 根据玩家朝向调整位置
            float angle = HandIndex * MathHelper.TwoPi / 3f + Main.GlobalTimeWrappedHourly * 0.5f;

            //根据玩家朝向镜像X偏移
            Vector2 circleOffset = angle.ToRotationVector2() * 150f;
            circleOffset.X *= ownerDirection;

            Vector2 targetPos = shoulderPos + circleOffset + idleOffset + new Vector2(0, -80f);
            MoveToPosition(targetPos, 0.15f);

            glowIntensity = 0.3f;
            armTension = 0.3f;
            throwActionActive = false;

            //搜索敌人
            if (StateTimer > IdleDuration) {
                NPC target = owner.Center.FindClosestNPC(SearchRange);
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
                }

                //锁定音效
                if (StateTimer == 1) {
                    SoundEngine.PlaySound(SoundID.DD2_SkeletonHurt with {
                        Volume = 0.4f,
                        Pitch = -0.3f
                    }, Projectile.Center);
                }
            }

            //如果无法接近目标,切换到投掷
            if (StateTimer > 30 && distanceToTarget >= 120) {
                AttackType = 3;
                State = HandState.WindingUp;
                StateTimer = 0;
                attackStartPos = Projectile.Center;
                attackTargetPos = target.Center;
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

            //根据攻击类型后拉-增大幅度，并考虑玩家朝向
            Vector2 windUpOffset = AttackType switch {
                0 => new Vector2(-200f * ownerDirection, -100f),  //挥击-向后上方拉
                1 => new Vector2(0, -250f),                        //下砸-向上拉
                2 => new Vector2(-220f * ownerDirection, 0),       //横扫-向侧后方拉
                3 => new Vector2(-180f * ownerDirection, -120f),   //投掷-向后上方
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

                //投掷态分支
                if (State == HandState.Throwing) {
                    throwActionActive = true;
                    throwStartPos = Projectile.Center;

                    //计算投掷目标点在目标敌人前方,考虑预判
                    if (IsTargetValid()) {
                        NPC target = Main.npc[targetNPCID];
                        Vector2 predictedPos = target.Center + target.velocity * 20f;
                        throwEndPos = predictedPos;
                    }
                }

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

            //快速挥击弧线-增大范围，考虑玩家朝向
            //朝右时：从右上挥到左下
            //朝左时：从左上挥到右下
            float startAngle = MathHelper.PiOver2 * 1.2f;
            float endAngle = -MathHelper.PiOver4 * 1.5f;

            //根据玩家朝向镜像角度
            if (ownerDirection == -1) {
                startAngle = MathHelper.Pi - startAngle;
                endAngle = MathHelper.Pi - endAngle;
            }

            float swingAngle = MathHelper.Lerp(startAngle, endAngle, VaultUtils.EaseInOutCubic(progress));

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

            //指数加速下砸：pow(4) 前段悬滞后段暴坠，重量走时间曲线
            Vector2 slamStart = attackTargetPos + new Vector2(0, -250f);
            Vector2 slamEnd = attackTargetPos + new Vector2(0, 50f);

            float easeProgress = MathF.Pow(MathHelper.Clamp(progress, 0f, 1f), 4f);
            Projectile.Center = Vector2.Lerp(slamStart, slamEnd, easeProgress);

            Projectile.velocity = Vector2.Lerp(
                Vector2.Zero,
                new Vector2(0, 60f),
                easeProgress
            );

            //下砸时手部逐渐握紧效果
            handScale = 1f + (1f - progress) * 0.5f;

            //中段破空声：坠速陡增的听觉对位
            if (StateTimer == (int)(SlamDuration * 0.55f)) {
                SoundEngine.PlaySound(SoundID.Item1 with {
                    Volume = 0.55f,
                    Pitch = -0.6f
                }, Projectile.Center);
            }

            //下砸轨迹特效：坠速起来后才开始剥落
            if (progress > 0.35f) {
                SpawnSlamTrail();
            }

            if (StateTimer >= SlamDuration) {
                State = HandState.Recovering;
                StateTimer = 0;
                //落点定帧：砸击后冻结数帧再回收
                impactHoldFrames = 4;
                impactHoldPos = slamEnd;
                CreateSlamImpact(slamEnd);
            }
        }

        private void SweepingBehavior() {
            float progress = StateTimer / SweepDuration;
            glowIntensity = 1f;
            armTension = 1f;

            //横扫弧线-增大范围，考虑玩家朝向
            float startAngle = -MathHelper.Pi * 1.1f;
            float endAngle = MathHelper.Pi * 1.1f;

            //根据玩家朝向调整横扫方向
            if (ownerDirection == -1) {
                (startAngle, endAngle) = (MathHelper.Pi - endAngle, MathHelper.Pi - startAngle);
            }

            float sweepAngle = MathHelper.Lerp(startAngle, endAngle, VaultUtils.EaseInOutQuad(progress));

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

            if (StateTimer < ThrowDuration * 0.3f) {
                //前30%-保持蓄力姿态
                float holdProgress = StateTimer / (ThrowDuration * 0.3f);
                Vector2 windUpPos = throwStartPos;
                MoveToPosition(windUpPos, 0.2f);
                handScale = 1f + 0.4f;

                //蓄力粒子持续生成
                if (Main.rand.NextBool(2)) {
                    SpawnWindUpDust();
                }
            }
            else if (StateTimer < ThrowDuration * 0.7f) {
                //中40%-快速前冲投掷动作
                float throwProgress = (StateTimer - ThrowDuration * 0.3f) / (ThrowDuration * 0.4f);
                float easeProgress = VaultUtils.EaseOutCubic(throwProgress);

                //手臂快速向前冲
                Vector2 currentPos = Vector2.Lerp(throwStartPos, throwEndPos, easeProgress);
                Projectile.Center = currentPos;
                Projectile.velocity = (throwEndPos - throwStartPos).SafeNormalize(Vector2.Zero) * 35f * (1f - easeProgress);

                handScale = 1f + 0.4f * (1f - throwProgress);

                //在投掷动作中段释放骨头
                if (StateTimer == (int)(ThrowDuration * 0.5f)) {
                    ThrowBones();
                }

                //投掷动作轨迹特效
                if (Main.rand.NextBool()) {
                    SpawnThrowTrailEffect();
                }
            }
            else {
                //后30%-收手减速
                float recoverProgress = (StateTimer - ThrowDuration * 0.7f) / (ThrowDuration * 0.3f);
                Projectile.velocity *= 0.85f;
                handScale = 1f + 0.2f * (1f - recoverProgress);
            }

            if (StateTimer >= ThrowDuration) {
                State = HandState.Recovering;
                StateTimer = 0;
                throwActionActive = false;
            }
        }

        private void ThrowBones() {
            if (!IsTargetValid() || Main.myPlayer != Projectile.owner) return;

            NPC target = Main.npc[targetNPCID];

            //计算从手掌中心到目标的方向
            Vector2 throwOrigin = Projectile.Center;
            Vector2 toTarget = (target.Center - throwOrigin).SafeNormalize(Vector2.Zero);

            //投掷5-8根骨头
            int boneCount = 5 + Main.rand.Next(4);
            for (int i = 0; i < boneCount; i++) {
                //扇形散射角度
                float spreadAngle = MathHelper.Lerp(-0.35f, 0.35f, i / (float)(boneCount - 1));
                Vector2 velocity = toTarget.RotatedBy(spreadAngle) * Main.rand.NextFloat(20f, 28f);

                //从手掌位置生成骨头,添加轻微随机偏移
                Vector2 spawnOffset = Main.rand.NextVector2Circular(8f, 8f);

                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    throwOrigin + spawnOffset,
                    velocity,
                    ModContent.ProjectileType<FishotroningBone>(),
                    (int)(Projectile.damage * 0.1),
                    2f,
                    Projectile.owner
                );
            }

            //出手强调：沿投掷方向的定向骨尘喷流（方向性优先于数量）
            for (int i = 0; i < 10; i++) {
                Dust dust = Dust.NewDustPerfect(
                    throwOrigin,
                    DustID.Bone,
                    toTarget.RotatedByRandom(0.45f) * Main.rand.NextFloat(6f, 13f),
                    110,
                    default,
                    Main.rand.NextFloat(1.2f, 2f)
                );
                dust.noGravity = false;
            }

            //出手骨屑：受重力续落成余迹
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_FishOtronShard>(throwOrigin
                    , toTarget.RotatedByRandom(0.35f) * Main.rand.NextFloat(4f, 9f)
                    , default, Main.rand.NextFloat(0.45f, 0.7f))
                    ?.Configure(Main.rand.Next(18, 28));
            }

            //投掷音效
            SoundEngine.PlaySound(SoundID.Item1 with {
                Volume = 0.85f,
                Pitch = 0.4f
            }, throwOrigin);

            SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with {
                Volume = 0.75f,
                Pitch = 0.3f
            }, throwOrigin);

            //骨头碎裂音效
            SoundEngine.PlaySound(SoundID.NPCHit2 with {
                Volume = 0.6f,
                Pitch = 0.5f
            }, throwOrigin);
        }

        private void RecoveringBehavior(Player owner) {
            //落点定帧：冻结在冲击位，重量的余韵
            if (impactHoldFrames > 0) {
                impactHoldFrames--;
                Projectile.Center = impactHoldPos;
                Projectile.velocity = Vector2.Zero;
                return;
            }

            float progress = StateTimer / RecoverDuration;
            glowIntensity = 1f - progress * 0.7f;
            armTension = 0.5f;

            //返回待机位置 - 考虑玩家朝向
            float angle = HandIndex * MathHelper.TwoPi / 3f + Main.GlobalTimeWrappedHourly * 0.5f;
            Vector2 circleOffset = angle.ToRotationVector2() * 150f;
            circleOffset.X *= ownerDirection;

            Vector2 recoverPos = shoulderPos + circleOffset + idleOffset + new Vector2(0, -80f);
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
                //关键修复：根据玩家朝向调整弯曲方向
                float bendFactor = (float)Math.Sin((i / (float)ArmSegmentCount) * MathHelper.Pi) * armTension;
                Vector2 perpendicular = new Vector2(-direction.Y, direction.X) * bendFactor * 15f * ownerDirection;

                armSegments[i] = armSegments[i - 1] - direction * SegmentLength + perpendicular;
            }

            //反向遍历-从肩到手
            armSegments[ArmSegmentCount - 1] = shoulderPos;
            for (int i = ArmSegmentCount - 2; i >= 0; i--) {
                Vector2 direction = (armSegments[i] - armSegments[i + 1]).SafeNormalize(Vector2.Zero);

                //同样应用弯曲，考虑玩家朝向
                float bendFactor = (float)Math.Sin((i / (float)ArmSegmentCount) * MathHelper.Pi) * armTension;
                Vector2 perpendicular = new Vector2(-direction.Y, direction.X) * bendFactor * 15f * ownerDirection;

                armSegments[i] = armSegments[i + 1] + direction * SegmentLength + perpendicular;
            }

            //最终调整手的位置
            Projectile.Center = armSegments[0];
        }

        private void UpdateTrail() {
            //位置＋旋转角双历史：拖影才能编码弧线挥动的自旋分量
            trailPoints.Insert(0, (Projectile.Center, Projectile.rotation));
            if (trailPoints.Count > MaxTrailLength) {
                trailPoints.RemoveAt(trailPoints.Count - 1);
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

        private bool IsTargetValid() {
            if (targetNPCID < 0 || targetNPCID >= Main.maxNPCs) return false;
            NPC target = Main.npc[targetNPCID];
            return target.active && target.CanBeChasedBy();
        }

        //特效方法
        private void SpawnIdleDust() {
            //待机时偶落的钙化碎屑：老骨头一直在掉渣
            Dust dust = Dust.NewDustDirect(
                Projectile.position,
                Projectile.width,
                Projectile.height,
                DustID.Bone,
                Scale: Main.rand.NextFloat(0.8f, 1.3f)
            );
            dust.velocity = new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(0.3f, 1f));
            dust.noGravity = false;
        }

        private void SpawnWindUpDust() {
            //蓄力剥落：钙化碎屑受重力抖落（漂浮光点换成落地实物）
            Dust dust = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(34f, 34f) * handScale,
                DustID.Bone,
                new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(0.4f, 1.4f)),
                120,
                default,
                Main.rand.NextFloat(1f, 1.5f)
            );
            dust.noGravity = false;

            if (Main.rand.NextBool(6)) {
                PRTLoader.NewParticle<PRT_FishOtronShard>(
                    Projectile.Center + Main.rand.NextVector2Circular(28f, 28f)
                    , new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(0.5f, 1.5f))
                    , default, Main.rand.NextFloat(0.4f, 0.65f))
                    ?.Configure(Main.rand.Next(18, 28));
            }

            //腕部幽绿魔力细丝（极克制：小、暗、稀，绿火主场让给冥焰技能）
            if (Main.rand.NextBool(8) && armSegments.Count > 1) {
                PRTLoader.NewParticle<PRT_Spark>(armSegments[1]
                    , -Vector2.UnitY * Main.rand.NextFloat(0.8f, 1.6f)
                    , new Color(96, 178, 110) * 0.5f, 0.3f)
                    ?.Configure(false, Main.rand.Next(12, 18));
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
            //坠落中甩脱的骨屑：反向剥离后受重力续落
            for (int i = 0; i < 2; i++) {
                Dust dust = Dust.NewDustDirect(
                    Projectile.position,
                    Projectile.width,
                    Projectile.height,
                    DustID.Bone,
                    0, 0, 100,
                    default,
                    Main.rand.NextFloat(1.5f, 2.2f)
                );
                dust.velocity = -Projectile.velocity * 0.15f + Main.rand.NextVector2Circular(2f, 2f);
                dust.noGravity = false;
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

        private void SpawnThrowTrailEffect() {
            //投掷动作的高速轨迹特效
            for (int i = 0; i < 2; i++) {
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(20f, 20f),
                    DustID.Bone,
                    -Projectile.velocity * 0.3f + Main.rand.NextVector2Circular(3f, 3f),
                    100,
                    default,
                    Main.rand.NextFloat(1.5f, 2.2f)
                );
                dust.noGravity = true;
                dust.fadeIn = 1.3f;
            }

            //烟雾尾迹
            if (Main.rand.NextBool(2)) {
                Dust smoke = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Smoke,
                    -Projectile.velocity * 0.2f,
                    100,
                    new Color(160, 160, 160),
                    Main.rand.NextFloat(1.8f, 2.5f)
                );
                smoke.noGravity = true;
            }
        }

        private void CreateImpactEffect(Vector2 position) {
            impactShake = 9f;

            //冲击骨尘沿挥击切向喷出：全向圆环换成有方向的力
            Vector2 tangent = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 14; i++) {
                Dust dust = Dust.NewDustPerfect(
                    position,
                    DustID.Bone,
                    tangent.RotatedByRandom(0.55f) * Main.rand.NextFloat(4f, 11f),
                    110,
                    default,
                    Main.rand.NextFloat(1.2f, 2f)
                );
                dust.noGravity = false;
            }

            //切向暗尘
            for (int i = 0; i < 8; i++) {
                Dust smoke = Dust.NewDustPerfect(
                    position + Main.rand.NextVector2Circular(10f, 10f),
                    DustID.Smoke,
                    tangent.RotatedByRandom(0.8f) * Main.rand.NextFloat(2f, 6f),
                    130,
                    new Color(110, 103, 90),
                    Main.rand.NextFloat(1.4f, 2.2f)
                );
                smoke.noGravity = true;
            }

            //骨屑抛物余迹：活得比这一击久
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_FishOtronShard>(position
                    , tangent.RotatedByRandom(0.5f) * Main.rand.NextFloat(3f, 7f) - Vector2.UnitY * 2f
                    , default, Main.rand.NextFloat(0.5f, 0.85f))
                    ?.Configure(Main.rand.Next(22, 34));
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
            impactShake = 13f;

            //克制震屏：重击落点专用，幅度收着（镜像 OniFinaleCut 的 Punch 用法）
            if (!VaultUtils.isServer && CWRServerConfig.Instance.ScreenVibration) {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(position
                    , Vector2.UnitY, 4.5f, 7f, 9, 800f, FullName));
            }

            //尘墙：贴地向两侧奔涌的横向烟尘
            for (int i = 0; i < 12; i++) {
                int dir = i % 2 == 0 ? 1 : -1;
                Dust wall = Dust.NewDustPerfect(
                    position + new Vector2(dir * Main.rand.NextFloat(6f, 26f), Main.rand.NextFloat(-4f, 6f)),
                    DustID.Smoke,
                    new Vector2(dir * Main.rand.NextFloat(4f, 11f), Main.rand.NextFloat(-1.6f, -0.2f)),
                    120,
                    new Color(126, 118, 102),
                    Main.rand.NextFloat(1.6f, 2.6f)
                );
                wall.noGravity = true;
            }

            //上锥骨钙尘
            for (int i = 0; i < 10; i++) {
                Dust dust = Dust.NewDustPerfect(
                    position,
                    DustID.Bone,
                    (-Vector2.UnitY).RotatedByRandom(0.9f) * Main.rand.NextFloat(3f, 10f),
                    110,
                    default,
                    Main.rand.NextFloat(1.3f, 2.2f)
                );
                dust.noGravity = false;
            }

            //地面碎石
            for (int i = 0; i < 8; i++) {
                Vector2 velocity = new Vector2(Main.rand.NextFloat(-9f, 9f), Main.rand.NextFloat(-14f, -5f));
                Dust debris = Dust.NewDustPerfect(
                    position,
                    DustID.Stone,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(1.4f, 2.2f)
                );
                debris.noGravity = false;
            }

            //碎石抛物：受重力翻滚的骨屑与大块骨骸（英雄时刻由 PRT 承担）
            for (int i = 0; i < 7; i++) {
                PRTLoader.NewParticle<PRT_FishOtronShard>(
                    position + Main.rand.NextVector2Circular(16f, 6f)
                    , new Vector2(Main.rand.NextFloat(-7f, 7f), Main.rand.NextFloat(-13f, -5f))
                    , default, Main.rand.NextFloat(0.6f, 1.05f))
                    ?.Configure(Main.rand.Next(26, 42));
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_FishOtronShard>(position
                    , new Vector2(Main.rand.NextFloat(-4.5f, 4.5f), Main.rand.NextFloat(-11f, -7f))
                    , default, Main.rand.NextFloat(0.85f, 1.15f))
                    ?.Configure(Main.rand.Next(30, 46), bigChunk: true);
            }

            //低伏尘环：暗色扁环压底，不做亮盘
            PRT_DWave wave = PRTLoader.NewParticle<PRT_DWave>(position, Vector2.Zero
                , new Color(150, 138, 114) * 0.42f, 0.4f);
            wave?.Configure(new Vector2(1f, 0.32f), 0f, 1.7f, 16);

            //烟尘残留：活得比砸击动作久的余韵
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(
                    position + new Vector2(Main.rand.NextFloat(-30f, 30f), Main.rand.NextFloat(-8f, 2f))
                    , new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), Main.rand.NextFloat(-0.8f, -0.2f))
                    , new Color(88, 82, 70), Main.rand.NextFloat(0.9f, 1.4f))
                    ?.Configure(Main.rand.Next(34, 48), 0.5f, Main.rand.NextFloat(0.01f, 0.03f));
            }

            SoundEngine.PlaySound(SoundID.Item14 with {
                Volume = 1f,
                Pitch = -0.5f
            }, position);

            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                Volume = 0.9f
            }, position);


            if (Projectile.IsOwnedByLocalPlayer()) {
                Projectile.NewProjectile(Projectile.FromObjectGetParent(), Projectile.Center, Vector2.Zero
                    , ModContent.ProjectileType<SkeletronHandExplode>(), Projectile.damage, Projectile.knockBack, Projectile.owner);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //命中骨尘沿打击方向溅出
            Vector2 hitDir = Projectile.velocity.SafeNormalize(Vector2.UnitY);
            for (int i = 0; i < 10; i++) {
                Dust dust = Dust.NewDustPerfect(
                    target.Center,
                    DustID.Bone,
                    hitDir.RotatedByRandom(0.7f) * Main.rand.NextFloat(3f, 9f),
                    110,
                    default,
                    Main.rand.NextFloat(1.2f, 2f)
                );
                dust.noGravity = false;
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_FishOtronShard>(target.Center
                    , hitDir.RotatedByRandom(0.6f) * Main.rand.NextFloat(3f, 6f) - Vector2.UnitY * 2f
                    , default, Main.rand.NextFloat(0.45f, 0.7f))
                    ?.Configure(Main.rand.Next(20, 30));
            }

            SoundEngine.PlaySound(SoundID.NPCHit2 with {
                Volume = 0.6f,
                Pitch = 0.2f
            }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            //英雄时刻：巨骨崩碎成大块骨骸＋细骨屑＋钙尘云
            for (int i = 0; i < 7; i++) {
                PRTLoader.NewParticle<PRT_FishOtronShard>(
                    Projectile.Center + Main.rand.NextVector2Circular(20f, 20f)
                    , (-Vector2.UnitY).RotatedByRandom(1.1f) * Main.rand.NextFloat(4f, 9f)
                    , default, Main.rand.NextFloat(0.9f, 1.3f))
                    ?.Configure(Main.rand.Next(34, 50), bigChunk: true);
            }
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_FishOtronShard>(Projectile.Center
                    , Main.rand.NextVector2Circular(6f, 5f) - Vector2.UnitY * 3f
                    , default, Main.rand.NextFloat(0.5f, 0.8f))
                    ?.Configure(Main.rand.Next(24, 38));
            }
            for (int i = 0; i < 12; i++) {
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(24f, 24f),
                    DustID.Bone,
                    Main.rand.NextVector2Circular(4f, 4f) - Vector2.UnitY * 1.5f,
                    120,
                    default,
                    Main.rand.NextFloat(1.1f, 1.8f)
                );
                dust.noGravity = false;
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(
                    Projectile.Center + Main.rand.NextVector2Circular(16f, 16f)
                    , Main.rand.NextVector2Circular(1f, 1f)
                    , new Color(96, 90, 78), Main.rand.NextFloat(0.8f, 1.2f))
                    ?.Configure(Main.rand.Next(30, 44), 0.45f, Main.rand.NextFloat(0.01f, 0.03f));
            }
            //束缚骨骸的诅咒魔力逸散（极克制幽绿）
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_Spark>(
                    Projectile.Center + Main.rand.NextVector2Circular(14f, 14f)
                    , -Vector2.UnitY * Main.rand.NextFloat(1.2f, 2.4f)
                    , new Color(96, 178, 110) * 0.55f, 0.34f)
                    ?.Configure(false, Main.rand.Next(16, 24));
            }

            //碎骨双层：低沉断裂＋高频崩裂
            SoundEngine.PlaySound(SoundID.NPCDeath2 with {
                Volume = 0.5f,
                Pitch = -0.3f
            }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.NPCHit2 with {
                Volume = 0.65f,
                Pitch = -0.55f
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

            //绘制投掷动作残影
            if (throwActionActive && State == HandState.Throwing) {
                DrawThrowActionTrail(sb, handTexture, origin);
            }

            //冲击震动＋蓄力微颤（末端渐强，仅视觉层抖动，判定不动）
            Vector2 shakeOffset = Main.rand.NextVector2Circular(impactShake, impactShake);
            if (State == HandState.WindingUp) {
                float wt = MathHelper.Clamp(StateTimer / WindUpDuration, 0f, 1f);
                shakeOffset += Main.rand.NextVector2Circular(1f, 1f) * (wt * wt * 2.8f);
            }
            Vector2 drawPos = Projectile.Center - Main.screenPosition + shakeOffset;
            float bodyRot = Projectile.rotation + MathHelper.Pi;

            //下砸速度拉伸残影：纵向压窄拉长，编码坠落各向异性
            if (State == HandState.Slamming && Projectile.velocity.Y > 6f) {
                for (int i = 3; i >= 1; i--) {
                    float fade = 1f - i / 4f;
                    sb.Draw(
                        handTexture,
                        drawPos - Projectile.velocity * (i * 0.55f),
                        null,
                        lightColor * (0.24f * fade),
                        bodyRot,
                        origin,
                        Projectile.scale * handScale * new Vector2(0.92f, 1.15f),
                        SpriteEffects.None,
                        0
                    );
                }
            }

            //暗影底衬：夹在骨臂链之上、手掌之下的质量剪影
            sb.Draw(
                handTexture,
                drawPos + new Vector2(0f, 5f),
                null,
                new Color(26, 22, 18, 190) * 0.5f,
                bodyRot,
                origin,
                Projectile.scale * handScale * 1.07f,
                SpriteEffects.None,
                0
            );

            //攻击期单层骨白压边（同贴图加色堆叠禁令：只此一层、低透明度）
            if (glowIntensity > 0.85f) {
                sb.Draw(
                    handTexture,
                    drawPos,
                    null,
                    new Color(226, 216, 188, 0) * ((glowIntensity - 0.85f) * 0.85f),
                    bodyRot,
                    origin,
                    Projectile.scale * handScale * 1.04f,
                    SpriteEffects.None,
                    0
                );
            }

            //下砸启动过冲白闪（≤2 帧爆点，非常驻）
            if (State == HandState.Slamming && StateTimer <= 1f) {
                sb.Draw(
                    handTexture,
                    drawPos,
                    null,
                    new Color(255, 248, 235, 0) * 0.5f,
                    bodyRot,
                    origin,
                    Projectile.scale * handScale * 1.05f,
                    SpriteEffects.None,
                    0
                );
            }

            //主体绘制
            sb.Draw(
                handTexture,
                drawPos,
                null,
                lightColor,
                bodyRot,
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

                //近肩端偏暗：骨臂链的纵深明暗
                float depthShade = MathHelper.Lerp(0.92f, 0.7f, (i + 1) / (float)armSegments.Count);

                for (int j = 0; j < boneCount; j++) {
                    float progress = j / (float)boneCount;
                    Vector2 bonePos = Vector2.Lerp(start, end, progress);

                    //根据位置添加轻微的骨头大小变化
                    float boneScale = Projectile.scale * MathHelper.Lerp(0.6f, 0.8f, (float)Math.Sin(progress * MathHelper.Pi));

                    sb.Draw(
                        boneTexture,
                        bonePos - Main.screenPosition,
                        null,
                        lightColor * depthShade,
                        rotation + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f + i + j) * 0.1f,
                        boneTexture.Size() / 2f,
                        boneScale * 2f * handScale,
                        SpriteEffects.None,
                        0
                    );
                }
            }
        }

        private void DrawAttackTrail(SpriteBatch sb, Texture2D texture, Vector2 origin) {
            //历史位置＋历史旋转角的哑光旋转拖影：弧线挥动读作扫掠而非贴图平移
            for (int i = 2; i < trailPoints.Count; i += 2) {
                float fade = 1f - i / (float)trailPoints.Count;
                Color trailColor = new Color(196, 188, 170) * (fade * 0.34f);

                sb.Draw(
                    texture,
                    trailPoints[i].pos - Main.screenPosition,
                    null,
                    trailColor,
                    trailPoints[i].rot + MathHelper.Pi,
                    origin,
                    Projectile.scale * handScale * (0.75f + fade * 0.25f),
                    SpriteEffects.None,
                    0
                );
            }
        }

        private void DrawThrowActionTrail(SpriteBatch sb, Texture2D texture, Vector2 origin) {
            //投掷主段（30%-75%）的旋转拖影链，哑光衰减、无白闪层
            float throwProgress = StateTimer / ThrowDuration;
            if (throwProgress < 0.3f || throwProgress > 0.75f) {
                return;
            }

            int count = Math.Min(trailPoints.Count, 10);
            for (int i = 1; i < count; i++) {
                float fade = 1f - i / (float)count;
                sb.Draw(
                    texture,
                    trailPoints[i].pos - Main.screenPosition,
                    null,
                    new Color(200, 192, 174) * (fade * 0.3f),
                    trailPoints[i].rot + MathHelper.Pi,
                    origin,
                    Projectile.scale * handScale * (0.8f + fade * 0.2f),
                    SpriteEffects.None,
                    0
                );
            }
        }
    }
    #endregion
}
