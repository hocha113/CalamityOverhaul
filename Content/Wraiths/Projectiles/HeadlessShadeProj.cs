using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniDismembers;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Wraiths.Attunements;
using CalamityOverhaul.Content.Wraiths.Core;
using InnoVault;
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
    /// 无头鬼影共鸣体。ai[0]=状态，ai[1]=状态计时，ai[2]=驾驭度；
    /// 目标、锁定点与冲刺路径通过 ExtraAI 同步，伤害仅由拥有者结算。
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
        private const int DashDuration = 8;
        private const int RecoverDuration = 24;
        private const int DismissDuration = 18;
        private const int ImpactHoldFrames = 6;
        private const int BossVisualDuration = 38;
        private const int WakeAfterFrames = 12;
        private const int WakeSamples = 12;
        private const float WakePointSpacing = 5f;
        private const float WakeNoiseTilePx = 260f;
        private const float WakeMaxWidth = 28f;
        private const float ImpactRadiusPadding = 24f;
        private const float BodyDrawSize = 184f;

        private int targetNPCID = -1;
        private int targetNPCType = -1;
        private int ownerDirection = 1;
        private int reacquireTimer;
        private int visualAge;
        private int wispTimer;
        private int pendingDamageTicks;
        private int wakeAfterTimer;
        private int wakePointCount;
        private ushort impactSerial;
        private ushort processedImpactSerial;
        private bool impactEventPending;
        private bool pendingDamage;
        private bool strikeResolved;
        private bool wakeAnchoredToImpact;

        private ShadeState previousState = ShadeState.Idle;
        private Vector2 stalkStart;
        private Vector2 chargeStart;
        private Vector2 dashOrigin;
        private Vector2 dashEnd;
        private Vector2 dashDirection = Vector2.UnitX;
        private Vector2 lockedTargetCenter;
        private Vector2 impactCenter;
        private Vector2 recoverStart;
        private Vector2 dismissStart;

        private float opacitySmooth;
        private float dissolveSmooth = 1f;
        private float phaseSmooth;
        private float drawOpacity;

        private readonly Vector2[] wakePoints = new Vector2[WakeSamples];
        private readonly VertexPositionColorTexture[] quadVertices = new VertexPositionColorTexture[4];
        private readonly VertexPositionColorTexture[] wakeVertices = new VertexPositionColorTexture[WakeSamples * 2];

        private float Seed => Projectile.identity * 0.173f % 1f;
        private float BodyScale => MathHelper.Lerp(0.92f, 1.08f, MathHelper.Clamp(Mastery, 0f, 1f));
        private int ReacquireDelay => (int)MathHelper.Lerp(105f, 72f, MathHelper.Clamp(Mastery, 0f, 1f));

        public override void SetStaticDefaults()
            => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 240;

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

            SoundEngine.PlaySound(SoundID.NPCDeath6 with {
                Volume = 0.42f,
                Pitch = -0.92f,
                MaxInstances = 3
            }, Projectile.Center);
            for (int i = 0; i < 9; i++) {
                Vector2 pos = Projectile.Center + Main.rand.NextVector2Circular(28f, 48f);
                Vector2 velocity = new(Main.rand.NextFloat(-0.7f, 0.7f), Main.rand.NextFloat(-1.2f, -0.25f));
                PRTLoader.NewParticle<PRT_Smoke>(pos, velocity, new Color(31, 24, 53), Main.rand.NextFloat(0.08f, 0.14f))
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
            if (State != ShadeState.Dismissing
                && (Owner.dead || Projectile.IsOwnedByLocalPlayer() && !HasValidAttunement())) {
                Transition(ShadeState.Dismissing);
            }

            visualAge++;
            StateTimer++;

            if (State != previousState) {
                OnStateChanged(State);
                previousState = State;
            }

            ProcessImpactEvent();
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

            UpdateWake();
            UpdateVisuals();
        }

        private bool HasValidAttunement() {
            WraithVesselHandle vessel = WraithVessels.ResolveHeld(Owner);
            return vessel.IsValid
                && vessel.Store.AttunedKey == HeadlessShadeAttunement.Key
                && vessel.Store.TryGet(HeadlessShadeAttunement.Key, out WraithProgressRecord record)
                && record.State == WraithBindState.Bound;
        }

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
            return Owner.Center + new Vector2(-ownerDirection * 48f, -48f) + bob;
        }

        private void IdleBehavior() {
            ownerDirection = Owner.direction;
            MoveToward(IdleHoverPosition(), 0.12f, 12f);

            if (reacquireTimer > 0) {
                reacquireTimer--;
                return;
            }
            if (!Projectile.IsOwnedByLocalPlayer() || StateTimer < InitialScanDelay || (int)StateTimer % 6 != 0) {
                return;
            }

            NPC target = FindTarget();
            if (target == null) {
                return;
            }

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

            float progress = MathHelper.Clamp(StateTimer / ChargeDuration, 0f, 1f);
            float compression = progress * progress * progress;
            Vector2 aim = (target.Center - chargeStart).SafeNormalize(new Vector2(ownerDirection, 0f));
            Vector2 next = chargeStart - aim * (38f * compression)
                + Vector2.UnitY * (6f * MathF.Sin(progress * MathHelper.Pi));
            SetCenter(next);

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
            float progress = MathHelper.Clamp(StateTimer / DashDuration, 0f, 1f);
            float travel = 1f - MathF.Pow(1f - progress, 3.2f);
            SetCenter(Vector2.Lerp(dashOrigin, dashEnd, travel));

            if (!strikeResolved && target != null && Projectile.IsOwnedByLocalPlayer()
                && PathTouchesTarget(target, previousCenter, Projectile.Center, out Vector2 hitPoint)) {
                PublishImpact(target, hitPoint);
            }

            if (!Main.dedServ) {
                SpawnDashWake();
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
            AnchorWakeToImpact();

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

            if (Projectile.IsOwnedByLocalPlayer()) {
                pendingDamage = true;
                pendingDamageTicks = ImpactHoldFrames + 1;
            }
            PlayImpactFx(target);
        }

        private void UpdatePendingDamage() {
            if (!pendingDamage || --pendingDamageTicks > 0) {
                return;
            }
            pendingDamage = false;
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }

            NPC target = GetTarget(requireHuntRange: false);
            if (target == null) {
                return;
            }

            int hitDirection = dashDirection.X >= 0f ? 1 : -1;
            bool crit = Projectile.CritChance > 0 && Main.rand.Next(100) < Projectile.CritChance;
            Owner.ApplyDamageToNPC(target, Projectile.damage, Projectile.knockBack,
                hitDirection, crit, Projectile.DamageType);
        }

        private void RecoveringBehavior() {
            ownerDirection = Owner.direction;
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
                    PlayChargeFx();
                    break;
                case ShadeState.Dashing:
                    strikeResolved = false;
                    wakeAfterTimer = 0;
                    wakePointCount = 0;
                    wakeAnchoredToImpact = false;
                    AppendWakePoint(Projectile.Center, force: true);
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
                    wakeAfterTimer = 0;
                    if (wakePointCount > 0 && !wakeAnchoredToImpact) {
                        wakePoints[0] = recoverStart;
                        wakeAnchoredToImpact = true;
                    }
                    break;
                case ShadeState.Idle:
                    targetNPCID = -1;
                    targetNPCType = -1;
                    wakePointCount = 0;
                    wakeAfterTimer = 0;
                    wakeAnchoredToImpact = false;
                    break;
                case ShadeState.Dismissing:
                    targetNPCID = -1;
                    targetNPCType = -1;
                    wakePointCount = 0;
                    wakeAfterTimer = 0;
                    wakeAnchoredToImpact = false;
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
                HeadlessShadeAttunement.HuntRange,
                ignoreTiles: true,
                chasedByNPC: npc => HeadlessShadeAttunement.CanHunt(npc)
                    && NpcGroupHelper.IsBossTier(npc));
            return boss ?? origin.FindClosestNPC(
                HeadlessShadeAttunement.HuntRange,
                ignoreTiles: true,
                chasedByNPC: HeadlessShadeAttunement.CanHunt);
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
                    > HeadlessShadeAttunement.HuntRange * HeadlessShadeAttunement.HuntRange * 1.44f) {
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

        private void UpdateWake() {
            if (State == ShadeState.Dashing) {
                if (wakeAnchoredToImpact) {
                    if (wakePointCount > 0) {
                        wakePoints[0] = impactCenter;
                    }
                }
                else {
                    AppendWakePoint(Projectile.Center);
                }
                return;
            }

            if (State != ShadeState.Recovering || wakePointCount < 2) {
                return;
            }

            wakeAfterTimer++;
            if (wakeAfterTimer >= WakeAfterFrames) {
                wakePointCount = 0;
                wakeAfterTimer = 0;
                wakeAnchoredToImpact = false;
            }
        }

        private void AppendWakePoint(Vector2 point, bool force = false) {
            if (wakePointCount == 0) {
                wakePoints[0] = point;
                wakePointCount = 1;
                return;
            }

            if (!force && Vector2.DistanceSquared(point, wakePoints[0]) < WakePointSpacing * WakePointSpacing) {
                wakePoints[0] = point;
                return;
            }

            int nextCount = Math.Min(wakePointCount + 1, wakePoints.Length);
            for (int i = nextCount - 1; i > 0; i--) {
                wakePoints[i] = wakePoints[i - 1];
            }
            wakePoints[0] = point;
            wakePointCount = nextCount;
        }

        private void AnchorWakeToImpact() {
            wakeAnchoredToImpact = true;
            wakeAfterTimer = 0;
            if (wakePointCount == 0) {
                wakePoints[0] = impactCenter;
                wakePointCount = 1;
            }
            else {
                if (wakePointCount == 1
                    && Vector2.DistanceSquared(wakePoints[0], impactCenter) > WakePointSpacing * WakePointSpacing) {
                    wakePoints[1] = wakePoints[0];
                    wakePointCount = 2;
                }
                while (wakePointCount > 1
                    && Vector2.Dot(wakePoints[1] - impactCenter, dashDirection) > 0f) {
                    for (int i = 1; i < wakePointCount - 1; i++) {
                        wakePoints[i] = wakePoints[i + 1];
                    }
                    wakePointCount--;
                }
                wakePoints[0] = impactCenter;
            }
        }

        private bool BuildWakeVertices(out float totalLength) {
            totalLength = 0f;
            if (wakePointCount < 2) {
                return false;
            }

            for (int i = 1; i < wakePointCount; i++) {
                totalLength += Vector2.Distance(wakePoints[i - 1], wakePoints[i]);
            }
            if (totalLength < 1f) {
                return false;
            }

            float distance = 0f;
            for (int i = 0; i < wakePointCount; i++) {
                if (i > 0) {
                    distance += Vector2.Distance(wakePoints[i - 1], wakePoints[i]);
                }
                float u = distance / totalLength;
                Vector2 tangent = i == 0
                    ? wakePoints[1] - wakePoints[0]
                    : i == wakePointCount - 1
                        ? wakePoints[i] - wakePoints[i - 1]
                        : wakePoints[i + 1] - wakePoints[i - 1];
                Vector2 normal = tangent.SafeNormalize(dashDirection).RotatedBy(MathHelper.PiOver2);
                float envelope = MathF.Pow(MathHelper.Clamp(MathF.Sin(u * MathHelper.Pi), 0f, 1f), 0.62f);
                float width = MathHelper.Lerp(2.2f, WakeMaxWidth * BodyScale, envelope);
                Vector2 center = wakePoints[i];
                wakeVertices[i * 2] = new VertexPositionColorTexture(
                    (center - normal * width).ToVector3(), Color.White, new Vector2(u, 0f));
                wakeVertices[i * 2 + 1] = new VertexPositionColorTexture(
                    (center + normal * width).ToVector3(), Color.White, new Vector2(u, 1f));
            }
            return true;
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

            opacitySmooth = MathHelper.Lerp(opacitySmooth, opacityTarget, State == ShadeState.Dashing ? 0.45f : 0.14f);
            dissolveSmooth = MathHelper.Lerp(dissolveSmooth, dissolveTarget, 0.16f);
            phaseSmooth = MathHelper.Lerp(phaseSmooth, phaseTarget, 0.20f);
            float appear = MathHelper.Clamp(visualAge / 18f, 0f, 1f);
            drawOpacity = MathHelper.Clamp(opacitySmooth * appear, 0f, 1f);

            if (Main.dedServ || drawOpacity < 0.08f) {
                return;
            }

            if (++wispTimer >= (State == ShadeState.Dashing ? 2 : 9)) {
                wispTimer = 0;
                Vector2 pos = Projectile.Center + new Vector2(
                    Main.rand.NextFloat(-30f, 30f), Main.rand.NextFloat(8f, 58f));
                Vector2 velocity = State == ShadeState.Dashing
                    ? -dashDirection * Main.rand.NextFloat(1.4f, 3.4f) + Main.rand.NextVector2Circular(0.8f, 0.8f)
                    : new Vector2(Main.rand.NextFloat(-0.35f, 0.35f), Main.rand.NextFloat(-0.9f, -0.2f));
                PRTLoader.NewParticle<PRT_Smoke>(pos, velocity, new Color(35, 27, 61) * drawOpacity,
                    Main.rand.NextFloat(0.07f, 0.12f))
                    ?.Configure(Main.rand.Next(18, 31), 0.30f * drawOpacity,
                        Main.rand.NextFloat(-0.015f, 0.015f));
            }
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
                    velocity, new Color(44, 31, 72), Main.rand.NextFloat(0.08f, 0.14f))
                    ?.Configure(Main.rand.Next(20, 34), 0.42f,
                        Main.rand.NextFloat(-0.02f, 0.02f));
            }
        }

        private void SpawnDashWake() {
            Vector2 perpendicular = dashDirection.RotatedBy(MathHelper.PiOver2);
            for (int i = 0; i < 2; i++) {
                Vector2 pos = Projectile.Center - dashDirection * Main.rand.NextFloat(12f, 52f)
                    + perpendicular * Main.rand.NextFloat(-28f, 28f);
                Vector2 velocity = -dashDirection * Main.rand.NextFloat(2.2f, 5.5f)
                    + perpendicular * Main.rand.NextFloat(-0.8f, 0.8f);
                PRTLoader.NewParticle<PRT_Smoke>(pos, velocity, new Color(39, 27, 66), Main.rand.NextFloat(0.07f, 0.12f))
                    ?.Configure(Main.rand.Next(15, 25), 0.34f,
                        Main.rand.NextFloat(-0.018f, 0.018f));
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

            CrimsonRendHitVFX.SpawnImpactBurst(center, dashDirection,
                power: 0.86f, sizeMul: BodyScale, steel: steel);
            for (int i = 0; i < 8; i++) {
                Vector2 velocity = -dashDirection * Main.rand.NextFloat(1f, 4f)
                    + Main.rand.NextVector2Circular(1.8f, 1.8f);
                PRTLoader.NewParticle<PRT_Smoke>(center + Main.rand.NextVector2Circular(22f, 26f),
                    velocity, new Color(43, 28, 69), Main.rand.NextFloat(0.09f, 0.16f))
                    ?.Configure(Main.rand.Next(22, 38), 0.48f,
                        Main.rand.NextFloat(-0.025f, 0.025f));
            }

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
                    new Color(34, 26, 57) * drawOpacity, Main.rand.NextFloat(0.08f, 0.14f))
                    ?.Configure(Main.rand.Next(18, 30), 0.35f * drawOpacity,
                        Main.rand.NextFloat(-0.02f, 0.02f));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (drawOpacity <= 0.01f) {
                return false;
            }

            Texture2D shutter = CWRAsset.Shutter?.Value;
            Effect effect = EffectLoader.HeadlessShadeBody?.Value;
            Effect wakeEffect = EffectLoader.WraithScapeArm?.Value;
            Texture2D noise = CWRAsset.NoiseSoft01?.Value;
            if (shutter == null) {
                return false;
            }
            if (effect == null || noise == null) {
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

                if (wakeEffect != null) {
                    DrawWake(device, wakeEffect, noise);
                }

                effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
                effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                effect.Parameters["uSeed"]?.SetValue(Seed * 11.7f);
                effect.Parameters["uShutterTex"]?.SetValue(shutter);
                effect.Parameters["uNoiseTex"]?.SetValue(noise);

                float stretch = State == ShadeState.Dashing ? 1.30f : 1f;
                DrawShade(device, effect, Projectile.Center, drawOpacity, dissolveSmooth, phaseSmooth, stretch);
            }
            finally {
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

        private void DrawWake(GraphicsDevice device, Effect effect, Texture2D noise) {
            if (!BuildWakeVertices(out float totalLength)) {
                return;
            }

            float retract = State == ShadeState.Recovering
                ? MathHelper.Clamp(wakeAfterTimer / (float)WakeAfterFrames, 0f, 1f)
                : 0f;
            retract = retract * retract * (3f - 2f * retract);
            float pulse = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 8.5f + Seed * 17f);

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            effect.Parameters["uOpacity"]?.SetValue(drawOpacity * (State == ShadeState.Dashing ? 0.78f : 0.68f));
            effect.Parameters["uRetract"]?.SetValue(retract);
            effect.Parameters["uSeed"]?.SetValue(Seed * 83f);
            effect.Parameters["uTearAmp"]?.SetValue(1.08f + retract * 0.74f);
            effect.Parameters["uPulse"]?.SetValue(pulse);
            effect.Parameters["uPulseAmp"]?.SetValue(0.22f + pulse * 0.24f);
            effect.Parameters["uLenScale"]?.SetValue(totalLength / WakeNoiseTilePx);
            effect.Parameters["uColBase"]?.SetValue(new Vector3(0.010f, 0.006f, 0.022f));
            effect.Parameters["uColVein"]?.SetValue(new Vector3(0.070f, 0.032f, 0.130f));
            effect.Parameters["uColHot"]?.SetValue(new Vector3(0.155f, 0.070f, 0.245f));

            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, wakeVertices, 0, wakePointCount * 2 - 2);
            }
        }

        private void DrawShade(GraphicsDevice device, Effect effect, Vector2 center, float opacity,
            float dissolve, float phase, float motionStretch) {
            float lean = State == ShadeState.Dashing
                ? dashDirection.X * 0.22f
                : MathHelper.Clamp(Projectile.velocity.X * 0.008f, -0.12f, 0.12f);
            float stretchDelta = motionStretch - 1f;
            float widthStretch = 1f + MathF.Abs(dashDirection.X) * stretchDelta;
            float heightStretch = 1f + MathF.Abs(dashDirection.Y) * stretchDelta;
            float halfWidth = BodyDrawSize * BodyScale * widthStretch * 0.5f;
            float halfHeight = BodyDrawSize * BodyScale * heightStretch * 0.5f;
            Vector2 xAxis = lean.ToRotationVector2() * halfWidth;
            Vector2 yAxis = (lean + MathHelper.PiOver2).ToRotationVector2() * halfHeight;
            float leftU = ownerDirection >= 0 ? 0f : 1f;
            float rightU = 1f - leftU;

            quadVertices[0] = new VertexPositionColorTexture((center - xAxis - yAxis).ToVector3(), Color.White,
                new Vector2(leftU, 0f));
            quadVertices[1] = new VertexPositionColorTexture((center - xAxis + yAxis).ToVector3(), Color.White,
                new Vector2(leftU, 1f));
            quadVertices[2] = new VertexPositionColorTexture((center + xAxis - yAxis).ToVector3(), Color.White,
                new Vector2(rightU, 0f));
            quadVertices[3] = new VertexPositionColorTexture((center + xAxis + yAxis).ToVector3(), Color.White,
                new Vector2(rightU, 1f));

            effect.Parameters["uOpacity"]?.SetValue(MathHelper.Clamp(opacity, 0f, 1f));
            effect.Parameters["uDissolve"]?.SetValue(MathHelper.Clamp(dissolve, 0f, 1f));
            effect.Parameters["uPhase"]?.SetValue(MathHelper.Clamp(phase, 0f, 1f));
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, quadVertices, 0, 2);
            }
        }

        private void DrawFallback(Texture2D shutter) {
            float lean = State == ShadeState.Dashing
                ? dashDirection.X * 0.22f
                : MathHelper.Clamp(Projectile.velocity.X * 0.008f, -0.12f, 0.12f);
            float scale = BodyDrawSize * BodyScale / shutter.Width;
            float stretchDelta = State == ShadeState.Dashing ? 0.30f : 0f;
            Vector2 directionalScale = new(
                scale * (1f + MathF.Abs(dashDirection.X) * stretchDelta),
                scale * (1f + MathF.Abs(dashDirection.Y) * stretchDelta));
            Color color = new Color(10, 8, 19) * drawOpacity;
            SpriteEffects effects = ownerDirection >= 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            Main.EntitySpriteDraw(shutter, Projectile.Center - Main.screenPosition, null, color, lean,
                shutter.Size() * 0.5f, directionalScale, effects, 0f);
        }
    }
}