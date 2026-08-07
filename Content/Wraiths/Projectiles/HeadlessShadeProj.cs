using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniDismembers;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Wraiths.Abilities;
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
    /// 无头鬼影役鬼体。ai[0]=状态，ai[1]=状态计时，ai[2]=驾驭度；
    /// 目标、锁定点与冲刺路径通过 ExtraAI 同步，命中由权威端复核结算。
    /// 表现层是"影"：本体走骨架条带 + 地面投影，穿体时本体熄灭、由斩痕承担行程。
    /// </summary>
    internal sealed class HeadlessShadeProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private enum ShadeState
        {
            Idle,
            Stalking,
            DashCharge,
            Dashing,
            Recovering,
            Dismissing
        }

        private ref float StateRaw => ref Projectile.ai[0];
        private ref float StateTimer => ref Projectile.ai[1];
        private ref float Mastery => ref Projectile.ai[2];

        private ShadeState State {
            get => (ShadeState)StateRaw;
            set => StateRaw = (float)value;
        }

        private Player Owner => Main.player[Projectile.owner];

        private const int InitialScanDelay = 24;
        private const int StalkDuration = 20;
        private const int ChargeDuration = 16;
        /// <summary>蓄力尾段完全定住的帧数，静止谷是给爆发让位</summary>
        private const int ChargeStillFrames = 3;
        private const int DashDuration = 8;
        private const int RecoverDuration = 24;
        private const int DismissDuration = 18;
        private const int ImpactHoldFrames = 6;
        private const int BossVisualDuration = 38;
        private const float ImpactRadiusPadding = 24f;
        private const float BodyDrawSize = 184f;
        /// <summary>本体重新亮起的帧序号（穿体期熄灭）</summary>
        private const int RematerializeFrame = 5;
        /// <summary>交叉刀补刀间隔</summary>
        private const int CrossCutInterval = 3;

        //收-爆-停：1 帧死寂 → 1 帧到位 → 过冲 → 硬停 → 静止谷
        private static readonly float[] DashTravel = [0f, 0.06f, 0.88f, 1.06f, 1f, 1f, 1f, 1f, 1f];

        private int targetNPCID = -1;
        private int targetNPCType = -1;
        private int ownerDirection = 1;
        private int reacquireTimer;
        private int visualAge;
        private int wispTimer;
        private int pendingDamageTicks;
        private ushort impactSerial;
        private ushort processedImpactSerial;
        private ushort lastSeenImpactSerial;
        private bool impactEventPending;
        private bool pendingDamage;
        private bool strikeResolved;
        private bool strikeInvalidated;

        private ShadeState previousState = ShadeState.Idle;
        private Vector2 stalkStart;
        private Vector2 chargeStart;
        private Vector2 chargeHold;
        private Vector2 dashOrigin;
        private Vector2 dashEnd;
        private Vector2 dashDirection = Vector2.UnitX;
        private Vector2 lockedTargetCenter;
        private Vector2 impactCenter;
        private Vector2 recoverStart;
        private Vector2 dismissStart;

        //补刀调度：主刀落下后再撕两道交叉口，滞拍归到同一帧一起崩
        private int crossCutStep;
        private int crossCutTimer;
        private float crossAngleA;
        private float crossAngleB;
        private Vector2 crossPointA;
        private Vector2 crossPointB;

        private float opacitySmooth;
        private float dissolveSmooth = 1f;
        private float phaseSmooth;
        private float stretchSmooth;
        private float drawOpacity;
        private float bodyVeil = 1f;
        private float rimFlash;
        private int twitchCountdown;
        private int twitchHold;
        private Vector2 twitchOffset;

        private ShadeStrikeField strikeField;
        private HeadlessShadeRig rig;

        private float Seed => Projectile.identity * 0.173f % 1f;
        private float BodyScale => MathHelper.Lerp(0.92f, 1.08f, MathHelper.Clamp(Mastery, 0f, 1f));
        private int ReacquireDelay => (int)MathHelper.Lerp(105f, 72f, MathHelper.Clamp(Mastery, 0f, 1f));

        public override void SetStaticDefaults()
            => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 620;

        public override void SetDefaults() {
            Projectile.width = 54;
            Projectile.height = 104;
            Projectile.aiStyle = -1;
            Projectile.DamageType = CWRRef.GetTrueMeleeDamageClass();
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
            Projectile.Center = IdleHoverPosition();

            if (Main.dedServ) {
                return;
            }

            strikeField = new ShadeStrikeField();
            rig = new HeadlessShadeRig();
            rig.SetSeed(Seed);
            SoundEngine.PlaySound(SoundID.NPCDeath6 with {
                Volume = 0.42f,
                Pitch = -0.92f,
                MaxInstances = 3
            }, Projectile.Center);
            for (int i = 0; i < 9; i++) {
                Vector2 pos = Projectile.Center + Main.rand.NextVector2Circular(28f, 48f);
                Vector2 velocity = new(Main.rand.NextFloat(-0.7f, 0.7f), Main.rand.NextFloat(-1.2f, -0.25f));
                PRTLoader.NewParticle<PRT_Smoke>(pos, velocity, new Color(18, 17, 23), Main.rand.NextFloat(0.08f, 0.14f))
                    ?.Configure(Main.rand.Next(24, 40), Main.rand.NextFloat(0.28f, 0.48f),
                        Main.rand.NextFloat(-0.018f, 0.018f));
            }
        }

        public override bool ShouldUpdatePosition() => false;

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write((short)targetNPCID);
            writer.Write(targetNPCType);
            writer.Write((sbyte)ownerDirection);
            writer.Write(dashOrigin.X);
            writer.Write(dashOrigin.Y);
            writer.Write(dashEnd.X);
            writer.Write(dashEnd.Y);
            writer.Write(dashDirection.X);
            writer.Write(dashDirection.Y);
            writer.Write(lockedTargetCenter.X);
            writer.Write(lockedTargetCenter.Y);
            writer.Write(impactSerial);
            writer.Write(impactCenter.X);
            writer.Write(impactCenter.Y);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            targetNPCID = reader.ReadInt16();
            targetNPCType = reader.ReadInt32();
            ownerDirection = reader.ReadSByte();
            dashOrigin = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            dashEnd = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            dashDirection = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            lockedTargetCenter = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            ushort incomingImpactSerial = reader.ReadUInt16();
            Vector2 incomingImpactCenter = new(reader.ReadSingle(), reader.ReadSingle());
            if (IsNewerImpact(incomingImpactSerial, impactSerial)) {
                impactSerial = incomingImpactSerial;
                impactCenter = incomingImpactCenter;
                impactEventPending = true;
                strikeResolved = true;
            }
        }

        private static bool IsNewerImpact(ushort incoming, ushort current)
            => incoming != current && (ushort)(incoming - current) < 0x8000;

        public override void AI() {
            if (!Owner.active) {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 2;
            bool finishingStrike = State is ShadeState.DashCharge
                or ShadeState.Dashing or ShadeState.Recovering;
            if (State != ShadeState.Dismissing && Owner.dead) {
                Transition(ShadeState.Dismissing);
            }
            else if (State != ShadeState.Dismissing && Projectile.IsOwnedByLocalPlayer()
                && !HasValidAbility()) {
                if (finishingStrike) {
                    strikeInvalidated = true;
                }
                else {
                    Transition(ShadeState.Dismissing);
                }
            }

            visualAge++;
            StateTimer++;

            if (State != previousState) {
                OnStateChanged(State);
                previousState = State;
            }

            if (!Main.dedServ) {
                strikeField ??= new ShadeStrikeField();
            }

            ProcessImpactEvent();
            UpdateCrossCuts();
            UpdatePendingDamage();

            switch (State) {
                case ShadeState.Idle:
                    IdleBehavior();
                    break;
                case ShadeState.Stalking:
                    StalkingBehavior();
                    break;
                case ShadeState.DashCharge:
                    DashChargeBehavior();
                    break;
                case ShadeState.Dashing:
                    DashingBehavior();
                    break;
                case ShadeState.Recovering:
                    RecoveringBehavior();
                    break;
                case ShadeState.Dismissing:
                    DismissingBehavior();
                    break;
            }

            strikeField?.Update();
            UpdateVisuals();
        }

        private bool HasValidAbility()
            => WraithAbilityService.TryResolve(Owner, HeadlessShadeAbility.Key, out _);

        private void Transition(ShadeState next) {
            if (State == next) {
                return;
            }

            State = next;
            StateTimer = 0f;
            OnStateChanged(next);
            previousState = next;
            if (Projectile.IsOwnedByLocalPlayer()) {
                Projectile.netUpdate = true;
            }
        }

        private Vector2 IdleHoverPosition() {
            float time = Main.GlobalTimeWrappedHourly + Seed * MathHelper.TwoPi;
            Vector2 bob = new(MathF.Sin(time * 1.25f) * 7f, MathF.Cos(time * 1.7f) * 5f);
            return Owner.Center + new Vector2(-ownerDirection * 48f, -48f) + bob + twitchOffset;
        }

        /// <summary>影子的抽动：偶尔错开一小段再收回，而不是匀速漂浮</summary>
        private void UpdateTwitch() {
            if (--twitchCountdown <= 0) {
                twitchCountdown = Main.rand.Next(72, 165);
                twitchOffset = Main.rand.NextVector2Circular(10f, 6f);
                twitchHold = 3;
            }
            if (twitchHold > 0) {
                twitchHold--;
                return;
            }
            twitchOffset = Vector2.Lerp(twitchOffset, Vector2.Zero, 0.22f);
        }

        private void IdleBehavior() {
            ownerDirection = Owner.direction;
            UpdateTwitch();
            MoveToward(IdleHoverPosition(), 0.12f, 12f);

            if (reacquireTimer > 0) {
                reacquireTimer--;
                return;
            }
            if (!Projectile.IsOwnedByLocalPlayer() || StateTimer < InitialScanDelay || (int)StateTimer % 6 != 0) {
                return;
            }
            if (!WraithAbilityService.TryResolve(Owner, HeadlessShadeAbility.Key,
                out WraithAbilityContext context)) {
                Transition(ShadeState.Dismissing);
                return;
            }

            NPC target = FindTarget();
            if (target == null) {
                return;
            }

            Mastery = context.Mastery;
            strikeInvalidated = false;
            targetNPCID = target.whoAmI;
            targetNPCType = target.type;
            Transition(ShadeState.Stalking);
        }

        private void StalkingBehavior() {
            NPC target = GetTarget(requireHuntRange: true);
            if (target == null) {
                Transition(ShadeState.Recovering);
                return;
            }

            float progress = MathHelper.Clamp(StateTimer / StalkDuration, 0f, 1f);
            float eased = VaultUtils.EaseOutCubic(progress);
            Vector2 retreatSide = (Owner.Center - target.Center)
                .SafeNormalize(new Vector2(-ownerDirection, 0f));
            float standOff = MathHelper.Clamp(target.Size.Length() * 0.45f + 105f, 130f, 195f);
            Vector2 staging = target.Center + retreatSide * standOff - Vector2.UnitY * 32f;
            SetCenter(Vector2.Lerp(stalkStart, staging, eased));

            if (StateTimer >= StalkDuration) {
                lockedTargetCenter = target.Center;
                Transition(ShadeState.DashCharge);
            }
        }

        private void DashChargeBehavior() {
            NPC target = GetTarget(requireHuntRange: true);
            if (target == null) {
                Transition(ShadeState.Recovering);
                return;
            }

            //蓄力压到 ChargeStillFrames 之前为止，剩下几帧完全定住——静止谷框住爆发
            int compressFrames = ChargeDuration - ChargeStillFrames;
            if (StateTimer <= compressFrames) {
                float progress = MathHelper.Clamp(StateTimer / compressFrames, 0f, 1f);
                float compression = progress * progress * progress;
                Vector2 aim = (target.Center - chargeStart).SafeNormalize(new Vector2(ownerDirection, 0f));
                chargeHold = chargeStart - aim * (44f * compression)
                    + Vector2.UnitY * (6f * MathF.Sin(progress * MathHelper.Pi));
            }
            SetCenter(chargeHold);

            if (StateTimer < ChargeDuration) {
                return;
            }

            dashOrigin = Projectile.Center;
            lockedTargetCenter = target.Center;
            dashDirection = (lockedTargetCenter - dashOrigin).SafeNormalize(new Vector2(ownerDirection, 0f));
            if (MathF.Abs(dashDirection.X) > 0.08f) {
                ownerDirection = dashDirection.X >= 0f ? 1 : -1;
            }
            float overrun = MathHelper.Clamp(MathF.Max(target.width, target.height) * 0.65f + 105f, 120f, 230f);
            dashEnd = lockedTargetCenter + dashDirection * overrun;
            Transition(ShadeState.Dashing);
        }

        private void DashingBehavior() {
            NPC target = strikeResolved ? null : GetTarget(requireHuntRange: false);
            if (!strikeResolved && target == null && Projectile.IsOwnedByLocalPlayer()) {
                Transition(ShadeState.Recovering);
                return;
            }

            Vector2 previousCenter = Projectile.Center;
            int frame = Math.Clamp((int)StateTimer, 0, DashTravel.Length - 1);
            SetCenter(Vector2.Lerp(dashOrigin, dashEnd, DashTravel[frame]));

            if (!strikeResolved && target != null && Projectile.IsOwnedByLocalPlayer()
                && PathTouchesTarget(target, previousCenter, Projectile.Center, out Vector2 hitPoint)) {
                PublishImpact(target, hitPoint);
            }

            if (!Main.dedServ) {
                SpawnDashSmear(previousCenter, Projectile.Center);
            }

            if (StateTimer >= DashDuration) {
                Transition(ShadeState.Recovering);
            }
        }

        private static bool PathTouchesTarget(NPC target, Vector2 start, Vector2 end, out Vector2 hitPoint) {
            float collisionPoint = 0f;
            bool touches = Collision.CheckAABBvLineCollision(target.Hitbox.TopLeft(), target.Hitbox.Size(),
                start, end, ImpactRadiusPadding, ref collisionPoint);
            if (!touches) {
                hitPoint = end;
                return false;
            }

            Vector2 path = end - start;
            float lengthSquared = path.LengthSquared();
            float t = lengthSquared > 0.001f
                ? Vector2.Dot(target.Center - start, path) / lengthSquared
                : 0f;
            hitPoint = start + path * MathHelper.Clamp(t, 0f, 1f);
            return true;
        }

        private void PublishImpact(NPC target, Vector2 hitPoint) {
            if (strikeResolved || target == null || !target.active || target.type != targetNPCType) {
                return;
            }

            strikeResolved = true;
            impactCenter = hitPoint;
            impactSerial++;
            impactEventPending = true;
            Projectile.netUpdate = true;
            ProcessImpactEvent();
        }

        private void ProcessImpactEvent() {
            if (!impactEventPending || processedImpactSerial == impactSerial) {
                return;
            }
            impactEventPending = false;
            processedImpactSerial = impactSerial;

            NPC target = GetTarget(requireHuntRange: false);
            if (target == null) {
                return;
            }

            float cutAngle = dashDirection.ToRotation();
            if (NpcGroupHelper.IsBossTier(target)) {
                OniDismember.TriggerVisualOnly(target, impactCenter, cutAngle,
                    BossVisualDuration, ImpactHoldFrames);
            }
            else {
                float halfLength = MathHelper.Clamp(target.Size.Length() * 1.25f, 96f, 280f);
                DismemberStroke stroke = new(impactCenter, cutAngle, halfLength, 52f);
                int duration = (int)MathHelper.Lerp(78f, 114f, MathHelper.Clamp(Mastery, 0f, 1f));
                OniDismember.TriggerGroup(target, in stroke, duration, ImpactHoldFrames);
            }
            ScheduleCrossCuts(cutAngle);

            if (Projectile.IsOwnedByLocalPlayer()) {
                pendingDamage = true;
                pendingDamageTicks = ImpactHoldFrames + 1;
            }
            PlayImpactFx(target);
        }

        /// <summary>「撕成数段」：主刀之后再补两道交叉口，各自的滞拍收敛到同一帧一起崩</summary>
        private void ScheduleCrossCuts(float cutAngle) {
            Vector2 perpendicular = dashDirection.RotatedBy(MathHelper.PiOver2);
            crossAngleA = cutAngle + MathHelper.ToRadians(Main.rand.NextFloat(48f, 68f));
            crossAngleB = cutAngle - MathHelper.ToRadians(Main.rand.NextFloat(48f, 68f));
            crossPointA = impactCenter + perpendicular * Main.rand.NextFloat(-18f, 18f);
            crossPointB = impactCenter + dashDirection * Main.rand.NextFloat(-24f, 24f);
            crossCutStep = 2;
            crossCutTimer = CrossCutInterval;
        }

        private void UpdateCrossCuts() {
            if (crossCutStep <= 0 || --crossCutTimer > 0) {
                return;
            }

            NPC target = GetTarget(requireHuntRange: false);
            if (target == null) {
                crossCutStep = 0;
                return;
            }

            bool first = crossCutStep == 2;
            float angle = first ? crossAngleA : crossAngleB;
            Vector2 point = first ? crossPointA : crossPointB;
            int hold = Math.Max(ImpactHoldFrames - (first ? CrossCutInterval : CrossCutInterval * 2), 0);
            if (NpcGroupHelper.IsBossTier(target)) {
                OniDismember.TriggerVisualOnly(target, point, angle, BossVisualDuration, hold);
            }
            else {
                OniDismember.Trigger(target, point, angle,
                    (int)MathHelper.Lerp(78f, 114f, MathHelper.Clamp(Mastery, 0f, 1f)), hold);
            }

            crossCutStep--;
            crossCutTimer = CrossCutInterval;
        }

        private void UpdatePendingDamage() {
            if (!pendingDamage || --pendingDamageTicks > 0) {
                return;
            }
            pendingDamage = false;
            if (!Projectile.IsOwnedByLocalPlayer() || strikeInvalidated) {
                return;
            }

            NPC target = GetTarget(requireHuntRange: false);
            if (target == null) {
                return;
            }

            WraithNet.RequestHeadlessImpact(Projectile, impactSerial,
                targetNPCID, targetNPCType, impactCenter);
        }

        internal bool TryApplyAuthorityImpact(ushort serial, int targetId, int targetType,
            Vector2 impact) {
            if (Main.netMode == NetmodeID.MultiplayerClient || !Projectile.active
                || State is not (ShadeState.Dashing or ShadeState.Recovering)
                || serial != impactSerial || !IsNewerImpact(serial, lastSeenImpactSerial)
                || targetId != targetNPCID || targetType != targetNPCType
                || !float.IsFinite(impact.X) || !float.IsFinite(impact.Y)
                || targetId < 0 || targetId >= Main.maxNPCs) {
                return false;
            }
            lastSeenImpactSerial = serial;

            NPC target = Main.npc[targetId];
            if (!target.active || target.type != targetType || !target.CanBeChasedBy()
                || Vector2.DistanceSquared(Owner.Center, target.Center)
                    > HeadlessShadeAbility.HuntRange * HeadlessShadeAbility.HuntRange * 2.25f
                || !IsFinite(dashOrigin) || !IsFinite(dashEnd) || !IsFinite(dashDirection)
                || Vector2.DistanceSquared(Owner.Center, dashOrigin)
                    > HeadlessShadeAbility.HuntRange * HeadlessShadeAbility.HuntRange * 2.25f
                || !PathTouchesTarget(target, dashOrigin, dashEnd, out Vector2 verifiedImpact)) {
                return false;
            }

            float impactTolerance = Math.Max(80f,
                target.Size.Length() * 0.75f + ImpactRadiusPadding);
            float directionLengthSq = dashDirection.LengthSquared();
            if (directionLengthSq < 0.81f || directionLengthSq > 1.21f) {
                return false;
            }
            Vector2 verifiedDirection = dashDirection / MathF.Sqrt(directionLengthSq);
            Vector2 originToTarget = target.Center - dashOrigin;
            if (originToTarget.LengthSquared() < 1f
                || Vector2.Dot(originToTarget.SafeNormalize(Vector2.UnitX), verifiedDirection) < 0.85f) {
                return false;
            }
            Vector2 overrun = dashEnd - target.Center;
            float projectedOverrun = Vector2.Dot(overrun, verifiedDirection);
            float lateralOverrun = MathF.Abs(overrun.X * verifiedDirection.Y
                - overrun.Y * verifiedDirection.X);
            if (projectedOverrun < 70f || projectedOverrun > 300f
                || lateralOverrun > impactTolerance) {
                return false;
            }
            if (Vector2.DistanceSquared(impact, target.Center) > impactTolerance * impactTolerance
                || Vector2.DistanceSquared(impact, verifiedImpact) > impactTolerance * impactTolerance) {
                return false;
            }

            if (!WraithAbilityService.TryResolve(Owner, HeadlessShadeAbility.Key,
                out WraithAbilityContext context)) {
                return false;
            }

            float mastery = MathHelper.Clamp(context.Mastery, 0f, 1f);
            int weaponDamage = Math.Max(Owner.GetWeaponDamage(context.VesselItem), 1);
            int damage = Math.Max((int)(weaponDamage * MathHelper.Lerp(0.55f, 0.90f, mastery)), 1);
            float knockback = Owner.GetWeaponKnockback(context.VesselItem)
                * MathHelper.Lerp(0.65f, 1f, mastery);
            int critChance = Math.Max(Owner.GetWeaponCrit(context.VesselItem), 0);
            bool crit = critChance > 0 && Main.rand.Next(100) < critChance;
            int hitDirection = dashDirection.X >= 0f ? 1 : -1;
            int lifeBefore = target.life;
            Owner.ApplyDamageToNPC(target, damage, knockback,
                hitDirection, crit, Projectile.DamageType);
            if (target.life >= lifeBefore) {
                return false;
            }
            return WraithAbilityService.TryCommitUse(in context);
        }

        private static bool IsFinite(Vector2 value)
            => float.IsFinite(value.X) && float.IsFinite(value.Y);

        private void RecoveringBehavior() {
            ownerDirection = Owner.direction;
            UpdateTwitch();
            float progress = MathHelper.Clamp(StateTimer / RecoverDuration, 0f, 1f);
            Vector2 next = Vector2.Lerp(recoverStart, IdleHoverPosition(), VaultUtils.EaseOutCubic(progress));
            SetCenter(next);

            if (StateTimer >= RecoverDuration) {
                Transition(ShadeState.Idle);
            }
        }

        private void DismissingBehavior() {
            ownerDirection = Owner.direction;
            float progress = MathHelper.Clamp(StateTimer / DismissDuration, 0f, 1f);
            Vector2 sink = Owner.Center + new Vector2(-ownerDirection * 20f, 12f);
            SetCenter(Vector2.Lerp(dismissStart, sink, VaultUtils.EaseOutCubic(progress)));

            if (StateTimer >= DismissDuration) {
                Projectile.Kill();
            }
        }

        private void OnStateChanged(ShadeState next) {
            switch (next) {
                case ShadeState.Stalking:
                    stalkStart = Projectile.Center;
                    if (!Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.NPCDeath6 with {
                            Volume = 0.32f,
                            Pitch = -0.72f,
                            MaxInstances = 3
                        }, Projectile.Center);
                    }
                    break;
                case ShadeState.DashCharge:
                    chargeStart = Projectile.Center;
                    chargeHold = chargeStart;
                    PlayChargeFx();
                    break;
                case ShadeState.Dashing:
                    strikeResolved = false;
                    if (!Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.Item71 with {
                            Volume = 0.72f,
                            Pitch = -0.68f,
                            PitchVariance = 0.08f,
                            MaxInstances = 3
                        }, Projectile.Center);
                    }
                    break;
                case ShadeState.Recovering:
                    recoverStart = Projectile.Center;
                    reacquireTimer = ReacquireDelay;
                    break;
                case ShadeState.Idle:
                    targetNPCID = -1;
                    targetNPCType = -1;
                    break;
                case ShadeState.Dismissing:
                    targetNPCID = -1;
                    targetNPCType = -1;
                    dismissStart = Projectile.Center;
                    if (!Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.NPCDeath6 with {
                            Volume = 0.28f,
                            Pitch = -1f,
                            MaxInstances = 3
                        }, Projectile.Center);
                    }
                    break;
            }
        }

        private NPC FindTarget() {
            Vector2 origin = Owner.Center;
            NPC boss = origin.FindClosestNPC(
                HeadlessShadeAbility.HuntRange,
                ignoreTiles: true,
                chasedByNPC: npc => HeadlessShadeAbility.CanHunt(npc)
                    && NpcGroupHelper.IsBossTier(npc));
            return boss ?? origin.FindClosestNPC(
                HeadlessShadeAbility.HuntRange,
                ignoreTiles: true,
                chasedByNPC: HeadlessShadeAbility.CanHunt);
        }

        private NPC GetTarget(bool requireHuntRange) {
            if (targetNPCID < 0 || targetNPCID >= Main.maxNPCs) {
                return null;
            }

            NPC target = Main.npc[targetNPCID];
            if (!target.active || target.type != targetNPCType || !target.CanBeChasedBy()) {
                return null;
            }
            if (requireHuntRange
                && Vector2.DistanceSquared(target.Center, Owner.Center)
                    > HeadlessShadeAbility.HuntRange * HeadlessShadeAbility.HuntRange * 1.44f) {
                return null;
            }
            return target;
        }

        private void MoveToward(Vector2 target, float response, float maxSpeed) {
            Vector2 desiredVelocity = (target - Projectile.Center) * response;
            if (desiredVelocity.LengthSquared() > maxSpeed * maxSpeed) {
                desiredVelocity = desiredVelocity.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVelocity, 0.28f);
            Projectile.Center += Projectile.velocity;
        }

        private void SetCenter(Vector2 center) {
            Projectile.velocity = center - Projectile.Center;
            Projectile.Center = center;
        }

        private void UpdateVisuals() {
            float opacityTarget = State switch {
                ShadeState.Idle => 0.30f,
                ShadeState.Stalking => 0.52f,
                ShadeState.DashCharge => MathHelper.Lerp(0.58f, 0.98f,
                    MathHelper.Clamp(StateTimer / ChargeDuration, 0f, 1f)),
                ShadeState.Dashing => 1f,
                ShadeState.Recovering => 0.48f,
                _ => 0f,
            };
            float dissolveTarget = State switch {
                ShadeState.Idle => 0.72f,
                ShadeState.Stalking => 0.48f,
                ShadeState.DashCharge => MathHelper.Lerp(0.42f, 0.04f,
                    MathHelper.Clamp(StateTimer / ChargeDuration, 0f, 1f)),
                ShadeState.Dashing => 0.02f,
                ShadeState.Recovering => 0.58f,
                _ => 1f,
            };
            float phaseTarget = State is ShadeState.DashCharge or ShadeState.Dashing ? 1f : 0f;
            float stretchTarget = State == ShadeState.DashCharge
                ? MathHelper.Clamp(StateTimer / ChargeDuration, 0f, 1f)
                : State == ShadeState.Dashing ? 1f : 0f;

            opacitySmooth = MathHelper.Lerp(opacitySmooth, opacityTarget, State == ShadeState.Dashing ? 0.45f : 0.14f);
            dissolveSmooth = MathHelper.Lerp(dissolveSmooth, dissolveTarget, 0.16f);
            phaseSmooth = MathHelper.Lerp(phaseSmooth, phaseTarget, 0.20f);
            stretchSmooth = MathHelper.Lerp(stretchSmooth, stretchTarget, 0.24f);
            float appear = MathHelper.Clamp(visualAge / 18f, 0f, 1f);
            drawOpacity = MathHelper.Clamp(opacitySmooth * appear, 0f, 1f);

            rimFlash *= 0.74f;
            UpdateExtinguish();

            if (Main.dedServ) {
                return;
            }

            UpdateRig();
            if (drawOpacity < 0.08f) {
                return;
            }

            if (++wispTimer >= (State == ShadeState.Dashing ? 2 : 9)) {
                wispTimer = 0;
                Vector2 pos = Projectile.Center + new Vector2(
                    Main.rand.NextFloat(-30f, 30f), Main.rand.NextFloat(8f, 58f));
                Vector2 velocity = State == ShadeState.Dashing
                    ? -dashDirection * Main.rand.NextFloat(1.4f, 3.4f) + Main.rand.NextVector2Circular(0.8f, 0.8f)
                    : new Vector2(Main.rand.NextFloat(-0.35f, 0.35f), Main.rand.NextFloat(-0.9f, -0.2f));
                PRTLoader.NewParticle<PRT_Smoke>(pos, velocity, new Color(20, 19, 25) * drawOpacity,
                    Main.rand.NextFloat(0.07f, 0.12f))
                    ?.Configure(Main.rand.Next(18, 31), 0.30f * drawOpacity,
                        Main.rand.NextFloat(-0.015f, 0.015f));
            }
        }

        /// <summary>穿体那几帧本体熄灭，行程交给斩痕；重新亮起时补一次骨白撕口</summary>
        private void UpdateExtinguish() {
            if (State != ShadeState.Dashing) {
                bodyVeil = MathHelper.Lerp(bodyVeil, 1f, 0.30f);
                return;
            }

            int frame = (int)StateTimer;
            if (frame >= 2 && frame < RematerializeFrame) {
                bodyVeil = 0.05f;
                return;
            }
            if (frame == RematerializeFrame) {
                bodyVeil = 1f;
                rimFlash = 1f;
                rig?.Snap();
                return;
            }
            bodyVeil = MathHelper.Lerp(bodyVeil, 1f, 0.45f);
        }

        private void UpdateRig() {
            if (rig == null) {
                rig = new HeadlessShadeRig();
                rig.SetSeed(Seed);
            }
            float baseHalf = BodyDrawSize * BodyScale * 0.5f;
            Vector2 lead = State is ShadeState.DashCharge or ShadeState.Dashing
                ? dashDirection
                : Vector2.Zero;
            rig.Update(Projectile.Center,
                baseHalf * (1f + stretchSmooth * 0.18f),
                baseHalf * (1f - stretchSmooth * 0.20f),
                ownerDirection, lead, stretchSmooth, Main.GlobalTimeWrappedHourly);
        }

        private void PlayChargeFx() {
            if (Main.dedServ) {
                return;
            }

            SoundEngine.PlaySound(SoundID.DD2_SkeletonHurt with {
                Volume = 0.50f,
                Pitch = -0.82f,
                MaxInstances = 3
            }, Projectile.Center);
            for (int i = 0; i < 7; i++) {
                Vector2 velocity = (Projectile.Center - lockedTargetCenter)
                    .SafeNormalize(new Vector2(-ownerDirection, 0f))
                    .RotatedByRandom(0.75f) * Main.rand.NextFloat(0.8f, 2.5f);
                PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center + Main.rand.NextVector2Circular(24f, 38f),
                    velocity, new Color(26, 24, 32), Main.rand.NextFloat(0.08f, 0.14f))
                    ?.Configure(Main.rand.Next(20, 34), 0.42f,
                        Main.rand.NextFloat(-0.02f, 0.02f));
            }
        }

        /// <summary>本体熄灭期间由路径影屑承担行程，看不见的抡才是扑</summary>
        private void SpawnDashSmear(Vector2 from, Vector2 to) {
            if (strikeField == null || Vector2.DistanceSquared(from, to) < 4f) {
                return;
            }

            Vector2 perpendicular = dashDirection.RotatedBy(MathHelper.PiOver2);
            for (int i = 0; i < 3; i++) {
                Vector2 spot = Vector2.Lerp(from, to, Main.rand.NextFloat())
                    + perpendicular * Main.rand.NextFloat(-30f, 30f);
                Vector2 velocity = -dashDirection * Main.rand.NextFloat(1.6f, 4.6f)
                    + perpendicular * Main.rand.NextFloat(-0.9f, 0.9f);
                strikeField.AddShard(spot, velocity, Main.rand.NextFloat(28f, 62f),
                    Main.rand.NextFloat(2.4f, 5.4f), 0f, Main.rand.Next(14, 24));
            }
        }

        private void PlayImpactFx(NPC target) {
            if (Main.dedServ || target == null) {
                return;
            }

            Vector2 center = target.Center;
            bool steel = CWRLoad.NPCValue.ISTheofSteel(target);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                Volume = 0.88f,
                Pitch = -0.72f,
                PitchVariance = 0.06f,
                MaxInstances = 3
            }, center);
            SoundEngine.PlaySound(steel
                ? CWRSound.KatanaHit with { Pitch = 0.28f, Volume = 0.72f }
                : CWRSound.KatanaHitB with { Pitch = -0.18f, Volume = 0.72f }, center);

            //刀路长度跟目标体型走，巨物身上不能只留一道小口
            float sizeMul = BodyScale * MathHelper.Clamp(target.Size.Length() / 90f, 0.85f, 2.1f);
            strikeField?.SpawnImpact(impactCenter, dashDirection, sizeMul, steel);

            if (CWRServerConfig.Instance.ScreenVibration && Main.LocalPlayer.active
                && Vector2.DistanceSquared(Main.LocalPlayer.Center, center) < 1400f * 1400f) {
                PunchCameraModifier modifier = new(center, dashDirection, 7.5f, 6f, 12, 1000f, FullName);
                Main.instance.CameraModifiers.Add(modifier);
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ || drawOpacity <= 0.04f) {
                return;
            }

            for (int i = 0; i < 8; i++) {
                Vector2 pos = Projectile.Center + new Vector2(
                    Main.rand.NextFloat(-28f, 28f), Main.rand.NextFloat(-48f, 54f));
                PRTLoader.NewParticle<PRT_Smoke>(pos,
                    new Vector2(Main.rand.NextFloat(-0.7f, 0.7f), Main.rand.NextFloat(-1.2f, -0.25f)),
                    new Color(19, 18, 24) * drawOpacity, Main.rand.NextFloat(0.08f, 0.14f))
                    ?.Configure(Main.rand.Next(18, 30), 0.35f * drawOpacity,
                        Main.rand.NextFloat(-0.02f, 0.02f));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            bool hasCuts = strikeField?.HasCuts ?? false;
            if (drawOpacity <= 0.01f && !hasCuts) {
                return false;
            }

            Texture2D shutter = CWRAsset.Shutter?.Value;
            Effect effect = EffectLoader.HeadlessShadeBody?.Value;
            Effect cutEffect = EffectLoader.HeadlessShadeCut?.Value;
            Texture2D noise = CWRAsset.NoiseSoft01?.Value;
            if (shutter == null) {
                return false;
            }
            if (effect == null || noise == null || rig == null) {
                DrawFallback(shutter);
                return false;
            }

            SpriteBatch spriteBatch = Main.spriteBatch;
            spriteBatch.End();

            GraphicsDevice device = Main.instance.GraphicsDevice;
            BlendState previousBlend = device.BlendState;
            RasterizerState previousRasterizer = device.RasterizerState;
            DepthStencilState previousDepth = device.DepthStencilState;
            SamplerState previousSampler = device.SamplerStates[0];
            try {
                device.BlendState = BlendState.AlphaBlend;
                device.RasterizerState = RasterizerState.CullNone;
                device.DepthStencilState = DepthStencilState.None;

                float bodyOpacity = drawOpacity * bodyVeil;
                float seed = Seed * 11.7f;
                effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
                effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                effect.Parameters["uShutterTex"]?.SetValue(shutter);
                effect.Parameters["uNoiseTex"]?.SetValue(noise);

                rig.DrawGroundCast(device, effect, bodyOpacity, dissolveSmooth, seed);
                strikeField?.DrawCuts(device, cutEffect, noise);

                //三明治：漏影与后侧臂压在剪影底下，近侧臂盖在上面
                rig.DrawNeckSpill(device, effect, bodyOpacity, phaseSmooth, rimFlash, seed);
                rig.DrawFarArm(device, effect, bodyOpacity, phaseSmooth, rimFlash, seed);
                rig.DrawBody(device, effect, bodyOpacity, dissolveSmooth, phaseSmooth, rimFlash, seed);
                rig.DrawNearArm(device, effect, bodyOpacity, phaseSmooth, rimFlash, seed);
                strikeField?.DrawShards(device, effect, noise, drawOpacity);
            } finally {
                device.BlendState = previousBlend;
                device.RasterizerState = previousRasterizer;
                device.DepthStencilState = previousDepth;
                device.SamplerStates[0] = previousSampler;
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, RasterizerState.CullNone, null,
                    Main.GameViewMatrix.TransformationMatrix);
            }
            return false;
        }

        private void DrawFallback(Texture2D shutter) {
            float lean = State == ShadeState.Dashing
                ? dashDirection.X * 0.22f
                : MathHelper.Clamp(Projectile.velocity.X * 0.008f, -0.12f, 0.12f);
            float scale = BodyDrawSize * BodyScale / shutter.Width;
            Vector2 directionalScale = new(
                scale * (1f - stretchSmooth * 0.20f),
                scale * (1f + stretchSmooth * 0.18f));
            Color color = new Color(7, 7, 10) * (drawOpacity * bodyVeil);
            SpriteEffects effects = ownerDirection >= 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            Main.EntitySpriteDraw(shutter, Projectile.Center - Main.screenPosition, null, color, lean,
                shutter.Size() * 0.5f, directionalScale, effects, 0f);
        }
    }
}
