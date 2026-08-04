using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Wraiths.Abilities;
using CalamityOverhaul.Content.Wraiths.Buffs;
using CalamityOverhaul.Content.Wraiths.Core;
using CalamityOverhaul.Content.Wraiths.Runtime;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
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
    /// 鬼手本体，纯顶点绘制无贴图。常驻循环：背后待机 → 扑抓 → 攥握压制 → 回位；<br/>
    /// 失去当前役鬼资格后无害退场。ai[0]=状态 ai[1]=计时 ai[2]=驾驭度；
    /// 目标与朝向走 SendExtraAI，索敌和退场由 owner 决策后同步
    /// </summary>
    internal sealed class GhostHandProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

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
        /// <summary>驾驭度 0~1，生成时传入，攥握时长插值</summary>
        private ref float Mastery => ref Projectile.ai[2];

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

        //==== IK 手臂 ====
        private const int ArmSegmentCount = 6;
        private const float SegmentLength = 52f;
        private const float MaxReach = ArmSegmentCount * SegmentLength;
        private readonly Vector2[] armSegments = new Vector2[ArmSegmentCount];
        private Vector2 shoulderPos;
        private float armTension;

        //==== 五指（前向链，2 节）====
        private static readonly float[] FingerSpread = [-1.222f, -0.611f, 0f, 0.611f, 1.222f];   //±70°/±35°/0°
        private const float FingerLen0 = 16f;
        private const float FingerLen1 = 13f;
        //[指, 关节] 0=掌根 1=中节 2=指尖
        private readonly Vector2[,] fingerJoints = new Vector2[5, 3];

        //==== 时长 ====
        private const int InitialScanDelay = 18;
        private const int ReacquireDelay = 45;
        private const int LungeDuration = 20;
        private const int ReturnDuration = 20;
        private const int DismissDuration = 18;
        private const int MinimumAuthorityGripInterval = ReacquireDelay + LungeDuration + ReturnDuration;
        private int GripDuration => (int)MathHelper.Lerp(60f, 120f, MathHelper.Clamp(Mastery, 0f, 1f));

        //==== 视觉状态（本地平滑）====
        private HandState prevState = HandState.Idle;
        private int visualAge;
        private int reacquireTimer;
        private float fingerCurl;
        private float gripBlend;
        private float opacitySmooth;
        private float drawOpacity;
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
            UpdateShoulderPosition();
            Projectile.Center = IdleHoverPosition();
            for (int i = 0; i < ArmSegmentCount; i++) {
                armSegments[i] = Vector2.Lerp(Projectile.Center, shoulderPos, i / (float)(ArmSegmentCount - 1));
            }

            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 10; i++) {
                Vector2 pos = shoulderPos + new Vector2(Main.rand.NextFloat(-14f, 14f), Main.rand.NextFloat(-16f, 10f));
                Vector2 velocity = new(
                    Main.rand.NextFloat(-0.6f, 0.6f) - Owner.direction * 0.4f,
                    Main.rand.NextFloat(-1.3f, -0.4f));
                Color color = Color.Lerp(new Color(96, 12, 18), new Color(150, 22, 30), Main.rand.NextFloat());
                PRTLoader.NewParticle<PRT_Smoke>(pos, velocity, color, Main.rand.NextFloat(0.09f, 0.15f))
                    ?.Configure(Main.rand.Next(26, 44), Main.rand.NextFloat(0.4f, 0.65f),
                        Main.rand.NextFloat(-0.02f, 0.02f));
            }
            SoundEngine.PlaySound(SoundID.NPCDeath6 with { Pitch = -0.85f, Volume = 0.4f }, Owner.Center);
            SoundEngine.PlaySound(SoundID.Item32 with { Pitch = -0.7f, Volume = 0.3f }, Owner.Center);
        }

        /// <summary>自管位移</summary>
        public override bool ShouldUpdatePosition() => false;

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write((short)targetNPCID);
            writer.Write(targetNPCType);
            writer.Write((sbyte)ownerDirection);
            writer.Write(gripSerial);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            targetNPCID = reader.ReadInt16();
            targetNPCType = reader.ReadInt32();
            ownerDirection = reader.ReadSByte();
            gripSerial = reader.ReadUInt16();
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
            shoulderPos = Owner.Center + new Vector2(-ownerDirection * 28f, -8f);
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
            return shoulderPos + new Vector2(-ownerDirection * 34f, -52f) + bob;
        }

        private void IdleBehavior() {
            armTension = 0.3f;
            MoveToPosition(IdleHoverPosition(), 0.12f);

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
                Mastery = context.Mastery;
                targetNPCID = target.whoAmI;
                targetNPCType = target.type;
                Transition(HandState.Lunging);
            }
        }

        private NPC FindGrabTarget() {
            NPC best = null;
            float bestSq = float.MaxValue;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!GhostHandAbility.CanGrab(npc, Owner.Center)) {
                    continue;
                }
                float distSq = Vector2.DistanceSquared(npc.Center, Owner.Center);
                if (distSq < bestSq) {
                    bestSq = distSq;
                    best = npc;
                }
            }
            return best;
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
                //前摇回拉蓄势
                float back = VaultUtils.EaseOutCubic(t / 0.28f);
                Vector2 windup = lungeStart + (shoulderPos - lungeStart).SafeNormalize(Vector2.Zero) * 24f * back
                    - Vector2.UnitY * 10f * back;
                next = windup;
            }
            else {
                //鞭击突进，末端全速扣向目标
                float strike = MathF.Pow((t - 0.28f) / 0.72f, 2.4f);
                next = Vector2.Lerp(lungeStart, target.Center, strike);
            }
            Projectile.velocity = next - Projectile.Center;
            Projectile.Center = next;

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

            //拖出臂展被扯脱
            if (Vector2.Distance(shoulderPos, target.Center) > MaxReach * 1.08f) {
                Transition(HandState.Returning);
                return;
            }

            //钉在目标身上，微幅使劲颤动
            Vector2 pin = target.Center + Main.rand.NextVector2Circular(1.4f, 1.4f);
            Projectile.velocity = pin - Projectile.Center;
            Projectile.Center = pin;

            if (!gripCommitAttempted && Projectile.IsOwnedByLocalPlayer()) {
                gripCommitAttempted = true;
                WraithNet.RequestGhostHandGrip(Projectile, gripSerial,
                    targetNPCID, targetNPCType);
            }

            //权威端滚动续期，8 帧短债松手即断
            if (!VaultUtils.isClient && gripAuthorized) {
                target.AddBuff(ModContent.BuffType<GhostGripDebuff>(), 8);
            }

            int duration = GripDuration;
            float squeeze = SqueezePulse(duration);

            if (!VaultUtils.isServer) {
                //攥紧顿挫，闷响+挤出血珠
                if (squeeze > 0.9f && (int)StateTimer % 3 == 0) {
                    SoundEngine.PlaySound(SoundID.NPCHit54 with { Volume = 0.38f, Pitch = -0.55f }, target.Center);
                }
                if (squeeze > 0.6f && Main.rand.NextBool(2)) {
                    PRTLoader.NewParticle<PRT_HeartcarverDroplet>(
                        target.Center + Main.rand.NextVector2Circular(10f, 10f)
                        , Main.rand.NextVector2Circular(2.6f, 2f) - Vector2.UnitY * 1.4f
                        , new Color(120, 15, 20), Main.rand.NextFloat(0.5f, 0.85f))
                        ?.Configure(Main.rand.Next(16, 26), 0.3f);
                }
            }

            if (StateTimer >= duration) {
                Transition(HandState.Returning);
            }
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
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.5f, Pitch = -0.5f }, Projectile.Center);
                        for (int i = 0; i < 5; i++) {
                            PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f)
                                , Main.rand.NextVector2Circular(1.2f, 1.2f)
                                , new Color(96, 12, 18), Main.rand.NextFloat(0.08f, 0.13f))
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

        private void PlayCatchFx() {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.DD2_SkeletonHurt with { Volume = 0.75f, Pitch = -0.6f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item32 with { Volume = 0.5f, Pitch = -0.45f }, Projectile.Center);

            //攥中血雾+血珠
            for (int i = 0; i < 7; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 6f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f)
                    , vel, new Color(140, 18, 24), Main.rand.NextFloat(0.6f, 1f))
                    ?.Configure(Main.rand.Next(16, 28), 0.32f);
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center + Main.rand.NextVector2Circular(12f, 12f)
                    , -Vector2.UnitY * Main.rand.NextFloat(0.4f, 1f)
                    , new Color(70, 10, 14), Main.rand.NextFloat(0.1f, 0.16f))
                    ?.Configure(Main.rand.Next(22, 36), 0.5f);
            }

            //攥中顿挫震屏（尊重配置）
            if (CWRServerConfig.Instance.ScreenVibration && Main.LocalPlayer.active
                && Vector2.DistanceSquared(Main.LocalPlayer.Center, Projectile.Center) < 1200f * 1200f) {
                var modifier = new PunchCameraModifier(Projectile.Center, Main.rand.NextVector2Unit()
                    , 4f, 5f, 9, 800f, FullName);
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

        /// <summary>指链前向解算，掌根=armSegments[0]，包拢角随 fingerCurl</summary>
        private void UpdateFingers() {
            Vector2 handDir = (armSegments[0] - armSegments[1]).SafeNormalize(Vector2.UnitX);
            float handAng = handDir.ToRotation();

            for (int k = 0; k < 5; k++) {
                float spread = FingerSpread[k] * (1f - fingerCurl * 0.45f);
                float a0 = handAng + spread;
                //包拢向轴心弯，中指小幅偏折
                float bendSign = FingerSpread[k] == 0f ? -0.5f : -MathF.Sign(FingerSpread[k]);
                float a1 = a0 + bendSign * fingerCurl * 1.25f;

                Vector2 j0 = armSegments[0];
                Vector2 j1 = j0 + a0.ToRotationVector2() * FingerLen0;
                Vector2 j2 = j1 + a1.ToRotationVector2() * FingerLen1;
                fingerJoints[k, 0] = j0;
                fingerJoints[k, 1] = j1;
                fingerJoints[k, 2] = j2;
            }
        }

        //==== 视觉更新 ====

        private void UpdateVisuals() {
            float curlTarget = State switch {
                HandState.Idle => 0.22f + 0.06f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2.3f + Seed * 9f),
                HandState.Lunging => 0.05f,
                HandState.Gripping => 0.85f + SqueezePulse(GripDuration) * 0.15f,
                _ => 0.35f,
            };
            fingerCurl = MathHelper.Lerp(fingerCurl, curlTarget, State == HandState.Gripping ? 0.22f : 0.12f);

            gripBlend = MathHelper.Lerp(gripBlend, State == HandState.Gripping ? 1f : 0f, 0.15f);

            float opacityTarget = State switch {
                HandState.Idle => 0.35f,
                HandState.Lunging => 0.85f,
                HandState.Gripping => 0.9f,
                HandState.Returning => 0.55f,
                _ => 0f,
            };
            opacitySmooth = MathHelper.Lerp(opacitySmooth, opacityTarget, 0.16f);
            float fadeIn = MathHelper.Clamp(visualAge / 14f, 0f, 1f);
            drawOpacity = MathHelper.Clamp(opacitySmooth * fadeIn, 0f, 1f);

            if (VaultUtils.isServer) {
                return;
            }

            //贴臂血烟点缀
            if (++wispTimer > 8 && drawOpacity > 0.1f) {
                wispTimer = 0;
                Vector2 pos = Vector2.Lerp(shoulderPos, Projectile.Center, Main.rand.NextFloat(0.25f, 0.95f));
                PRTLoader.NewParticle<PRT_Smoke>(pos + Main.rand.NextVector2Circular(6f, 6f)
                    , -Vector2.UnitY * Main.rand.NextFloat(0.3f, 0.8f)
                    , new Color(80, 10, 16) * drawOpacity, Main.rand.NextFloat(0.06f, 0.11f))
                    ?.Configure(Main.rand.Next(18, 30), 0.35f * drawOpacity);
            }

            //掌心幽暗血光
            float pulse = 0.7f + 0.3f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 5f + Seed * 12f);
            Lighting.AddLight(Projectile.Center, new Vector3(0.16f, 0.03f, 0.04f) * pulse * drawOpacity * 2f);
        }

        private bool IsTargetValid() {
            if (targetNPCID < 0 || targetNPCID >= Main.maxNPCs) {
                return false;
            }
            NPC target = Main.npc[targetNPCID];
            return target.active && target.type == targetNPCType
                && target.CanBeChasedBy() && !target.boss;
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
            const float handTolerance = 96f;
            int buffType = ModContent.BuffType<GhostGripDebuff>();
            if (!target.active || target.type != targetType || target.buffImmune[buffType]
                || !GhostHandAbility.CanGrab(target, Owner.Center)
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
            Mastery = context.Mastery;
            if (!WraithAbilityService.TryCommitUse(in context)) {
                target.DelBuff(buffIndex);
                return false;
            }

            authorityGripCommitted = true;
            lastAuthorityGripTick = Main.GameUpdateCount;
            gripAuthorized = true;
            return true;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer || drawOpacity < 0.05f) {
                return;
            }
            //残影散烟
            for (int i = 0; i < 6; i++) {
                Vector2 pos = Vector2.Lerp(shoulderPos, Projectile.Center, i / 5f);
                PRTLoader.NewParticle<PRT_Smoke>(pos, -Vector2.UnitY * Main.rand.NextFloat(0.3f, 0.9f)
                    , new Color(80, 10, 16) * 0.7f, Main.rand.NextFloat(0.07f, 0.12f))
                    ?.Configure(Main.rand.Next(16, 26), 0.3f);
            }
        }

        //==== 顶点绘制 ====

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
            fx.Parameters["uNoiseTex"]?.SetValue(noise);

            var armVerts = BuildArmStrip();
            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, armVerts, 0, armVerts.Length - 2);
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

        /// <summary>主臂 uv.x 段位</summary>
        private const float ArmUMax = 0.80f;

        /// <summary>
        /// 六节 IK 曲线 Catmull-Rom 加宽条带，肩(u=0)→腕(u=0.80)<br/>
        /// 上侧加宽烟向上散；臂中线 v 进顶点色 R
        /// </summary>
        private VertexPositionColorTexture[] BuildArmStrip() {
            const int sampleCount = 26;
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
                pts[i] = Vector2.CatmullRom(p0, p1, p2, p3, frac);
            }

            //宽度包络：肩根细 → 中段饱满 → 腕略收；攥握整体收紧
            float tighten = 1f - gripBlend * 0.16f;
            const float upBias = 0.30f;
            var verts = new VertexPositionColorTexture[sampleCount * 2];
            for (int i = 0; i < sampleCount; i++) {
                float t = i / (float)(sampleCount - 1);
                Vector2 tangent = i < sampleCount - 1
                    ? (pts[i + 1] - pts[i]).SafeNormalize(Vector2.UnitX)
                    : (pts[i] - pts[i - 1]).SafeNormalize(Vector2.UnitX);
                Vector2 normal = new(-tangent.Y, tangent.X);

                float width = (MathHelper.Lerp(7f, 9.5f, t)
                    + (float)Math.Sin(t * MathHelper.Pi) * 5f) * tighten;
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

        /// <summary>单指条带，腕(u=0.80)→指尖(u=1.0)，尖端由 shader 撕散</summary>
        private VertexPositionColorTexture[] BuildFingerStrip(int k) {
            const int sampleCount = 7;
            Span<Vector2> pts = stackalloc Vector2[sampleCount];
            for (int i = 0; i < sampleCount; i++) {
                float t = i / (float)(sampleCount - 1);
                float ft = t * 2f;
                int i0 = Math.Min((int)ft, 1);
                float frac = ft - i0;
                Vector2 p0 = fingerJoints[k, Math.Max(i0 - 1, 0)];
                Vector2 p1 = fingerJoints[k, i0];
                Vector2 p2 = fingerJoints[k, i0 + 1];
                Vector2 p3 = fingerJoints[k, Math.Min(i0 + 2, 2)];
                pts[i] = Vector2.CatmullRom(p0, p1, p2, p3, frac);
            }

            var verts = new VertexPositionColorTexture[sampleCount * 2];
            Color vCenter = new(0.5f, 0f, 0f);
            for (int i = 0; i < sampleCount; i++) {
                float t = i / (float)(sampleCount - 1);
                Vector2 tangent = i < sampleCount - 1
                    ? (pts[i + 1] - pts[i]).SafeNormalize(Vector2.UnitX)
                    : (pts[i] - pts[i - 1]).SafeNormalize(Vector2.UnitX);
                Vector2 normal = new(-tangent.Y, tangent.X);

                float width = MathHelper.Lerp(5.2f, 1.8f, t);
                float u = ArmUMax + t * (1f - ArmUMax);
                verts[i * 2] = new VertexPositionColorTexture((pts[i] + normal * width).ToVector3()
                    , vCenter, new Vector2(u, 0f));
                verts[i * 2 + 1] = new VertexPositionColorTexture((pts[i] - normal * width).ToVector3()
                    , vCenter, new Vector2(u, 1f));
            }
            return verts;
        }
    }
}
