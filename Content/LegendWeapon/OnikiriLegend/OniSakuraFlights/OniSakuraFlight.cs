using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniSakuraFlights
{
    /// <summary>
    /// 鬼切樱流化身：玩家化作分层风幕般的樱流高速飞行，并在终点回卷重组。
    /// <br/>真实移动仅由持有者客户端推进；控制弹幕负责同步生命周期，花瓣在各客户端本地重建。
    /// </summary>
    internal sealed class OniSakuraFlight : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        private enum PetalRole : byte
        {
            Core,
            Braid,
            Loose
        }

        private sealed class Petal
        {
            public PetalRole Role;
            public Vector2 Position;
            public Vector2 PreviousPosition;
            public Vector2 Velocity;
            public Vector2 BodyOffset;
            public Vector2 ReformStart;
            public Vector2 ReformControlA;
            public Vector2 ReformControlB;
            public float Phase;
            public float Spin;
            public float Radius;
            public float BaseTrailDistance;
            public float BaseScale;
            public float RenderScale;
            public float Flip;
            public float Stretch = 1f;
            public float Rotation;
            public float RotSpeed;
            public float Depth;
            public float Alpha;
            public float InitialAlpha = 1f;
            public float Release;
            public float Seed;
            public float FlowRate;
            public float Wander;
            public float Opacity;
            public int Lane;
            public int Age;
            public int MaxLife;
            public bool DeepColor;
            public bool Glow;
        }

        private readonly struct PathFrame
        {
            public readonly Vector2 Position;
            public readonly Vector2 Tangent;
            public readonly Vector2 Normal;
            public readonly float Curvature;

            public PathFrame(Vector2 position, Vector2 tangent, float curvature) {
                Position = position;
                Tangent = tangent;
                Normal = new Vector2(-tangent.Y, tangent.X);
                Curvature = curvature;
            }
        }

        private const int DissolveFrames = 6;
        private const int HideStartFrame = 3;
        private const int ReformFrames = 12;
        private const int AfterglowFrames = 22;
        private const int CorePetalCount = 20;
        private const int BraidPetalCount = 48;
        private const int MaxPetalCount = 164;
        private const int MaxLoosePetals = MaxPetalCount - CorePetalCount - BraidPetalCount;
        private const float PathSpacing = 10f;
        private const float MaxTrailLength = 420f;
        private const int MaxPathPoints = 52;
        private const float MinFlightSpeed = 14f;
        private const float MaxFlightSpeed = 48f;

        private readonly List<Vector2> path = new(MaxPathPoints);
        private readonly List<Petal> petals = new(MaxPetalCount);
        private readonly List<Petal> drawBuffer = new(MaxPetalCount);

        private bool initialized;
        private bool reformStarted;
        private bool afterglowStarted;
        private bool ownerReleased;
        private Vector2 moveDirection;
        private Vector2 lastObservedCenter;
        private Vector2 lastVisualDirection;
        private float flightSpeed;
        private float pathCarry;
        private float availablePathLength;
        private float looseSpawnCarry;
        private float visualSpeedRatio;

        public override string Texture => CWRConstant.Placeholder;

        private Player Owner => Main.player[Projectile.owner];
        private int Timer {
            get => (int)Projectile.ai[0];
            set => Projectile.ai[0] = value;
        }
        private int FlightDuration => Math.Clamp((int)Projectile.ai[1], 12, 120);
        private float Seed => Projectile.ai[2];
        private int FlightEndFrame => DissolveFrames + FlightDuration;
        private int ReformEndFrame => FlightEndFrame + ReformFrames;
        private int ReappearFrame => FlightEndFrame + (int)(ReformFrames * 0.72f);
        private int KillFrame => ReformEndFrame + AfterglowFrames;

        /// <summary>该控制器当前是否应取代持有者本体绘制。</summary>
        internal bool ShouldHideOwner => Timer >= HideStartFrame && Timer < ReappearFrame;

        /// <summary>
        /// 在持有者客户端启动樱流飞行。方向键可在飞行期间平滑转向，超时后自动重组。
        /// </summary>
        public static Projectile Fire(Player player, Vector2 aim, float speed = 32f,
            int flightFrames = 40, IEntitySource source = null) {
            if (player == null || !player.Alives()) {
                return null;
            }

            int type = ModContent.ProjectileType<OniSakuraFlight>();
            foreach (Projectile projectile in Main.ActiveProjectiles) {
                if (projectile.type == type && projectile.owner == player.whoAmI) {
                    return projectile;
                }
            }

            source ??= player.GetSource_Misc("CWR_OniSakuraFlight");
            Vector2 direction = aim.SafeNormalize(Vector2.UnitX * player.direction);
            speed = MathHelper.Clamp(speed, MinFlightSpeed, MaxFlightSpeed);
            flightFrames = Math.Clamp(flightFrames, 12, 120);
            float seed = Main.rand.NextFloat(0.01f, 0.99f);

            return Projectile.NewProjectileDirect(source, player.Center, direction * speed,
                type, 0, 0f, player.whoAmI, ai0: 0f, ai1: flightFrames, ai2: seed);
        }

        /// <summary>令指定玩家正在进行的樱流飞行立即进入回卷重组阶段。</summary>
        public static void RequestStop(Player player) {
            if (player == null) {
                return;
            }

            int type = ModContent.ProjectileType<OniSakuraFlight>();
            foreach (Projectile projectile in Main.ActiveProjectiles) {
                if (projectile.type != type || projectile.owner != player.whoAmI
                    || projectile.ModProjectile is not OniSakuraFlight flight
                    || flight.Timer >= flight.FlightEndFrame) {
                    continue;
                }

                flight.Timer = flight.FlightEndFrame;
                projectile.netUpdate = true;
            }
        }

        /// <summary>供 PlayerOverride 查询任意玩家是否已被同步的樱流控制器取代。</summary>
        internal static bool IsPlayerHidden(int playerIndex) {
            int type = ModContent.ProjectileType<OniSakuraFlight>();
            foreach (Projectile projectile in Main.ActiveProjectiles) {
                if (projectile.type == type && projectile.owner == playerIndex
                    && projectile.ModProjectile is OniSakuraFlight flight
                    && flight.ShouldHideOwner) {
                    return true;
                }
            }
            return false;
        }

        public override void SetStaticDefaults() {
            CWRLoad.ProjValue.ImmuneFrozen[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.aiStyle = -1;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
            Projectile.timeLeft = 240;
            Projectile.hide = true;
        }

        public override bool ShouldUpdatePosition() => false;

        private void Initialize() {
            initialized = true;
            flightSpeed = MathHelper.Clamp(Projectile.velocity.Length(), MinFlightSpeed, MaxFlightSpeed);
            moveDirection = Projectile.velocity.SafeNormalize(Vector2.UnitX * Owner.direction);
            lastVisualDirection = moveDirection;
            Projectile.velocity = moveDirection * flightSpeed;
            Projectile.timeLeft = KillFrame + 12;

            lastObservedCenter = Owner.Center;
            path.Add(lastObservedCenter);

            if (!Main.dedServ) {
                InitializePetals();
                SoundEngine.PlaySound(SoundID.Grass with {
                    Pitch = -0.28f,
                    Volume = 0.78f
                }, Owner.Center);
                SoundEngine.PlaySound(CWRSound.KatanaSprint with {
                    Pitch = 0.12f,
                    Volume = 0.48f
                }, Owner.Center);
            }

            if (Projectile.IsOwnedByLocalPlayer()) {
                Owner.RemoveAllGrapplingHooks();
                Owner.CWR().GetScreenShake(2.4f);
                if (CWRServerConfig.Instance.LensEasing) {
                    Main.SetCameraLerp(0.08f, 18);
                }
            }
        }

        public override void AI() {
            if (!initialized) {
                Initialize();
            }

            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            Timer++;

            if (Projectile.IsOwnedByLocalPlayer()) {
                UpdateOwnerMovement();
            }

            Vector2 currentCenter = Owner.Center;
            Vector2 observedDelta = currentCenter - lastObservedCenter;
            float frameTravel = observedDelta.Length();

            if (!Projectile.IsOwnedByLocalPlayer() && frameTravel > 0.8f) {
                Vector2 observedDirection = observedDelta / frameTravel;
                moveDirection = Vector2.Lerp(moveDirection, observedDirection, 0.32f)
                    .SafeNormalize(moveDirection);
            }

            RecordPath(currentCenter);
            Projectile.Center = currentCenter;
            visualSpeedRatio = MathHelper.Clamp(frameTravel / Math.Max(flightSpeed, 1f), 0f, 1.35f);

            if (!reformStarted && Timer >= FlightEndFrame) {
                BeginReform();
            }
            if (!afterglowStarted && Timer >= ReformEndFrame) {
                BeginAfterglow();
            }
            if (!ownerReleased && Timer >= ReappearFrame) {
                ReleaseOwner();
            }

            if (!Main.dedServ) {
                UpdatePetals(frameTravel);
                float pulse = 0.72f + 0.18f * MathF.Sin(Timer * 0.22f);
                Lighting.AddLight(Owner.Center, new Vector3(0.82f, 0.24f, 0.34f) * pulse);
            }

            if (Timer >= KillFrame) {
                Projectile.Kill();
            }
        }

        private void UpdateOwnerMovement() {
            if (Timer < HideStartFrame) {
                HoldOwner();
                return;
            }

            if (Timer >= FlightEndFrame) {
                if (Timer < ReappearFrame) {
                    HoldOwner();
                }
                return;
            }

            Vector2 input = new(
                (Owner.controlRight ? 1f : 0f) - (Owner.controlLeft ? 1f : 0f),
                (Owner.controlDown ? 1f : 0f) - (Owner.controlUp ? 1f : 0f));

            if (input.LengthSquared() > 0.01f) {
                Vector2 desiredDirection = input.SafeNormalize(moveDirection);
                float currentAngle = moveDirection.ToRotation();
                float turn = MathHelper.WrapAngle(desiredDirection.ToRotation() - currentAngle);
                float maxTurn = MathHelper.Lerp(0.12f, 0.205f, 1f - MathHelper.Clamp(visualSpeedRatio, 0f, 1f));
                moveDirection = (currentAngle + MathHelper.Clamp(turn, -maxTurn, maxTurn))
                    .ToRotationVector2();
            }

            float oldSyncedAngle = Projectile.velocity.ToRotation();
            Projectile.velocity = moveDirection * flightSpeed;
            if (Timer % 3 == 0
                && MathF.Abs(MathHelper.WrapAngle(moveDirection.ToRotation() - oldSyncedAngle)) > 0.025f) {
                Projectile.netUpdate = true;
            }

            float launch = MathHelper.SmoothStep(0.34f, 1f,
                MathHelper.Clamp((Timer - HideStartFrame) / 5f, 0f, 1f));
            float braking = MathHelper.SmoothStep(0.30f, 1f,
                MathHelper.Clamp((FlightEndFrame - Timer) / 6f, 0f, 1f));
            Vector2 desiredMove = moveDirection * flightSpeed * launch * braking;
            Vector2 allowedMove = Collision.TileCollision(Owner.position, desiredMove,
                Owner.width, Owner.height, fallThrough: true, fall2: true, (int)Owner.gravDir);

            Owner.position += allowedMove;
            Owner.velocity = Vector2.Zero;
            Owner.fallStart = (int)(Owner.position.Y / 16f);
            Owner.GivePlayerImmuneState(5);
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.controlUseItem = false;
            Owner.controlUseTile = false;

            if (MathF.Abs(moveDirection.X) > 0.08f) {
                Owner.ChangeDir(moveDirection.X > 0f ? 1 : -1);
            }

            float desiredLength = desiredMove.Length();
            if (desiredLength > 3f && allowedMove.Length() < desiredLength * 0.28f) {
                Timer = FlightEndFrame;
                Projectile.netUpdate = true;
            }
        }

        private void HoldOwner() {
            Owner.velocity = Vector2.Zero;
            Owner.fallStart = (int)(Owner.position.Y / 16f);
            Owner.GivePlayerImmuneState(4);
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.controlUseItem = false;
            Owner.controlUseTile = false;
        }

        private void ReleaseOwner() {
            ownerReleased = true;
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }

            Owner.velocity = moveDirection * 5.5f;
            Owner.CWR().GetScreenShake(1.8f);
        }

        private void RecordPath(Vector2 currentCenter) {
            Vector2 delta = currentCenter - lastObservedCenter;
            float distance = delta.Length();

            if (distance > 900f) {
                path.Clear();
                path.Add(currentCenter);
                lastObservedCenter = currentCenter;
                pathCarry = 0f;
                availablePathLength = 0f;
                return;
            }

            if (distance > 0.001f) {
                Vector2 direction = delta / distance;
                Vector2 cursor = lastObservedCenter;
                float remaining = distance;
                float toNext = PathSpacing - pathCarry;

                while (remaining >= toNext) {
                    cursor += direction * toNext;
                    path.Add(cursor);
                    remaining -= toNext;
                    pathCarry = 0f;
                    toNext = PathSpacing;
                }

                pathCarry += remaining;
                lastObservedCenter = currentCenter;
            }

            if (path.Count > MaxPathPoints) {
                path.RemoveRange(0, path.Count - MaxPathPoints);
            }

            availablePathLength = GetPathLength(currentCenter);
        }

        private float GetPathLength(Vector2 head) {
            float length = 0f;
            Vector2 newer = head;
            for (int i = path.Count - 1; i >= 0; i--) {
                length += Vector2.Distance(newer, path[i]);
                newer = path[i];
            }
            return MathF.Min(length, MaxTrailLength);
        }

        private Vector2 SamplePath(float distanceBehind) {
            Vector2 newer = Owner.Center;
            for (int i = path.Count - 1; i >= 0; i--) {
                Vector2 older = path[i];
                float segmentLength = Vector2.Distance(newer, older);
                if (segmentLength <= 0.001f) {
                    newer = older;
                    continue;
                }

                if (distanceBehind <= segmentLength) {
                    return Vector2.Lerp(newer, older, distanceBehind / segmentLength);
                }

                distanceBehind -= segmentLength;
                newer = older;
            }
            return path.Count > 0 ? path[0] : Owner.Center;
        }

        private PathFrame SamplePathFrame(float distanceBehind) {
            const float sampleStep = 17f;
            Vector2 position = SamplePath(distanceBehind);
            Vector2 ahead = SamplePath(MathF.Max(0f, distanceBehind - sampleStep));
            Vector2 behind = SamplePath(distanceBehind + sampleStep);
            Vector2 tangent = (ahead - behind).SafeNormalize(moveDirection);

            Vector2 nearTangent = (ahead - position).SafeNormalize(tangent);
            Vector2 farTangent = (position - behind).SafeNormalize(tangent);
            float cross = farTangent.X * nearTangent.Y - farTangent.Y * nearTangent.X;
            float curvature = MathHelper.Clamp(cross * 4.5f, -1f, 1f);
            return new PathFrame(position, tangent, curvature);
        }

        private void InitializePetals() {
            for (int i = 0; i < CorePetalCount; i++) {
                petals.Add(CreateFlowPetal(PetalRole.Core, i, CorePetalCount));
            }
            for (int i = 0; i < BraidPetalCount; i++) {
                petals.Add(CreateFlowPetal(PetalRole.Braid, i, BraidPetalCount));
            }
        }

        private Petal CreateFlowPetal(PetalRole role, int index, int count) {
            float y = Main.rand.NextFloat(-Owner.height * 0.54f, Owner.height * 0.54f);
            float yRatio = MathF.Abs(y) / Math.Max(Owner.height * 0.54f, 1f);
            float halfWidth = MathHelper.Lerp(Owner.width * 0.52f, Owner.width * 0.20f, yRatio);
            Vector2 bodyOffset = new(Main.rand.NextFloat(-halfWidth, halfWidth), y);
            float along = Vector2.Dot(bodyOffset, moveDirection);
            float release = MathHelper.Clamp(0.18f + along / Math.Max(Owner.height, 1f) * 0.38f
                + Main.rand.NextFloat(0f, 0.22f), 0.05f, 0.78f);

            float baseDistance = role == PetalRole.Braid
                ? (index + 0.5f) / count * MaxTrailLength + Main.rand.NextFloat(-15f, 15f)
                : Main.rand.NextFloat(0f, 24f);
            float baseScale = role == PetalRole.Core
                ? Main.rand.NextFloat(0.72f, 1.16f)
                : Main.rand.NextFloat(0.46f, 0.94f);

            return new Petal {
                Role = role,
                Position = Owner.Center + bodyOffset,
                PreviousPosition = Owner.Center + bodyOffset,
                BodyOffset = bodyOffset,
                Phase = Main.rand.NextFloat(MathHelper.TwoPi),
                Spin = Main.rand.NextFloat(0.050f, 0.135f),
                Radius = role == PetalRole.Core
                    ? Main.rand.NextFloat(17f, 36f)
                    : Main.rand.NextFloat(38f, 78f),
                BaseTrailDistance = baseDistance,
                BaseScale = baseScale,
                RenderScale = baseScale,
                Flip = 1f,
                Release = release,
                Seed = Main.rand.NextFloat(MathHelper.TwoPi),
                FlowRate = Main.rand.NextFloat(0.62f, 1.42f),
                Wander = Main.rand.NextFloat(0.65f, 1.45f),
                Opacity = role == PetalRole.Core
                    ? Main.rand.NextFloat(0.84f, 0.98f)
                    : Main.rand.NextFloat(0.64f, 0.90f),
                Lane = index % 2 == 0 ? 1 : -1,
                DeepColor = Main.rand.NextBool(role == PetalRole.Core ? 18 : 22),
                Glow = Main.rand.NextBool(role == PetalRole.Core ? 7 : 17)
            };
        }

        private void UpdatePetals(float frameTravel) {
            if (Timer < FlightEndFrame) {
                SpawnFlightLoosePetals(frameTravel);
            }

            for (int i = petals.Count - 1; i >= 0; i--) {
                Petal petal = petals[i];
                petal.PreviousPosition = petal.Position;

                if (petal.Role == PetalRole.Loose) {
                    if (!UpdateLoosePetal(petal)) {
                        petals.RemoveAt(i);
                    }
                    continue;
                }

                if (Timer < FlightEndFrame) {
                    UpdateFlowPetal(petal);
                }
                else if (Timer < ReformEndFrame) {
                    UpdateReformPetal(petal);
                }
            }
        }

        private void UpdateFlowPetal(Petal petal) {
            Vector2 target;
            float targetAlpha;

            if (petal.Role == PetalRole.Core) {
                target = GetCoreTarget(petal);
                targetAlpha = petal.Opacity;
            }
            else {
                target = GetBraidTarget(petal, out targetAlpha);
            }

            if (Timer <= DissolveFrames) {
                float dissolve = Timer / (float)DissolveFrames;
                float localT = MathHelper.Clamp((dissolve - petal.Release) / Math.Max(1f - petal.Release, 0.08f), 0f, 1f);
                float eased = MathHelper.SmoothStep(0f, 1f, localT);
                Vector2 bodyPosition = Owner.Center + petal.BodyOffset;
                Vector2 peel = moveDirection * (eased * eased * 16f)
                    + new Vector2(-moveDirection.Y, moveDirection.X)
                    * MathF.Sin(petal.Seed + eased * MathHelper.Pi) * 7f;
                petal.Position = Vector2.Lerp(bodyPosition, target, eased) + peel;
                petal.Alpha = eased * 0.96f;
                petal.RenderScale = petal.BaseScale * MathHelper.Lerp(0.62f, 1f, eased);
                return;
            }

            float follow = petal.Role == PetalRole.Core
                ? 0.62f
                : MathHelper.Lerp(0.27f, 0.40f, 1f / petal.Wander);
            petal.Position = Vector2.Lerp(petal.Position, target, follow);
            petal.Alpha = MathHelper.Lerp(petal.Alpha, targetAlpha, 0.17f);
            petal.RenderScale = petal.BaseScale
                * MathHelper.Lerp(0.78f, 1.08f, (petal.Depth + 1f) * 0.5f);
        }

        private Vector2 GetCoreTarget(Petal petal) {
            Vector2 normal = new(-moveDirection.Y, moveDirection.X);
            float theta = petal.Phase + petal.Lane * Timer * petal.Spin;
            float driftWave = MathF.Sin(Timer * (0.042f + petal.FlowRate * 0.018f)
                + petal.Seed * 1.7f);
            float sideWave = MathF.Sin(theta) * 0.58f + driftWave * 0.42f;
            float depth = MathF.Cos(theta * 0.83f + driftWave * 0.9f);
            float breath = 0.76f + 0.24f * MathF.Sin(Timer * 0.12f - petal.Seed);
            float compression = MathHelper.Lerp(1.16f, 0.91f,
                MathHelper.Clamp(visualSpeedRatio, 0f, 1f));
            float radius = petal.Radius * breath * compression;

            petal.Depth = depth;
            petal.Flip = MathHelper.Lerp(0.18f, 1f, MathF.Abs(depth));
            petal.Stretch = MathHelper.Lerp(0.96f, 1.16f, MathHelper.Clamp(visualSpeedRatio, 0f, 1f));
            petal.Rotation = moveDirection.ToRotation() - MathHelper.PiOver2
                + MathF.Sin(theta * 0.57f + petal.Seed) * 0.72f;

            return Owner.Center
                - moveDirection * (petal.BaseTrailDistance + MathF.Abs(driftWave) * 8f)
                + normal * sideWave * radius
                + moveDirection * depth * 4f;
        }

        private Vector2 GetBraidTarget(Petal petal, out float targetAlpha) {
            float cycleLength = MathHelper.Clamp(availablePathLength, 72f, MaxTrailLength);
            float flowDistance = petal.BaseTrailDistance
                + MathF.Max(0, Timer - DissolveFrames)
                * (1.35f + petal.BaseScale * 0.48f) * petal.FlowRate;
            float distanceBehind = flowDistance % cycleLength;
            PathFrame frame = SamplePathFrame(distanceBehind);

            float theta = petal.Phase + petal.Lane
                * (Timer * petal.Spin * 0.62f + distanceBehind * 0.027f * petal.FlowRate);
            float secondary = MathF.Sin(Timer * (0.025f + petal.FlowRate * 0.017f)
                - distanceBehind * 0.018f + petal.Seed * 2.1f);
            float depth = MathF.Cos(theta + secondary * 0.86f);
            float side = MathF.Sin(theta) * 0.36f + secondary * 0.64f;
            float speedSpread = MathHelper.Lerp(1.22f, 0.96f,
                MathHelper.Clamp(visualSpeedRatio, 0f, 1f));
            float breath = 0.68f + 0.32f
                * MathF.Sin(Timer * 0.105f - distanceBehind * 0.024f + petal.Seed);
            float outerBloom = frame.Curvature * side > 0f
                ? 1f + MathF.Abs(frame.Curvature) * 1.12f
                : 1f - MathF.Abs(frame.Curvature) * 0.12f;
            float radius = petal.Radius * speedSpread * breath * outerBloom;
            float driftingCenter = MathF.Sin(Timer * 0.031f - distanceBehind * 0.014f
                + petal.Seed * 1.3f) * (12f + petal.Wander * 12f);

            float headFade = MathHelper.SmoothStep(0f, 1f,
                MathHelper.Clamp(distanceBehind / 18f, 0f, 1f));
            float tailFade = MathHelper.SmoothStep(0f, 1f,
                MathHelper.Clamp((cycleLength - distanceBehind) / 34f, 0f, 1f));
            float coverage = MathHelper.Clamp((availablePathLength - distanceBehind + 20f) / 20f, 0f, 1f);
            float patchWave = 0.5f + 0.5f * MathF.Sin(distanceBehind * 0.043f
                - Timer * 0.073f + Seed * 8f + petal.Lane * 0.62f);
            float patch = MathHelper.SmoothStep(0f, 1f,
                MathHelper.Clamp((patchWave - 0.16f) / 0.72f, 0f, 1f));
            targetAlpha = headFade * tailFade * coverage * petal.Opacity
                * MathHelper.Lerp(0.42f, 1f, patch);

            petal.Depth = depth;
            petal.Flip = MathHelper.Lerp(0.14f, 1f, MathF.Abs(depth));
            petal.Stretch = MathHelper.Lerp(0.94f, 1.24f,
                MathHelper.Clamp(visualSpeedRatio, 0f, 1f))
                * MathHelper.Lerp(0.90f, 1.08f, (depth + 1f) * 0.5f);
            petal.Rotation = frame.Tangent.ToRotation() - MathHelper.PiOver2
                + MathF.Sin(theta * 0.51f + petal.Seed + secondary) * 0.92f;

            return frame.Position
                + frame.Normal * (driftingCenter + side * radius)
                + frame.Tangent * depth * (2f + petal.BaseScale * 3f);
        }

        private void SpawnFlightLoosePetals(float frameTravel) {
            if (Timer <= DissolveFrames || frameTravel <= 0.5f || CountLoosePetals() >= MaxLoosePetals) {
                lastVisualDirection = moveDirection;
                return;
            }

            looseSpawnCarry += frameTravel;
            Vector2 normal = new(-moveDirection.Y, moveDirection.X);
            while (looseSpawnCarry >= 14f && CountLoosePetals() < MaxLoosePetals) {
                looseSpawnCarry -= 14f;
                Vector2 position = Owner.Center - moveDirection * Main.rand.NextFloat(0f, MathF.Min(frameTravel, 30f))
                    + normal * Main.rand.NextFloat(-46f, 46f);
                Vector2 velocity = -moveDirection * Main.rand.NextFloat(0.2f, 2.1f)
                    + normal * Main.rand.NextFloat(-3.6f, 3.6f)
                    + Main.rand.NextVector2Circular(0.8f, 0.8f)
                    - Vector2.UnitY * Main.rand.NextFloat(0f, 0.7f);
                SpawnLoosePetal(position, velocity, Main.rand.Next(46, 82), Main.rand.NextFloat(0.56f, 0.84f));
            }

            float turn = lastVisualDirection.X * moveDirection.Y - lastVisualDirection.Y * moveDirection.X;
            if (MathF.Abs(turn) > 0.045f && Main.rand.NextBool(2) && CountLoosePetals() < MaxLoosePetals) {
                float side = MathF.Sign(turn);
                int burst = Main.rand.Next(1, 4);
                for (int i = 0; i < burst && CountLoosePetals() < MaxLoosePetals; i++) {
                    Vector2 position = Owner.Center + normal * side * Main.rand.NextFloat(20f, 48f);
                    Vector2 velocity = normal * side * Main.rand.NextFloat(3.0f, 7.0f)
                        - moveDirection * Main.rand.NextFloat(0.2f, 1.6f)
                        + Main.rand.NextVector2Circular(0.8f, 0.8f);
                    SpawnLoosePetal(position, velocity, Main.rand.Next(52, 88), Main.rand.NextFloat(0.62f, 0.88f));
                }
            }
            lastVisualDirection = moveDirection;
        }

        private int CountLoosePetals() {
            int count = 0;
            foreach (Petal petal in petals) {
                if (petal.Role == PetalRole.Loose) {
                    count++;
                }
            }
            return count;
        }

        private void SpawnLoosePetal(Vector2 position, Vector2 velocity, int life, float alpha) {
            if (petals.Count >= MaxPetalCount) {
                return;
            }

            petals.Add(new Petal {
                Role = PetalRole.Loose,
                Position = position,
                PreviousPosition = position,
                Velocity = velocity,
                Phase = Main.rand.NextFloat(MathHelper.TwoPi),
                Spin = Main.rand.NextFloat(0.055f, 0.145f),
                RotSpeed = Main.rand.NextFloat(-0.12f, 0.12f),
                BaseScale = Main.rand.NextFloat(0.42f, 0.92f),
                RenderScale = Main.rand.NextFloat(0.42f, 0.92f),
                Flip = 1f,
                Alpha = alpha,
                InitialAlpha = alpha,
                Seed = Main.rand.NextFloat(MathHelper.TwoPi),
                MaxLife = life,
                DeepColor = Main.rand.NextBool(18),
                Glow = Main.rand.NextBool(19)
            });
        }

        private bool UpdateLoosePetal(Petal petal) {
            petal.Age++;
            if (petal.Age >= petal.MaxLife) {
                return false;
            }

            petal.Velocity *= 0.975f;
            petal.Velocity.Y += 0.006f;
            petal.Velocity.X += MathF.Sin(petal.Age * 0.105f + petal.Seed) * 0.028f;
            petal.Velocity.Y += MathF.Cos(petal.Age * 0.073f + petal.Seed) * 0.012f;
            petal.Position += petal.Velocity;
            petal.Rotation += petal.RotSpeed;

            float life = petal.Age / (float)petal.MaxLife;
            float envelope = MathF.Pow(MathF.Sin(life * MathHelper.Pi), 0.48f);
            petal.Depth = MathF.Sin(petal.Age * petal.Spin + petal.Seed);
            petal.Flip = MathHelper.Lerp(0.20f, 1f, MathF.Abs(petal.Depth));
            petal.Stretch = 1f + MathHelper.Clamp(petal.Velocity.Length() / 9f, 0f, 0.34f);
            petal.RenderScale = petal.BaseScale
                * MathHelper.Lerp(0.82f, 1.08f, (petal.Depth + 1f) * 0.5f);
            petal.Alpha = petal.InitialAlpha * envelope;
            return true;
        }

        private void BeginReform() {
            reformStarted = true;
            Vector2 normal = new(-moveDirection.Y, moveDirection.X);
            foreach (Petal petal in petals) {
                if (petal.Role == PetalRole.Loose) {
                    continue;
                }

                petal.ReformStart = petal.Position;
                float lane = petal.Lane;
                petal.ReformControlA = petal.Position
                    + moveDirection * Main.rand.NextFloat(36f, 74f)
                    + normal * lane * Main.rand.NextFloat(18f, 48f);
                petal.ReformControlB = Owner.Center + petal.BodyOffset
                    + moveDirection * Main.rand.NextFloat(24f, 58f)
                    - normal * lane * Main.rand.NextFloat(8f, 26f);
            }

            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Grass with {
                    Pitch = 0.38f,
                    Volume = 0.72f
                }, Owner.Center);
                SoundEngine.PlaySound(CWRSound.SwiftSlice with {
                    Pitch = 0.42f,
                    Volume = 0.42f
                }, Owner.Center);
            }
        }

        private void UpdateReformPetal(Petal petal) {
            float t = MathHelper.Clamp((Timer - FlightEndFrame) / (float)ReformFrames, 0f, 1f);
            float eased = MathHelper.SmoothStep(0f, 1f, t);
            Vector2 target = Owner.Center + petal.BodyOffset;
            Vector2 position = CubicBezier(petal.ReformStart, petal.ReformControlA,
                petal.ReformControlB, target, eased);

            Vector2 normal = new(-moveDirection.Y, moveDirection.X);
            float recoilSpiral = MathF.Sin(t * MathHelper.TwoPi + petal.Phase)
                * petal.Radius * 0.32f * (1f - eased);
            petal.Position = position + normal * recoilSpiral;
            petal.Depth = MathF.Cos(t * MathHelper.TwoPi * petal.Lane + petal.Phase);
            petal.Flip = MathHelper.Lerp(0.20f, 1f, MathF.Abs(petal.Depth));
            petal.Stretch = MathHelper.Lerp(1.32f, 0.92f, eased);
            petal.RenderScale = petal.BaseScale * MathHelper.Lerp(1.08f, 0.58f, eased);
            petal.Rotation = (target - petal.Position).SafeNormalize(moveDirection).ToRotation()
                - MathHelper.PiOver2 + MathF.Sin(petal.Phase + t * 9f) * 0.32f;
            float finalFade = MathHelper.Clamp((t - 0.70f) / 0.30f, 0f, 1f);
            petal.Alpha = MathHelper.Lerp(0.98f, 0.16f, finalFade);
        }

        private void BeginAfterglow() {
            afterglowStarted = true;
            foreach (Petal petal in petals) {
                if (petal.Role == PetalRole.Loose) {
                    continue;
                }

                petal.Role = PetalRole.Loose;
                petal.Velocity = (petal.Position - petal.PreviousPosition) * 0.32f
                    + Main.rand.NextVector2Circular(0.9f, 0.9f)
                    - Vector2.UnitY * Main.rand.NextFloat(0.15f, 0.65f);
                petal.Age = 0;
                petal.MaxLife = AfterglowFrames + Main.rand.Next(-4, 5);
                petal.InitialAlpha = MathF.Max(petal.Alpha, 0.16f);
                petal.RotSpeed = Main.rand.NextFloat(-0.10f, 0.10f);
            }
        }

        private static Vector2 CubicBezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t) {
            float inv = 1f - t;
            return a * (inv * inv * inv)
                + b * (3f * inv * inv * t)
                + c * (3f * inv * t * t)
                + d * (t * t * t);
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || petals.Count == 0
                || CWRAsset.Placeholder_White?.Value is not Texture2D white
                || EffectLoader.OniDomainDeco?.Value is not Effect effect) {
                return;
            }

            drawBuffer.Clear();
            drawBuffer.AddRange(petals);
            drawBuffer.Sort(static (a, b) => a.Depth.CompareTo(b.Depth));

            SpriteBatch spriteBatch = Main.spriteBatch;
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);

            effect.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.05f);
            effect.CurrentTechnique = effect.Techniques["TechPetal"];
            effect.CurrentTechnique.Passes[0].Apply();

            Vector2 origin = white.Size() * 0.5f;
            foreach (Petal petal in drawBuffer) {
                if (petal.Alpha <= 0.005f) {
                    continue;
                }

                float front = (petal.Depth + 1f) * 0.5f;
                Color back = petal.DeepColor ? new Color(178, 48, 79) : new Color(244, 157, 183);
                Color middle = petal.DeepColor ? new Color(229, 90, 119) : new Color(255, 196, 213);
                Color face = petal.DeepColor ? new Color(255, 174, 191) : new Color(255, 243, 247);
                Color color = front < 0.5f
                    ? Color.Lerp(back, middle, front * 2f)
                    : Color.Lerp(middle, face, front * 2f - 1f);
                //PSPetal 会自行输出预乘色；这里只写透明度，不能再次压暗 RGB。
                float opacity = MathHelper.Clamp(
                    petal.Alpha * MathHelper.Lerp(0.76f, 1f, front), 0f, 1f);
                color.A = (byte)(opacity * byte.MaxValue);

                float width = 19f * petal.RenderScale * petal.Flip;
                float height = 25f * petal.RenderScale * petal.Stretch;
                spriteBatch.Draw(white, petal.Position - Main.screenPosition, null, color,
                    petal.Rotation, origin,
                    new Vector2(width / white.Width, height / white.Height),
                    SpriteEffects.None, 0f);
            }

            spriteBatch.End();
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (Main.dedServ || CWRAsset.SoftGlow?.Value is not Texture2D glow) {
                return;
            }

            Vector2 glowOrigin = glow.Size() * 0.5f;
            float phaseEnvelope;
            if (Timer <= DissolveFrames) {
                phaseEnvelope = MathF.Sin(MathHelper.Clamp(Timer / (float)DissolveFrames, 0f, 1f)
                    * MathHelper.Pi);
            }
            else if (Timer >= FlightEndFrame && Timer <= ReformEndFrame) {
                float t = (Timer - FlightEndFrame) / (float)ReformFrames;
                phaseEnvelope = MathF.Sin(MathHelper.Clamp(t, 0f, 1f) * MathHelper.Pi);
            }
            else {
                phaseEnvelope = 0.11f;
            }

            if (phaseEnvelope > 0.01f) {
                Color coreColor = new Color(1f, 0.30f, 0.46f, 0f) * (0.26f * phaseEnvelope);
                float coreScale = (92f + phaseEnvelope * 54f) / glow.Width;
                spriteBatch.Draw(glow, Owner.Center - Main.screenPosition, null, coreColor,
                    0f, glowOrigin, coreScale, SpriteEffects.None, 0f);
            }

            for (int i = 0; i < petals.Count; i++) {
                Petal petal = petals[i];
                if (!petal.Glow || petal.Depth < 0.45f || petal.Alpha < 0.15f) {
                    continue;
                }

                float alpha = petal.Alpha * (petal.Depth - 0.45f) / 0.55f * 0.12f;
                Color color = new Color(1f, 0.34f, 0.50f, 0f) * alpha;
                float scale = 34f * petal.RenderScale / glow.Width;
                spriteBatch.Draw(glow, petal.Position - Main.screenPosition, null, color,
                    0f, glowOrigin, scale, SpriteEffects.None, 0f);
            }
        }
    }
}
