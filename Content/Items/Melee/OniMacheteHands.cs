using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 鬼手：六只被硫火锁在刀上的不安分之物（三个蠢货，一对一双）。<br/>
    /// 八态基础状态机 + FABRIK IK 手臂骨架保留；本轮重做：<br/>
    /// 1. 攻击令牌：同一时刻至多 2 只手处于攻击段，其余保持编队（替代纯随机错开）<br/>
    /// 2. 动作编排：蓄力 pow(t,8) 迟滞后吸、砸地后余震弹跳、投掷后座<br/>
    /// 3. 躁动态：硫火压制耗尽时攻击更凶，并周期性回头掐向持刀者（扼颈）<br/>
    /// 4. 手臂画法：OniArm 鬼影青灰底 + 附着式硫火火鞘 shader 条带
    /// （压制走低火鞘断续变薄、躁动时大面积熄灭并迸暗红危焰）<br/>
    /// 网络：状态/计时走 ai[]，攻击锚点与目标走 NetHeldSend，躁动位走 BitsByte[2]；
    /// 一切随机决策仅 owner 端做出并 netUpdate 广播
    /// </summary>
    internal class OniHandMinion : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item_Melee + "OniHand";

        public const int HandCount = 6;
        /// <summary>鬼手单击伤害系数（相对武器面板）</summary>
        public const float HandDamageFactor = 0.5f;
        /// <summary>同时处于攻击段的手数上限</summary>
        public const int AttackTokens = 2;
        /// <summary>单次出击消耗的硫火压制</summary>
        public const float AttackCost = 6f;

        private enum HandState
        {
            Idle = 0,        //编队漂浮
            Targeting,       //锁定接近
            WindupSwing,     //蓄力：挥击
            WindupSlam,      //蓄力：下砸
            WindupSweep,     //蓄力：横扫
            WindupThrow,     //蓄力：投掷
            Swinging,        //挥击
            Slamming,        //下砸
            Sweeping,        //横扫
            Throwing,        //投掷鬼火
            Recovering,      //收势（含砸地余震弹跳）
            GripApproach,    //躁动：扑向持刀者
            Gripping,        //躁动：扼颈
            GripReturn       //躁动：收手归队
        }

        private ref float HandIndex => ref Projectile.ai[0];
        private ref float StateRaw => ref Projectile.ai[1];
        private ref float StateTimer => ref Projectile.ai[2];

        private HandState State {
            get => (HandState)StateRaw;
            set => StateRaw = (float)value;
        }

        private bool InStrike => State is HandState.Swinging or HandState.Slamming or HandState.Sweeping;

        //==== 同步数据（NetHeldSend / BitsByte）====
        private int targetNPCID = -1;
        private Vector2 attackStartPos;
        private Vector2 attackTargetPos;
        private bool restlessSynced;
        private bool prevRestless;

        //==== IK 手臂 ====
        private readonly List<Vector2> armSegments = new();
        private const int ArmSegmentCount = 6;
        private const float SegmentLength = 45f;
        private Vector2 shoulderPos;
        private Vector2 handPos;
        private float armTension;
        private int ownerDirection = 1;

        //==== 攻击参数 ====
        private const float SearchRange = 1500f;
        private const int IdleDuration = 36;
        private const int WindupDuration = 26;
        private const int SwingDuration = 14;
        private const int SlamDuration = 20;
        private const int SweepDuration = 26;
        private const int ThrowDuration = 46;
        private const int RecoverDuration = 34;
        private const int GripApproachDuration = 42;
        private const int GripDuration = 55;
        private const int GripReturnDuration = 24;
        /// <summary>蓄力收束粒子硬切点（爆发前的静默）</summary>
        private const float ChargeSilenceAt = 0.72f;

        //==== 视觉状态（本地）====
        private float glowIntensity;
        private float handScale = 1f;
        private float restlessBlend;      //躁动视觉的平滑混合 0..1
        private Vector2 throwRecoil;      //投掷后座位移
        private Vector2 slamLandPos;
        private bool slamAftermath;       //本次收势带余震弹跳
        private float armSparkTimer;
        private readonly List<Vector2> trailPositions = new();
        private const int MaxTrailLength = 12;

        //==== 个性（确定性种子，各端一致）====
        private float personalityTimeOffset;
        private float personalitySpeedMultiplier = 1f;
        private readonly float[] attackPreference = new float[4];
        private int personalityIdleDelay;
        private bool personalityInitialized;
        private float personalityRangePreference = 1f;

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 1;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = false;
        }

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 24;
            Projectile.netImportant = true;

            for (int i = 0; i < ArmSegmentCount; i++) {
                armSegments.Add(Vector2.Zero);
            }
        }

        /// <summary>全部状态自管位移（MoveToPosition / 直接设 Center），关掉原版速度积分防双重步进</summary>
        public override bool ShouldUpdatePosition() => false;

        //==== 网络：锚点/目标/躁动位 ====

        public override void NetHeldSend(BinaryWriter writer) {
            writer.Write((short)targetNPCID);
            writer.WriteVector2(attackStartPos);
            writer.WriteVector2(attackTargetPos);
        }

        public override void NetHeldReceive(BinaryReader reader) {
            targetNPCID = reader.ReadInt16();
            attackStartPos = reader.ReadVector2();
            attackTargetPos = reader.ReadVector2();
        }

        public override BitsByte SendBitsByte(BitsByte flags) {
            flags = base.SendBitsByte(flags);
            flags[2] = restlessSynced;
            return flags;
        }

        public override void ReceiveBitsByte(BitsByte flags) {
            base.ReceiveBitsByte(flags);
            restlessSynced = flags[2];
        }

        /// <summary>躁动态：owner 端直读 ModPlayer，远端读同步位</summary>
        private bool IsRestless => Owner.whoAmI == Main.myPlayer
            ? Owner.GetModPlayer<OniMachetePlayer>().Restless
            : restlessSynced;

        //==== 个性 ====

        private void InitializePersonality() {
            if (personalityInitialized) {
                return;
            }
            personalityInitialized = true;

            int seed = (int)(HandIndex * 1000) + Projectile.owner * 10000;
            Random personalRand = new(seed);

            personalityTimeOffset = (float)personalRand.NextDouble();
            personalityIdleDelay = personalRand.Next(0, 50);
            personalitySpeedMultiplier = 0.88f + (float)personalRand.NextDouble() * 0.24f;

            float total = 0f;
            for (int i = 0; i < 4; i++) {
                attackPreference[i] = 0.5f + (float)personalRand.NextDouble() * 0.5f;
                total += attackPreference[i];
            }
            for (int i = 0; i < 4; i++) {
                attackPreference[i] /= total;
            }
            personalityRangePreference = 0.8f + (float)personalRand.NextDouble() * 0.4f;
        }

        private int Dur(int baseDuration) {
            float mul = personalitySpeedMultiplier * (IsRestless ? 0.72f : 1f);
            return Math.Max(6, (int)(baseDuration * mul));
        }

        /// <summary>不吃躁动加速的时长（扼颈演出需要完整播放）</summary>
        private int DurRaw(int baseDuration)
            => Math.Max(6, (int)(baseDuration * personalitySpeedMultiplier));

        private float PersonalTime() => Main.GlobalTimeWrappedHourly + personalityTimeOffset * MathHelper.TwoPi;

        //==== 主循环 ====

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }
            if (Owner.GetItem().type != ModContent.ItemType<OniMachete>()) {
                Projectile.Kill();
                return;
            }

            InitializePersonality();
            Projectile.timeLeft = 120;

            //面板伤害逐帧对齐武器管线（owner 端权威）：Boss 阶段增伤等后续加成即时生效
            if (Projectile.IsOwnedByLocalPlayer()) {
                Projectile.damage = (int)(Owner.GetWeaponDamage(Owner.GetItem(), true) * HandDamageFactor);
            }

            //出生帧演出（各端本地跑）
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                SpawnBirthBurst();
            }

            StateTimer++;
            UpdateShoulderPosition();

            switch (State) {
                case HandState.Idle: IdleBehavior(); break;
                case HandState.Targeting: TargetingBehavior(); break;
                case HandState.WindupSwing:
                case HandState.WindupSlam:
                case HandState.WindupSweep:
                case HandState.WindupThrow: WindupBehavior(); break;
                case HandState.Swinging: SwingingBehavior(); break;
                case HandState.Slamming: SlammingBehavior(); break;
                case HandState.Sweeping: SweepingBehavior(); break;
                case HandState.Throwing: ThrowingBehavior(); break;
                case HandState.Recovering: RecoveringBehavior(); break;
                case HandState.GripApproach: GripApproachBehavior(); break;
                case HandState.Gripping: GrippingBehavior(); break;
                case HandState.GripReturn: GripReturnBehavior(); break;
            }

            //躁动扼颈调度：owner 端在编队里挑一只手扑向主人
            TryScheduleGrip();

            UpdateArmIK();
            UpdateTrail();
            UpdateRotation();

            //躁动视觉平滑混合：压制走低时火鞘先变薄断续（owner 端可读自己的精确压制值），
            //彻底躁动后大面积熄灭，臂上火势即压制量的在场仪表
            float rageTarget = IsRestless ? 1f : 0f;
            if (!IsRestless && Owner.whoAmI == Main.myPlayer) {
                float supp = Owner.GetModPlayer<OniMachetePlayer>().Suppression;
                rageTarget = MathHelper.Clamp((OniMachetePlayer.LowLine - supp) / OniMachetePlayer.LowLine, 0f, 1f) * 0.40f;
            }
            restlessBlend = MathHelper.Lerp(restlessBlend, rageTarget, 0.06f);

            //贴骨火星（少量点缀，火鞘条带才是主体）：忠仆偶发橙红火星上飘；
            //躁动改为熄火处迸出的暗红余烬 + 黑烟
            if (!VaultUtils.isServer && ++armSparkTimer > 9f) {
                armSparkTimer = 0f;
                Vector2 sparkPos = Vector2.Lerp(shoulderPos, Projectile.Center, Main.rand.NextFloat(0.2f, 0.92f));
                if (restlessBlend <= 0.4f) {
                    if (Main.rand.NextBool(3)) {
                        Dust fleck = Dust.NewDustPerfect(sparkPos, DustID.Torch
                            , new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.6f, 1.6f))
                            , 0, Color.Red, Main.rand.NextFloat(0.9f, 1.4f));
                        fleck.noGravity = true;
                    }
                }
                else if (Main.rand.NextBool(2)) {
                    PRTLoader.NewParticle<PRT_OniMacheteGold>(sparkPos
                        , Main.rand.NextVector2Circular(1.8f, 1.8f) - Vector2.UnitY * 1.2f
                        , default, Main.rand.NextFloat(0.2f, 0.4f))
                        ?.Configure(Main.rand.Next(10, 16), gravity: true, cooling: 1.8f);
                    Dust puff = Dust.NewDustPerfect(sparkPos, DustID.Smoke
                        , -Vector2.UnitY * Main.rand.NextFloat(0.4f, 1f), 150, default
                        , Main.rand.NextFloat(0.8f, 1.3f));
                    puff.noGravity = true;
                }
            }

            //躁动位同步：owner 端翻转时广播
            if (Projectile.IsOwnedByLocalPlayer()) {
                bool restless = IsRestless;
                restlessSynced = restless;
                if (restless != prevRestless) {
                    prevRestless = restless;
                    Projectile.netUpdate = true;
                }
            }

            //硫火照明：忠仆金橙 / 躁动血红
            float pulse = (float)Math.Sin(PersonalTime() * 6f) * 0.3f + 0.7f;
            Vector3 lightCol = Vector3.Lerp(new Vector3(0.75f, 0.45f, 0.12f)
                , new Vector3(0.95f, 0.16f, 0.06f), restlessBlend);
            Lighting.AddLight(Projectile.Center, lightCol * pulse);

            handScale = MathHelper.Lerp(handScale, 1f, 0.1f);
            throwRecoil *= 0.85f;

            //环境余烬（低频，Dust 只做碎屑）
            if (!VaultUtils.isServer && Main.rand.NextBool(14)) {
                PRTLoader.NewParticle<PRT_LavaFire>(Projectile.Center + VaultUtils.RandVr(24f)
                    , -Vector2.UnitY * Main.rand.NextFloat(0.4f, 1.2f)
                    , Color.White, Main.rand.NextFloat(0.4f, 0.7f))?.SetLifetime(10, 22);
            }
        }

        private void SpawnBirthBurst() {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.55f, Pitch = -0.4f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.DD2_BetsyFireballShot with { Volume = 0.4f, Pitch = -0.5f }, Projectile.Center);
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_OniMacheteGold>(Projectile.Center
                    , Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 8f)
                    , default, Main.rand.NextFloat(0.3f, 0.6f))?.Configure(Main.rand.Next(14, 22));
            }
            var flame = PRTLoader.NewParticle<PRT_HellFlame>(Projectile.Center, -Vector2.UnitY * 2f
                , Color.White, 0.7f);
            if (flame != null) {
                flame.ai[0] = 1;
            }
        }

        private void UpdateShoulderPosition() {
            if (State == HandState.Idle) {
                ownerDirection = Owner.direction;
            }
            shoulderPos = Owner.GetPlayerStabilityCenter() + new Vector2(8f * ownerDirection, -4f);
        }

        //==== 编队 / 索敌 ====

        /// <summary>六手满圆编队位（顶部偏置），成对的手相邻（三个蠢货，各出一双）</summary>
        private Vector2 FormationPos() {
            int pair = (int)HandIndex / 2;
            float ang = HandIndex * MathHelper.TwoPi / HandCount - MathHelper.PiOver2
                + PersonalTime() * 0.30f;
            Vector2 offset = ang.ToRotationVector2() * (118f + pair * 12f);
            offset.X *= ownerDirection;
            //躁动时编队涣散：漂移振幅加大
            float bobMul = 1f + restlessBlend * 1.6f;
            Vector2 bob = new((float)Math.Sin(PersonalTime() * 2f) * 26f * bobMul
                , (float)Math.Cos(PersonalTime() * 1.5f) * 16f * bobMul);
            return shoulderPos + offset + bob + new Vector2(0f, -66f);
        }

        private void IdleBehavior() {
            glowIntensity = 0.4f + restlessBlend * 0.3f;
            armTension = 0.3f;
            slamAftermath = false;
            MoveToPosition(FormationPos(), 0.14f * personalitySpeedMultiplier);

            int wait = Dur(IdleDuration) + personalityIdleDelay;
            if (StateTimer > wait && Projectile.IsOwnedByLocalPlayer()) {
                NPC target = Owner.Center.FindClosestNPC(SearchRange);
                if (target != null) {
                    targetNPCID = target.whoAmI;
                    Transition(HandState.Targeting);
                }
            }
        }

        /// <summary>攻击段令牌计数（无存储、逐帧重扫，天然免同步）</summary>
        private static int CountAttackingHands(int owner) {
            int count = 0;
            int type = ModContent.ProjectileType<OniHandMinion>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner == owner && proj.type == type
                    && proj.ai[1] >= (float)HandState.WindupSwing && proj.ai[1] <= (float)HandState.Throwing) {
                    count++;
                }
            }
            return count;
        }

        private void TargetingBehavior() {
            if (!IsTargetValid()) {
                if (Projectile.IsOwnedByLocalPlayer()) {
                    personalityIdleDelay = Main.rand.Next(0, 40);
                    Transition(HandState.Idle);
                }
                return;
            }

            NPC target = Main.npc[targetNPCID];
            float distance = Vector2.Distance(Projectile.Center, target.Center);
            glowIntensity = 0.6f;
            armTension = 0.6f;

            //接近位：目标上方悬停
            Vector2 approach = target.Center + new Vector2(0f, -170f);
            MoveToPosition(approach, 0.2f * personalitySpeedMultiplier);

            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }

            //攻击令牌：编队里最多两只手同时出击，其余悬停施压
            bool tokenFree = CountAttackingHands(Projectile.owner) < AttackTokens;
            if (!tokenFree) {
                if (StateTimer > 90f) {
                    Transition(HandState.Idle);   //排队过久回编队，防全员堵在目标头顶
                }
                return;
            }

            //臂长约束：IK 手臂全展 ~270px，超出者只能投掷（近战招式必然够得着才选）
            float armReach = Vector2.Distance(Owner.Center, target.Center);
            if (armReach > 300f || distance > 380f * personalityRangePreference) {
                BeginAttack(HandState.WindupThrow, target);
            }
            else if (Vector2.Distance(Projectile.Center, approach) < 120f
                || StateTimer > (int)(34 * personalitySpeedMultiplier)) {
                BeginAttack(ChooseMeleeWindup(target), target);
            }
        }

        private HandState ChooseMeleeWindup(NPC target) {
            Vector2 toTarget = target.Center - Projectile.Center;
            float swingScore = attackPreference[0] * (0.8f + Main.rand.NextFloat(0.4f));
            float slamScore = attackPreference[1]
                * (Math.Abs(toTarget.Y) > Math.Abs(toTarget.X) * 1.1f && toTarget.Y > -40f ? 2.0f : 0.5f)
                * (0.8f + Main.rand.NextFloat(0.4f));
            float sweepScore = attackPreference[2] * (0.8f + Main.rand.NextFloat(0.4f));

            if (slamScore > swingScore && slamScore > sweepScore) {
                return HandState.WindupSlam;
            }
            return sweepScore > swingScore ? HandState.WindupSweep : HandState.WindupSwing;
        }

        /// <summary>owner 端出击起手：扣压制、冻结锚点、广播</summary>
        private void BeginAttack(HandState windup, NPC target) {
            Owner.GetModPlayer<OniMachetePlayer>().ConsumeSuppression(AttackCost);
            attackStartPos = Projectile.Center;
            attackTargetPos = target.Center;
            Transition(windup);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.45f, Pitch = -0.35f }, Projectile.Center);
            }
        }

        private void Transition(HandState next) {
            State = next;
            StateTimer = 0;
            Projectile.netUpdate = true;
        }

        //==== 蓄力（四式共用：pow(t,8) 迟滞后吸）====

        private Vector2 WindupOffset() => State switch {
            HandState.WindupSwing => new Vector2(-190f * ownerDirection, -90f),
            HandState.WindupSlam => new Vector2(0f, -240f),
            HandState.WindupSweep => new Vector2(-210f * ownerDirection, 10f),
            _ => new Vector2(-170f * ownerDirection, -110f),
        };

        private void WindupBehavior() {
            if (!IsTargetValid() && Projectile.IsOwnedByLocalPlayer()) {
                Transition(HandState.Idle);
                return;
            }

            int duration = Dur(WindupDuration);
            float t = MathHelper.Clamp(StateTimer / duration, 0f, 1f);
            glowIntensity = 0.6f + t * 0.4f;
            armTension = 0.9f;

            //迟滞后吸：前段缓浮，末几帧猛地抽回（sharp inhale）
            float snap = 0.30f * VaultUtils.EaseOutCubic(t) + 0.70f * MathF.Pow(t, 8f);
            Vector2 windPos = attackStartPos + WindupOffset() * snap;
            //末端蓄满的细颤
            if (t > 0.75f) {
                windPos += Main.rand.NextVector2Circular(2.5f, 2.5f) * (t - 0.75f) * 4f;
            }
            MoveToPosition(windPos, 0.42f * personalitySpeedMultiplier);

            handScale = 1f + t * 0.3f;

            //收束金屑（72% 硬切静默）
            if (!VaultUtils.isServer && t < ChargeSilenceAt && Main.rand.NextBool(2)) {
                Vector2 spawn = Projectile.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(40f, 110f);
                PRTLoader.NewParticle<PRT_OniMacheteGold>(spawn
                    , (Projectile.Center - spawn) * 0.09f, default, Main.rand.NextFloat(0.25f, 0.5f))
                    ?.Configure(Main.rand.Next(10, 16), gravity: false, cooling: 1.5f);
            }

            if (StateTimer >= duration) {
                //蓄力打满：单帧切换 + 爆发音（各端 timer 对齐，确定性转换）
                HandState strike = State switch {
                    HandState.WindupSwing => HandState.Swinging,
                    HandState.WindupSlam => HandState.Slamming,
                    HandState.WindupSweep => HandState.Sweeping,
                    _ => HandState.Throwing,
                };
                if (strike == HandState.Slamming) {
                    //砸地锚点：从目标当前位置向下取地面（各端世界一致，免同步）
                    attackTargetPos = IsTargetValid() ? Main.npc[targetNPCID].Center : attackTargetPos;
                    slamLandPos = FindGroundBelow(attackTargetPos) ?? (attackTargetPos + new Vector2(0f, 60f));
                }
                if (strike == HandState.Throwing && IsTargetValid()) {
                    NPC target = Main.npc[targetNPCID];
                    attackStartPos = Projectile.Center;
                    attackTargetPos = target.Center + target.velocity * 18f * personalitySpeedMultiplier;
                }
                State = strike;
                StateTimer = 0;
                //owner 广播打击段锚点（砸地落点/投掷预判），远端下一包对齐
                if (Projectile.IsOwnedByLocalPlayer()) {
                    Projectile.netUpdate = true;
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.7f, Pitch = -0.45f }, Projectile.Center);
                }
            }
        }

        //==== 打击段 ====

        private void SwingingBehavior() {
            int duration = Dur(SwingDuration);
            float t = MathHelper.Clamp(StateTimer / duration, 0f, 1f);
            glowIntensity = 1f;
            armTension = 1f;

            //陡峭 ease-out：几乎所有角距离在头几帧完成
            float eased = 1f - MathF.Pow(1f - t, 9f);
            float startAngle = MathHelper.PiOver2 * 1.2f;
            float endAngle = -MathHelper.PiOver4 * 1.5f;
            if (ownerDirection == -1) {
                startAngle = MathHelper.Pi - startAngle;
                endAngle = MathHelper.Pi - endAngle;
            }
            float ang = MathHelper.Lerp(startAngle, endAngle, eased);
            Vector2 offset = new((float)Math.Cos(ang) * 230f, (float)Math.Sin(ang) * 170f);
            Vector2 next = attackTargetPos + offset;
            Projectile.velocity = next - Projectile.Center;
            Projectile.Center = next;

            handScale = 1f + (float)Math.Sin(t * MathHelper.Pi) * 0.35f;

            if (StateTimer >= duration) {
                slamAftermath = false;
                State = HandState.Recovering;
                StateTimer = 0;
                SmallImpactBurst(Projectile.Center, 0.6f);
            }
        }

        private void SlammingBehavior() {
            int duration = Dur(SlamDuration);
            float t = MathHelper.Clamp(StateTimer / duration, 0f, 1f);
            glowIntensity = 1f;
            armTension = 1f;

            Vector2 slamStart = new(attackTargetPos.X, Math.Min(attackTargetPos.Y, slamLandPos.Y) - 250f);
            float eased = MathF.Pow(t, 2.6f);
            Vector2 next = Vector2.Lerp(slamStart, slamLandPos, eased);
            Projectile.velocity = next - Projectile.Center;
            Projectile.Center = next;

            //下砸握拳收紧
            handScale = 1.35f - t * 0.30f;

            //坠落拉出火线
            if (!VaultUtils.isServer && t > 0.3f && Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_LavaFire>(Projectile.Center + VaultUtils.RandVr(14f)
                    , -Projectile.velocity * 0.1f, Color.White, Main.rand.NextFloat(0.5f, 0.9f))
                    ?.SetLifetime(8, 16);
            }

            if (StateTimer >= duration) {
                slamAftermath = true;
                //IK 臂长可能把拳头拦在半路：结算与余震都锚在拳头实际停住的位置，伤害与画面同址
                slamLandPos = Projectile.Center;
                State = HandState.Recovering;
                StateTimer = 0;
                //砸地结算：owner 生成 OniHandExplode（伤害盒 + 熔金裂缝 decal + 全套演出）
                if (Projectile.IsOwnedByLocalPlayer()) {
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), slamLandPos, Vector2.Zero
                        , ModContent.ProjectileType<OniHandExplode>(), (int)(Projectile.damage * 1.5f)
                        , Projectile.knockBack, Projectile.owner);
                }
            }
        }

        private void SweepingBehavior() {
            int duration = Dur(SweepDuration);
            float t = MathHelper.Clamp(StateTimer / duration, 0f, 1f);
            glowIntensity = 1f;
            armTension = 1f;

            float eased = 1f - MathF.Pow(1f - t, 6f);
            float startAngle = -MathHelper.Pi * 1.05f;
            float endAngle = MathHelper.Pi * 0.05f;
            if (ownerDirection == -1) {
                (startAngle, endAngle) = (MathHelper.Pi - startAngle, MathHelper.Pi - endAngle);
            }
            float ang = MathHelper.Lerp(startAngle, endAngle, eased);
            Vector2 offset = new((float)Math.Cos(ang) * 220f, (float)Math.Sin(ang) * 80f);
            Vector2 next = attackTargetPos + offset;
            Projectile.velocity = next - Projectile.Center;
            Projectile.Center = next;

            handScale = 1f + (float)Math.Sin(t * MathHelper.Pi) * 0.3f;

            if (StateTimer >= duration) {
                slamAftermath = false;
                State = HandState.Recovering;
                StateTimer = 0;
                SmallImpactBurst(Projectile.Center, 0.6f);
            }
        }

        private void ThrowingBehavior() {
            int duration = Dur(ThrowDuration);
            float t = MathHelper.Clamp(StateTimer / duration, 0f, 1f);
            glowIntensity = 1f;
            armTension = 0.8f;

            if (t < 0.3f) {
                MoveToPosition(attackStartPos, 0.2f * personalitySpeedMultiplier);
                handScale = 1.4f;
            }
            else if (t < 0.62f) {
                //前冲抛掷
                float throwT = VaultUtils.EaseOutCubic((t - 0.3f) / 0.32f);
                Vector2 reach = Vector2.Lerp(attackStartPos
                    , Vector2.Lerp(attackStartPos, attackTargetPos, 0.35f), throwT);
                Projectile.velocity = reach - Projectile.Center;
                Projectile.Center = reach + throwRecoil;
                handScale = 1.4f - throwT * 0.4f;

                if (StateTimer == (int)(duration * 0.46f)) {
                    ReleaseFireballs();
                    //投掷后座：手猛地向后一顿
                    throwRecoil = (attackStartPos - attackTargetPos).SafeNormalize(Vector2.Zero) * 26f;
                }
            }
            else {
                //收手：后座衰减，缓慢回稳
                Projectile.velocity *= 0.85f;
                Projectile.Center += throwRecoil * 0.12f;
                handScale = MathHelper.Lerp(handScale, 1f, 0.1f);
            }

            if (StateTimer >= duration) {
                slamAftermath = false;
                State = HandState.Recovering;
                StateTimer = 0;
            }
        }

        private void ReleaseFireballs() {
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            Vector2 dir = (attackTargetPos - Projectile.Center).SafeNormalize(Vector2.UnitX);
            int count = 4 + Main.rand.Next(3);
            for (int i = 0; i < count; i++) {
                float spread = MathHelper.Lerp(-0.32f, 0.32f, count <= 1 ? 0.5f : i / (float)(count - 1));
                Vector2 vel = dir.RotatedBy(spread) * Main.rand.NextFloat(10f, 12f);
                Projectile.NewProjectile(Projectile.GetSource_FromThis()
                    , Projectile.Center + Main.rand.NextVector2Circular(8f, 8f), vel
                    , ModContent.ProjectileType<OniFireBall>(), (int)(Projectile.damage * 0.15f)
                    , 2f, Projectile.owner);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.8f, Pitch = 0.25f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.DD2_BetsyFireballShot with { Volume = 0.7f, Pitch = 0.3f }, Projectile.Center);
                for (int i = 0; i < 12; i++) {
                    PRTLoader.NewParticle<PRT_OniMacheteGold>(Projectile.Center
                        , dir.RotatedByRandom(0.6) * Main.rand.NextFloat(5f, 12f)
                        , default, Main.rand.NextFloat(0.3f, 0.6f))?.Configure(Main.rand.Next(12, 20));
                }
            }
        }

        //==== 收势（含砸地余震）====

        private void RecoveringBehavior() {
            int duration = Dur(RecoverDuration);
            float t = MathHelper.Clamp(StateTimer / duration, 0f, 1f);
            glowIntensity = 1f - t * 0.6f;
            armTension = 0.5f;

            if (slamAftermath && t < 0.42f) {
                //余震弹跳：拳头钉在坑里颤 2~3 次再抬起（阻尼正弦）
                float bt = t / 0.42f;
                float bounce = MathF.Abs(MathF.Sin(bt * MathF.PI * 2.6f)) * 34f * (1f - bt);
                Vector2 next = slamLandPos - new Vector2(0f, bounce);
                Projectile.velocity = next - Projectile.Center;
                Projectile.Center = next;
                handScale = 1.05f - bt * 0.05f;
                return;
            }

            MoveToPosition(FormationPos(), 0.2f * personalitySpeedMultiplier);
            if (StateTimer >= duration) {
                if (Projectile.IsOwnedByLocalPlayer()) {
                    personalityIdleDelay = Main.rand.Next(0, 45);
                }
                State = HandState.Idle;
                StateTimer = 0;
            }
        }

        //==== 躁动：扼颈 ====

        /// <summary>owner 端调度：躁动 + 冷却结束时，编队/索敌中的手抢占扼颈名额
        /// （已进入攻击段的手先把招打完，挥出去的拳头收不回来）</summary>
        private void TryScheduleGrip() {
            if (!Projectile.IsOwnedByLocalPlayer() || !IsRestless) {
                return;
            }
            if (State is not (HandState.Idle or HandState.Targeting) || StateTimer < 20f) {
                return;
            }
            OniMachetePlayer mp = Owner.GetModPlayer<OniMachetePlayer>();
            if (mp.GripCooldown > 0) {
                return;
            }
            mp.GripCooldown = 420;   //抢占即锁冷却，同帧只有一只手能拿到
            Transition(HandState.GripApproach);
            if (!VaultUtils.isServer) {
                //预警声：鬼物转头的呜咽（风险可读的起点）
                SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.75f, Pitch = -0.7f }, Owner.Center);
                SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.5f, Pitch = -0.8f }, Projectile.Center);
            }
        }

        private Vector2 NeckPos() => Owner.MountedCenter + new Vector2(0f, -12f * Owner.gravDir);

        private void GripApproachBehavior() {
            glowIntensity = 1f;
            armTension = 1f;
            int duration = DurRaw(GripApproachDuration);
            float t = MathHelper.Clamp(StateTimer / duration, 0f, 1f);

            //可规避窗口：接近期内重新压服（挥刀命中回气）即中止
            if (Projectile.IsOwnedByLocalPlayer() && !IsRestless) {
                Transition(HandState.Recovering);
                return;
            }

            //先绕后直扑：前段拉开一点距离蓄势，末段 ease-in 猛扑
            Vector2 neck = NeckPos();
            float lunge = MathF.Pow(t, 2.2f);
            Vector2 hover = neck + new Vector2(60f * ownerDirection, -110f);
            Vector2 next = Vector2.Lerp(hover, neck, lunge);
            MoveToPosition(next, 0.5f);
            handScale = 1f + t * 0.25f;

            if (StateTimer >= duration) {
                State = HandState.Gripping;
                StateTimer = 0;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.DD2_SkeletonHurt with { Volume = 0.8f, Pitch = -0.75f }, Owner.Center);
                    SoundEngine.PlaySound(SoundID.Item32 with { Volume = 0.6f, Pitch = -0.5f }, Owner.Center);
                }
            }
        }

        private void GrippingBehavior() {
            glowIntensity = 1f;
            armTension = 1f;
            int duration = DurRaw(GripDuration);
            float t = MathHelper.Clamp(StateTimer / duration, 0f, 1f);

            //钉在脖子上（随玩家移动），微幅使劲颤动
            Vector2 neck = NeckPos() + Main.rand.NextVector2Circular(1.2f, 1.2f);
            Projectile.velocity = neck - Projectile.Center;
            Projectile.Center = neck;

            //两次离散攥紧（非节拍脉动）：掐一下、再掐一下
            float squeeze = 0f;
            float st1 = MathF.Abs(StateTimer - duration * 0.30f);
            float st2 = MathF.Abs(StateTimer - duration * 0.66f);
            if (st1 < 5f) {
                squeeze = 1f - st1 / 5f;
            }
            else if (st2 < 5f) {
                squeeze = 1f - st2 / 5f;
            }
            handScale = 1.15f - squeeze * 0.22f;

            if (Projectile.IsOwnedByLocalPlayer()) {
                OniMachetePlayer mp = Owner.GetModPlayer<OniMachetePlayer>();
                Owner.AddBuff(ModContent.BuffType<OniNeckGripDebuff>(), 8);
                //暗角包络：入掐渐紧，攥紧瞬间顶满
                mp.PushGripVignette(MathHelper.Clamp(t * 2.2f, 0f, 0.85f) + squeeze * 0.15f);

                //攥紧的顿挫震屏（小幅，尊重配置）
                if (squeeze > 0.9f && CWRServerConfig.Instance.ScreenVibration && Owner.whoAmI == Main.myPlayer) {
                    var modifier = new PunchCameraModifier(Owner.Center, Main.rand.NextVector2Unit()
                        , 3f, 5f, 8, 500f, FullName);
                    Main.instance.CameraModifiers.Add(modifier);
                }

                //挣脱条件：压制回升（挥刀命中）即提前松手
                if (mp.Suppression >= 10f && t > 0.25f) {
                    Transition(HandState.GripReturn);
                    return;
                }
            }

            if (!VaultUtils.isServer && squeeze > 0.9f && StateTimer % 3 == 0) {
                SoundEngine.PlaySound(SoundID.NPCHit54 with { Volume = 0.4f, Pitch = -0.5f }, Owner.Center);
            }

            if (StateTimer >= duration) {
                State = HandState.GripReturn;
                StateTimer = 0;
            }
        }

        private void GripReturnBehavior() {
            glowIntensity = 0.6f;
            armTension = 0.5f;
            MoveToPosition(FormationPos(), 0.22f);
            if (StateTimer >= DurRaw(GripReturnDuration)) {
                State = HandState.Idle;
                StateTimer = 0;
            }
        }

        //==== 运动/IK/工具 ====

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

            float targetDistance = Vector2.Distance(shoulderPos, handPos);
            float maxReach = SegmentLength * ArmSegmentCount;
            if (targetDistance > maxReach * 0.98f) {
                Vector2 direction = (handPos - shoulderPos).SafeNormalize(Vector2.Zero);
                handPos = shoulderPos + direction * maxReach * 0.98f;
                Projectile.Center = handPos;
            }

            //FABRIK：前向手→肩
            armSegments[0] = handPos;
            for (int i = 1; i < ArmSegmentCount; i++) {
                Vector2 direction = (armSegments[i - 1] - (i == ArmSegmentCount - 1 ? shoulderPos : armSegments[i])).SafeNormalize(Vector2.Zero);
                float bendFactor = (float)Math.Sin(i / (float)ArmSegmentCount * MathHelper.Pi) * armTension;
                Vector2 perpendicular = new Vector2(-direction.Y, direction.X) * bendFactor * 15f * ownerDirection;
                armSegments[i] = armSegments[i - 1] - direction * SegmentLength + perpendicular;
            }
            //反向肩→手
            armSegments[ArmSegmentCount - 1] = shoulderPos;
            for (int i = ArmSegmentCount - 2; i >= 0; i--) {
                Vector2 direction = (armSegments[i] - armSegments[i + 1]).SafeNormalize(Vector2.Zero);
                float bendFactor = (float)Math.Sin(i / (float)ArmSegmentCount * MathHelper.Pi) * armTension;
                Vector2 perpendicular = new Vector2(-direction.Y, direction.X) * bendFactor * 15f * ownerDirection;
                armSegments[i] = armSegments[i + 1] + direction * SegmentLength + perpendicular;
            }
            Projectile.Center = armSegments[0];
        }

        private void UpdateTrail() {
            trailPositions.Insert(0, Projectile.Center);
            if (trailPositions.Count > MaxTrailLength) {
                trailPositions.RemoveAt(trailPositions.Count - 1);
            }
        }

        private void UpdateRotation() {
            if (State == HandState.Gripping) {
                //扼颈：指尖朝下扣住
                Projectile.rotation = MathHelper.Lerp(Projectile.rotation, 0f, 0.3f);
                return;
            }
            if (Projectile.velocity.LengthSquared() > 0.1f) {
                Projectile.rotation = MathHelper.Lerp(Projectile.rotation
                    , Projectile.velocity.ToRotation() + MathHelper.PiOver2, 0.2f);
            }
        }

        private bool IsTargetValid() {
            if (targetNPCID < 0 || targetNPCID >= Main.maxNPCs) {
                return false;
            }
            NPC target = Main.npc[targetNPCID];
            return target.active && target.CanBeChasedBy();
        }

        internal static Vector2? FindGroundBelow(Vector2 from, int maxTiles = 24) {
            Point tile = from.ToTileCoordinates();
            for (int i = 0; i < maxTiles; i++) {
                int ty = tile.Y + i;
                if (ty >= Main.maxTilesY - 10) {
                    break;
                }
                if (WorldGen.SolidTile(tile.X, ty)) {
                    return new Vector2(from.X, ty * 16f - 8f);
                }
            }
            return null;
        }

        //==== 判定 ====

        public override bool? CanDamage() => InStrike ? null : false;

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            OniMachete.ApplyGoldRend(target, ref modifiers);
            if (IsRestless) {
                modifiers.SourceDamage *= 1.2f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.GetGlobalNPC<OniMacheteGlobalNPC>().AddCrack(0.4f);
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.6f, Pitch = -0.2f }, target.Center);
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_OniMacheteGold>(target.Center
                    , Main.rand.NextVector2Circular(7f, 7f), default, Main.rand.NextFloat(0.35f, 0.65f))
                    ?.Configure(Main.rand.Next(14, 24));
            }
        }

        private void SmallImpactBurst(Vector2 position, float power) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.55f * power + 0.25f, Pitch = -0.45f }, position);
            for (int i = 0; i < (int)(10 * power); i++) {
                PRTLoader.NewParticle<PRT_OniMacheteGold>(position
                    , Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 9f)
                    , default, Main.rand.NextFloat(0.3f, 0.6f))?.Configure(Main.rand.Next(12, 22));
            }
            var flame = PRTLoader.NewParticle<PRT_HellFlame>(position, Vector2.Zero, Color.White, 0.6f * power + 0.3f);
            if (flame != null) {
                flame.ai[0] = 1;
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.5f, Pitch = -0.4f }, Projectile.Center);
            for (int i = 0; i < 12; i++) {
                PRTLoader.NewParticle<PRT_OniMacheteGold>(Projectile.Center
                    , Main.rand.NextVector2Circular(6f, 6f), default, Main.rand.NextFloat(0.3f, 0.6f))
                    ?.Configure(Main.rand.Next(14, 24));
            }
        }

        //==== 绘制 ====

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;

            //1. 硫火火鞘图元最先绘制：压在手臂贴图之下，火读作从骨臂背后缠上来
            sb.End();
            DrawFlameSheath();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            //2. 手臂鬼影底（盖在火鞘上，露出骨形轮廓）
            DrawArmSprites(sb, lightColor);

            //3. 攻击拖尾 + 辉光 + 手本体
            Texture2D handTexture = OniMachete.OniHand;
            if (handTexture == null) {
                return false;
            }
            Vector2 origin = handTexture.Size() / 2f;

            if (InStrike) {
                DrawAttackTrail(sb, handTexture, origin);
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float glowMix = glowIntensity;
            if (glowMix > 0.45f) {
                Color glowColor = Color.Lerp(new Color(255, 150, 40, 0), new Color(255, 60, 25, 0), restlessBlend);
                for (int i = 0; i < 3; i++) {
                    float glowScale = handScale * (1.12f + i * 0.11f);
                    float glowAlpha = (glowMix - 0.45f) * (1f - i * 0.3f) * 0.75f;
                    sb.Draw(handTexture, drawPos, null, glowColor * glowAlpha
                        , Projectile.rotation + MathHelper.Pi, origin
                        , Projectile.scale * glowScale, SpriteEffects.None, 0);
                }
            }

            //鬼影青灰底色（仅手本体），躁动时向红偏
            Color ghost = Color.Lerp(new Color(168, 182, 186), new Color(210, 150, 140), restlessBlend);
            Color bodyColor = lightColor.MultiplyRGB(ghost);
            sb.Draw(handTexture, drawPos, null, bodyColor
                , Projectile.rotation + MathHelper.Pi, origin
                , Projectile.scale * handScale, SpriteEffects.None, 0);

            return false;
        }

        /// <summary>手臂骨节：OniArm 鬼影青灰底（沿 IK 曲线），躁动微微泛红</summary>
        private void DrawArmSprites(SpriteBatch sb, Color lightColor) {
            Texture2D armTexture = OniMachete.OniArm;
            if (armTexture == null) {
                return;
            }
            Color ghost = Color.Lerp(new Color(150, 165, 170), new Color(190, 130, 120), restlessBlend);
            Color armColor = lightColor.MultiplyRGB(ghost) * 0.92f;

            for (int i = 0; i < armSegments.Count - 1; i++) {
                Vector2 start = armSegments[i + 1];
                Vector2 end = armSegments[i];
                Vector2 diff = end - start;
                float length = diff.Length();
                if (length < 4f) {
                    continue;
                }
                float rotation = diff.ToRotation() + MathHelper.PiOver2;
                int boneCount = Math.Max(1, (int)(length / (armTexture.Height * 0.8f)));
                for (int j = 0; j < boneCount; j++) {
                    float progress = (j + 0.5f) / boneCount;
                    Vector2 bonePos = Vector2.Lerp(start, end, progress);
                    float boneScale = Projectile.scale * MathHelper.Lerp(0.85f, 1.05f
                        , (float)Math.Sin(((i + progress) / ArmSegmentCount) * MathHelper.Pi));
                    sb.Draw(armTexture, bonePos - Main.screenPosition, null, armColor
                        , rotation, armTexture.Size() / 2f, boneScale, SpriteEffects.None, 0);
                }
            }
        }

        /// <summary>
        /// 硫火火鞘：沿 IK 曲线细分采样的加宽 TriangleStrip + OniMacheteFlame.fx。<br/>
        /// 两侧宽度按世界向上偏置（上侧留更多火舌空间，火向上飘），
        /// 臂中线在 v 上的实际位置编码进顶点色 R 供 shader 归一化，火根永远贴着臂线生长
        /// </summary>
        private void DrawFlameSheath() {
            Effect fx = OniMacheteAssets.OniMacheteFlame;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || noise == null || armSegments.Count < 2) {
                return;
            }

            //Catmull-Rom 平滑采样：肩(尾)→手(头)
            const int sampleCount = 26;
            Span<Vector2> pts = stackalloc Vector2[sampleCount];
            for (int i = 0; i < sampleCount; i++) {
                float t = i / (float)(sampleCount - 1);
                //armSegments[0]=手 ... [^1]=肩；条带 uv.x 0=肩 → 1=手
                float ft = (1f - t) * (ArmSegmentCount - 1);
                int i0 = (int)ft;
                int i1 = Math.Min(i0 + 1, ArmSegmentCount - 1);
                float frac = ft - i0;
                Vector2 p0 = armSegments[Math.Max(i0 - 1, 0)];
                Vector2 p1 = armSegments[i0];
                Vector2 p2 = armSegments[i1];
                Vector2 p3 = armSegments[Math.Min(i1 + 1, ArmSegmentCount - 1)];
                pts[i] = Vector2.CatmullRom(p0, p1, p2, p3, frac);
            }

            //火鞘预算：臂线到火尖的最大空间（贴骨收窄）；上侧按世界向上加宽（火向上舔）
            const float sheathReach = 15f;
            const float upBias = 0.38f;
            var verts = new VertexPositionColorTexture[sampleCount * 2];
            for (int i = 0; i < sampleCount; i++) {
                Vector2 tangent = i < sampleCount - 1
                    ? (pts[i + 1] - pts[i]).SafeNormalize(Vector2.UnitX)
                    : (pts[i] - pts[i - 1]).SafeNormalize(Vector2.UnitX);
                Vector2 normal = new(-tangent.Y, tangent.X);
                float upDot = Vector2.Dot(-Vector2.UnitY, normal);   //+normal 侧的"向上程度"
                float w0 = sheathReach * (1f + upBias * upDot);      //uv.y=0 侧（+normal）
                float w1 = sheathReach * (1f - upBias * upDot);      //uv.y=1 侧（-normal）
                Color vCenter = new(w0 / (w0 + w1), 0f, 0f);         //臂中线的 v 位置

                float u = i / (float)(sampleCount - 1);
                verts[i * 2] = new VertexPositionColorTexture((pts[i] + normal * w0).ToVector3()
                    , vCenter, new Vector2(u, 0f));
                verts[i * 2 + 1] = new VertexPositionColorTexture((pts[i] - normal * w1).ToVector3()
                    , vCenter, new Vector2(u, 1f));
            }

            GraphicsDevice device = Main.instance.GraphicsDevice;
            BlendState prevBlend = device.BlendState;
            RasterizerState prevRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(HandIndex * 0.137f % 1f);
            fx.Parameters["uRage"]?.SetValue(restlessBlend);
            fx.Parameters["uGlow"]?.SetValue(glowIntensity);
            fx.Parameters["uFade"]?.SetValue(1f);
            fx.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);
            }

            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
        }

        private void DrawAttackTrail(SpriteBatch sb, Texture2D texture, Vector2 origin) {
            for (int i = 2; i < trailPositions.Count; i += 2) {
                float fade = 1f - i / (float)trailPositions.Count;
                Color trailColor = Color.Lerp(new Color(255, 140, 50, 0), new Color(255, 60, 25, 0), restlessBlend)
                    * (fade * 0.45f);
                sb.Draw(texture, trailPositions[i] - Main.screenPosition, null, trailColor
                    , Projectile.rotation + MathHelper.Pi, origin
                    , Projectile.scale * handScale * (0.75f + fade * 0.25f), SpriteEffects.None, 0);
            }
        }
    }

    /// <summary>
    /// 鬼手之火：投掷的硫火鬼球，短程追踪，命中挂硫火。<br/>
    /// 表现层三件套：硫磺火 Dust 尾迹（本弹幕特批的主视觉 Dust）+ 武器贴图多层黑底加色辉光弹头
    /// + OniMacheteComet.fx 图元彗尾条带（宽度衰减/头亮尾灭/热扰动撕边），
    /// 读作"拖着硫磺焰彗尾的火球"
    /// </summary>
    internal class OniFireBall : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Item_Melee + "OniMachete";

        private ref float Timer => ref Projectile.ai[0];
        private ref float TargetNPCID => ref Projectile.ai[1];

        public override void SetStaticDefaults() {
            //extraUpdates=1 → 每渲染帧压入 2 个轨迹点，26 点 ≈ 13 帧彗尾
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 26;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 30;
            Projectile.hostile = false;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.timeLeft = 180;
            Projectile.extraUpdates = 1;
            Projectile.tileCollide = true;
            Projectile.maxPenetrate = Projectile.penetrate = 3;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => Projectile.damage = (int)(Projectile.damage * 0.60f);

        public override void AI() {
            Timer++;
            //翻滚的燃烧刀铁 + 火球呼吸胀缩（回归旧版手感）
            Projectile.rotation += Projectile.velocity.X * 0.13f;
            Projectile.scale = 1f + (float)Math.Sin(Timer * 0.02f) * 0.1f;

            float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 8f + Projectile.whoAmI) * 0.3f + 0.7f;
            Lighting.AddLight(Projectile.Center, 0.9f * pulse, 0.3f * pulse, 0.1f * pulse);

            if (Timer < 15f) {
                Projectile.velocity *= 1.02f;
            }
            else if (Timer < 120f) {
                HomeInOnTarget();
            }
            else {
                Projectile.velocity *= 0.98f;
            }

            if (!VaultUtils.isServer) {
                SpawnBrimstoneTrail();
            }
        }

        /// <summary>硫磺火 Dust 尾迹（用户特批的主视觉 Dust 例外，回归旧版基底）</summary>
        private void SpawnBrimstoneTrail() {
            if (Main.rand.NextBool(2)) {
                Dust brimstone = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    CWRID.Dust_Brimstone,
                    -Projectile.velocity * 0.3f + Main.rand.NextVector2Circular(1f, 1f),
                    0, default, Main.rand.NextFloat(1.2f, 2f));
                brimstone.noGravity = true;
                brimstone.fadeIn = 1.3f;
            }
            if (Main.rand.NextBool(3)) {
                Dust fire = Dust.NewDustPerfect(Projectile.Center, DustID.Torch
                    , -Projectile.velocity * 0.2f, 0, Color.Red, Main.rand.NextFloat(1f, 1.8f));
                fire.noGravity = true;
            }
        }

        private void HomeInOnTarget() {
            NPC target = null;
            const float maxDistance = 600f;

            if (TargetNPCID >= 0 && TargetNPCID < Main.maxNPCs) {
                NPC potential = Main.npc[(int)TargetNPCID];
                if (potential.active && potential.CanBeChasedBy()
                    && Vector2.Distance(Projectile.Center, potential.Center) < maxDistance) {
                    target = potential;
                }
            }
            if (target == null) {
                target = Projectile.Center.FindClosestNPC(maxDistance);
                if (target != null) {
                    TargetNPCID = target.whoAmI;
                }
            }
            if (target != null) {
                Vector2 dir = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                float speed = Projectile.velocity.Length();
                Projectile.velocity = Vector2.Lerp(Projectile.velocity.SafeNormalize(Vector2.Zero), dir, 0.08f) * speed;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 180);
            target.GetGlobalNPC<OniMacheteGlobalNPC>().AddCrack(0.2f);
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_OniMacheteGold>(Projectile.Center
                    , Main.rand.NextVector2Circular(6f, 6f), default, Main.rand.NextFloat(0.3f, 0.55f))
                    ?.Configure(Main.rand.Next(12, 20));
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Projectile.Kill();
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.5f, Pitch = 0.2f }, Projectile.Center);
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_OniMacheteGold>(Projectile.Center
                    , Main.rand.NextVector2Unit() * Main.rand.NextFloat(2.5f, 7f)
                    , default, Main.rand.NextFloat(0.3f, 0.6f))?.Configure(Main.rand.Next(12, 20));
            }
            var flame = PRTLoader.NewParticle<PRT_HellFlame>(Projectile.Center, Vector2.Zero, Color.White, 0.55f);
            if (flame != null) {
                flame.ai[0] = 1;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //弹头：武器贴图多层黑底加色辉光（回归旧版基底）：
            //外发光 3 层 → 熔金热核（暖金，非冷白）→ 主体 → 炽热外缘
            SpriteBatch sb = Main.spriteBatch;
            Texture2D mainValue = TextureAssets.Projectile[Type].Value;
            Rectangle rectangle = mainValue.GetRectangle();
            Vector2 origin = rectangle.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            for (int i = 0; i < 3; i++) {
                float glowScale = Projectile.scale * (1.3f + i * 0.2f);
                float glowAlpha = 0.4f * (1f - i * 0.3f);
                sb.Draw(mainValue, drawPos, rectangle, new Color(255, 100, 50, 0) * glowAlpha
                    , Projectile.rotation, origin, glowScale, SpriteEffects.None, 0);
            }

            float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 10f + Projectile.whoAmI) * 0.3f + 0.7f;
            sb.Draw(mainValue, drawPos, rectangle, new Color(255, 216, 150, 0) * (0.55f * pulse)
                , Projectile.rotation, origin, Projectile.scale * 0.8f, SpriteEffects.None, 0);

            sb.Draw(mainValue, drawPos, rectangle, new Color(255, 180, 100, 200)
                , Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

            sb.Draw(mainValue, drawPos, rectangle, new Color(255, 80, 40, 0) * 0.6f
                , Projectile.rotation, origin, Projectile.scale * 1.15f, SpriteEffects.None, 0);

            return false;
        }

        /// <summary>彗尾条带：沿 oldPos 轨迹的 TriangleStrip（宽度衰减，头亮尾灭，热扰动撕边）</summary>
        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ) {
                return;
            }
            Effect fx = OniMacheteAssets.OniMacheteComet;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || noise == null) {
                return;
            }

            //采样点：当前中心打头，oldPos 依次向尾（去掉未写入的零槽与过近点）
            Vector2 half = Projectile.Size / 2f;
            Span<Vector2> pts = stackalloc Vector2[1 + Projectile.oldPos.Length];
            int count = 0;
            pts[count++] = Projectile.Center;
            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                if (Projectile.oldPos[k] == Vector2.Zero) {
                    break;
                }
                Vector2 p = Projectile.oldPos[k] + half;
                if (Vector2.DistanceSquared(p, pts[count - 1]) < 4f) {
                    continue;
                }
                pts[count++] = p;
            }
            if (count < 3) {
                return;
            }

            //条带顶点：头段先快速铺满宽度再向尾收拢成尖
            float maxWidth = 21f * Projectile.scale;
            var verts = new VertexPositionColorTexture[count * 2];
            for (int i = 0; i < count; i++) {
                float t = i / (float)(count - 1);
                Vector2 tangent = i < count - 1
                    ? (pts[i] - pts[i + 1]).SafeNormalize(Vector2.UnitX)
                    : (pts[i - 1] - pts[i]).SafeNormalize(Vector2.UnitX);
                Vector2 normal = new(-tangent.Y, tangent.X);
                float width = maxWidth * (0.55f + 0.45f * MathHelper.Clamp(t / 0.15f, 0f, 1f))
                    * MathF.Pow(1f - t, 0.72f);
                verts[i * 2] = new VertexPositionColorTexture((pts[i] + normal * width).ToVector3()
                    , Color.White, new Vector2(t, 0f));
                verts[i * 2 + 1] = new VertexPositionColorTexture((pts[i] - normal * width).ToVector3()
                    , Color.White, new Vector2(t, 1f));
            }

            GraphicsDevice device = Main.instance.GraphicsDevice;
            BlendState prevBlend = device.BlendState;
            RasterizerState prevRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(Projectile.whoAmI * 0.61f % 1f);
            fx.Parameters["uFade"]?.SetValue(MathHelper.Clamp(Timer / 12f, 0f, 1f));
            fx.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);
            }

            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
        }
    }

    /// <summary>
    /// 鬼手砸地结算：短促伤害盒（前 5 帧）+ 地面熔金裂缝 decal（存活 ~2.6 秒）+ 全套冲击演出。<br/>
    /// 伤害与画面同源同址，裂缝亮着的地方就是刚刚挨过打的地方
    /// </summary>
    internal class OniHandExplode : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>总寿命 = 伤害窗(5) + decal 余寿</summary>
        public const int DecalLife = 155;

        private ref float GroundedAi => ref Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 200;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = DecalLife;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override bool? CanDamage() => Projectile.timeLeft > DecalLife - 5 ? null : false;

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => OniMachete.ApplyGoldRend(target, ref modifiers);

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.GetGlobalNPC<OniMacheteGlobalNPC>().AddCrack(0.6f);

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                //地面锚定（各端世界一致，免同步）：找到地面则裂缝贴地，否则只留空中爆发
                Vector2? ground = OniHandMinion.FindGroundBelow(Projectile.Center, 18);
                if (ground.HasValue) {
                    GroundedAi = 1f;
                    Projectile.Center = ground.Value + new Vector2(0f, -4f);
                }
                BirthBurst();
            }

            if (GroundedAi > 0.5f && Projectile.timeLeft > DecalLife - 60) {
                //裂缝余温：零星熔金泡与上升余烬
                if (!VaultUtils.isServer && Main.rand.NextBool(4)) {
                    float span = Main.rand.NextFloat(-120f, 120f);
                    PRTLoader.NewParticle<PRT_LavaFire>(Projectile.Center + new Vector2(span, 2f)
                        , -Vector2.UnitY * Main.rand.NextFloat(0.6f, 1.6f)
                        , Color.White, Main.rand.NextFloat(0.4f, 0.8f))?.SetLifetime(12, 26);
                }
                Lighting.AddLight(Projectile.Center, 0.8f, 0.45f, 0.10f);
            }
        }

        /// <summary>冲击帧全层：定向震屏 + 分层音效 + 冲击环 + 火柱 + 金屑抛洒</summary>
        private void BirthBurst() {
            //定向震屏（沿砸击方向向下，尊重配置开关）
            if (CWRServerConfig.Instance.ScreenVibration && !Main.dedServ) {
                var modifier = new PunchCameraModifier(Projectile.Center, Vector2.UnitY
                    , 7f, 6f, 12, 900f, FullName);
                Main.instance.CameraModifiers.Add(modifier);
            }
            if (VaultUtils.isServer) {
                return;
            }

            SoundEngine.PlaySound(SoundID.Item74 with { Volume = 1.1f, Pitch = -0.6f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.DD2_BetsyFireballImpact with { Volume = 0.95f, Pitch = -0.3f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.9f, Pitch = -0.4f }, Projectile.Center);

            //横扁冲击环（贴地压扁的伪透视）
            PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero, new Color(255, 160, 60), 0.6f)
                ?.Configure(new Vector2(1.6f, 0.55f), 0f, 2.4f, 24);
            PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero, new Color(255, 90, 30), 0.4f)
                ?.Configure(new Vector2(1.2f, 0.4f), 0f, 1.7f, 18);

            //地狱火柱：中央高两侧矮
            for (int i = 0; i < 9; i++) {
                float span = MathHelper.Lerp(-110f, 110f, i / 8f);
                float centrality = 1f - MathF.Abs(span) / 130f;
                var flame = PRTLoader.NewParticle<PRT_HellFlame>(Projectile.Center + new Vector2(span, 0f)
                    , -Vector2.UnitY * (2.5f + centrality * 4.5f) + Main.rand.NextVector2Circular(0.8f, 0.5f)
                    , Color.White, 0.5f + centrality * 0.6f);
                if (flame != null) {
                    flame.ai[0] = 0;
                }
            }

            //熔金抛洒：数量∝冲击动能
            for (int i = 0; i < 26; i++) {
                Vector2 vel = new(Main.rand.NextFloat(-9f, 9f), Main.rand.NextFloat(-13f, -4f));
                PRTLoader.NewParticle<PRT_OniMacheteGold>(Projectile.Center + new Vector2(Main.rand.NextFloat(-60f, 60f), 0f)
                    , vel, default, Main.rand.NextFloat(0.4f, 0.85f))
                    ?.Configure(Main.rand.Next(24, 44));
            }
            //扬尘碎屑（Dust 只做环境衬托）
            for (int i = 0; i < 14; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-90f, 90f), 0f)
                    , DustID.Smoke, new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-3f, -0.5f))
                    , 120, default, Main.rand.NextFloat(1f, 1.8f));
                dust.noGravity = false;
            }
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || GroundedAi < 0.5f) {
                return;
            }
            Effect fx = OniMacheteAssets.OniMacheteCrack;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || noise == null) {
                return;
            }

            float life = 1f - Projectile.timeLeft / (float)DecalLife;
            Vector2 c = Projectile.Center + new Vector2(0f, 6f);
            const float halfX = 160f;
            const float halfY = 58f;

            var verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture((c + new Vector2(-halfX, -halfY)).ToVector3(), Color.White, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture((c + new Vector2(halfX, -halfY)).ToVector3(), Color.White, new Vector2(1f, 0f));
            verts[2] = new VertexPositionColorTexture((c + new Vector2(-halfX, halfY)).ToVector3(), Color.White, new Vector2(0f, 1f));
            verts[3] = new VertexPositionColorTexture((c + new Vector2(halfX, halfY)).ToVector3(), Color.White, new Vector2(1f, 1f));

            GraphicsDevice device = Main.instance.GraphicsDevice;
            BlendState prevBlend = device.BlendState;
            RasterizerState prevRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            fx.CurrentTechnique = fx.Techniques["GroundTech"];
            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uLife"]?.SetValue(life);
            fx.Parameters["uSeed"]?.SetValue(Projectile.whoAmI * 0.61f % 1f);
            fx.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }

            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
        }
    }

    /// <summary>
    /// 削甲可见化：被鬼砍刀系打击的 NPC 挂熔金裂纹覆盖层（加色重绘本体贴图帧），
    /// 强度随命中叠加、随时间冷却。命中登记发生在打击方客户端，纯视觉不入网络
    /// </summary>
    internal class OniMacheteGlobalNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>裂纹强度 0..1.2，命中叠加，逐帧冷却</summary>
        public float Crack { get; private set; }
        private float seed = -1f;

        public void AddCrack(float amount) {
            Crack = Math.Min(1.2f, Crack + amount);
            if (seed < 0f) {
                seed = Main.rand.NextFloat(10f);
            }
        }

        public override void PostAI(NPC npc) {
            if (Crack > 0f) {
                Crack = Math.Max(0f, Crack - 0.0045f);
            }
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ || Crack <= 0.03f || npc.IsABestiaryIconDummy) {
                return;
            }
            Effect fx = OniMacheteAssets.OniMacheteCrack;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            Texture2D tex = TextureAssets.Npc[npc.type]?.Value;
            if (fx == null || noise == null || tex == null) {
                return;
            }

            Rectangle frame = npc.frame;
            if (frame.Width <= 0 || frame.Height <= 0) {
                frame = tex.GetRectangle();
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            fx.CurrentTechnique = fx.Techniques["OverlayTech"];
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCrack"]?.SetValue(MathHelper.Clamp(Crack, 0f, 1f));
            fx.Parameters["uSeed"]?.SetValue(seed);
            fx.Parameters["uNoiseTex"]?.SetValue(noise);
            fx.CurrentTechnique.Passes[0].Apply();

            SpriteEffects flip = npc.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Vector2 drawPos = npc.Center - screenPos + new Vector2(0f, npc.gfxOffY);
            spriteBatch.Draw(tex, drawPos, frame, Color.White, npc.rotation
                , frame.Size() / 2f, npc.scale, flip, 0f);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }

    /// <summary>
    /// 鬼手扼颈全屏后效：screenTarget ping-pong 带 OniMacheteGrip.fx 回写，
    /// 包络由本地玩家 <see cref="OniMachetePlayer.GripVignette"/> 驱动（掐颈的手逐帧推高、自然衰减）
    /// </summary>
    internal sealed class OniMacheteGripRender : RenderHandle
    {
        /// <summary>权重 1.12：晚于鬼切冲击后效(1.10)，早于弹幕扩展层(1.2)</summary>
        public override float Weight => 1.12f;

        public override void EndCaptureDraw(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D screenSwap) {
            if (Main.gameMenu || Main.LocalPlayer == null || !Main.LocalPlayer.active) {
                return;
            }
            float grip = Main.LocalPlayer.GetModPlayer<OniMachetePlayer>().GripVignette;
            if (grip <= 0.01f) {
                return;
            }
            Effect fx = OniMacheteAssets.OniMacheteGrip;
            if (fx == null || screenSwap == null || Main.screenTarget == null) {
                return;
            }

            fx.Parameters["uGrip"]?.SetValue(grip);
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uAspect"]?.SetValue(Main.screenWidth / (float)Main.screenHeight);

            gd.SetRenderTarget(screenSwap);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Deferred, BlendState.Opaque);
            sb.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            sb.End();

            gd.SetRenderTarget(Main.screenTarget);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Immediate, BlendState.Opaque);
            fx.CurrentTechnique.Passes[0].Apply();
            sb.Draw(screenSwap, Vector2.Zero, Color.White);
            sb.End();
        }
    }
}
