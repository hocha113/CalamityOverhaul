using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniDismembers;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Wraiths.Attunements;
using CalamityOverhaul.Content.Wraiths.Core;
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
        private const int HistoryLength = 8;
        private const float BodyDrawSize = 184f;

        private int targetNPCID = -1;
        private int targetNPCType = -1;
        private int ownerDirection = 1;
        private int reacquireTimer;
        private int visualAge;
        private int wispTimer;
        private int pendingDamageTicks;
        private bool pendingDamage;
        private bool strikeResolved;

        private ShadeState previousState = ShadeState.Idle;
        private Vector2 stalkStart;
        private Vector2 chargeStart;
        private Vector2 dashOrigin;
        private Vector2 dashEnd;
        private Vector2 dashDirection = Vector2.UnitX;
        private Vector2 lockedTargetCenter;
        private Vector2 recoverStart;
        private Vector2 dismissStart;

        private float opacitySmooth;
        private float dissolveSmooth = 1f;
        private float phaseSmooth;
        private float drawOpacity;

        private readonly Vector2[] centerHistory = new Vector2[HistoryLength];
        private readonly VertexPositionColorTexture[] quadVertices = new VertexPositionColorTexture[4];

        private float Seed => Projectile.identity * 0.173f % 1f;
        private float BodyScale => MathHelper.Lerp(0.92f, 1.08f, MathHelper.Clamp(Mastery, 0f, 1f));
        private int ReacquireDelay => (int)MathHelper.Lerp(105f, 72f, MathHelper.Clamp(Mastery, 0f, 1f));

        public override void SetDefaults() {
            Projectile.width = 54;
            Projectile.height = 104;
            Projectile.aiStyle = -1;
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
            for (int i = 0; i < centerHistory.Length; i++) {
                centerHistory[i] = Projectile.Center;
            }

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
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            targetNPCID = reader.ReadInt16();
            targetNPCType = reader.ReadInt32();
            ownerDirection = reader.ReadSByte();
            dashOrigin = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            dashEnd = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            dashDirection = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            lockedTargetCenter = new Vector2(reader.ReadSingle(), reader.ReadSingle());
        }

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
            RecordHistory();
            UpdatePendingDamage();

            if (State != previousState) {
                OnStateChanged(State);
                previousState = State;
            }

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
            float overrun = MathHelper.Clamp(MathF.Max(target.width, target.height) * 0.65f + 105f, 120f, 230f);
            dashEnd = lockedTargetCenter + dashDirection * overrun;
            Transition(ShadeState.Dashing);
        }

        private void DashingBehavior() {
            NPC target = strikeResolved ? null : GetTarget(requireHuntRange: false);
            if (!strikeResolved && target == null) {
                Transition(ShadeState.Recovering);
                return;
            }

            float progress = MathHelper.Clamp(StateTimer / DashDuration, 0f, 1f);
            float travel = 1f - MathF.Pow(1f - progress, 3.2f);
            SetCenter(Vector2.Lerp(dashOrigin, dashEnd, travel));

            if (!strikeResolved) {
                Vector2 path = dashEnd - dashOrigin;
                float pathLengthSq = MathF.Max(path.LengthSquared(), 1f);
                float impactProgress = MathHelper.Clamp(
                    Vector2.Dot(lockedTargetCenter - dashOrigin, path) / pathLengthSq, 0f, 1f);
                if (travel >= impactProgress) {
                    ResolveStrike(target);
                }
            }

            if (!Main.dedServ) {
                SpawnDashWake();
            }

            if (StateTimer >= DashDuration) {
                Transition(ShadeState.Recovering);
            }
        }

        private void ResolveStrike(NPC target) {
            if (strikeResolved || target == null || !target.active || target.type != targetNPCType) {
                return;
            }
            strikeResolved = true;

            float cutAngle = dashDirection.ToRotation();
            float halfLength = MathHelper.Clamp(target.Size.Length() * 1.25f, 96f, 280f);
            DismemberStroke stroke = new(lockedTargetCenter, cutAngle, halfLength, 52f);
            int duration = (int)MathHelper.Lerp(78f, 114f, MathHelper.Clamp(Mastery, 0f, 1f));
            OniDismember.TriggerGroup(target, in stroke, duration, ImpactHoldFrames);
            pendingDamage = true;
            pendingDamageTicks = ImpactHoldFrames + 1;

            PlayImpactFx(target.Center);
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
            Owner.ApplyDamageToNPC(target, Projectile.damage, Projectile.knockBack, hitDirection, false);
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
            NPC best = null;
            float bestDistanceSq = float.MaxValue;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!HeadlessShadeAttunement.CanHunt(npc, Owner.Center)) {
                    continue;
                }

                float distanceSq = Vector2.DistanceSquared(npc.Center, Owner.Center);
                if (distanceSq < bestDistanceSq) {
                    bestDistanceSq = distanceSq;
                    best = npc;
                }
            }
            return best;
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

        private void RecordHistory() {
            for (int i = centerHistory.Length - 1; i > 0; i--) {
                centerHistory[i] = centerHistory[i - 1];
            }
            centerHistory[0] = Projectile.Center;
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

        private void PlayImpactFx(Vector2 center) {
            if (Main.dedServ) {
                return;
            }

            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                Volume = 0.88f,
                Pitch = -0.72f,
                PitchVariance = 0.06f,
                MaxInstances = 3
            }, center);
            SoundEngine.PlaySound(SoundID.NPCHit18 with {
                Volume = 0.62f,
                Pitch = -0.38f,
                MaxInstances = 3
            }, center);

            Vector2 normal = dashDirection.RotatedBy(MathHelper.PiOver2);
            for (int i = 0; i < 14; i++) {
                Vector2 velocity = normal * Main.rand.NextFloat(-7.5f, 7.5f)
                    + dashDirection * Main.rand.NextFloat(1f, 5f)
                    - Vector2.UnitY * Main.rand.NextFloat(0f, 1.8f);
                Color color = Main.rand.NextBool(3) ? new Color(154, 20, 35) : new Color(92, 10, 25);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(
                    center + Main.rand.NextVector2Circular(12f, 16f), velocity, color,
                    Main.rand.NextFloat(0.65f, 1.15f))
                    ?.Configure(Main.rand.Next(18, 30), 0.31f, 0.987f);
            }
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
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.None;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uSeed"]?.SetValue(Seed * 11.7f);
            effect.Parameters["uShutterTex"]?.SetValue(shutter);
            effect.Parameters["uNoiseTex"]?.SetValue(noise);

            int afterimageCount = State == ShadeState.Dashing ? 6
                : State == ShadeState.Recovering && StateTimer < 10f ? 3 : 0;
            for (int i = afterimageCount; i >= 1; i--) {
                float fade = 1f - i / (float)(afterimageCount + 1);
                float opacity = drawOpacity * fade * (State == ShadeState.Dashing ? 0.24f : 0.13f);
                DrawShade(device, effect, centerHistory[Math.Min(i, centerHistory.Length - 1)], opacity,
                    MathHelper.Clamp(dissolveSmooth + 0.16f + i * 0.045f, 0f, 1f),
                    phaseSmooth, 1.12f + i * 0.025f);
            }

            float stretch = State == ShadeState.Dashing ? 1.18f : 1f;
            DrawShade(device, effect, Projectile.Center, drawOpacity, dissolveSmooth, phaseSmooth, stretch);

            device.BlendState = previousBlend;
            device.RasterizerState = previousRasterizer;
            device.DepthStencilState = previousDepth;
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        private void DrawShade(GraphicsDevice device, Effect effect, Vector2 center, float opacity,
            float dissolve, float phase, float horizontalStretch) {
            float lean = State == ShadeState.Dashing
                ? dashDirection.X * 0.22f
                : MathHelper.Clamp(Projectile.velocity.X * 0.008f, -0.12f, 0.12f);
            float halfWidth = BodyDrawSize * BodyScale * horizontalStretch * 0.5f;
            float halfHeight = BodyDrawSize * BodyScale * 0.5f;
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
            Color color = new Color(10, 8, 19) * drawOpacity;
            SpriteEffects effects = ownerDirection >= 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            Main.EntitySpriteDraw(shutter, Projectile.Center - Main.screenPosition, null, color, lean,
                shutter.Size() * 0.5f, new Vector2(scale * (State == ShadeState.Dashing ? 1.18f : 1f), scale),
                effects, 0f);
        }
    }
}