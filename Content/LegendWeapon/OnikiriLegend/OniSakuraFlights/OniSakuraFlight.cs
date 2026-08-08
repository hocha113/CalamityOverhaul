using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
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
    /// <summary>樱流飞行. 表世界化樱巡航(樱流键直起 或 疾走衔接)</summary>
    internal sealed class OniSakuraFlight : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable, IWarpDrawable
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
        //核瓣压密成可读的一团(而非稀疏环绕)，总量守恒:从编织带匀过来，不涨 draw call
        private const int CorePetalCount = 34;
        private const int BraidPetalCount = 42;
        private const int MaxPetalCount = 164;
        private const int MaxLoosePetals = MaxPetalCount - CorePetalCount - BraidPetalCount;
        private const float PathSpacing = 10f;
        private const float MaxTrailLength = 420f;
        private const int MaxPathPoints = 52;
        private const float MinFlightSpeed = 14f;
        //上限抬到能容下巡航加速的终值(40 × CruiseGain)，否则加速被钳制掉
        private const float MaxFlightSpeed = 56f;
        /// <summary>巡航加速的爬升帧数与终值倍率:硬规禁匀速直线，速度全程在演化</summary>
        private const int CruiseRampFrames = 40;
        private const float CruiseGain = 1.30f;
        private const int MinFlightFrames = 12;
        private const int MaxFlightFrames = 180;
        /// <summary>贴光标死区(px²):过近不改向,避免绕着光标原地抖转</summary>
        private const float AimDeadzoneSq = 576f;

        /// <summary>
        /// 四股流带静态档:瓣白主脊 + 主流带 + 上下两侧股(三种流速造层间视差).
        /// 幅宽/偏移在绘制时按航线长与速度再缩
        /// </summary>
        private static readonly OniSakuraFlowRenderer.StreamDef[] StreamDefs =
        [
            //中脊档位整体压低:瓣流的亮来自密度，白热常驻是能量拖尾的腔
            new() { HalfWidth = 13f, PerpOffset = 0f, Seed = 0.71f
                , FlowMul = 1.55f, GrainAmp = 0.30f, HeadBoost = 1.00f, OpacityMul = 0.70f },
            new() { HalfWidth = 42f, PerpOffset = 0f, Seed = 0.05f
                , FlowMul = 1.00f, GrainAmp = 0.95f, HeadBoost = 0.35f, OpacityMul = 0.95f },
            new() { HalfWidth = 21f, PerpOffset = 32f, Seed = 0.37f
                , FlowMul = 1.42f, GrainAmp = 1.25f, HeadBoost = 0.22f, OpacityMul = 0.78f },
            new() { HalfWidth = 17f, PerpOffset = -37f, Seed = 0.89f
                , FlowMul = 0.76f, GrainAmp = 1.40f, HeadBoost = 0.20f, OpacityMul = 0.66f },
        ];

        private readonly List<Vector2> path = new(MaxPathPoints);
        private readonly List<Petal> petals = new(MaxPetalCount);
        private readonly List<Petal> drawBuffer = new(MaxPetalCount);
        /// <summary>流带顶点用的尾→头点列(path + 当前头端)，每帧重填不重新分配</summary>
        private readonly List<Vector2> streamPoints = new(MaxPathPoints + 2);

        private bool initialized;
        private bool reformStarted;
        private bool afterglowStarted;
        private bool ownerReleased;
        private Vector2 moveDirection;
        private Vector2 lastObservedCenter;
        private Vector2 lastVisualDirection;
        private float flightSpeed;
        /// <summary>起飞时的基准速度，巡航包络在此之上做增益</summary>
        private float baseFlightSpeed;
        private float pathCarry;
        private float availablePathLength;
        private float looseSpawnCarry;
        private float visualSpeedRatio;
        /// <summary>平滑后的转向侧倾(-1..1)，供涡核偏摆与瓣盘倾角共用</summary>
        private float turnBank;
        private Vector2 lastBankDirection;

        public override string Texture => CWRConstant.VaultPlaceholder;

        private Player Owner => Main.player[Projectile.owner];
        private int Timer {
            get => (int)Projectile.ai[0];
            set => Projectile.ai[0] = value;
        }
        private int FlightDuration => Math.Clamp((int)Projectile.ai[1], MinFlightFrames, MaxFlightFrames);
        private float Seed => Projectile.ai[2];
        private int FlightEndFrame => DissolveFrames + FlightDuration;
        private int ReformEndFrame => FlightEndFrame + ReformFrames;
        private int ReappearFrame => FlightEndFrame + (int)(ReformFrames * 0.72f);
        private int KillFrame => ReformEndFrame + AfterglowFrames;

        /// <summary>该控制器当前是否应取代持有者本体绘制</summary>
        internal bool ShouldHideOwner => Timer >= HideStartFrame && Timer < ReappearFrame;

        /// <summary>流带召回进度:飞行段恒 0，回卷段自尾端擦到头</summary>
        private float StreamRetract => Timer <= FlightEndFrame
            ? 0f
            : MathHelper.Clamp((Timer - FlightEndFrame) / (float)ReformFrames, 0f, 1f);

        /// <summary>过曝拍:散瓣起飞的头几帧 + 合拢那一下</summary>
        private float StreamFlash {
            get {
                if (Timer <= DissolveFrames) {
                    return MathHelper.Clamp(1f - Timer / (float)DissolveFrames, 0f, 1f);
                }
                int sinceStop = Timer - FlightEndFrame;
                return sinceStop >= 0 && sinceStop <= 3
                    ? 0.70f * (1f - sinceStop / 3f)
                    : 0f;
            }
        }

        /// <summary>花核在场强度:成形→满→回卷时交还本体</summary>
        private float CoreEnvelope {
            get {
                if (Timer < HideStartFrame) {
                    return 0f;
                }
                if (Timer <= DissolveFrames + 2) {
                    return MathHelper.Clamp(
                        (Timer - HideStartFrame) / (float)(DissolveFrames + 2 - HideStartFrame), 0f, 1f);
                }
                return Timer < FlightEndFrame
                    ? 1f
                    : MathHelper.Clamp(1f - (Timer - FlightEndFrame) / (ReformFrames * 0.70f), 0f, 1f);
            }
        }

        /// <summary>在持有者客户端启动樱流飞行。巡航段跟随光标平滑转向</summary>
        public static Projectile Fire(Player player, Vector2 aim, float speed = 32f,
            int flightFrames = 40, IEntitySource source = null, bool seamless = false) {
            if (player == null || !player.Alives()) {
                return null;
            }

            OniSakuraFlight existing = Find(player.whoAmI);
            if (existing != null) {
                return existing.Projectile;
            }

            source ??= player.GetSource_Misc("CWR_OniSakuraFlight");
            Vector2 direction = aim.SafeNormalize(Vector2.UnitX * player.direction);
            speed = MathHelper.Clamp(speed, MinFlightSpeed, MaxFlightSpeed);
            flightFrames = Math.Clamp(flightFrames, MinFlightFrames, MaxFlightFrames);
            float seed = Main.rand.NextFloat(0.01f, 0.99f);

            return Projectile.NewProjectileDirect(source, player.Center, direction * speed,
                ModContent.ProjectileType<OniSakuraFlight>(), 0, 0f, player.whoAmI,
                ai0: seamless ? HideStartFrame : 0f, ai1: flightFrames, ai2: seed);
        }

        /// <summary>令指定玩家正在进行的樱流飞行立即进入回卷重组阶段</summary>
        public static void RequestStop(Player player) {
            if (player == null) {
                return;
            }
            OniSakuraFlight flight = Find(player.whoAmI);
            if (flight == null || flight.Timer >= flight.FlightEndFrame) {
                return;
            }
            flight.Timer = flight.FlightEndFrame;
            flight.Projectile.netUpdate = true;
        }

        /// <summary>该玩家当前的樱流控制器，无则 null(Fire 去重保证至多一个)</summary>
        private static OniSakuraFlight Find(int playerIndex) {
            int type = ModContent.ProjectileType<OniSakuraFlight>();
            foreach (Projectile projectile in Main.ActiveProjectiles) {
                if (projectile.type == type && projectile.owner == playerIndex
                    && projectile.ModProjectile is OniSakuraFlight flight) {
                    return flight;
                }
            }
            return null;
        }

        /// <summary>供 PlayerOverride 查询任意玩家是否已被同步的樱流控制器取代</summary>
        internal static bool IsPlayerHidden(int playerIndex) => Find(playerIndex)?.ShouldHideOwner ?? false;

        /// <summary>飞行段(可被 <see cref="RequestStop"/> 收束的窗口)</summary>
        internal static bool IsTraveling(int playerIndex) {
            OniSakuraFlight flight = Find(playerIndex);
            return flight != null && flight.Timer < flight.FlightEndFrame;
        }

        /// <summary>樱流仍握有本体操控(起飞至重现身)，期间疾走/处决等不受理</summary>
        internal static bool ControlsOwner(int playerIndex) {
            OniSakuraFlight flight = Find(playerIndex);
            return flight != null && flight.Timer < flight.ReappearFrame;
        }

        /// <summary>该玩家存在樱流控制器(任意阶段)，疾走衔接后的视觉分支在远端凭此推断</summary>
        internal static bool AnyFor(int playerIndex) => Find(playerIndex) != null;

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
            baseFlightSpeed = flightSpeed;
            moveDirection = Projectile.velocity.SafeNormalize(Vector2.UnitX * Owner.direction);
            lastVisualDirection = moveDirection;
            lastBankDirection = moveDirection;
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
                if (OnLocalScreen()) {
                    CrimsonImpactFX.PushImpact(Owner.Center, 0.34f);
                }
                //散瓣那口气:少量活瓣向外炸开，与向后汇入流路的类内瓣一收一放
                for (int i = 0; i < 9; i++) {
                    Vector2 burstVel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.6f, 4.4f)
                        + moveDirection * Main.rand.NextFloat(0.5f, 1.6f);
                    Color burstTint = Main.rand.NextBool(4)
                        ? new Color(214, 76, 108)
                        : new Color(255, 206, 220);
                    PRTLoader.NewParticle<PRT_OniSakuraDrift>(
                        Owner.Center + Main.rand.NextVector2Circular(10f, 16f), burstVel, burstTint
                        , Main.rand.NextFloat(0.65f, 1.05f))
                        ?.Configure(Main.rand.Next(45, 75), Main.rand.NextFloat(0.40f, 0.55f));
                }
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
            //纯 Timer 的确定性函数，各端算得一样，远端的 visualSpeedRatio 才不会飘
            UpdateFlightSpeed();

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

            //转向侧倾:方向叉积平滑到 -1..1，系数取满转角(≈0.31rad/帧)刚好打满
            float turnCross = lastBankDirection.X * moveDirection.Y - lastBankDirection.Y * moveDirection.X;
            turnBank = MathHelper.Lerp(turnBank, MathHelper.Clamp(turnCross * 3.2f, -1f, 1f), 0.22f);
            lastBankDirection = moveDirection;

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
                PushScreenState();
                float pulse = 0.72f + 0.18f * MathF.Sin(Timer * 0.22f);
                Lighting.AddLight(Owner.Center, new Vector3(0.82f, 0.24f, 0.34f) * pulse);
            }

            if (Timer >= KillFrame) {
                Projectile.Kill();
            }
        }

        /// <summary>
        /// 巡航速度包络:起飞速度 → 复合加速到 <see cref="CruiseGain"/> 倍 → 极缓地沉回一点.
        /// 全程有量在演化，不是一条匀速直线
        /// </summary>
        private void UpdateFlightSpeed() {
            int travel = Timer - HideStartFrame;
            if (travel < 0 || baseFlightSpeed <= 0f) {
                return;
            }
            float ramp = MathHelper.Clamp(travel / (float)CruiseRampFrames, 0f, 1f);
            float gain = MathHelper.Lerp(1f, CruiseGain, ramp * (2f - ramp))
                - 0.06f * MathHelper.Clamp((travel - CruiseRampFrames) / 90f, 0f, 1f);
            flightSpeed = MathHelper.Clamp(baseFlightSpeed * gain, MinFlightSpeed, MaxFlightSpeed);
        }

        private void UpdateOwnerMovement() {
            if (Timer < HideStartFrame) {
                HoldOwner();
                PullbackWindup();
                return;
            }

            if (Timer >= FlightEndFrame) {
                if (Timer < ReappearFrame) {
                    HoldOwner();
                }
                return;
            }

            Vector2 toMouse = Main.MouseWorld - Owner.Center;
            if (toMouse.LengthSquared() > AimDeadzoneSq) {
                Vector2 desiredDirection = toMouse.SafeNormalize(moveDirection);
                float currentAngle = moveDirection.ToRotation();
                float turn = MathHelper.WrapAngle(desiredDirection.ToRotation() - currentAngle);
                //光标瞄准比方向键更吃响应:略放宽每帧转角,高速时仍保留一点惯性
                float maxTurn = MathHelper.Lerp(0.18f, 0.32f, 1f - MathHelper.Clamp(visualSpeedRatio, 0f, 1f));
                moveDirection = (currentAngle + MathHelper.Clamp(turn, -maxTurn, maxTurn))
                    .ToRotationVector2();
            }

            float oldSyncedAngle = Projectile.velocity.ToRotation();
            Projectile.velocity = moveDirection * flightSpeed;
            if (Timer % 3 == 0
                && MathF.Abs(MathHelper.WrapAngle(moveDirection.ToRotation() - oldSyncedAngle)) > 0.025f) {
                Projectile.netUpdate = true;
            }

            //起飞过冲:两帧 1.35× 再落回 1(旧的 0.34→1 平滑爬升读不出"被吸走"那一顿)
            int sinceLaunch = Timer - HideStartFrame;
            float launch = sinceLaunch <= 1
                ? 1.35f
                : MathHelper.Lerp(1.35f, 1f, MathHelper.Clamp((sinceLaunch - 1f) / 4f, 0f, 1f));
            //刹停:只在最后三帧急收(旧的 6 帧 smoothstep 是滑行)
            float braking = MathHelper.Clamp((FlightEndFrame - Timer) / 3f, 0f, 1f);
            braking = 0.06f + 0.94f * braking * braking;
            Vector2 desiredMove = moveDirection * flightSpeed * launch * braking;
            Vector2 allowedMove = Collision.TileCollision(Owner.position, desiredMove,
                Owner.width, Owner.height, fallThrough: true, fall2: true, (int)Owner.gravDir);

            Owner.position += allowedMove;
            Owner.velocity = Vector2.Zero;
            Owner.fallStart = (int)(Owner.position.Y / 16f);
            //无敌只给起飞散瓣窗，巡航段仅免击退、可转向的持续位移不附赠免伤按钮

            if (Timer <= DissolveFrames + 4) {
                Owner.GivePlayerImmuneState(4);
            }
            Owner.noKnockback = true;
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.controlUseItem = false;
            Owner.controlUseTile = false;

            if (MathF.Abs(moveDirection.X) > 0.08f) {
                Owner.ChangeDir(moveDirection.X > 0f ? 1 : -1);
            }

            //撞墙判据用未加过冲/刹停的基准步长:表现层倍率不该改变撞墙灵敏度
            //(刹停帧 refLength < 3 自动停判，本来也在收尾)
            float refLength = flightSpeed * braking;
            if (refLength > 3f && allowedMove.Length() < refLength * 0.28f) {
                Timer = FlightEndFrame;
                Projectile.netUpdate = true;
            }
        }

        /// <summary>是否在可见范围内。远端玩家的樱流不该给本地屏幕加辉光</summary>
        private bool OnLocalScreen() {
            Rectangle view = new((int)Main.screenPosition.X - 260, (int)Main.screenPosition.Y - 260
                , Main.screenWidth + 520, Main.screenHeight + 520);
            return view.Contains(Owner.Center.ToPoint());
        }

        /// <summary>屏幕级包络:巡航恒亮 Bloom，回卷回落(复用绯红裂空 Bloom 管线)</summary>
        private void PushScreenState() {
            if (!OnLocalScreen()) {
                return;
            }
            float envelope = Timer <= FlightEndFrame ? 1f : 1f - StreamRetract;
            if (envelope <= 0.02f) {
                return;
            }
            CrimsonImpactFX.PushAmbience(Owner.Center
                , (0.16f + 0.14f * MathHelper.Clamp(visualSpeedRatio, 0f, 1f)) * envelope);
        }

        /// <summary>
        /// 散瓣那几帧的反向预备:身子先被往后拽一小段(逐帧加速，末帧最明显)，
        /// 紧接着的过冲才有对比。总位移约 8px，走碰撞不入墙
        /// </summary>
        private void PullbackWindup() {
            float t = (Timer + 1f) / HideStartFrame;
            NudgeOwner(-moveDirection * MathHelper.Lerp(1.6f, 4.4f, t * t));
        }

        /// <summary>过碰撞的一次性位移，供预备与后坐用</summary>
        private void NudgeOwner(Vector2 offset) {
            Owner.position += Collision.TileCollision(Owner.position, offset
                , Owner.width, Owner.height, fallThrough: true, fall2: true, (int)Owner.gravDir);
            Owner.fallStart = (int)(Owner.position.Y / 16f);
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
            if (!Main.dedServ) {
                SpawnDriftPetals();
            }
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }

            Owner.velocity = moveDirection * 5.5f;
            Owner.CWR().GetScreenShake(1.8f);
            //落地=操控交还帧,开追斩窗;樱流只存在于表世界,追斩自然采樱衣

            Owner.GetModPlayer<OnikiriPlayer>().OpenZanshinWindow(0, 0, moveDirection);
            Tutorial.OnikiriTutorialEvents.FireSakuraReleased();
        }

        /// <summary>
        /// 交还操控时撒一把生樱瓣。类内花瓣到 KillFrame 就全没了，
        /// 这批走 PRT 活得比弹幕久——落地之后现场还有东西在飘
        /// </summary>
        private void SpawnDriftPetals() {
            Vector2 normal = new(-moveDirection.Y, moveDirection.X);
            for (int i = 0; i < 16; i++) {
                Vector2 at = Owner.Center + Main.rand.NextVector2Circular(22f, 30f);
                Vector2 velocity = -moveDirection * Main.rand.NextFloat(0.4f, 2.6f)
                    + normal * Main.rand.NextFloat(-2.2f, 2.2f)
                    - Vector2.UnitY * Main.rand.NextFloat(0.2f, 1.5f);
                Color tint = Main.rand.NextBool(4)
                    ? new Color(214, 76, 108)
                    : new Color(255, 206, 220);
                PRTLoader.NewParticle<PRT_OniSakuraDrift>(at, velocity, tint
                    , Main.rand.NextFloat(0.85f, 1.35f))
                    ?.Configure(Main.rand.Next(90, 150), Main.rand.NextFloat(0.34f, 0.52f));
            }
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
            //核瓣放大到剪影能互相咬合，编织瓣保持细碎——两层不同空间频率
            float baseScale = role == PetalRole.Core
                ? Main.rand.NextFloat(0.95f, 1.45f)
                : Main.rand.NextFloat(0.46f, 0.94f);

            return new Petal {
                Role = role,
                Position = Owner.Center + bodyOffset,
                PreviousPosition = Owner.Center + bodyOffset,
                BodyOffset = bodyOffset,
                Phase = Main.rand.NextFloat(MathHelper.TwoPi),
                Spin = Main.rand.NextFloat(0.050f, 0.135f),
                Radius = role == PetalRole.Core
                    ? Main.rand.NextFloat(8f, 20f)
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
            //满速抹成流线,不是"一个旋转的贴图在平移"(2.05 有糊成条的风险，收到 1.75)
            petal.Stretch = MathHelper.Lerp(1f, 1.75f, MathHelper.Clamp(visualSpeedRatio, 0f, 1f));
            petal.Rotation = moveDirection.ToRotation() - MathHelper.PiOver2
                + MathF.Sin(theta * 0.57f + petal.Seed) * 0.72f;

            return Owner.Center
                - moveDirection * (petal.BaseTrailDistance + MathF.Abs(driftWave) * 8f)
                //转向时涡核甩向弯道外侧(离心)。y 向下的屏幕系里
                //normal 指向转入侧，故取负号
                + normal * (sideWave * radius - turnBank * (radius * 0.85f + 9f))
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
            petal.Stretch = MathHelper.Lerp(0.98f, 1.85f,
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
            if (Projectile.IsOwnedByLocalPlayer()) {
                //刹停后坐:合拢那一帧往回坠一下，力有去处
                NudgeOwner(-moveDirection * 6.5f);
            }
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
                if (OnLocalScreen()) {
                    CrimsonImpactFX.PushImpact(Owner.Center, 0.28f);
                }
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
            if (Main.dedServ) {
                return;
            }
            //先垫底:流带与花核走顶点层(自管设备状态)，再让离散花瓣压在上面
            DrawFlowLayer();
            DrawPetalLayer();
        }

        /// <summary>四股樱流带 + 三重花核瓣盘。顶点绘制，无 SpriteBatch</summary>
        private void DrawFlowLayer() {
            if (Timer > ReformEndFrame) {
                return;
            }

            GraphicsDevice device = Main.instance.GraphicsDevice;
            if (!OniSakuraFlowRenderer.BeginDraw(device, out Effect fx
                , out BlendState prevBlend, out RasterizerState prevRaster, out DepthStencilState prevDepth)) {
                return;
            }

            float retract = StreamRetract;
            float flash = StreamFlash;
            float speed01 = MathHelper.Clamp(visualSpeedRatio, 0f, 1f);

            streamPoints.Clear();
            streamPoints.AddRange(path);
            streamPoints.Add(Owner.Center);
            //起飞后几帧才把带铺满，避免第一帧一条硬边突然出现
            float streamOpacity = MathHelper.Clamp((Timer - HideStartFrame + 1f) / 5f, 0f, 1f);

            if (streamOpacity > 0.01f) {
                //航线还短时整条收窄,别让起飞那两帧甩出一截齐头粗带
                float sizeMul = MathHelper.Clamp(availablePathLength / 300f, 0.35f, 1f);
                for (int i = 0; i < StreamDefs.Length; i++) {
                    OniSakuraFlowRenderer.StreamDef def = StreamDefs[i];
                    def.HalfWidth *= sizeMul;
                    //侧股缓慢编织摆动，四股不再钉死在固定平行线上
                    def.PerpOffset *= sizeMul * (0.82f + 0.30f * MathF.Sin(Timer * 0.045f + i * 2.4f));
                    def.Seed += Seed * 6.28f;
                    //流速与瓣粒分明度都挂速度:飞得越快，粒被抹得越长、孔越少
                    def.FlowMul *= MathHelper.Lerp(0.72f, 1.24f, speed01);
                    def.GrainAmp *= MathHelper.Lerp(1.18f, 0.82f, speed01);
                    OniSakuraFlowRenderer.DrawStream(device, fx, streamPoints, def
                        , retract, flash, streamOpacity);
                }
            }

            float core = CoreEnvelope;
            if (core > 0.01f) {
                float stretch = 1f + speed01 * 0.85f;
                float radius = (31f + 5f * MathF.Sin(Timer * 0.13f + Seed * 5f)) * (0.55f + core * 0.45f);
                float spin = Timer * 0.048f + Seed * MathHelper.TwoPi + turnBank * 0.55f;
                float bloom = 0.85f + flash * 0.6f;
                //涡着色器自行预乘输出，淡入淡出只走 opacity 参数，顶点色不压暗 RGB。
                //两层就够:主涡 + 拖影(沿航线拖后、更扁更暗、臂相反向错开)，
                //旧的三盘同心叠加是同形堆叠，只加亮不加信息
                OniSakuraFlowRenderer.DrawCore(device, fx
                    , Owner.Center - moveDirection * (16f + speed01 * 28f)
                    , radius * 0.90f, moveDirection, stretch * 1.40f, -spin * 0.66f
                    , Seed * 6.28f + 2.1f, new Color(198, 62, 96), 0.30f, 0.16f, core * 0.36f);
                OniSakuraFlowRenderer.DrawCore(device, fx, Owner.Center
                    , radius, moveDirection, stretch, spin, Seed * 6.28f
                    , new Color(244, 157, 183), bloom, 0.90f + flash * 0.5f, core);
            }

            OniSakuraFlowRenderer.EndDraw(device, prevBlend, prevRaster, prevDepth);
        }

        /// <summary>离散花瓣群(OniDomainDeco.TechPetal)，压在流带与花核之上</summary>
        private void DrawPetalLayer() {
            if (petals.Count == 0
                || VaultAsset.placeholder2?.Value is not Texture2D white
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

            //这条批是真 Additive(源因子=SourceAlpha):tint 的 A=0 等于不画。
            //旧代码全用 A=0 染色，所以樱流的加色层此前从未显示过——沿疾走/残心的
            //写法让 A 随强度走(new Color(rgb) * x)，强度数值本身不放大
            if (phaseEnvelope > 0.01f) {
                Color coreColor = new Color(1f, 0.30f, 0.46f) * (0.26f * phaseEnvelope);
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
                Color color = new Color(1f, 0.34f, 0.50f) * alpha;
                float scale = 34f * petal.RenderScale / glow.Width;
                spriteBatch.Draw(glow, petal.Position - Main.screenPosition, null, color,
                    0f, glowOrigin, scale, SpriteEffects.None, 0f);
            }

            DrawWindStreaks(spriteBatch);
            DrawBurstFlares(spriteBatch);
        }

        /// <summary>
        /// 起飞与合拢的爆点:镜像疾走原点爆闪的语法，换樱色。
        /// 起飞锚在航线起点(人已飞远它也留在原地)，合拢锚在人身上
        /// </summary>
        private void DrawBurstFlares(SpriteBatch spriteBatch) {
            Vector2 launchAt = (path.Count > 0 ? path[0] : Owner.Center) - Main.screenPosition;
            if (Timer <= 9 && CWRAsset.TearSpread01?.Value is Texture2D tear) {
                float t = Timer / 9f;
                float tA = MathF.Pow(1f - t, 1.7f) * 0.80f;
                float tS = 0.85f + CrimsonSlashRenderer.EaseOutCubic(t) * 0.50f;
                spriteBatch.Draw(tear, launchAt, null, new Color(255, 178, 199) * tA, Seed * 6f
                    , tear.Size() * 0.5f, tS, SpriteEffects.None, 0);
                spriteBatch.Draw(tear, launchAt, null, new Color(229, 90, 119) * (tA * 0.75f)
                    , Seed * 6f + 0.45f, tear.Size() * 0.5f, tS * 0.72f, SpriteEffects.FlipVertically, 0);
            }
            if (Timer <= 4 && CWRAsset.StarFlare02?.Value is Texture2D flare) {
                float fA = 1f - Timer / 4f;
                spriteBatch.Draw(flare, launchAt, null, new Color(255, 232, 240) * (fA * 0.80f)
                    , Seed * 6f, flare.Size() * 0.5f, 0.65f + fA * 0.30f, SpriteEffects.None, 0);
            }
            int sinceStop = Timer - FlightEndFrame;
            if (sinceStop >= 0 && sinceStop <= 7 && CWRAsset.StarFlare02?.Value is Texture2D snap) {
                float t = sinceStop / 7f;
                float fA = MathF.Pow(1f - t, 1.5f) * 0.70f;
                spriteBatch.Draw(snap, Owner.Center - Main.screenPosition, null
                    , new Color(255, 214, 226) * fA, Seed * 9f + t * 0.6f
                    , snap.Size() * 0.5f, 0.55f + t * 0.45f, SpriteEffects.None, 0);
            }
        }

        /// <summary>
        /// 沿航线跑的风线:拉丝条纹，与花瓣不是同一介质也不是同一空间频率;
        /// 位置沿 path 采样故随航线弯曲，长度与亮度全挂速度
        /// </summary>
        private void DrawWindStreaks(SpriteBatch spriteBatch) {
            if (Timer <= DissolveFrames || Timer > FlightEndFrame + 4
                || CWRAsset.SlashStreak01?.Value is not Texture2D streak) {
                return;
            }
            float speed01 = MathHelper.Clamp(visualSpeedRatio, 0f, 1f);
            if (speed01 <= 0.18f) {
                return;
            }

            Vector2 origin = streak.Size() * 0.5f;
            for (int i = 0; i < 5; i++) {
                float phase = (Timer * (0.052f + i * 0.009f) + i * 0.37f + Seed) % 1f;
                PathFrame frame = SamplePathFrame(phase * MathHelper.Lerp(120f, 260f, speed01));
                float lateral = MathF.Sin(i * 2.1f + Seed * 9f) * (12f + i * 7f);
                float envelope = MathF.Sin(phase * MathHelper.Pi);
                float alpha = envelope * speed01 * 0.42f;
                float length = (78f + speed01 * 132f) * (0.60f + envelope * 0.40f);
                float thick = 4.5f + i % 2 * 2f;
                //Additive 批源乘 srcAlpha，A 必须随强度走，A=0 = 不画
                spriteBatch.Draw(streak, frame.Position + frame.Normal * lateral - Main.screenPosition
                    , null, new Color(255, 214, 226) * alpha
                    , frame.Tangent.ToRotation(), origin
                    , new Vector2(length / streak.Width, thick / streak.Height)
                    , SpriteEffects.None, 0f);
            }
        }

        /// <summary>
        /// 沿航向的空气拉扯(KamuiLine 位移场)，与疾走同一条语汇。
        /// 尾锚只取最近一段航线，否则急转后轴向会指到反方向
        /// </summary>
        void IWarpDrawable.Warp() {
            if (path.Count < 2 || EffectLoader.NeutronWarp?.Value is not Effect warpFx
                || VaultAsset.placeholder2?.Value is not Texture2D px) {
                return;
            }
            float envelope = (Timer <= FlightEndFrame ? 1f : 1f - StreamRetract)
                * MathHelper.Clamp(visualSpeedRatio * 1.4f, 0f, 1f);
            if (envelope <= 0.03f) {
                return;
            }

            Vector2 tail = SamplePath(180f);
            float length = Vector2.Distance(tail, Owner.Center);
            if (length < 60f) {
                return;
            }
            float angle = moveDirection.ToRotation();

            warpFx.Parameters["uTime"]?.SetValue((float)Main.GameUpdateCount * 0.05f);
            warpFx.Parameters["uIntensity"]?.SetValue(0.16f);
            warpFx.Parameters["uProgress"]?.SetValue(envelope);
            warpFx.Parameters["uRotation"]?.SetValue(angle);
            warpFx.CurrentTechnique = warpFx.Techniques["KamuiLine"];

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState
                , DepthStencilState.None, RasterizerState.CullNone, warpFx
                , Main.GameViewMatrix.TransformationMatrix);
            warpFx.CurrentTechnique.Passes[0].Apply();

            //长度余量喂给 shader 两端羽化；局部 +Y 旋到航向
            Vector2 mid = (tail + Owner.Center) * 0.5f - Main.screenPosition;
            Vector2 quad = new(250f, length * 1.5f + 120f);
            sb.Draw(px, mid, new Rectangle(0, 0, 1, 1), Color.White
                , angle - MathHelper.PiOver2, new Vector2(0.5f), quad, SpriteEffects.None, 0);

            sb.End();
            sb.Begin(0, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None
                , RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>樱的空气拉扯要中性色差，蓝移是中子星语言</summary>
        public bool DontUseBlueshiftEffect() => true;

        public void DrawCustom(SpriteBatch spriteBatch) { }
    }
}
