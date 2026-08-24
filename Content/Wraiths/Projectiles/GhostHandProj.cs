using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Wraiths.Abilities;
using CalamityOverhaul.Content.Wraiths.Buffs;
using CalamityOverhaul.Content.Wraiths.Core;
using CalamityOverhaul.Content.Wraiths.Marks;
using CalamityOverhaul.Content.Wraiths.Runtime;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.Projectiles
{
    /// <summary>
    /// 焦黑枯手本体，纯顶点绘制无贴图：焦炭枯尸手，常驻近实心，龟裂缝透血烬。<br/>
    /// 常驻循环：背后待机（揉捏/痉挛/窥伺）→ 扑抓 → 攥握碾轧（脉冲+松手碾碎伤害）→ 回位；<br/>
    /// boss 亦可被攥住；猎物多时由 <see cref="GhostHandAbility"/> 生成至多三只手（扇形手位）。<br/>
    /// 失去当前役鬼资格后无害退场。ai[0]=状态 ai[1]=计时 ai[2]=复苏值；<br/>
    /// 目标/朝向/手位走 SendExtraAI，索敌和退场由 owner 决策后同步
    /// </summary>
    internal sealed class GhostHandProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        [VaultLoaden(CWRConstant.Masking + "SoftGlow")]
        private static Asset<Texture2D> GlowTex = null;

        private enum HandState
        {
            Idle = 0,
            Lunging,
            Gripping,
            Returning,
            Dismissing
        }

        private ref float StateRaw => ref Projectile.ai[0];
        private ref float StateTimer => ref Projectile.ai[1];
        /// <summary>复苏值 0~1，生成时传入，攥握时长与伤害插值，越接近复苏越凶</summary>
        private ref float Revival => ref Projectile.ai[2];

        private HandState State {
            get => (HandState)StateRaw;
            set => StateRaw = (float)value;
        }

        private Player Owner => Main.player[Projectile.owner];

        //==== 同步数据 ====
        private int targetNPCID = -1;
        private int targetNPCType = -1;
        private int ownerDirection = 1;
        private ushort gripSerial;
        private ushort lastSeenGripSerial;
        private bool gripCommitAttempted;
        private bool gripAuthorized;
        private bool authorityGripCommitted;
        private ulong lastAuthorityGripTick;
        private int handSlot;

        internal int HandSlot => handSlot;

        //==== 权威伤害 ====
        /// <summary>抓取结算成功时冻结的武器伤害快照，仅权威端使用</summary>
        private int weaponDamageSnapshot;
        private const float PulseDamageMin = 0.22f;
        private const float PulseDamageMax = 0.38f;
        private const float CrushDamageMin = 0.45f;
        private const float CrushDamageMax = 0.75f;

        //==== IK 手臂 ====
        private const int ArmSegmentCount = 6;
        private const float SegmentLength = 52f;
        private const float MaxReach = ArmSegmentCount * SegmentLength;
        private readonly Vector2[] armSegments = new Vector2[ArmSegmentCount];
        private Vector2 shoulderPos;
        private float armTension;

        //==== 手掌与五指（3 节骨 + 爪尖）====
        private const float PalmLength = 24f;
        private static readonly float[] FingerSpread = [-0.72f, -0.36f, 0f, 0.36f, 0.72f];
        private static readonly float[] KnuckleOffsets = [-13f, -6.5f, 0f, 6.5f, 13f];
        private static readonly float[] FingerLengths = [30f, 40f, 46f, 40f, 32f];
        private static readonly float[] FingerSegFractions = [0.40f, 0.32f, 0.28f];
        //[指, 关节] 0=指根 1..3=骨节 4=爪尖
        private readonly Vector2[,] fingerJoints = new Vector2[5, 5];
        private Vector2 knuckleCenter;

        //==== 手位扇形分布（x=身后距离，随朝向翻转）====
        //后两位是雨中才伸出的加位，摆得更高更外，与常态三位错开
        private static readonly Vector2[] ShoulderFan = [
            new(28f, -8f), new(14f, -34f), new(42f, 12f), new(56f, -26f), new(4f, 20f),
        ];
        private static readonly Vector2[] HoverFan = [
            new(34f, -52f), new(10f, -58f), new(50f, -6f), new(64f, -66f), new(-6f, 34f),
        ];

        //==== 雨里伸手 ====
        /// <summary>枯手自雨线垂下的高度：肩挪到目标头顶这么高的雨里</summary>
        private const float RainLineHeight = 210f;
        /// <summary>肩位向雨线迁移的缓动，别瞬移过去</summary>
        private const float RainShoulderEase = 0.14f;

        //==== 时长 ====
        private const int InitialScanDelay = 18;
        private const int ReacquireDelay = 45;
        private const int LungeDuration = 20;
        private const int ReturnDuration = 20;
        private const int DismissDuration = 18;
        /// <summary>客户端抓握等待权威确认的宽限帧数，超时松手不空攥</summary>
        private const int GripAuthGraceFrames = 30;
        /// <summary>副手无猎物自动退场的滞留帧数</summary>
        private const int ExtraHandLingerTicks = 150;
        private const int MinimumAuthorityGripInterval = ReacquireDelay + LungeDuration + ReturnDuration;
        private int GripDuration => (int)MathHelper.Lerp(60f, 120f, MathHelper.Clamp(Revival, 0f, 1f));

        //==== 视觉状态（本地平滑）====
        private HandState prevState = HandState.Idle;
        private int visualAge;
        private int reacquireTimer;
        private float fingerCurl;
        private readonly float[] fingerCurlOffsets = new float[5];
        private float gripBlend;
        private float opacitySmooth;
        private float drawOpacity;
        private float emberSmooth;
        private float emberFlash;
        private int twitchCountdown;
        private float twitchCurl;
        private Vector2 menaceDrift;
        private int menaceScanTimer;
        private int menaceNPCID = -1;
        private int noPreyTicks;
        private Vector2 lungeStart;
        private Vector2 returnStart;
        private Vector2 dismissStart;
        private int wispTimer;

        private float Seed => Projectile.identity * 0.137f % 1f;

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 46;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 2;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
        }

        public override void OnSpawn(IEntitySource source) {
            ownerDirection = Owner.direction;
            twitchCountdown = 90 + Projectile.identity % 97;
            SnapToHandSlot();
        }

        /// <summary>由能力端在生成后指定手位并重摆初始位置</summary>
        internal void AssignHandSlot(int slot) {
            handSlot = Math.Clamp(slot, 0, GhostHandAbility.MaxHands - 1);
            SnapToHandSlot();
            Projectile.netUpdate = true;
        }

        private void SnapToHandSlot() {
            UpdateShoulderPosition();
            Projectile.Center = IdleHoverPosition();
            for (int i = 0; i < ArmSegmentCount; i++) {
                armSegments[i] = Vector2.Lerp(Projectile.Center, shoulderPos, i / (float)(ArmSegmentCount - 1));
            }
        }

        /// <summary>自管位移</summary>
        public override bool ShouldUpdatePosition() => false;

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write((short)targetNPCID);
            writer.Write(targetNPCType);
            writer.Write((sbyte)ownerDirection);
            writer.Write(gripSerial);
            writer.Write((byte)handSlot);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            targetNPCID = reader.ReadInt16();
            targetNPCType = reader.ReadInt32();
            ownerDirection = reader.ReadSByte();
            gripSerial = reader.ReadUInt16();
            handSlot = Math.Clamp((int)reader.ReadByte(), 0, GhostHandAbility.MaxHands - 1);
        }

        private static bool IsNewerGrip(ushort incoming, ushort current)
            => incoming != current && (ushort)(incoming - current) < 0x8000;

        //==== 主循环 ====

        public override void AI() {
            if (!Owner.active) {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 2;
            bool finishingGrip = State is HandState.Gripping or HandState.Returning;
            bool channelValid = WraithAbilityService.HasAbilityChannel(Owner, GhostHandAbility.Key);
            if (!channelValid) {
                //休眠可以完成已结算的抓取；收刀、换鬼或死亡则立即停止权威续期。
                gripAuthorized = false;
            }
            if (Projectile.IsOwnedByLocalPlayer() && State != HandState.Dismissing
                && (!channelValid || !finishingGrip && !HasValidAbility())) {
                Transition(HandState.Dismissing);
            }

            visualAge++;
            StateTimer++;
            UpdateShoulderPosition();

            if (State != prevState) {
                OnStateChanged(State);
                prevState = State;
            }

            switch (State) {
                case HandState.Idle: IdleBehavior(); break;
                case HandState.Lunging: LungingBehavior(); break;
                case HandState.Gripping: GrippingBehavior(); break;
                case HandState.Returning: ReturningBehavior(); break;
                case HandState.Dismissing: DismissingBehavior(); break;
            }

            UpdateArmIK();
            UpdateFingers();
            UpdateVisuals();
        }

        private bool HasValidAbility()
            => WraithAbilityService.TryResolve(Owner, GhostHandAbility.Key, out _);

        private void UpdateShoulderPosition() {
            if (State is HandState.Idle or HandState.Returning or HandState.Dismissing) {
                ownerDirection = Owner.direction;
            }
            Vector2 fan = ShoulderFan[handSlot];
            Vector2 home = Owner.Center + new Vector2(-ownerDirection * fan.X, fan.Y);
            //「雨里伸手」：淋着雨的猎物够不着时，肩挪到它头顶的雨线上，
            //手就从雨里垂下来抓，臂展没变，是雨把手送过去的
            if (TryRainLineShoulder(home, out Vector2 rainAnchor)) {
                shoulderPos = shoulderPos == Vector2.Zero
                    ? rainAnchor : Vector2.Lerp(shoulderPos, rainAnchor, RainShoulderEase);
                return;
            }
            shoulderPos = shoulderPos == Vector2.Zero
                ? home : Vector2.Lerp(shoulderPos, home, RainShoulderEase * 1.6f);
        }

        private bool TryRainLineShoulder(Vector2 home, out Vector2 anchor) {
            anchor = default;
            if (State is HandState.Idle or HandState.Dismissing
                || targetNPCID < 0 || targetNPCID >= Main.maxNPCs) {
                return false;
            }
            NPC target = Main.npc[targetNPCID];
            if (!target.active
                || !WraithSynergy.TriggersOn(GhostHandAbility.RainReach, target, Projectile.owner)) {
                return false;
            }
            //够得着就照常从身后伸，别为了演出把近身抓也搬到天上
            float slack = MaxReach * 0.82f;
            if (Vector2.DistanceSquared(target.Center, home) <= slack * slack) {
                return false;
            }
            Vector2 fan = ShoulderFan[handSlot];
            anchor = target.Center + new Vector2(fan.X * 0.5f,
                -RainLineHeight - target.height * 0.5f);
            return true;
        }

        private void Transition(HandState next) {
            if (State == next) {
                return;
            }
            State = next;
            StateTimer = 0;
            OnStateChanged(next);
            prevState = next;
            if (Projectile.IsOwnedByLocalPlayer()) {
                Projectile.netUpdate = true;
            }
        }

        //==== 行为 ====

        private Vector2 IdleHoverPosition() {
            float t = Main.GlobalTimeWrappedHourly + Seed * MathHelper.TwoPi;
            Vector2 bob = new((float)Math.Sin(t * 1.7f) * 9f, (float)Math.Cos(t * 1.3f) * 7f);
            Vector2 fan = HoverFan[handSlot];
            return shoulderPos + new Vector2(-ownerDirection * fan.X, fan.Y) + bob;
        }

        /// <summary>窥伺漂移：待机时向最近猎物微微倾身，纯表现</summary>
        private void UpdateMenaceDrift() {
            if (++menaceScanTimer >= 12) {
                menaceScanTimer = 0;
                menaceNPCID = -1;
                float bestSq = 460f * 460f;
                foreach (NPC npc in Main.ActiveNPCs) {
                    if (!npc.CanBeChasedBy()) {
                        continue;
                    }
                    float distSq = Vector2.DistanceSquared(npc.Center, Projectile.Center);
                    if (distSq < bestSq) {
                        bestSq = distSq;
                        menaceNPCID = npc.whoAmI;
                    }
                }
            }
            Vector2 driftTarget = Vector2.Zero;
            if (menaceNPCID >= 0 && menaceNPCID < Main.maxNPCs) {
                NPC menace = Main.npc[menaceNPCID];
                if (menace.active) {
                    driftTarget = (menace.Center - Projectile.Center)
                        .SafeNormalize(Vector2.Zero) * 16f;
                }
            }
            menaceDrift = Vector2.Lerp(menaceDrift, driftTarget, 0.05f);
        }

        private void IdleBehavior() {
            armTension = 0.3f;
            UpdateMenaceDrift();
            MoveToPosition(IdleHoverPosition() + menaceDrift, 0.12f);

            if (reacquireTimer > 0) {
                reacquireTimer--;
                return;
            }
            if (!Projectile.IsOwnedByLocalPlayer() || StateTimer < InitialScanDelay || (int)StateTimer % 6 != 0) {
                return;
            }
            if (!WraithAbilityService.TryResolve(Owner, GhostHandAbility.Key,
                out WraithAbilityContext context)) {
                Transition(HandState.Dismissing);
                return;
            }

            NPC target = FindGrabTarget();
            if (target != null) {
                noPreyTicks = 0;
                Revival = context.Revival;
                targetNPCID = target.whoAmI;
                targetNPCType = target.type;
                Transition(HandState.Lunging);
                return;
            }

            //副手长期无猎物则自行退场，主手常驻
            if (handSlot > 0) {
                noPreyTicks += 6;
                if (noPreyTicks >= ExtraHandLingerTicks) {
                    Transition(HandState.Dismissing);
                }
            }
        }

        private NPC FindGrabTarget() {
            NPC best = null;
            float bestSq = float.MaxValue;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!GhostHandAbility.CanGrab(npc, Owner.Center, Projectile.owner)
                    || TargetClaimedByOther(npc.whoAmI)) {
                    continue;
                }
                float distSq = Vector2.DistanceSquared(npc.Center, Owner.Center);
                //照见：灯照过的目标在手看来近得多，权重声明在 LitSeek 规则里
                distSq *= WraithSynergy.Factor(GhostHandAbility.LitSeek, npc, Projectile.owner);
                if (distSq < bestSq) {
                    bestSq = distSq;
                    best = npc;
                }
            }
            return best;
        }

        /// <summary>多手互不抢目标：跳过已被同主其它手锁定或攥住的目标</summary>
        private bool TargetClaimedByOther(int npcId) {
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.whoAmI != Projectile.whoAmI
                    && proj.owner == Projectile.owner && proj.type == Projectile.type
                    && proj.ModProjectile is GhostHandProj other
                    && other.targetNPCID == npcId
                    && other.State is HandState.Lunging or HandState.Gripping) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>贴附点：目标朝肩侧的近缘，巨物不把手埋进贴图中心</summary>
        private Vector2 GrabAnchor(NPC target) {
            Vector2 toShoulder = (shoulderPos - target.Center).SafeNormalize(Vector2.UnitX);
            float inset = MathF.Min(target.width, target.height) * 0.30f;
            Vector2 perp = new(-toShoulder.Y, toShoulder.X);
            return target.Center + toShoulder * inset + perp * ((Seed - 0.5f) * 0.6f * inset);
        }

        private void LungingBehavior() {
            armTension = 0.95f;
            if (!IsTargetValid()) {
                Transition(HandState.Returning);
                return;
            }

            NPC target = Main.npc[targetNPCID];
            float t = MathHelper.Clamp(StateTimer / LungeDuration, 0f, 1f);

            Vector2 next;
            if (t < 0.28f) {
                //前摇回拉蓄势，五指张爪
                float back = VaultUtils.EaseOutCubic(t / 0.28f);
                Vector2 windup = lungeStart + (shoulderPos - lungeStart).SafeNormalize(Vector2.Zero) * 24f * back
                    - Vector2.UnitY * 10f * back;
                next = windup;
            }
            else {
                //鞭击突进，末端全速扣向贴附点
                float strike = MathF.Pow((t - 0.28f) / 0.72f, 2.4f);
                next = Vector2.Lerp(lungeStart, GrabAnchor(target), strike);
            }
            Projectile.velocity = next - Projectile.Center;
            Projectile.Center = next;

            //突进余烬拉丝
            if (!VaultUtils.isServer && t > 0.28f) {
                for (int i = 0; i < 2; i++) {
                    PRTLoader.NewParticle<PRT_PallbearerEmber>(
                        Projectile.Center + Main.rand.NextVector2Circular(7f, 7f)
                        , -Projectile.velocity * 0.12f + Main.rand.NextVector2Circular(1.1f, 1.1f)
                        , new Color(208, 66, 26), Main.rand.NextFloat(0.5f, 0.9f))
                        ?.Configure(Main.rand.Next(12, 20), 0.04f);
                }
            }

            if (StateTimer >= LungeDuration) {
                Transition(HandState.Gripping);
            }
        }

        private void GrippingBehavior() {
            armTension = MathHelper.Lerp(armTension, 1f, 0.15f);
            if (!IsTargetValid()) {
                Transition(HandState.Returning);
                return;
            }

            NPC target = Main.npc[targetNPCID];

            //手被拖出臂展即被扯脱（位移由 AI 直接改写坐标的目标由此自然挣脱）
            if (Vector2.Distance(shoulderPos, Projectile.Center) > MaxReach * 1.08f) {
                Transition(HandState.Returning);
                return;
            }

            //钉在贴附点上使劲颤动，攥 boss 时挣扎更烈
            float jitter = target.boss ? 2.8f : 1.4f;
            Vector2 pin = GrabAnchor(target) + Main.rand.NextVector2Circular(jitter, jitter);
            Projectile.velocity = pin - Projectile.Center;
            Projectile.Center = pin;

            if (!gripCommitAttempted && Projectile.IsOwnedByLocalPlayer()) {
                gripCommitAttempted = true;
                WraithNet.RequestGhostHandGrip(Projectile, gripSerial,
                    targetNPCID, targetNPCType);
            }

            //迟迟未获权威确认（资格失败/免疫）则提前松手，不空攥整轮
            if (Projectile.IsOwnedByLocalPlayer() && gripCommitAttempted
                && StateTimer > GripAuthGraceFrames
                && !target.HasBuff<GhostGripDebuff>()) {
                Transition(HandState.Returning);
                return;
            }

            int duration = GripDuration;
            int pulse1 = duration * 32 / 100;
            int pulse2 = duration * 68 / 100;
            bool pulseFrame = (int)StateTimer == pulse1 || (int)StateTimer == pulse2;

            //权威端滚动续期，8 帧短债松手即断；捏紧脉冲碾轧
            if (!VaultUtils.isClient && gripAuthorized) {
                target.AddBuff(ModContent.BuffType<GhostGripDebuff>(), 8);
                WraithMarks.Apply(target, WraithMark.Gripped, WraithMarks.GrippedTicks,
                    Revival, Projectile.owner, WraithPlayer.GhostHandKey);
                if (pulseFrame) {
                    ApplyGripDamage(target, PulseDamageMin, PulseDamageMax);
                }
            }

            float squeeze = SqueezePulse(duration);

            if (!VaultUtils.isServer) {
                if (pulseFrame) {
                    PlaySqueezeFx(target);
                }
                //攥紧顿挫，闷响+挤出血珠
                if (squeeze > 0.9f && (int)StateTimer % 3 == 0) {
                    SoundEngine.PlaySound(SoundID.NPCHit54 with { Volume = 0.38f, Pitch = -0.55f }, target.Center);
                }
                if (squeeze > 0.6f && Main.rand.NextBool(2)) {
                    PRTLoader.NewParticle<PRT_HeartcarverDroplet>(
                        Projectile.Center + Main.rand.NextVector2Circular(10f, 10f)
                        , Main.rand.NextVector2Circular(2.6f, 2f) - Vector2.UnitY * 1.4f
                        , new Color(120, 15, 20), Main.rand.NextFloat(0.5f, 0.85f))
                        ?.Configure(Main.rand.Next(16, 26), 0.3f);
                }
                //boss 挣扎：崩落火星不断
                if (target.boss && (int)StateTimer % 5 == 0) {
                    PRTLoader.NewParticle<PRT_PallbearerEmber>(
                        Projectile.Center + Main.rand.NextVector2Circular(12f, 12f)
                        , Main.rand.NextVector2Circular(2.2f, 2.2f) - Vector2.UnitY * 0.8f
                        , new Color(214, 74, 28), Main.rand.NextFloat(0.4f, 0.8f))
                        ?.Configure(Main.rand.Next(14, 22), 0.05f);
                }
            }

            if (StateTimer >= duration) {
                //自然松手前碾碎一记，仅对已结算的抓取生效
                if (!VaultUtils.isClient && gripAuthorized) {
                    ApplyGripDamage(target, CrushDamageMin, CrushDamageMax);
                }
                if (!VaultUtils.isServer) {
                    PlayCrushFx(target);
                }
                Transition(HandState.Returning);
            }
        }

        /// <summary>权威端碾轧伤害：抓取结算帧的武器伤害快照 × 复苏插值，真近战无击退</summary>
        private void ApplyGripDamage(NPC target, float minFraction, float maxFraction) {
            float fraction = MathHelper.Lerp(minFraction, maxFraction,
                MathHelper.Clamp(Revival, 0f, 1f));
            //湿手好使力：雨印越重碾得越狠，量级曲线声明在 RainCrush 规则里
            fraction *= WraithSynergy.Factor(GhostHandAbility.RainCrush, target, Projectile.owner);
            int damage = Math.Max(1, (int)(weaponDamageSnapshot * fraction));
            int direction = target.Center.X >= Owner.Center.X ? 1 : -1;
            Owner.ApplyDamageToNPC(target, damage, 0f, direction, false,
                CWRRef.GetTrueMeleeDamageClass());
        }

        /// <summary>两次离散攥紧脉冲 0..1</summary>
        private float SqueezePulse(int duration) {
            float p1 = MathF.Abs(StateTimer - duration * 0.32f);
            float p2 = MathF.Abs(StateTimer - duration * 0.68f);
            float near = MathF.Min(p1, p2);
            return near < 5f ? 1f - near / 5f : 0f;
        }

        private void ReturningBehavior() {
            armTension = 0.5f;
            float t = MathHelper.Clamp(StateTimer / ReturnDuration, 0f, 1f);
            Vector2 next = Vector2.Lerp(returnStart, IdleHoverPosition(), VaultUtils.EaseOutCubic(t));
            Projectile.velocity = next - Projectile.Center;
            Projectile.Center = next;

            if (StateTimer >= ReturnDuration) {
                targetNPCID = -1;
                Transition(HandState.Idle);
            }
        }

        private void DismissingBehavior() {
            armTension = 0.45f;
            float t = MathHelper.Clamp(StateTimer / DismissDuration, 0f, 1f);
            Vector2 home = shoulderPos + new Vector2(-ownerDirection * 22f, -24f);
            Vector2 next = Vector2.Lerp(dismissStart, home, VaultUtils.EaseOutCubic(t));
            Projectile.velocity = next - Projectile.Center;
            Projectile.Center = next;

            if (StateTimer >= DismissDuration) {
                Projectile.Kill();
            }
        }

        private void OnStateChanged(HandState next) {
            switch (next) {
                case HandState.Lunging:
                    gripCommitAttempted = false;
                    gripAuthorized = false;
                    if (Projectile.IsOwnedByLocalPlayer()) {
                        gripSerial++;
                    }
                    lungeStart = Projectile.Center;
                    emberFlash += 0.7f;
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.5f, Pitch = -0.5f }, Projectile.Center);
                        for (int i = 0; i < 5; i++) {
                            PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f)
                                , Main.rand.NextVector2Circular(1.2f, 1.2f)
                                , new Color(70, 36, 24), Main.rand.NextFloat(0.08f, 0.13f))
                                ?.Configure(Main.rand.Next(16, 26), 0.4f);
                        }
                    }
                    break;
                case HandState.Gripping:
                    PlayCatchFx();
                    break;
                case HandState.Returning:
                    returnStart = Projectile.Center;
                    reacquireTimer = ReacquireDelay;
                    break;
                case HandState.Idle:
                    targetNPCID = -1;
                    targetNPCType = -1;
                    break;
                case HandState.Dismissing:
                    targetNPCID = -1;
                    targetNPCType = -1;
                    dismissStart = Projectile.Center;
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.28f, Pitch = -0.9f }, Projectile.Center);
                    }
                    break;
            }
        }

        /// <summary>破空显形：肩口撕开的焦烟与火星，所有客户端各自播放</summary>
        private void EmergenceFx() {
            for (int i = 0; i < 10; i++) {
                Vector2 pos = shoulderPos + new Vector2(Main.rand.NextFloat(-14f, 14f), Main.rand.NextFloat(-16f, 10f));
                Vector2 velocity = new(
                    Main.rand.NextFloat(-0.6f, 0.6f) - Owner.direction * 0.4f,
                    Main.rand.NextFloat(-1.3f, -0.4f));
                Color color = Color.Lerp(new Color(58, 30, 22), new Color(96, 44, 26), Main.rand.NextFloat());
                PRTLoader.NewParticle<PRT_Smoke>(pos, velocity, color, Main.rand.NextFloat(0.09f, 0.15f))
                    ?.Configure(Main.rand.Next(26, 44), Main.rand.NextFloat(0.4f, 0.65f),
                        Main.rand.NextFloat(-0.02f, 0.02f));
            }
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_PallbearerEmber>(
                    shoulderPos + Main.rand.NextVector2Circular(10f, 10f)
                    , Main.rand.NextVector2Circular(2.5f, 2.5f) - Vector2.UnitY * 0.6f
                    , new Color(200, 64, 28), Main.rand.NextFloat(0.4f, 0.8f))
                    ?.Configure(Main.rand.Next(14, 24), 0.05f);
            }
            SoundEngine.PlaySound(SoundID.NPCDeath6 with { Pitch = -0.85f, Volume = 0.4f }, Owner.Center);
            SoundEngine.PlaySound(SoundID.Item32 with { Pitch = -0.7f, Volume = 0.3f }, Owner.Center);
        }

        private void PlayCatchFx() {
            if (VaultUtils.isServer) {
                return;
            }
            bool bossCatch = targetNPCID >= 0 && targetNPCID < Main.maxNPCs
                && Main.npc[targetNPCID].boss;
            SoundEngine.PlaySound(SoundID.DD2_SkeletonHurt with { Volume = 0.75f, Pitch = -0.6f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item32 with { Volume = 0.5f, Pitch = -0.45f }, Projectile.Center);
            emberFlash += 0.9f;

            //攥中血雾+血珠+崩烬
            for (int i = 0; i < 7; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 6f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f)
                    , vel, new Color(140, 18, 24), Main.rand.NextFloat(0.6f, 1f))
                    ?.Configure(Main.rand.Next(16, 28), 0.32f);
            }
            for (int i = 0; i < 9; i++) {
                PRTLoader.NewParticle<PRT_PallbearerEmber>(
                    Projectile.Center + Main.rand.NextVector2Circular(10f, 10f)
                    , Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f)
                    , new Color(212, 70, 26), Main.rand.NextFloat(0.5f, 0.95f))
                    ?.Configure(Main.rand.Next(14, 24), 0.05f);
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center + Main.rand.NextVector2Circular(12f, 12f)
                    , -Vector2.UnitY * Main.rand.NextFloat(0.4f, 1f)
                    , new Color(52, 26, 18), Main.rand.NextFloat(0.1f, 0.16f))
                    ?.Configure(Main.rand.Next(22, 36), 0.5f);
            }

            //攥中顿挫震屏（尊重配置），攥中 boss 更重
            if (CWRClientConfig.Instance.ScreenVibration && Main.LocalPlayer.active
                && Vector2.DistanceSquared(Main.LocalPlayer.Center, Projectile.Center) < 1200f * 1200f) {
                var modifier = new PunchCameraModifier(Projectile.Center, Main.rand.NextVector2Unit()
                    , bossCatch ? 6f : 4f, 5f, 9, 800f, FullName);
                Main.instance.CameraModifiers.Add(modifier);
            }
        }

        /// <summary>捏紧脉冲的碾轧反馈：闷响+火星+血珠</summary>
        private void PlaySqueezeFx(NPC target) {
            SoundEngine.PlaySound(SoundID.NPCHit54 with { Volume = 0.55f, Pitch = -0.68f }, target.Center);
            emberFlash += 0.8f;
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_PallbearerEmber>(
                    Projectile.Center + Main.rand.NextVector2Circular(9f, 9f)
                    , Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.2f, 3.4f)
                    , new Color(216, 76, 28), Main.rand.NextFloat(0.45f, 0.85f))
                    ?.Configure(Main.rand.Next(12, 22), 0.05f);
            }
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(
                    target.Center + Main.rand.NextVector2Circular(12f, 12f)
                    , Main.rand.NextVector2Circular(3f, 2.4f) - Vector2.UnitY * 1.6f
                    , new Color(126, 16, 20), Main.rand.NextFloat(0.5f, 0.9f))
                    ?.Configure(Main.rand.Next(16, 26), 0.3f);
            }
        }

        /// <summary>松手碾碎的大顿挫：重响+爆烬+震屏</summary>
        private void PlayCrushFx(NPC target) {
            SoundEngine.PlaySound(SoundID.DD2_SkeletonHurt with { Volume = 0.9f, Pitch = -0.85f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.45f, Pitch = -0.6f }, Projectile.Center);
            emberFlash += 1.2f;

            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2.5f, 6.5f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(target.Center + Main.rand.NextVector2Circular(10f, 10f)
                    , vel, new Color(132, 16, 22), Main.rand.NextFloat(0.6f, 1f))
                    ?.Configure(Main.rand.Next(18, 30), 0.32f);
            }
            for (int i = 0; i < 12; i++) {
                PRTLoader.NewParticle<PRT_PallbearerEmber>(
                    Projectile.Center + Main.rand.NextVector2Circular(10f, 10f)
                    , Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f)
                    , new Color(222, 82, 30), Main.rand.NextFloat(0.5f, 1f))
                    ?.Configure(Main.rand.Next(14, 26), 0.05f);
            }
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center + Main.rand.NextVector2Circular(14f, 14f)
                    , -Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.2f)
                    , new Color(56, 28, 20), Main.rand.NextFloat(0.11f, 0.17f))
                    ?.Configure(Main.rand.Next(24, 38), 0.5f);
            }

            if (CWRClientConfig.Instance.ScreenVibration && Main.LocalPlayer.active
                && Vector2.DistanceSquared(Main.LocalPlayer.Center, Projectile.Center) < 1200f * 1200f) {
                var modifier = new PunchCameraModifier(Projectile.Center, Main.rand.NextVector2Unit()
                    , target.boss ? 7f : 5.5f, 5f, 10, 900f, FullName);
                Main.instance.CameraModifiers.Add(modifier);
            }
        }

        //==== 运动 / IK ====

        private void MoveToPosition(Vector2 target, float speed) {
            Vector2 direction = target - Projectile.Center;
            float distance = direction.Length();
            if (distance > 4f) {
                direction.Normalize();
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, direction * distance * speed, 0.3f);
            }
            else {
                Projectile.velocity *= 0.9f;
            }
            Projectile.Center += Projectile.velocity;
        }

        private void UpdateArmIK() {
            Vector2 handPos = Projectile.Center;

            float targetDistance = Vector2.Distance(shoulderPos, handPos);
            if (targetDistance > MaxReach * 0.98f) {
                Vector2 direction = (handPos - shoulderPos).SafeNormalize(Vector2.Zero);
                handPos = shoulderPos + direction * MaxReach * 0.98f;
                Projectile.Center = handPos;
            }

            //FABRIK 前向手→肩
            armSegments[0] = handPos;
            for (int i = 1; i < ArmSegmentCount; i++) {
                Vector2 direction = (armSegments[i - 1] - (i == ArmSegmentCount - 1 ? shoulderPos : armSegments[i])).SafeNormalize(Vector2.Zero);
                float bendFactor = (float)Math.Sin(i / (float)ArmSegmentCount * MathHelper.Pi) * armTension;
                Vector2 perpendicular = new Vector2(-direction.Y, direction.X) * bendFactor * 16f * ownerDirection;
                armSegments[i] = armSegments[i - 1] - direction * SegmentLength + perpendicular;
            }
            //反向肩→手
            armSegments[ArmSegmentCount - 1] = shoulderPos;
            for (int i = ArmSegmentCount - 2; i >= 0; i--) {
                Vector2 direction = (armSegments[i] - armSegments[i + 1]).SafeNormalize(Vector2.Zero);
                float bendFactor = (float)Math.Sin(i / (float)ArmSegmentCount * MathHelper.Pi) * armTension;
                Vector2 perpendicular = new Vector2(-direction.Y, direction.X) * bendFactor * 16f * ownerDirection;
                armSegments[i] = armSegments[i + 1] + direction * SegmentLength + perpendicular;
            }
            Projectile.Center = armSegments[0];
        }

        /// <summary>骨节僵直歪扭的确定性种子，客户端间一致</summary>
        private float JointHash(int k, int j) {
            float h = MathF.Sin(Projectile.identity * 7.31f + k * 13.7f + j * 5.3f) * 43758.547f;
            return h - MathF.Floor(h);
        }

        /// <summary>
        /// 掌指前向解算：腕(armSegments[0]) → 掌根线(knuckleCenter±KnuckleOffsets) → 三节骨 → 爪尖。<br/>
        /// 每指长度不同、骨节带种子化歪扭，包拢角随各指有效包拢度
        /// </summary>
        private void UpdateFingers() {
            Vector2 handDir = (armSegments[0] - armSegments[1]).SafeNormalize(Vector2.UnitX);
            float handAng = handDir.ToRotation();
            Vector2 perp = new(-handDir.Y, handDir.X);
            knuckleCenter = armSegments[0] + handDir * PalmLength;

            for (int k = 0; k < 5; k++) {
                float curl = MathHelper.Clamp(fingerCurl + fingerCurlOffsets[k], -0.2f, 1.05f);
                float lenScale = 1f + (JointHash(k, 9) - 0.5f) * 0.16f;
                float total = FingerLengths[k] * lenScale;
                float spread = FingerSpread[k] * (1f - curl * 0.42f);
                float bendSign = FingerSpread[k] == 0f ? -0.55f : -MathF.Sign(FingerSpread[k]);
                float ang = handAng + spread;

                Vector2 p = knuckleCenter + perp * (KnuckleOffsets[k] * (1f - curl * 0.22f));
                fingerJoints[k, 0] = p;
                for (int j = 0; j < 3; j++) {
                    ang += (JointHash(k, j) - 0.5f) * 0.36f + bendSign * curl * (0.42f + j * 0.34f);
                    p += ang.ToRotationVector2() * (total * FingerSegFractions[j]);
                    fingerJoints[k, j + 1] = p;
                }
                //爪尖：额外硬弯的尖钩
                float clawAng = ang + bendSign * (0.42f + curl * 0.55f);
                fingerJoints[k, 4] = p + clawAng.ToRotationVector2() * (8f + total * 0.12f);
            }
        }

        //==== 视觉更新 ====

        private void UpdateVisuals() {
            float squeezeNow = State == HandState.Gripping ? SqueezePulse(GripDuration) : 0f;

            //待机偶发痉挛：骤然攥拢又松开
            if (State == HandState.Idle && --twitchCountdown <= 0) {
                twitchCountdown = 100 + (int)(Seed * 90f) + Main.rand.Next(70);
                twitchCurl = 0.55f;
                emberFlash += 0.6f;
            }
            twitchCurl *= 0.85f;

            float curlTarget = State switch {
                HandState.Idle => 0.24f + 0.05f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2.1f + Seed * 9f) + twitchCurl,
                HandState.Lunging => StateTimer < LungeDuration * 0.28f ? -0.10f : 0.06f,
                HandState.Gripping => 0.85f + squeezeNow * 0.15f,
                _ => 0.38f,
            };
            fingerCurl = MathHelper.Lerp(fingerCurl, curlTarget, State == HandState.Gripping ? 0.25f : 0.13f);

            //待机各指错拍揉捏
            for (int k = 0; k < 5; k++) {
                float idleKnead = State == HandState.Idle
                    ? (float)Math.Sin(Main.GlobalTimeWrappedHourly * 1.9f + k * 1.7f + Seed * 11f) * 0.07f
                    : 0f;
                fingerCurlOffsets[k] = MathHelper.Lerp(fingerCurlOffsets[k], idleKnead, 0.1f);
            }

            gripBlend = MathHelper.Lerp(gripBlend, State == HandState.Gripping ? 1f : 0f, 0.15f);

            //常驻近实心，只有退场才隐去
            float opacityTarget = State switch {
                HandState.Idle => 0.92f,
                HandState.Lunging => 1f,
                HandState.Gripping => 1f,
                HandState.Returning => 0.95f,
                _ => 0f,
            };
            opacitySmooth = MathHelper.Lerp(opacitySmooth, opacityTarget, 0.16f);
            float fadeIn = MathHelper.Clamp(visualAge / 14f, 0f, 1f);
            drawOpacity = MathHelper.Clamp(opacitySmooth * fadeIn, 0f, 1f);

            //余烬活性：待机呼吸，扑抓/攥握崩亮
            float emberTarget = State switch {
                HandState.Idle => 0.60f,
                HandState.Lunging => 1.15f,
                HandState.Gripping => 1.05f + squeezeNow * 0.55f,
                HandState.Returning => 0.75f,
                _ => 0.25f,
            };
            emberFlash *= 0.86f;
            emberSmooth = MathHelper.Lerp(emberSmooth, emberTarget + emberFlash, 0.2f);

            if (VaultUtils.isServer) {
                return;
            }

            if (visualAge == 2) {
                EmergenceFx();
            }

            //贴臂焦烟与剥落的余烬碎屑
            if (++wispTimer > 8 && drawOpacity > 0.1f) {
                wispTimer = 0;
                Vector2 pos = Vector2.Lerp(shoulderPos, Projectile.Center, Main.rand.NextFloat(0.25f, 0.95f));
                PRTLoader.NewParticle<PRT_Smoke>(pos + Main.rand.NextVector2Circular(6f, 6f)
                    , -Vector2.UnitY * Main.rand.NextFloat(0.3f, 0.8f)
                    , new Color(62, 32, 22) * drawOpacity, Main.rand.NextFloat(0.06f, 0.11f))
                    ?.Configure(Main.rand.Next(18, 30), 0.35f * drawOpacity);
                if (Main.rand.NextBool(3)) {
                    PRTLoader.NewParticle<PRT_PallbearerEmber>(pos + Main.rand.NextVector2Circular(5f, 5f)
                        , Main.rand.NextVector2Circular(0.6f, 0.6f) - Vector2.UnitY * Main.rand.NextFloat(0.2f, 0.7f)
                        , new Color(190, 58, 24) * drawOpacity, Main.rand.NextFloat(0.35f, 0.6f))
                        ?.Configure(Main.rand.Next(16, 26), 0.03f);
                }
            }

            //掌心余烬光，随活性起伏
            float pulse = 0.7f + 0.3f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 5f + Seed * 12f);
            Lighting.AddLight(Projectile.Center, new Vector3(0.38f, 0.11f, 0.04f) * pulse * emberSmooth * drawOpacity);
        }

        private bool IsTargetValid() {
            if (targetNPCID < 0 || targetNPCID >= Main.maxNPCs) {
                return false;
            }
            NPC target = Main.npc[targetNPCID];
            return target.active && target.type == targetNPCType && target.CanBeChasedBy();
        }

        internal bool TryApplyAuthorityGrip(ushort serial, int targetId, int targetType) {
            if (Main.netMode == NetmodeID.MultiplayerClient || !Projectile.active
                || State != HandState.Gripping || serial != gripSerial
                || !IsNewerGrip(serial, lastSeenGripSerial)
                || targetId != targetNPCID || targetType != targetNPCType
                || targetId < 0 || targetId >= Main.maxNPCs
                || authorityGripCommitted
                    && Main.GameUpdateCount - lastAuthorityGripTick < MinimumAuthorityGripInterval) {
                return false;
            }
            lastSeenGripSerial = serial;

            NPC target = Main.npc[targetId];
            //容差按目标体型放宽：手贴附在巨物近缘而非中心
            float handTolerance = 96f + MathF.Min(target.width, target.height) * 0.5f;
            int buffType = ModContent.BuffType<GhostGripDebuff>();
            if (!target.active || target.type != targetType || target.buffImmune[buffType]
                || !GhostHandAbility.CanGrab(target, Owner.Center, Projectile.owner)
                || Vector2.DistanceSquared(Projectile.Center, target.Center)
                    > handTolerance * handTolerance
                || !WraithAbilityService.TryResolve(Owner, GhostHandAbility.Key,
                    out WraithAbilityContext context)) {
                return false;
            }

            target.AddBuff(buffType, 8);
            int buffIndex = target.FindBuffIndex(buffType);
            if (buffIndex < 0) {
                return false;
            }
            Revival = context.Revival;
            if (!WraithAbilityService.TryCommitUse(in context)) {
                target.DelBuff(buffIndex);
                return false;
            }

            //结算帧冻结武器伤害快照，本轮碾轧全程沿用
            weaponDamageSnapshot = Math.Max(1, Owner.GetWeaponDamage(context.VesselItem));
            WraithMarks.Apply(target, WraithMark.Gripped, WraithMarks.GrippedTicks,
                context.Revival, Projectile.owner, WraithPlayer.GhostHandKey);
            authorityGripCommitted = true;
            lastAuthorityGripTick = Main.GameUpdateCount;
            gripAuthorized = true;
            return true;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer || drawOpacity < 0.05f) {
                return;
            }
            //残影散作焦烟与余烬
            for (int i = 0; i < 6; i++) {
                Vector2 pos = Vector2.Lerp(shoulderPos, Projectile.Center, i / 5f);
                PRTLoader.NewParticle<PRT_Smoke>(pos, -Vector2.UnitY * Main.rand.NextFloat(0.3f, 0.9f)
                    , new Color(58, 30, 20) * 0.7f, Main.rand.NextFloat(0.07f, 0.12f))
                    ?.Configure(Main.rand.Next(16, 26), 0.3f);
                if (Main.rand.NextBool(2)) {
                    PRTLoader.NewParticle<PRT_PallbearerEmber>(pos
                        , Main.rand.NextVector2Circular(1f, 1f) - Vector2.UnitY * 0.5f
                        , new Color(186, 56, 24), Main.rand.NextFloat(0.35f, 0.6f))
                        ?.Configure(Main.rand.Next(14, 22), 0.04f);
                }
            }
        }

        //==== 顶点绘制 ====

        /// <summary>条带 uv.x 段位：肩→腕 0~0.70，掌 0.70~0.84，指爪 0.84~1.0</summary>
        private const float ArmUMax = 0.70f;
        private const float PalmUMax = 0.84f;

        public override bool PreDraw(ref Color lightColor) {
            if (drawOpacity <= 0.01f) {
                return false;
            }
            Effect fx = EffectLoader.GhostHandSheath?.Value;
            Texture2D noise = CWRAsset.NoiseSoft01?.Value;
            if (fx == null || noise == null) {
                return false;
            }

            SpriteBatch sb = Main.spriteBatch;

            //掌心余烬底光：先入队再 End，垫在条带之下
            Texture2D glowTex = GlowTex?.Value;
            if (glowTex != null && drawOpacity > 0.05f) {
                float squeeze = State == HandState.Gripping ? SqueezePulse(GripDuration) : 0f;
                float glowStrength = 0.10f + gripBlend * 0.30f + squeeze * 0.18f;
                sb.Draw(glowTex, Projectile.Center - Main.screenPosition, null,
                    new Color(196, 58, 22, 0) * (glowStrength * drawOpacity),
                    0f, glowTex.Size() * 0.5f,
                    (0.42f + 0.22f * gripBlend + 0.10f * squeeze) * emberSmooth,
                    SpriteEffects.None, 0f);
            }
            sb.End();

            GraphicsDevice device = Main.instance.GraphicsDevice;
            BlendState prevBlend = device.BlendState;
            RasterizerState prevRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uOpacity"]?.SetValue(drawOpacity);
            fx.Parameters["uGrip"]?.SetValue(gripBlend);
            fx.Parameters["uSeed"]?.SetValue(Seed * 10f);
            fx.Parameters["uEmber"]?.SetValue(emberSmooth);
            fx.Parameters["uNoiseTex"]?.SetValue(noise);

            var armVerts = BuildArmStrip();
            var palmVerts = BuildPalmStrip();
            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, armVerts, 0, armVerts.Length - 2);
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, palmVerts, 0, palmVerts.Length - 2);
                for (int k = 0; k < 5; k++) {
                    var fingerVerts = BuildFingerStrip(k);
                    device.DrawUserPrimitives(PrimitiveType.TriangleStrip, fingerVerts, 0, fingerVerts.Length - 2);
                }
            }

            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        /// <summary>指节隆起包络</summary>
        private static float KnuckleBump(float x) => MathF.Exp(-x * x / 0.004f);

        /// <summary>
        /// 六节 IK 曲线 Catmull-Rom 加宽条带，肩(u=0)→腕(u=0.70)<br/>
        /// 中线低频扭结蠕动、种子化骨瘤与肘部隆块；上侧加宽烟向上散；臂中线 v 进顶点色 R
        /// </summary>
        private VertexPositionColorTexture[] BuildArmStrip() {
            const int sampleCount = 26;
            Span<Vector2> raw = stackalloc Vector2[sampleCount];
            Span<Vector2> pts = stackalloc Vector2[sampleCount];
            for (int i = 0; i < sampleCount; i++) {
                float t = i / (float)(sampleCount - 1);
                //armSegments[0]=手 ... [^1]=肩；条带 0=肩 → 1=腕
                float ft = (1f - t) * (ArmSegmentCount - 1);
                int i0 = (int)ft;
                int i1 = Math.Min(i0 + 1, ArmSegmentCount - 1);
                float frac = ft - i0;
                Vector2 p0 = armSegments[Math.Max(i0 - 1, 0)];
                Vector2 p1 = armSegments[i0];
                Vector2 p2 = armSegments[i1];
                Vector2 p3 = armSegments[Math.Min(i1 + 1, ArmSegmentCount - 1)];
                raw[i] = Vector2.CatmullRom(p0, p1, p2, p3, frac);
            }
            //扭结蠕动：两端固定，中段缓慢蠕行
            pts[0] = raw[0];
            pts[sampleCount - 1] = raw[sampleCount - 1];
            for (int i = 1; i < sampleCount - 1; i++) {
                float t = i / (float)(sampleCount - 1);
                Vector2 tangent = (raw[i + 1] - raw[i - 1]).SafeNormalize(Vector2.UnitX);
                Vector2 normal = new(-tangent.Y, tangent.X);
                float gnarl = MathF.Sin(t * 8.6f + Seed * 29f + Main.GlobalTimeWrappedHourly * 0.4f)
                    * 4.5f * MathF.Sin(t * MathHelper.Pi);
                pts[i] = raw[i] + normal * gnarl;
            }

            //宽度包络：肩根细 → 中段饱满带骨瘤 → 腕略收；攥握整体收紧
            float tighten = 1f - gripBlend * 0.16f;
            const float upBias = 0.30f;
            var verts = new VertexPositionColorTexture[sampleCount * 2];
            for (int i = 0; i < sampleCount; i++) {
                float t = i / (float)(sampleCount - 1);
                Vector2 tangent = i < sampleCount - 1
                    ? (pts[i + 1] - pts[i]).SafeNormalize(Vector2.UnitX)
                    : (pts[i] - pts[i - 1]).SafeNormalize(Vector2.UnitX);
                Vector2 normal = new(-tangent.Y, tangent.X);

                float knot = MathF.Sin(t * 21f + Seed * 37f) * 0.5f + 0.5f;
                float elbow = MathF.Exp(-(t - 0.46f) * (t - 0.46f) / 0.006f);
                float width = (MathHelper.Lerp(10f, 9f, t)
                    + MathF.Sin(t * MathHelper.Pi) * 5.5f
                    + knot * knot * 3.2f + elbow * 2.5f) * tighten;
                float upDot = Vector2.Dot(-Vector2.UnitY, normal);
                float w0 = width * (1f + upBias * upDot);
                float w1 = width * (1f - upBias * upDot);
                Color vCenter = new(w0 / (w0 + w1), 0f, 0f);

                float u = t * ArmUMax;
                verts[i * 2] = new VertexPositionColorTexture((pts[i] + normal * w0).ToVector3()
                    , vCenter, new Vector2(u, 0f));
                verts[i * 2 + 1] = new VertexPositionColorTexture((pts[i] - normal * w1).ToVector3()
                    , vCenter, new Vector2(u, 1f));
            }
            return verts;
        }

        /// <summary>手掌条带，腕(u=0.70)→掌根线(u=0.84)：腕口收窄向指根线展开成掌</summary>
        private VertexPositionColorTexture[] BuildPalmStrip() {
            const int sampleCount = 6;
            Vector2 root = armSegments[0];
            Vector2 axis = knuckleCenter - root;
            Vector2 dir = axis.SafeNormalize(Vector2.UnitX);
            Vector2 normal = new(-dir.Y, dir.X);
            var verts = new VertexPositionColorTexture[sampleCount * 2];
            Color vCenter = new(0.5f, 0f, 0f);
            for (int i = 0; i < sampleCount; i++) {
                float t = i / (float)(sampleCount - 1);
                Vector2 p = root + axis * t;
                float half = MathHelper.Lerp(9f, 16f, t) * (1f - gripBlend * 0.10f);
                float u = ArmUMax + t * (PalmUMax - ArmUMax);
                verts[i * 2] = new VertexPositionColorTexture((p + normal * half).ToVector3()
                    , vCenter, new Vector2(u, 0f));
                verts[i * 2 + 1] = new VertexPositionColorTexture((p - normal * half).ToVector3()
                    , vCenter, new Vector2(u, 1f));
            }
            return verts;
        }

        /// <summary>
        /// 单指条带，掌根(u=0.84)→爪尖(u=1.0)：三节骨带指节隆起，末段收成锐利硬爪；<br/>
        /// 顶点色 G 通道 0=焦肉 → 1=爪面，交由 shader 换质
        /// </summary>
        private VertexPositionColorTexture[] BuildFingerStrip(int k) {
            const int sampleCount = 11;
            Span<Vector2> pts = stackalloc Vector2[sampleCount];
            for (int i = 0; i < sampleCount; i++) {
                float t = i / (float)(sampleCount - 1);
                float ft = t * 4f;
                int i0 = Math.Min((int)ft, 3);
                float frac = ft - i0;
                Vector2 p0 = fingerJoints[k, Math.Max(i0 - 1, 0)];
                Vector2 p1 = fingerJoints[k, i0];
                Vector2 p2 = fingerJoints[k, i0 + 1];
                Vector2 p3 = fingerJoints[k, Math.Min(i0 + 2, 4)];
                pts[i] = Vector2.CatmullRom(p0, p1, p2, p3, frac);
            }

            var verts = new VertexPositionColorTexture[sampleCount * 2];
            for (int i = 0; i < sampleCount; i++) {
                float t = i / (float)(sampleCount - 1);
                Vector2 tangent = i < sampleCount - 1
                    ? (pts[i + 1] - pts[i]).SafeNormalize(Vector2.UnitX)
                    : (pts[i] - pts[i - 1]).SafeNormalize(Vector2.UnitX);
                Vector2 normal = new(-tangent.Y, tangent.X);

                float width;
                if (t < 0.75f) {
                    float bump = KnuckleBump(t - 0.25f) + KnuckleBump(t - 0.5f) + KnuckleBump(t - 0.75f);
                    width = MathHelper.Lerp(5.8f, 3.4f, t / 0.75f) * (1f + bump * 0.42f);
                }
                else {
                    width = MathHelper.Lerp(3.2f, 0.6f, (t - 0.75f) / 0.25f);
                }

                Color vCol = new(0.5f, MathHelper.Clamp((t - 0.70f) / 0.16f, 0f, 1f), 0f);
                float u = PalmUMax + t * (1f - PalmUMax);
                verts[i * 2] = new VertexPositionColorTexture((pts[i] + normal * width).ToVector3()
                    , vCol, new Vector2(u, 0f));
                verts[i * 2 + 1] = new VertexPositionColorTexture((pts[i] - normal * width).ToVector3()
                    , vCol, new Vector2(u, 1f));
            }
            return verts;
        }
    }
}
