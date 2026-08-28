using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaThralls;
using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Scenarios.Shenyo;
using InnoVault.Actors;
using InnoVault.Cinematics;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.OniRainWorlds.KasaOnis
{
    internal enum KasaOniPhase : byte
    {
        /// <summary>污水自地面汇聚凝成身形（出现与瞬移落点共用）</summary>
        Emerging,
        /// <summary>贴地蹒跚，走向目标玩家</summary>
        Walking,
        /// <summary>化作污水塌回地面</summary>
        Dissolving,
        /// <summary>地脉里潜行滑向落点，无形无碰撞</summary>
        Submerged,
    }

    /// <summary>
    /// 伞鬼栖息语境：叠加层雨世界（本地 Depth 标记）或鬼雨子世界（真实世界）。
    /// 权威端生成时按所在世界推断，随生成包的 SyncVar 到各端；
    /// 观察者可见性/音效/接触威胁按语境取各自的"身处雨中"判据
    /// </summary>
    internal enum KasaOniContext : byte
    {
        /// <summary>主世界叠加层：观察者=身处鬼雨 Depth 的本地玩家</summary>
        RainOverlay,
        /// <summary>鬼雨子世界：观察者=身处 Kiame 的所有人；夺伞下潜不开放</summary>
        KiameWorld,
    }

    /// <summary>
    /// 鬼雨世界的伞鬼：入第一层时从地下以污水凝聚现身。<br/>
    /// 服务器权威推进相位与瞬移调度，运动积分全端一致跑以获得平滑预测；
    /// 雨世界是本地叠加层，故绘制/粒子/音效只对身处雨中的观察者生效。<br/>
    /// 行走相位对身处雨中的本地玩家有接触伤害（本地自结算，经原版协议同步），
    /// 自身仍无受击；靠近自己的伞鬼可右键夺伞下潜（<see cref="OniRainDescentTransition"/>）。
    /// </summary>
    internal sealed class KasaOniActor : Actor
    {
        internal const int EmergeFrames = 96;
        internal const int DissolveFrames = 46;
        internal const int HitboxWidth = 36;
        internal const int HitboxHeight = 54;

        private const float WalkMaxSpeed = 1.15f;
        private const float WalkAccel = 0.055f;
        private const float Gravity = 0.35f;
        private const float MaxFallSpeed = 10f;
        private const float ChaseStopDistance = 52f;
        private const float TeleportDistance = 1150f;
        private const int StuckFramesForTeleport = 90;
        private const int TeleportCooldownFrames = 300;
        private const float ReformDistMin = 240f;
        private const float ReformDistMax = 420f;
        private const float SubmergeSpeed = 14f;
        private const int OrphanFramesToDespawn = 240;

        //接触威胁：新档百血下被围猎会真死，但单次可跑；原版无敌帧定节奏
        private const int ContactDamage = 22;
        private const float ContactKnockback = 5f;
        //夺伞交互距离，按伞盖锚点算
        private const float GrabDistance = 120f;

        //湿墨色板，与鬼雨体系一致
        internal static readonly Color SewageDeep = new(46, 56, 58);
        internal static readonly Color SewageDark = new(30, 38, 41);
        internal static readonly Color CorpseTeal = new(120, 150, 146);
        internal static readonly Color PaleSheen = new(176, 192, 196);

        [SyncVar]
        private int phaseRaw = (int)KasaOniPhase.Emerging;
        [SyncVar]
        private int ownerWhoAmI = -1;
        [SyncVar]
        private int contextRaw = (int)KasaOniContext.RainOverlay;

        private KasaOniPhase lastSeenPhase;
        private int phaseTimer;

        //权威端瞬移调度
        private Vector2 reformTarget;
        private int submergeFrames;
        private int stuckTimer;
        private int teleportCooldown;
        private float lastStuckX;
        private bool despawnOnDissolve;
        private int orphanTimer;

        //纯视觉，两端各自推
        internal bool FacingLeft;
        internal float WaddlePhase;
        private int dripTimer;
        private int squelchTimer;
        //夺伞提示淡入，仅本地
        private float grabPromptAlpha;

        internal KasaOniPhase Phase => (KasaOniPhase)phaseRaw;
        internal int OwnerWhoAmI => ownerWhoAmI;
        internal int PhaseTimer => phaseTimer;
        internal KasaOniContext Context => (KasaOniContext)contextRaw;

        /// <summary>本机观察者是否身处这只鬼所在的雨语境（可见性/音效/接触威胁共用门）</summary>
        private bool ObserverIn => Context == KasaOniContext.KiameWorld
            ? Kiame.KiameWorld.Active
            : OniRainWorldState.LocalIn;
        /// <summary>脚底中心锚点</summary>
        internal Vector2 FeetAnchor => Position + new Vector2(Width * 0.5f, Height);
        /// <summary>着色器地面裁切线的世界Y</summary>
        internal float GroundLineY => Position.Y + Height + 2f;
        /// <summary>伞面锚点：撑伞拍的环心与甩水点，按画出来的身量走</summary>
        internal Vector2 CanopyAnchor
            => FeetAnchor - new Vector2(0f, Height * KasaOniRenderer.PresenceScale);

        /// <summary>
        /// 个体站位距离：按槽位错开 52~123px，多只围观时呈弧散开而不是叠成一摞；
        /// WhoAmI 由服务器分配且全端一致，预测安全
        /// </summary>
        private float PersonalStop => ChaseStopDistance + WhoAmI * 29 % 72;

        /// <summary>凝聚度 0~1：Emerging 升、Dissolving 降、Walking=1、Submerged=0</summary>
        internal float CondenseProgress => Phase switch {
            KasaOniPhase.Emerging => MathHelper.Clamp(phaseTimer / (float)EmergeFrames, 0f, 1f),
            KasaOniPhase.Dissolving => 1f - MathHelper.Clamp(phaseTimer / (float)DissolveFrames, 0f, 1f),
            KasaOniPhase.Walking => 1f,
            _ => 0f,
        };

        public override void OnSpawn(params object[] args) {
            Width = HitboxWidth;
            Height = HitboxHeight;
            DrawExtendMode = 400;
            DrawLayer = ActorDrawLayer.AfterTiles;
            WaddlePhase = Main.rand.NextFloat(MathHelper.TwoPi);

            //客户端在 NetworkSpawn 里先套用了权威 SyncVar，这里绝不能覆写
            if (!VaultUtils.isClient) {
                Player nearest = Position.FindClosestPlayer(2000f);
                ownerWhoAmI = nearest?.whoAmI ?? -1;
                phaseRaw = (int)KasaOniPhase.Emerging;
                //栖息语境按生成时所在世界推断，随生成包的 SyncVar 到各端
                contextRaw = Kiame.KiameWorld.Active
                    ? (int)KasaOniContext.KiameWorld
                    : (int)KasaOniContext.RainOverlay;
            }

            lastSeenPhase = Phase;
            phaseTimer = 0;
            teleportCooldown = TeleportCooldownFrames / 2;

            //初次生成不经过 SetPhase，凝聚起拍在这里补；客户端收到生成包时即演出开始
            if (!Main.dedServ && Phase == KasaOniPhase.Emerging) {
                PlayPhaseCue(KasaOniPhase.Emerging);
            }
        }

        public override void SendExtraData(BinaryWriter writer) {
            writer.Write(phaseTimer);
        }

        public override void ReceiveExtraData(BinaryReader reader) {
            phaseTimer = Math.Max(reader.ReadInt32(), 0);
            lastSeenPhase = Phase;
        }

        public override void AI() {
            ObservePhaseChange();

            if (!VaultUtils.isClient) {
                UpdateAuthorityDecisions();
            }

            UpdateMotion();
            UpdatePresentation();
            UpdateLocalThreat();
            UpdateGrabInteraction();

            phaseTimer++;
        }

        #region 相位机
        /// <summary>客户端探测远端相位翻转：重置本地表现计时并补确认拍</summary>
        private void ObservePhaseChange() {
            if (lastSeenPhase == Phase) {
                return;
            }

            lastSeenPhase = Phase;
            phaseTimer = 0;
            OnPhaseEntered(Phase);
        }

        /// <summary>权威端切相位，本地直接演确认拍，客户端靠 ObservePhaseChange 补</summary>
        private void SetPhase(KasaOniPhase phase) {
            if (Phase == phase) {
                return;
            }
            phaseRaw = (int)phase;
            lastSeenPhase = phase;
            phaseTimer = 0;
            NetUpdate = true;
            OnPhaseEntered(phase);
        }

        private void OnPhaseEntered(KasaOniPhase phase) {
            switch (phase) {
                case KasaOniPhase.Emerging:
                    //瞬移落点：客户端直接采纳权威位置，防止一帧半空残影
                    if (VaultUtils.isClient && HasNetTarget) {
                        Position = NetTargetPosition;
                    }
                    Velocity = Vector2.Zero;
                    break;
                case KasaOniPhase.Dissolving:
                case KasaOniPhase.Submerged:
                    if (phase == KasaOniPhase.Dissolving) {
                        Velocity = Vector2.Zero;
                    }
                    break;
            }
            PlayPhaseCue(phase);
        }
        #endregion

        #region 权威决策
        private void UpdateAuthorityDecisions() {
            //叠加层语境：单机里玩家离开第一层（浮出或深潜）伞鬼失去栖息层，消融退场
            //（多人退场由 owner 端 Director 发销毁请求，专用服务器不知深度）。
            //子世界语境：世界本身即栖息层，卸载即整体消亡，不做深度退场
            if (Context == KasaOniContext.RainOverlay
                && VaultUtils.isSinglePlayer && OniRainWorldState.LocalDepth != 1) {
                HandleWorldExitAuthority();
                return;
            }

            UpdateOrphanState();

            //冷却相位无关地走表：消融+潜行的时间也计入，落地后不用再干等满额冷却
            if (teleportCooldown > 0) {
                teleportCooldown--;
            }

            switch (Phase) {
                case KasaOniPhase.Emerging:
                    if (phaseTimer >= EmergeFrames) {
                        SetPhase(KasaOniPhase.Walking);
                    }
                    break;
                case KasaOniPhase.Walking:
                    UpdateWalkDecisions();
                    break;
                case KasaOniPhase.Dissolving:
                    if (phaseTimer >= DissolveFrames) {
                        if (despawnOnDissolve) {
                            RequestKill();
                        }
                        else {
                            BeginSubmerge();
                        }
                    }
                    break;
                case KasaOniPhase.Submerged:
                    if (phaseTimer >= submergeFrames) {
                        ArriveAtReformPoint();
                    }
                    break;
            }
        }

        private void HandleWorldExitAuthority() {
            switch (Phase) {
                case KasaOniPhase.Emerging:
                case KasaOniPhase.Walking:
                    despawnOnDissolve = true;
                    SetPhase(KasaOniPhase.Dissolving);
                    break;
                case KasaOniPhase.Dissolving:
                    despawnOnDissolve = true;
                    if (phaseTimer >= DissolveFrames) {
                        RequestKill();
                    }
                    break;
                case KasaOniPhase.Submerged:
                    //本就无形，直接散了
                    RequestKill();
                    break;
            }
        }

        /// <summary>owner 断线判定：无主且找不到候补玩家，挂一阵后消融自灭</summary>
        private void UpdateOrphanState() {
            bool ownerConnected = ownerWhoAmI >= 0 && ownerWhoAmI < Main.maxPlayers
                && Main.player[ownerWhoAmI].active;
            if (!ownerConnected) {
                Player fallback = Center.FindClosestPlayer(2400f);
                if (fallback != null) {
                    ownerWhoAmI = fallback.whoAmI;
                    NetUpdate = true;
                    ownerConnected = true;
                }
            }

            if (ownerConnected) {
                orphanTimer = 0;
                return;
            }

            orphanTimer++;
            if (orphanTimer > OrphanFramesToDespawn
                && Phase is KasaOniPhase.Emerging or KasaOniPhase.Walking) {
                despawnOnDissolve = true;
                SetPhase(KasaOniPhase.Dissolving);
            }
        }

        private void UpdateWalkDecisions() {
            Player target = ResolveTargetPlayer();
            if (target == null) {
                stuckTimer = 0;
                return;
            }

            //卡死判定：想走却横向没挪动（被墙/坑拦住）
            float dx = target.Center.X - Center.X;
            bool wantsMove = Math.Abs(dx) > PersonalStop + 12f;
            if (wantsMove && Math.Abs(Position.X - lastStuckX) < 0.12f) {
                stuckTimer++;
            }
            else {
                stuckTimer = Math.Max(stuckTimer - 2, 0);
            }
            lastStuckX = Position.X;

            float distance = Center.Distance(target.Center);
            bool wantTeleport = distance > TeleportDistance
                || stuckTimer > StuckFramesForTeleport;
            //应急旁路：落点不巧再被卡死时不等冷却磨完，持续卡死直接强制瞬移
            bool emergencyStuck = stuckTimer > StuckFramesForTeleport + 60;
            if (!emergencyStuck && (!wantTeleport || teleportCooldown > 0)) {
                return;
            }

            if (TryPickReformPoint(target, out reformTarget)) {
                teleportCooldown = TeleportCooldownFrames;
                stuckTimer = 0;
                despawnOnDissolve = false;
                SetPhase(KasaOniPhase.Dissolving);
            }
            else {
                //探不到落点，缓一阵再试
                teleportCooldown = 90;
            }
        }

        private void BeginSubmerge() {
            Vector2 path = reformTarget - Position;
            submergeFrames = (int)MathHelper.Clamp(path.Length() / SubmergeSpeed, 24f, 80f);
            Velocity = path / submergeFrames;
            SetPhase(KasaOniPhase.Submerged);
        }

        private void ArriveAtReformPoint() {
            Position = reformTarget;
            Velocity = Vector2.Zero;
            NetUpdate = true;
            SetPhase(KasaOniPhase.Emerging);
        }

        /// <summary>瞬移落点：目标玩家近旁探可站立地面，六成概率落在其背后</summary>
        private bool TryPickReformPoint(Player target, out Vector2 topLeft) {
            for (int attempt = 0; attempt < 24; attempt++) {
                float side = Main.rand.NextFloat(ReformDistMin, ReformDistMax);
                int dir = Main.rand.NextFloat() < 0.6f
                    ? -target.direction
                    : (Main.rand.NextBool() ? 1 : -1);
                Vector2 from = new(target.Center.X + dir * side, target.Center.Y - 160f);
                if (TryFindStandableGround(from, Width, Height, out Vector2 feet)) {
                    topLeft = feet - new Vector2(Width * 0.5f, Height);
                    return true;
                }
            }
            topLeft = default;
            return false;
        }

        private Player ResolveTargetPlayer() {
            if (ownerWhoAmI >= 0 && ownerWhoAmI < Main.maxPlayers) {
                Player owner = Main.player[ownerWhoAmI];
                if (owner.Alives()) {
                    return owner;
                }
            }
            return Center.FindClosestPlayer(2400f);
        }
        #endregion

        #region 运动积分（全端一致）
        private void UpdateMotion() {
            switch (Phase) {
                case KasaOniPhase.Emerging:
                case KasaOniPhase.Dissolving:
                    //身形钉在地上凝聚/消融
                    Velocity = Vector2.Zero;
                    break;
                case KasaOniPhase.Submerged:
                    //地脉滑行：框架按 Velocity 平移，无碰撞
                    break;
                case KasaOniPhase.Walking:
                    WalkIntegrate();
                    break;
            }
        }

        private void WalkIntegrate() {
            Player target = ResolveTargetPlayer();
            float desiredX = 0f;
            if (target != null) {
                float dx = target.Center.X - Center.X;
                if (Math.Abs(dx) > PersonalStop) {
                    desiredX = Math.Sign(dx) * WalkMaxSpeed;
                }
            }

            Velocity.X = MathHelper.Lerp(Velocity.X, desiredX, WalkAccel);
            Velocity.Y = Math.Min(Velocity.Y + Gravity, MaxFallSpeed);

            //台阶蹭上（原版NPC口径 specialChecksMode=1），再物块裁剪与斜坡贴合；
            //框架随后执行 Position += Velocity
            Vector2 position = Position;
            Vector2 velocity = Velocity;
            if (velocity.Y >= 0f) {
                float stepSpeed = 1f;
                float gfxOffY = 0f;
                Collision.StepUp(ref position, ref velocity, Width, Height,
                    ref stepSpeed, ref gfxOffY, 1, false, 1);
                Position = position;
            }
            velocity = Collision.TileCollision(Position, velocity, Width, Height);
            Vector4 slope = Collision.SlopeCollision(Position, velocity,
                Width, Height, Gravity, false);
            Position = new Vector2(slope.X, slope.Y);
            Velocity = new Vector2(slope.Z, slope.W);
        }
        #endregion

        #region 本地表现（仅雨中观察者）
        private void UpdatePresentation() {
            if (Main.dedServ) {
                return;
            }

            UpdateFacing();

            if (!ObserverIn) {
                return;
            }

            //子世界里趟过洼地：向水面层报涉水足点，接触涟漪跟脚走
            if (Context == KasaOniContext.KiameWorld && Phase == KasaOniPhase.Walking) {
                Kiame.Water.KiameWaterRender.ReportWader(FeetAnchor, Width * 1.5f, 0.7f);
            }

            switch (Phase) {
                case KasaOniPhase.Emerging:
                    EmergingFx();
                    break;
                case KasaOniPhase.Walking:
                    WalkingFx();
                    break;
                case KasaOniPhase.Dissolving:
                    DissolvingFx();
                    break;
                case KasaOniPhase.Submerged:
                    SubmergedFx();
                    break;
            }

            if (Phase != KasaOniPhase.Submerged) {
                Lighting.AddLight(Center, 0.06f, 0.09f, 0.10f);
            }
        }

        private void UpdateFacing() {
            if (Phase == KasaOniPhase.Walking && Math.Abs(Velocity.X) > 0.15f) {
                FacingLeft = Velocity.X < 0f;
                WaddlePhase += 0.1f + Math.Abs(Velocity.X) * 0.06f;
                if (WaddlePhase > MathHelper.TwoPi) {
                    WaddlePhase -= MathHelper.TwoPi;
                }
            }
            else {
                //站定或凝聚期面向目标玩家
                Player target = ResolveTargetPlayer();
                if (target != null && Math.Abs(target.Center.X - Center.X) > 8f) {
                    FacingLeft = target.Center.X < Center.X;
                }
            }
        }

        /// <summary>
        /// 凝聚期：污水团自地面弧线扑入正在成形的身体，
        /// 潭里顶上来的水柱把身体撑起来，雨每隔一阵续着往里按（对齐伞奴成形语汇）
        /// </summary>
        private void EmergingFx() {
            float progress = CondenseProgress;
            float bodyHeight = Height * KasaOniRenderer.PresenceScale;
            if (progress < 0.85f && Main.GameUpdateCount % 2 == 0) {
                Vector2 feet = FeetAnchor;
                float side = Main.rand.NextFloat(26f, 100f) * (Main.rand.NextBool() ? 1f : -1f);
                Vector2 from = new(feet.X + side, feet.Y - Main.rand.NextFloat(0f, 5f));
                Vector2 to = feet - new Vector2(Main.rand.NextFloat(-8f, 8f),
                    Main.rand.NextFloat(6f, bodyHeight * (0.2f + progress * 0.75f)));
                PRTLoader.NewParticle<PRT_SewageGlob>(from,
                    new Vector2(-side * 0.015f, -Main.rand.NextFloat(1.4f, 3f)),
                    Color.Lerp(SewageDeep, CorpseTeal, Main.rand.NextFloat(0.4f))
                        * Main.rand.NextFloat(0.6f, 0.9f),
                    Main.rand.NextFloat(0.5f, 0.95f))
                    ?.Configure(Main.rand.Next(18, 32), to);
            }

            //自潭里顶上来的水柱：身体是被这股水撑起来的；微斜靠 wind，粒子每帧用它覆写 X 速
            if (phaseTimer % 4 == 0) {
                float driftX = Main.rand.NextFloat(-0.7f, 0.7f);
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    FeetAnchor + new Vector2(Main.rand.NextFloat(-0.55f, 0.55f) * Width, -2f),
                    new Vector2(driftX, -Main.rand.NextFloat(3.5f, 7f)),
                    PaleSheen * Main.rand.NextFloat(0.3f, 0.55f),
                    Main.rand.NextFloat(0.4f, 0.7f))
                    ?.Configure(Main.rand.Next(18, 30), driftX);
            }

            //雨一直在按着它成形，不是砸一下就走
            if (phaseTimer % 12 == 5) {
                KikasaThrallFX.RainYank(FeetAnchor - new Vector2(0f, bodyHeight * 0.45f),
                    5, 200f, 0.85f);
            }

            if (Main.rand.NextBool(9)) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    FeetAnchor + new Vector2(Main.rand.NextFloat(-24f, 24f), -4f),
                    new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), -Main.rand.NextFloat(0.05f, 0.2f)),
                    SewageDark * Main.rand.NextFloat(0.5f, 0.8f),
                    Main.rand.NextFloat(0.5f, 0.9f))
                    ?.Configure(Main.rand.Next(60, 100));
            }
        }

        /// <summary>行走期：伞沿垂滴与湿脚吧唧声，身上永远在滴水</summary>
        private void WalkingFx() {
            dripTimer++;
            if (dripTimer >= 34) {
                dripTimer = 0;
                Vector2 rim = Position + new Vector2(
                    Main.rand.NextFloat(4f, Width - 4f), Main.rand.NextFloat(4f, 14f));
                PRTLoader.NewParticle<PRT_GhostRainDrop>(rim,
                    new Vector2(0f, Main.rand.NextFloat(1.2f, 2f)),
                    PaleSheen * Main.rand.NextFloat(0.3f, 0.45f),
                    Main.rand.NextFloat(0.4f, 0.6f))
                    ?.Configure(Main.rand.Next(16, 26), 0f);
            }

            bool moving = Math.Abs(Velocity.X) > 0.25f && Math.Abs(Velocity.Y) < 0.4f;
            if (moving && ++squelchTimer >= 30) {
                squelchTimer = 0;
                SoundEngine.PlaySound(SoundID.Drip with {
                    Pitch = Main.rand.NextFloat(-0.55f, -0.3f),
                    Volume = 0.28f,
                    MaxInstances = 5,
                }, FeetAnchor);
                PRTLoader.NewParticle<PRT_SewageGlob>(
                    FeetAnchor + new Vector2(Main.rand.NextFloat(-8f, 8f), -2f),
                    new Vector2(-Velocity.X * 0.4f, -Main.rand.NextFloat(0.8f, 1.6f)),
                    SewageDeep * Main.rand.NextFloat(0.5f, 0.75f),
                    Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(Main.rand.Next(14, 24));
            }
        }

        /// <summary>消融期：洒落跟着熔断前沿走，顶部先化，残躯向脚底退缩</summary>
        private void DissolvingFx() {
            if (Main.GameUpdateCount % 2 != 0) {
                return;
            }
            float progress = CondenseProgress;
            //前沿自顶向下推进：progress 1→0 时 frontY 从 0 走到 Height
            float frontY = Height * MathHelper.Clamp(1f - progress, 0f, 1f);
            Vector2 from = Position + new Vector2(
                Main.rand.NextFloat(2f, Width - 2f),
                MathHelper.Clamp(frontY + Main.rand.NextFloat(-6f, 14f), 0f, Height - 2f));
            PRTLoader.NewParticle<PRT_SewageGlob>(from,
                new Vector2(Main.rand.NextFloat(-1.4f, 1.4f), Main.rand.NextFloat(0.4f, 1.8f)),
                Color.Lerp(SewageDeep, SewageDark, Main.rand.NextFloat())
                    * Main.rand.NextFloat(0.6f, 0.9f),
                Main.rand.NextFloat(0.5f, 0.9f))
                ?.Configure(Main.rand.Next(16, 28));
        }

        /// <summary>潜行期：头顶地表冒泡的行进痕迹</summary>
        private void SubmergedFx() {
            if (Main.GameUpdateCount % 3 != 0) {
                return;
            }
            if (!TryProbeSurface(new Vector2(Center.X, Position.Y - 8f), out float surfaceY)) {
                return;
            }
            Vector2 pos = new(Center.X + Main.rand.NextFloat(-10f, 10f), surfaceY - 2f);
            PRTLoader.NewParticle<PRT_SewageGlob>(pos,
                new Vector2(Velocity.X * 0.15f, -Main.rand.NextFloat(0.9f, 2f)),
                SewageDeep * Main.rand.NextFloat(0.45f, 0.7f),
                Main.rand.NextFloat(0.3f, 0.55f))
                ?.Configure(Main.rand.Next(12, 20));
            if (Main.rand.NextBool(4)) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(pos,
                    new Vector2(0f, -0.1f), SewageDark * 0.6f,
                    Main.rand.NextFloat(0.4f, 0.7f))
                    ?.Configure(Main.rand.Next(40, 70));
            }
        }

        /// <summary>相位确认拍：对齐伞奴成形的破土/撑伞语汇，仅雨中观察者可闻</summary>
        private void PlayPhaseCue(KasaOniPhase phase) {
            if (Main.dedServ || !ObserverIn) {
                return;
            }

            switch (phase) {
                case KasaOniPhase.Emerging:
                    //破土拍：雨自四面按出身形，地面先顶开一蓬污水
                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Pitch = -0.85f,
                        Volume = 0.5f,
                        MaxInstances = 3,
                    }, FeetAnchor);
                    SoundEngine.PlaySound(SoundID.Drip with {
                        Pitch = -0.8f,
                        Volume = 0.4f,
                        MaxInstances = 3,
                    }, FeetAnchor);
                    KikasaThrallFX.RainYank(FeetAnchor - new Vector2(0f, 30f), 12, 240f, 0.95f);
                    KikasaThrallFX.WaterBurst(FeetAnchor, 10, 0.85f, upward: true);
                    KikasaThrallFX.MistRing(FeetAnchor, 3, 30f, 0.9f);
                    ShakeViewer(1.6f);
                    break;
                case KasaOniPhase.Walking:
                    //撑伞拍：伞骨绷响 + 脚下整圈水爆 + 伞面甩水，鬼是被雨按着撑开的
                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Pitch = -0.3f,
                        Volume = 0.7f,
                        MaxInstances = 3,
                    }, FeetAnchor);
                    SoundEngine.PlaySound(SoundID.Drip with {
                        Pitch = -0.9f,
                        Volume = 0.5f,
                        MaxInstances = 3,
                    }, FeetAnchor);
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with {
                        Pitch = -0.45f,
                        Volume = 0.45f,
                        MaxInstances = 2,
                    }, CanopyAnchor);
                    KikasaThrallFX.WaterBurst(FeetAnchor, 16, 1.05f, upward: true);
                    KikasaThrallFX.WaterBurst(CanopyAnchor, 12, 0.95f, upward: false);
                    KikasaThrallFX.MistRing(FeetAnchor, 4, 42f, 1.05f);
                    ShakeViewer(2.2f);
                    break;
                case KasaOniPhase.Dissolving:
                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Pitch = -0.55f,
                        Volume = 0.5f,
                        MaxInstances = 3,
                    }, FeetAnchor);
                    break;
                case KasaOniPhase.Submerged:
                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Pitch = -0.95f,
                        Volume = 0.38f,
                        MaxInstances = 3,
                    }, FeetAnchor);
                    break;
            }
        }

        /// <summary>屏震落在雨中观察者身上；同屏最多六只，量刻意压过</summary>
        private static void ShakeViewer(float amount)
            => Main.LocalPlayer?.CWR()?.GetScreenShake(amount);
        #endregion

        #region 本地威胁与夺伞
        /// <summary>
        /// 接触威胁：行走相位擦到身处雨中的本地玩家就抓一把。
        /// 伤害由被击端自结算（雨世界是本地叠加层），经原版 Hurt 协议同步；
        /// 演出与叙事期间收爪。
        /// </summary>
        private void UpdateLocalThreat() {
            if (Main.dedServ || Phase != KasaOniPhase.Walking || !ObserverIn) {
                return;
            }
            Player player = Main.LocalPlayer;
            if (player == null || !player.Alives() || player.ghost || player.immune) {
                return;
            }
            if (NarrativeTriggerGate.IsBusy || CutsceneDirector.IsPlaying
                || OniRainWorldTransition.Active || OniRainDescentTransition.Active) {
                return;
            }
            Rectangle body = new((int)Position.X, (int)Position.Y, Width, Height);
            if (!body.Intersects(player.Hitbox)) {
                return;
            }

            //先登记命中源，致死打击与 PreKill 拦截同帧结算
            player.GetModPlayer<OniRainWorldPlayer>().NoteOniHit();
            int direction = player.Center.X < Center.X ? -1 : 1;
            player.Hurt(PlayerDeathReason.ByCustomReason(
                OniRainWorldSystem.OniDeathReason.Format(player.name)),
                ContactDamage, direction, knockback: ContactKnockback);
        }

        /// <summary>本地玩家可否对这只伞鬼夺伞：叠加层剧情专属（子世界不开放下潜），
        /// 只许夺自己的追猎者，行走相位、未达最深层</summary>
        private bool LocalPlayerCanGrab(Player player) {
            return Context == KasaOniContext.RainOverlay
                && player != null && player.Alives()
                && OwnerWhoAmI == player.whoAmI
                && Phase == KasaOniPhase.Walking
                && OniRainWorldState.LocalIn
                && OniRainWorldState.LocalDepth < OniRainWorldState.MaxDepth
                && !OniRainWorldTransition.Active && !OniRainDescentTransition.Active
                && !CutsceneDirector.IsPlaying && !NarrativeTriggerGate.IsBusy;
        }

        private void UpdateGrabInteraction() {
            if (Main.dedServ) {
                return;
            }
            Player player = Main.LocalPlayer;
            bool near = LocalPlayerCanGrab(player)
                && player.Center.Distance(CanopyAnchor) < GrabDistance;
            bool canTrigger = near && !Main.mapFullscreen && !player.mouseInterface;

            grabPromptAlpha = MathHelper.Clamp(
                grabPromptAlpha + (canTrigger ? 0.05f : -0.05f), 0f, 1f);

            if (canTrigger && grabPromptAlpha > 0.5f
                && Main.mouseRight && Main.mouseRightRelease) {
                TriggerGrab(player);
            }
        }

        /// <summary>
        /// 夺伞：确认拍后以这把伞为门起深潜演出，被夺了伞的鬼失去存形之物塌回污水。
        /// 多人客户端销毁走服务器请求，水幕合拢会盖住生硬处。
        /// </summary>
        private void TriggerGrab(Player player) {
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with {
                Pitch = -0.3f,
                Volume = 0.6f,
                MaxInstances = 3,
            }, CanopyAnchor);
            SoundEngine.PlaySound(SoundID.SplashWeak with {
                Pitch = -0.4f,
                Volume = 0.6f,
                MaxInstances = 3,
            }, CanopyAnchor);
            KikasaThrallFX.WaterBurst(CanopyAnchor, 14, 1.05f, upward: false);
            KikasaThrallFX.MistRing(FeetAnchor, 3, 36f, 0.95f);
            player.CWR()?.GetScreenShake(5f);

            //夺伞入深层：记入场方式，供沈幽初遇选项门
            ShenyoStorySync.ArrivedByDeath = false;

            //运镜失败不致命，演出照走
            OniRainDescentTransition.Begin(player, FeetAnchor);
            CutsceneDirector.Play<OniRainDescentCutscene>(player);

            if (VaultUtils.isClient) {
                ActorLoader.KillActor(WhoAmI);
            }
            else {
                BeginDespawnDissolve();
            }
        }

        /// <summary>夺伞提示，形制镜像立伞交互提示；仅雨中观察者可见</summary>
        private void DrawGrabPrompt(SpriteBatch sb) {
            if (grabPromptAlpha <= 0.01f) {
                return;
            }

            Vector2 textPos = CanopyAnchor - Main.screenPosition + new Vector2(0f, -46f);
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            string hint = OniRainWorldSystem.GrabHint.Value;
            Vector2 textSize = font.MeasureString(hint) * 0.9f;

            Texture2D glow = CWRAsset.SoftGlow.Value;
            float pulse = MathF.Sin(Main.GlobalTimeWrappedHourly * 3f) * 0.5f + 0.5f;

            Vector2 backingScale = new((textSize.X + 46f) / glow.Width, (textSize.Y + 26f) / glow.Height);
            Color backingColor = new Color(70, 92, 98) with { A = 0 } * (grabPromptAlpha * (0.3f + pulse * 0.1f));
            sb.Draw(glow, textPos, null, backingColor, 0f, glow.Size() / 2f, backingScale, SpriteEffects.None, 0f);

            Color textColor = new Color(214, 228, 230) * grabPromptAlpha;
            Utils.DrawBorderString(sb, hint, textPos - textSize / 2f, textColor, 0.9f);
        }
        #endregion

        #region 绘制
        public override bool PreDraw(SpriteBatch spriteBatch, ref Color drawColor) {
            //按语境裁观察者：叠加层只给雨中人看，子世界里人人可见
            if (Main.dedServ || !ObserverIn) {
                return false;
            }
            KasaOniRenderer.Draw(spriteBatch, this);
            return false;
        }

        public override void PostDraw(SpriteBatch spriteBatch, Color drawColor) {
            if (Main.dedServ || !ObserverIn) {
                return;
            }
            DrawGrabPrompt(spriteBatch);
        }
        #endregion

        #region 地面探测
        /// <summary>自起点向下探可站立地面（实心 + 身位净空），feet 为脚底中心。
        /// acceptPlatforms=true 时平台也算可站立：伞奴在离地平台击杀点就地成形，
        /// 不再穿到下方实心块（反馈三·#123）；役鬼场景 Actor 维持默认只认实心</summary>
        internal static bool TryFindStandableGround(Vector2 from, int width, int height,
            out Vector2 feet, bool acceptPlatforms = false) {
            int tileX = (int)(from.X / 16f);
            int tileY = (int)(from.Y / 16f);
            int columns = Math.Max(width / 16 + 1, 2);
            int clearance = height / 16 + 1;

            for (int i = 0; i < 80; i++) {
                int y = tileY + i;
                if (!WorldGen.InWorld(tileX, y, 40)) {
                    break;
                }
                Tile tile = Framing.GetTileSafely(tileX, y);
                if (!tile.HasTile || !Main.tileSolid[tile.TileType]
                    || (!acceptPlatforms && Main.tileSolidTop[tile.TileType])) {
                    continue;
                }

                //身宽范围内每列都要有净空
                bool blocked = false;
                for (int cx = -columns / 2; cx <= columns / 2 && !blocked; cx++) {
                    for (int cy = 1; cy <= clearance; cy++) {
                        Tile above = Framing.GetTileSafely(tileX + cx, y - cy);
                        if (above.HasTile && Main.tileSolid[above.TileType]
                            && !Main.tileSolidTop[above.TileType]) {
                            blocked = true;
                            break;
                        }
                    }
                }
                if (blocked) {
                    feet = default;
                    return false;
                }

                feet = new Vector2(from.X, y * 16f);
                return true;
            }

            feet = default;
            return false;
        }

        /// <summary>自起点向下找第一格实心面的顶面Y，潜行冒泡用</summary>
        private static bool TryProbeSurface(Vector2 from, out float surfaceY) {
            int tileX = (int)(from.X / 16f);
            int tileY = (int)(from.Y / 16f);
            for (int i = 0; i < 24; i++) {
                int y = tileY + i;
                if (!WorldGen.InWorld(tileX, y, 40)) {
                    break;
                }
                Tile tile = Framing.GetTileSafely(tileX, y);
                if (tile.HasTile && Main.tileSolid[tile.TileType]
                    && !Main.tileSolidTop[tile.TileType]) {
                    surfaceY = y * 16f;
                    return true;
                }
            }
            surfaceY = 0f;
            return false;
        }
        #endregion

        #region 退场接口
        /// <summary>权威端消融退场（单机/服务端用；多人客户端走 KillActor 请求）</summary>
        internal void BeginDespawnDissolve() {
            if (VaultUtils.isClient) {
                return;
            }
            if (Phase == KasaOniPhase.Submerged) {
                RequestKill();
                return;
            }
            despawnOnDissolve = true;
            if (Phase != KasaOniPhase.Dissolving) {
                SetPhase(KasaOniPhase.Dissolving);
            }
        }
        #endregion
    }
}
