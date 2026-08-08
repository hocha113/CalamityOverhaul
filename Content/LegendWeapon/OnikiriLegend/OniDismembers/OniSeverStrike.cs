using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFinaleSlashs;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniOmokages;
using CalamityOverhaul.Content.TimeFreezes;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using OFR = CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFinaleSlashs.OniFinaleRenderer;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniDismembers
{
    /// <summary>肢解结算斩. 直斩/锚模式</summary>
    internal class OniSeverStrike : ModProjectile, IOverlayDrawable, IOniBladeOccupant
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>蓄势帧数、刀在鞘位反向压势</summary>
        public const int WindupFrames = 6;
        /// <summary>拔刀闪帧数、快到只剩残影</summary>
        public const int DrawFlashFrames = 2;
        /// <summary>落刀帧、目标入冻 + 终斩刀线生成</summary>
        public const int StrikeFrame = WindupFrames + DrawFlashFrames;
        /// <summary>纳刀帧 = 刀线引爆帧 = 碎片分离帧</summary>
        public const int SheatheFrame = StrikeFrame + OniFinaleCut.HoldFrames;
        /// <summary>纳刀一挑时长</summary>
        private const int NotoFlickFrames = 6;
        /// <summary>反噬帧、刀入鞘的下一瞬，同等的肢解落回自己（两种模式，落刀成功即必反噬）</summary>
        public const int SelfCutFrame = SheatheFrame + NotoFlickFrames;
        /// <summary>纳刀后持刀淡出</summary>
        private const int NotoFadeFrames = 12;
        /// <summary>演出总时长</summary>
        public const int TotalDuration = SheatheFrame + NotoFlickFrames + NotoFadeFrames + 4;
        /// <summary>空挥（目标失效）后的快速收鞘时长</summary>
        private const int WhiffSheatheFrames = 8;
        /// <summary>点锚模式的 ai[0] 标记</summary>
        private const int PointModeMarker = -2;
        private const byte NetworkVersion = 1;
        private const float DismemberRange = 800f;
        private const float DismemberDamageMultiplier = 2.5f;
        private const int MaxAuthorityWaitFrames = 120;
        private const int MaxDamage = 10_000_000;
        private const float MaxKnockback = 100f;

        private enum PointResolution : byte
        {
            Pending,
            Succeeded,
            Failed,
        }

        [Flags]
        private enum AuthorityFlags : byte
        {
            None = 0,
            Accepted = 1 << 0,
            Rejected = 1 << 1,
            Struck = 1 << 2,
            Whiffed = 1 << 3,
            SelfCutDone = 1 << 4,
            GroupActivated = 1 << 5,
            InheritPose = 1 << 6,
            PointMode = 1 << 7,
        }

        private int timer;
        private bool requestInitialized;
        private bool requestPointMode;
        private NetworkNPCIdentity requestTargetIdentity;
        private Vector2 requestPointBodyLocal;
        private int omokageEntryId;
        private int tutorialTargetOwner = -1;
        private int tutorialTargetSession;
        private uint tutorialTargetToken;
        private PointResolution pointResolution;

        private bool authorityAccepted;
        private bool authorityRejected;
        private long activationId;
        private uint authorityRevision;
        private int authoritySnapshotTimer;
        private NetworkNPCIdentity authorityTargetIdentity;
        private Vector2 authorityAnchorCenter;
        private Vector2 authorityGroupCenter;
        private float authorityGroupHalfLength;
        private float authorityGroupWidth;
        private float authorityCutAngle;
        private float authorityScale = 1f;
        private int authorityPointTravel;
        //原版同步包把 Projectile.damage 截成 short，比对得用自己留的完整值，否则大数伤害必然误判
        private int authorityDamage;
        private float authorityKnockback;
        private int pointResolveAt = int.MaxValue;
        private int groupActivationTimer;
        private int receivedGroupElapsed;
        private int whiffStartedAt = -1;
        private bool groupActivated;
        private bool presentationInitialized;
        private bool drawSoundPlayed;
        private bool whiffSoundPlayed;
        private bool localSelfCutApplied;
        private bool localGroupApplied;
        private bool localTutorialEventApplied;
        /// <summary>落刀已执行（冻结+刀线已触发）</summary>
        private bool struck;
        /// <summary>落刀帧目标已失效、转空挥收势</summary>
        private bool whiffed;
        /// <summary>反噬已落下（防帧等值判断被计时抖动漏过）</summary>
        private bool selfCutDone;
        /// <summary>起手继承的连段刀角、从普攻移交时蓄势段顺势收拢入鞘，刀不跳变</summary>
        private float inheritRot;
        private bool inheritPose;
        private readonly OniBladePose bladePose = new();

        /// <summary>点锚模式、斩媒介（纸面），锚点=生成位置</summary>
        private bool PointMode => authorityAccepted ? requestPointMode
            : requestInitialized ? requestPointMode
            : (int)Projectile.ai[0] == PointModeMarker;
        private float CutAngle => authorityAccepted ? authorityCutAngle
            : float.IsFinite(Projectile.ai[1]) ? MathHelper.WrapAngle(Projectile.ai[1]) : 0f;
        private float SizeMul => authorityAccepted ? authorityScale
            : float.IsFinite(Projectile.ai[2]) && Projectile.ai[2] >= OniSeverReplicationSystem.MinScale
                ? Math.Min(Projectile.ai[2], OniSeverReplicationSystem.MaxScale) : 1f;
        private Player Owner => Projectile.owner >= 0 && Projectile.owner < Main.maxPlayers
            ? Main.player[Projectile.owner] : null;

        /// <summary>蓄+闪硬占刀权、人已进入拔刀的呼吸；残心起软姿态，玩家输入随时接管</summary>
        bool IOniBladeOccupant.HardOccupiesBlade => timer <= StrikeFrame + 2 && !whiffed;

        /// <summary>触发接口（直接模式）</summary>
        /// <param name="player">攻击发起者</param>
        /// <param name="target">肢解目标（落刀帧仍需存活，否则空挥收势）</param>
        /// <param name="cutAngle">切线角度（世界空间弧度，同时决定拔刀挥向）</param>
        /// <param name="damage">伤害（终斩刀线引爆窗单次巨额结算）</param>
        /// <param name="knockback">击退</param>
        /// <param name="scale">尺寸倍率（传给终斩刀线）</param>
        /// <param name="source">生成源，null 则回退 Misc 源</param>
        public static Projectile Fire(Player player, NPC target, float cutAngle, int damage, float knockback,
            float scale = 1f, IEntitySource source = null) {
            source ??= player.GetSource_Misc("CWR_OniSeverStrike");
            int tutorialOwner = -1;
            int tutorialSession = 0;
            uint tutorialToken = 0;
            bool tutorialTarget = target != null
                && Tutorial.OnikiriTutorialTargetGlobal.TryGetTutorialIdentity(target,
                    out tutorialOwner, out tutorialSession, out tutorialToken);
            if (tutorialTarget) {
                damage = 0;
            }
            Projectile projectile = Projectile.NewProjectileDirect(source,
                target?.Center ?? player.Center, Vector2.Zero
                , ModContent.ProjectileType<OniSeverStrike>(), damage, knockback, player.whoAmI
                , ai0: target?.whoAmI ?? -1, ai1: SanitizeAngle(cutAngle),
                ai2: SanitizeScale(scale));
            if (projectile.ModProjectile is OniSeverStrike strike) {
                strike.requestInitialized = true;
                strike.requestPointMode = false;
                strike.requestTargetIdentity = NetworkNPCIdentity.Capture(target);
                if (tutorialTarget) {
                    strike.tutorialTargetOwner = tutorialOwner;
                    strike.tutorialTargetSession = tutorialSession;
                    strike.tutorialTargetToken = tutorialToken;
                }
                projectile.netUpdate = true;
            }
            return projectile;
        }

        /// <summary>触发接口（点锚模式）</summary>
        /// <param name="player">攻击发起者</param>
        /// <param name="point">落刀点（世界坐标，应落在纸面内）</param>
        /// <param name="cutAngle">切线角度（弧度）</param>
        /// <param name="damage">脉冲到达帧对真身结算的伤害</param>
        /// <param name="knockback">击退</param>
        /// <param name="scale">尺寸倍率</param>
        /// <param name="source">生成源，null 则回退 Misc 源</param>
        /// <param name="omokageEntryId">点击时绑定的面影 ID</param>
        public static Projectile FireAtPoint(Player player, Vector2 point, float cutAngle, int damage, float knockback,
            float scale = 1f, IEntitySource source = null, int omokageEntryId = 0) {
            source ??= player.GetSource_Misc("CWR_OniSeverStrike");
            OmokageEntry boundEntry = FindOmokageEntry(omokageEntryId);
            Vector2 safePoint = IsFinite(point) ? point : player.Center;
            Projectile projectile = Projectile.NewProjectileDirect(source, safePoint, Vector2.Zero
                , ModContent.ProjectileType<OniSeverStrike>(), damage, knockback, player.whoAmI
                , ai0: PointModeMarker, ai1: SanitizeAngle(cutAngle),
                ai2: SanitizeScale(scale));
            if (projectile.ModProjectile is OniSeverStrike strike) {
                strike.requestInitialized = true;
                strike.requestPointMode = true;
                strike.omokageEntryId = omokageEntryId;
                if (boundEntry != null) {
                    strike.requestPointBodyLocal = safePoint - boundEntry.AnchorCenter;
                    NPC target = OniOmokage.ValidTarget(boundEntry.NpcIndex,
                        boundEntry.NpcType, boundEntry.NpcSpawnToken);
                    strike.requestTargetIdentity = NetworkNPCIdentity.Capture(target);
                }
                projectile.Center = safePoint;
                projectile.netUpdate = true;
            }
            return projectile;
        }

        public override void SetStaticDefaults() {
            CWRLoad.ProjValue.ImmuneFrozen[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.aiStyle = -1;
            Projectile.friendly = false;   //主控无判定，伤害全在终斩刀线

            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalDuration + MaxAuthorityWaitFrames + 4;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(NetworkVersion);
            writer.Write(requestInitialized);
            writer.Write(requestPointMode);
            requestTargetIdentity.Write(writer);
            writer.Write(requestPointBodyLocal.X);
            writer.Write(requestPointBodyLocal.Y);
            writer.Write(omokageEntryId);
            writer.Write(tutorialTargetToken != 0);
            if (tutorialTargetToken != 0) {
                writer.Write((byte)tutorialTargetOwner);
                writer.Write(tutorialTargetSession);
                writer.Write(tutorialTargetToken);
            }
            writer.Write((byte)pointResolution);

            bool hasAuthority = authorityAccepted || authorityRejected;
            writer.Write(hasAuthority);
            if (!hasAuthority) {
                return;
            }

            AuthorityFlags flags = BuildAuthorityFlags();
            writer.Write((byte)flags);
            writer.Write(activationId);
            writer.Write(authorityRevision);
            writer.Write((ushort)Math.Clamp(timer, 0,
                TotalDuration + MaxAuthorityWaitFrames));
            authorityTargetIdentity.Write(writer);
            writer.Write(authorityAnchorCenter.X);
            writer.Write(authorityAnchorCenter.Y);
            writer.Write(authorityGroupCenter.X);
            writer.Write(authorityGroupCenter.Y);
            writer.Write(authorityGroupHalfLength);
            writer.Write(authorityGroupWidth);
            writer.Write(authorityCutAngle);
            writer.Write(authorityScale);
            writer.Write(Projectile.damage);
            writer.Write(Projectile.knockBack);
            writer.Write((byte)Math.Clamp(authorityPointTravel, 0, 14));
            int groupElapsed = groupActivated
                ? Math.Max(timer - groupActivationTimer, 0) : 0;
            writer.Write((ushort)Math.Clamp(groupElapsed, 0,
                OniDismember.DefaultDuration - 1));
            writer.Write(inheritRot);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            try {
                if (reader.ReadByte() != NetworkVersion) {
                    return;
                }

                bool incomingRequestInitialized = reader.ReadBoolean();
                bool incomingPointMode = reader.ReadBoolean();
                bool incomingTargetValid = NetworkNPCIdentity.TryRead(reader,
                    out NetworkNPCIdentity incomingTarget);
                Vector2 incomingBodyLocal = new(reader.ReadSingle(),
                    reader.ReadSingle());
                int incomingEntryId = reader.ReadInt32();
                int incomingTutorialOwner = -1;
                int incomingTutorialSession = 0;
                uint incomingTutorialToken = 0;
                if (reader.ReadBoolean()) {
                    incomingTutorialOwner = reader.ReadByte();
                    incomingTutorialSession = reader.ReadInt32();
                    incomingTutorialToken = reader.ReadUInt32();
                }
                PointResolution incomingPointResolution =
                    (PointResolution)reader.ReadByte();

                bool hasAuthority = reader.ReadBoolean();
                if (Main.netMode == NetmodeID.Server) {
                    AcceptIncomingRequest(incomingRequestInitialized,
                        incomingPointMode, incomingTargetValid, incomingTarget,
                        incomingBodyLocal, incomingEntryId,
                        incomingTutorialOwner, incomingTutorialSession,
                        incomingTutorialToken, incomingPointResolution);
                }
                else if (Main.netMode == NetmodeID.MultiplayerClient) {
                    AcceptReplicatedRequest(incomingRequestInitialized,
                        incomingPointMode, incomingTargetValid, incomingTarget,
                        incomingBodyLocal, incomingEntryId,
                        incomingTutorialOwner, incomingTutorialSession,
                        incomingTutorialToken, incomingPointResolution);
                }

                if (!hasAuthority) {
                    return;
                }

                AuthorityFlags flags = (AuthorityFlags)reader.ReadByte();
                long incomingActivationId = reader.ReadInt64();
                uint incomingRevision = reader.ReadUInt32();
                int incomingTimer = reader.ReadUInt16();
                bool incomingAuthorityTargetValid = NetworkNPCIdentity.TryRead(
                    reader, out NetworkNPCIdentity incomingAuthorityTarget);
                Vector2 incomingAnchor = new(reader.ReadSingle(),
                    reader.ReadSingle());
                Vector2 incomingGroupCenter = new(reader.ReadSingle(),
                    reader.ReadSingle());
                float incomingGroupHalfLength = reader.ReadSingle();
                float incomingGroupWidth = reader.ReadSingle();
                float incomingAngle = reader.ReadSingle();
                float incomingScale = reader.ReadSingle();
                int incomingDamage = reader.ReadInt32();
                float incomingKnockback = reader.ReadSingle();
                int incomingPointTravel = reader.ReadByte();
                int incomingGroupElapsed = reader.ReadUInt16();
                float incomingInheritRot = reader.ReadSingle();

                if (Main.netMode == NetmodeID.MultiplayerClient) {
                    AcceptAuthoritySnapshot(flags, incomingActivationId,
                        incomingRevision, incomingTimer,
                        incomingAuthorityTargetValid, incomingAuthorityTarget,
                        incomingAnchor, incomingGroupCenter, incomingAngle,
                        incomingScale, incomingGroupHalfLength,
                        incomingGroupWidth, incomingDamage,
                        incomingKnockback, incomingPointTravel,
                        incomingGroupElapsed, incomingInheritRot);
                }
            } catch (EndOfStreamException) {
            } catch (IOException) {
            }
        }

        internal static void CancelPendingTutorialStrikes(Player player, int session) {
            if (player == null || session <= 0) {
                return;
            }
            for (int i = Main.maxProjectiles - 1; i >= 0; i--) {
                Projectile projectile = Main.projectile[i];
                if (!projectile.active || projectile.owner != player.whoAmI
                    || projectile.ModProjectile is not OniSeverStrike strike
                    || strike.struck || strike.tutorialTargetOwner != player.whoAmI
                    || strike.tutorialTargetSession != session || strike.tutorialTargetToken == 0) {
                    continue;
                }
                projectile.Kill();
            }
        }

        /// <summary>绑定目标的存活实例，死亡或网络代次不匹配返回 null。</summary>
        private NPC ValidTarget() {
            NetworkNPCIdentity identity = authorityAccepted
                ? authorityTargetIdentity : requestTargetIdentity;
            if (!identity.TryResolve(out NPC npc) || npc.life <= 0) {
                return null;
            }
            bool currentTutorial = Tutorial.OnikiriTutorialTargetGlobal.TryGetTutorialIdentity(npc,
                out int actualOwner, out int actualSession, out uint actualToken);
            if (tutorialTargetToken == 0) {
                return currentTutorial ? null : npc;
            }
            if (!currentTutorial || actualOwner != Projectile.owner
                || actualOwner != tutorialTargetOwner || actualSession != tutorialTargetSession
                || actualToken != tutorialTargetToken) {
                return null;
            }
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                Tutorial.OnikiriTutorialNetPlayer state = Main.player[actualOwner]
                    .GetModPlayer<Tutorial.OnikiriTutorialNetPlayer>();
                if (state.ServerSession != actualSession || state.ServerTargetIndex != npc.whoAmI) {
                    return null;
                }
            }
            return npc;
        }

        public override void AI() {
            if (Main.netMode != NetmodeID.MultiplayerClient
                && !authorityAccepted && !authorityRejected) {
                TryAuthorizeRequest();
            }
            if (authorityRejected) {
                Projectile.timeLeft = Math.Min(Projectile.timeLeft, 2);
                return;
            }
            if (!authorityAccepted) {
                UpdatePendingPresentation();
                return;
            }

            InitializePresentation();
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                UpdateReplicatedPresentation();
            }
            else {
                UpdateAuthority();
            }
            ApplyReplicatedStages();
            UpdatePresentationSounds();
            UpdatePose();
        }

        /// <summary>
        /// 批复要跑一个来回，持有者本地先把蓄势与拔刀闪演出来，停在落刀帧等回信；
        /// 批复落地时 timer 取两端较大值，姿态不会倒退。其余端在批复前不该有任何表现。
        /// </summary>
        private void UpdatePendingPresentation() {
            if (Main.netMode != NetmodeID.MultiplayerClient
                || Projectile.owner != Main.myPlayer || !requestInitialized) {
                bladePose.Opacity = 0f;
                return;
            }

            timer = Math.Min(timer + 1, StrikeFrame);
            InitializePresentation();
            UpdatePresentationSounds();
            UpdatePose();
        }

        private void TryAuthorizeRequest() {
            if (!ValidateRequest(out NPC target, out Item weapon)) {
                RejectAuthority();
                return;
            }

            authorityAccepted = true;
            activationId = OniSeverReplicationSystem.AllocateActivationId();
            authorityTargetIdentity = requestTargetIdentity;
            authorityAnchorCenter = requestPointMode ? Projectile.Center : target.Center;
            authorityCutAngle = SanitizeAngle(Projectile.ai[1]);
            authorityScale = SanitizeScale(OnikiriOverride.GetBladeScale(weapon));
            Projectile.ai[0] = requestPointMode ? PointModeMarker : target.whoAmI;
            Projectile.ai[1] = authorityCutAngle;
            Projectile.ai[2] = authorityScale;
            Projectile.Center = authorityAnchorCenter;

            bool tutorial = tutorialTargetToken != 0;
            long scaledDamage = (long)(Owner.GetWeaponDamage(weapon)
                * DismemberDamageMultiplier);
            Projectile.damage = tutorial ? 0
                : (int)Math.Clamp(scaledDamage, 0L, MaxDamage);
            float knockback = Owner.GetWeaponKnockback(weapon);
            Projectile.knockBack = float.IsFinite(knockback)
                ? MathHelper.Clamp(knockback, 0f, MaxKnockback) : 0f;
            authorityDamage = Projectile.damage;
            authorityKnockback = Projectile.knockBack;
            if (requestPointMode) {
                float distance = Vector2.Distance(authorityAnchorCenter,
                    target.Center);
                authorityPointTravel = (int)MathHelper.Clamp(distance / 24f,
                    6f, 14f);
            }

            CaptureInheritedPose();
            AdvanceAuthorityRevision();
        }

        private bool ValidateRequest(out NPC target, out Item weapon) {
            target = null;
            weapon = null;
            Player owner = Owner;
            if (!requestInitialized || owner?.active != true || owner.dead
                || activationId != 0 || requestPointMode != ((int)Projectile.ai[0] == PointModeMarker)
                || !requestTargetIdentity.IsValid
                || !requestTargetIdentity.TryResolve(out target)
                || target.life <= 0
                || !OniSeverReplicationSystem.IsValidWorldPosition(
                    Projectile.Center)
                || !float.IsFinite(Projectile.ai[1])
                || !float.IsFinite(Projectile.ai[2])
                || Projectile.ai[2] < OniSeverReplicationSystem.MinScale
                || Projectile.ai[2] > OniSeverReplicationSystem.MaxScale
                || DistanceToHitbox(target, owner.Center) > DismemberRange
                || !ValidateTutorialIdentity(target)
                || (!Tutorial.OnikiriTutorialTargetGlobal.IsTutorialTarget(target,
                    out _, out _) && !target.CanBeChasedBy())
                || HasCompetingAuthority()) {
                return false;
            }

            weapon = FindOnikiri(owner);
            if (weapon == null) {
                return false;
            }
            if (!requestPointMode) {
                return (int)Projectile.ai[0] == target.whoAmI;
            }
            if (omokageEntryId <= 0 || tutorialTargetToken != 0
                || !IsFinite(requestPointBodyLocal)
                || Vector2.Distance(owner.Center, Projectile.Center) > DismemberRange) {
                return false;
            }

            Vector2 body = OniDismember.ComputeBodySize(target) * 0.5f
                + new Vector2(96f);
            body.X = MathHelper.Clamp(body.X, 96f, 2048f);
            body.Y = MathHelper.Clamp(body.Y, 96f, 2048f);
            return MathF.Abs(requestPointBodyLocal.X) <= body.X
                && MathF.Abs(requestPointBodyLocal.Y) <= body.Y;
        }

        private void UpdateAuthority() {
            timer = Math.Min(timer + 1, TotalDuration + MaxAuthorityWaitFrames);
            NPC target = ValidTarget();
            ApplyAuthorityProjectileState(target);

            if (PointMode) {
                UpdatePointAuthority(target);
            }
            else if (!struck && !whiffed && timer >= StrikeFrame) {
                if (target == null || !ActivateDirectStrike(target)) {
                    BeginAuthorityWhiff();
                }
            }

            if (!selfCutDone && timer >= SelfCutFrame && struck && !whiffed) {
                selfCutDone = true;
                if (Main.netMode == NetmodeID.SinglePlayer) {
                    OniPlayerDismember.Trigger(Owner, CutAngle);
                    localSelfCutApplied = true;
                }
                AdvanceAuthorityRevision();
            }

            int completionFrame = PointMode && groupActivated
                ? Math.Max(TotalDuration, groupActivationTimer + 3)
                : TotalDuration;
            if (whiffed) {
                completionFrame = Math.Max(whiffStartedAt + WhiffSheatheFrames
                    + NotoFadeFrames + 2, timer);
            }
            bool pointPending = PointMode && pointResolution == PointResolution.Pending
                && timer < StrikeFrame + MaxAuthorityWaitFrames;
            if (!pointPending && timer >= completionFrame) {
                Projectile.Kill();
            }
        }

        private void UpdatePointAuthority(NPC target) {
            if (Main.netMode == NetmodeID.SinglePlayer && timer >= StrikeFrame
                && pointResolution == PointResolution.Pending) {
                bool success = TryCutLocalPaper(Projectile.damage,
                    Projectile.knockBack);
                pointResolution = success ? PointResolution.Succeeded
                    : PointResolution.Failed;
            }

            if (pointResolution == PointResolution.Failed && !whiffed) {
                BeginAuthorityWhiff();
                return;
            }
            if (pointResolution == PointResolution.Pending) {
                if (timer >= StrikeFrame + MaxAuthorityWaitFrames) {
                    BeginAuthorityWhiff();
                }
                return;
            }
            if (!struck) {
                struck = true;
                if (Main.netMode == NetmodeID.Server) {
                    //确认抵达后排程，让零伤害纸面脉冲先落地
                    pointResolveAt = timer + OniFinaleCut.HoldFrames
                        + authorityPointTravel;
                }
                AdvanceAuthorityRevision();
            }

            if (Main.netMode == NetmodeID.SinglePlayer || groupActivated
                || timer < pointResolveAt) {
                return;
            }
            if (target != null) {
                ActivatePointStrike(target);
            }
        }

        private void UpdateReplicatedPresentation() {
            timer = Math.Min(timer + 1, TotalDuration + MaxAuthorityWaitFrames);
            NPC target = ValidTarget();
            ApplyAuthorityProjectileState(target);
            if (PointMode && Projectile.owner == Main.myPlayer
                && pointResolution == PointResolution.Pending
                && timer >= StrikeFrame) {
                pointResolution = TryCutLocalPaper(0, 0f)
                    ? PointResolution.Succeeded : PointResolution.Failed;
                Projectile.netUpdate = true;
            }
        }

        private bool ActivateDirectStrike(NPC target) {
            OniSeverReplicationSystem.PrepareTargetIdentity(
                authorityTargetIdentity, target);
            DismemberStroke stroke = new(target.Center, CutAngle,
                OniFinaleCut.VisualHalfLength * SizeMul,
                OniFinaleCut.VisualPathWidth * SizeMul);
            if (!OniDismember.TriggerGroup(target, in stroke,
                holdFrames: OniFinaleCut.HoldFrames)) {
                return false;
            }

            struck = true;
            RegisterGroupActivation(in stroke, OniFinaleCut.HoldFrames);
            SyncFromServer(OniFinaleCut.Fire(Owner, target.Center, CutAngle,
                Projectile.damage, Projectile.knockBack, SizeMul,
                Projectile.GetSource_FromAI()));
            if (Main.netMode == NetmodeID.SinglePlayer) {
                FireTutorialEvent(target);
            }
            AdvanceAuthorityRevision();
            return true;
        }

        private void ActivatePointStrike(NPC target) {
            Vector2 cutCenter = target.Center + requestPointBodyLocal;
            if (!IsFinite(cutCenter)) {
                return;
            }
            OniSeverReplicationSystem.PrepareTargetIdentity(
                authorityTargetIdentity, target);
            DismemberStroke stroke = new(cutCenter, CutAngle,
                MathF.Max(target.Size.Length(), 64f),
                OniFinaleCut.VisualPathWidth);
            if (!OniDismember.TriggerGroup(target, in stroke, holdFrames: 0)) {
                return;
            }

            RegisterGroupActivation(in stroke, 0);
            if (Owner?.active == true && Projectile.damage > 0) {
                int hitDirection = MathF.Cos(CutAngle) >= 0f ? 1 : -1;
                Owner.ApplyDamageToNPC(target, Projectile.damage,
                    Projectile.knockBack, hitDirection, false);
            }
            AdvanceAuthorityRevision();
        }

        private void RegisterGroupActivation(in DismemberStroke stroke,
            int holdFrames) {
            groupActivated = true;
            groupActivationTimer = timer;
            authorityGroupCenter = stroke.Center;
            authorityGroupHalfLength = stroke.HalfLength;
            authorityGroupWidth = stroke.Width;
            OniSeverReplicationSystem.Publish(activationId, Projectile.owner,
                authorityTargetIdentity, in stroke, SizeMul,
                OniDismember.DefaultDuration, holdFrames, PointMode);
        }

        private void ApplyReplicatedStages() {
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                return;
            }
            if (groupActivated && !localGroupApplied) {
                localGroupApplied = true;
                OniSeverActivationSnapshot snapshot = new(activationId,
                    Projectile.owner, authorityTargetIdentity,
                    authorityGroupCenter, CutAngle, SizeMul,
                    authorityGroupHalfLength, authorityGroupWidth,
                    receivedGroupElapsed, OniDismember.DefaultDuration,
                    PointMode ? 0 : OniFinaleCut.HoldFrames, PointMode);
                OniSeverReplicationSystem.ApplySnapshot(in snapshot);
                if (!PointMode && authorityTargetIdentity.TryResolve(out NPC target)) {
                    FireTutorialEvent(target);
                }
            }
            if (selfCutDone && !localSelfCutApplied) {
                localSelfCutApplied = true;
                OniPlayerDismember.Trigger(Owner, CutAngle);
            }
            if (whiffed && !whiffSoundPlayed) {
                PlayWhiffSound();
            }
        }

        /// <summary>落刀落空、转空挥快速收鞘退场</summary>
        private void BeginAuthorityWhiff() {
            if (whiffed || struck) {
                return;
            }
            struck = false;
            whiffed = true;
            whiffStartedAt = timer;
            Projectile.timeLeft = Math.Min(Projectile.timeLeft,
                WhiffSheatheFrames + NotoFadeFrames + 3);
            AdvanceAuthorityRevision();
            if (Main.netMode == NetmodeID.SinglePlayer) {
                PlayWhiffSound();
            }
        }

        private void PlayWhiffSound() {
            whiffSoundPlayed = true;
            if (!Main.dedServ && Owner?.active == true) {
                SoundEngine.PlaySound(SoundID.Unlock with {
                    Pitch = 0.30f,
                    Volume = 0.35f,
                }, Owner.Center);
            }
        }

        private AuthorityFlags BuildAuthorityFlags() {
            AuthorityFlags flags = AuthorityFlags.None;
            if (authorityAccepted) {
                flags |= AuthorityFlags.Accepted;
            }
            if (authorityRejected) {
                flags |= AuthorityFlags.Rejected;
            }
            if (struck) {
                flags |= AuthorityFlags.Struck;
            }
            if (whiffed) {
                flags |= AuthorityFlags.Whiffed;
            }
            if (selfCutDone) {
                flags |= AuthorityFlags.SelfCutDone;
            }
            if (groupActivated) {
                flags |= AuthorityFlags.GroupActivated;
            }
            if (inheritPose) {
                flags |= AuthorityFlags.InheritPose;
            }
            if (PointMode) {
                flags |= AuthorityFlags.PointMode;
            }
            return flags;
        }

        private void AcceptIncomingRequest(bool initialized, bool pointMode,
            bool targetValid, NetworkNPCIdentity target, Vector2 bodyLocal,
            int entryId, int tutorialOwner, int tutorialSession,
            uint tutorialToken, PointResolution resolution) {
            if (!initialized || !targetValid || resolution > PointResolution.Failed
                || !IsFinite(bodyLocal)) {
                return;
            }
            if (!requestInitialized) {
                requestInitialized = true;
                requestPointMode = pointMode;
                requestTargetIdentity = target;
                requestPointBodyLocal = bodyLocal;
                omokageEntryId = entryId;
                tutorialTargetOwner = tutorialOwner;
                tutorialTargetSession = tutorialSession;
                tutorialTargetToken = tutorialToken;
                //批准请求前不接受客户端声称的斩纸结果
                pointResolution = PointResolution.Pending;
                return;
            }

            bool sameRequest = requestPointMode == pointMode
                && requestTargetIdentity == target
                && requestPointBodyLocal == bodyLocal
                && omokageEntryId == entryId
                && tutorialTargetOwner == tutorialOwner
                && tutorialTargetSession == tutorialSession
                && tutorialTargetToken == tutorialToken;
            if (!sameRequest || !authorityAccepted || !PointMode
                || pointResolution != PointResolution.Pending
                || resolution == PointResolution.Pending) {
                return;
            }
            pointResolution = resolution;
        }

        private void AcceptReplicatedRequest(bool initialized, bool pointMode,
            bool targetValid, NetworkNPCIdentity target, Vector2 bodyLocal,
            int entryId, int tutorialOwner, int tutorialSession,
            uint tutorialToken, PointResolution resolution) {
            if (!initialized || !targetValid || resolution > PointResolution.Failed
                || !IsFinite(bodyLocal)) {
                return;
            }
            if (!requestInitialized) {
                requestInitialized = true;
                requestPointMode = pointMode;
                requestTargetIdentity = target;
                requestPointBodyLocal = bodyLocal;
                omokageEntryId = entryId;
                tutorialTargetOwner = tutorialOwner;
                tutorialTargetSession = tutorialSession;
                tutorialTargetToken = tutorialToken;
            }
            if (requestPointMode == pointMode && requestTargetIdentity == target
                && pointResolution == PointResolution.Pending
                && resolution != PointResolution.Pending) {
                pointResolution = resolution;
            }
        }

        private void AcceptAuthoritySnapshot(AuthorityFlags flags,
            long incomingActivationId, uint incomingRevision, int incomingTimer,
            bool targetValid, NetworkNPCIdentity target, Vector2 anchor,
            Vector2 groupCenter, float angle, float scale,
            float groupHalfLength, float groupWidth, int damage,
            float knockback, int pointTravel, int groupElapsed,
            float incomingInheritRot) {
            bool accepted = (flags & AuthorityFlags.Accepted) != 0;
            bool rejected = (flags & AuthorityFlags.Rejected) != 0;
            bool incomingStruck = (flags & AuthorityFlags.Struck) != 0;
            bool incomingWhiffed = (flags & AuthorityFlags.Whiffed) != 0;
            bool incomingSelfCut = (flags & AuthorityFlags.SelfCutDone) != 0;
            bool incomingGroup = (flags & AuthorityFlags.GroupActivated) != 0;
            bool incomingPointMode = (flags & AuthorityFlags.PointMode) != 0;
            if (accepted == rejected || incomingRevision == 0
                || incomingTimer < 0
                || incomingTimer > TotalDuration + MaxAuthorityWaitFrames
                || incomingStruck && incomingWhiffed
                || incomingSelfCut && !incomingStruck
                || incomingGroup && !incomingStruck
                || incomingPointMode != requestPointMode
                || !float.IsFinite(incomingInheritRot)) {
                return;
            }

            if (rejected) {
                if (authorityAccepted || authorityRejected
                    || incomingActivationId != 0) {
                    return;
                }
                authorityRejected = true;
                authorityRevision = incomingRevision;
                whiffed = true;
                whiffStartedAt = incomingTimer;
                Projectile.timeLeft = Math.Min(Projectile.timeLeft, 2);
                return;
            }

            if (!targetValid || incomingActivationId <= 0
                || !OniSeverReplicationSystem.IsValidWorldPosition(anchor)
                || incomingGroup
                    && !OniSeverReplicationSystem.IsValidWorldPosition(groupCenter)
                || incomingGroup && (!float.IsFinite(groupHalfLength)
                    || groupHalfLength < 1f
                    || groupHalfLength > OniSeverReplicationSystem.MaxHalfLength
                    || !float.IsFinite(groupWidth) || groupWidth < 1f
                    || groupWidth > OniSeverReplicationSystem.MaxWidth)
                || !float.IsFinite(angle)
                || !float.IsFinite(scale)
                || scale < OniSeverReplicationSystem.MinScale
                || scale > OniSeverReplicationSystem.MaxScale
                || damage < 0 || damage > MaxDamage
                || !float.IsFinite(knockback) || knockback < 0f
                || knockback > MaxKnockback
                || incomingPointMode && (pointTravel < 6 || pointTravel > 14)
                || !incomingPointMode && pointTravel != 0
                || groupElapsed < 0
                || groupElapsed >= OniDismember.DefaultDuration) {
                return;
            }
            if (authorityAccepted) {
                if (activationId != incomingActivationId
                    || incomingRevision <= authorityRevision
                    || incomingTimer < authoritySnapshotTimer
                    || target != authorityTargetIdentity
                    || anchor != authorityAnchorCenter
                    || MathF.Abs(MathHelper.WrapAngle(angle
                        - authorityCutAngle)) > 1e-5f
                    || MathF.Abs(scale - authorityScale) > 1e-5f
                    || damage != authorityDamage
                    || MathF.Abs(knockback - authorityKnockback) > 1e-5f
                    || pointTravel != authorityPointTravel
                    || groupActivated
                        && (groupElapsed < receivedGroupElapsed
                            || groupCenter != authorityGroupCenter
                            || MathF.Abs(groupHalfLength
                                - authorityGroupHalfLength) > 1e-5f
                            || MathF.Abs(groupWidth
                                - authorityGroupWidth) > 1e-5f)) {
                    return;
                }
                if (struck && !incomingStruck || whiffed && !incomingWhiffed
                    || selfCutDone && !incomingSelfCut
                    || groupActivated && !incomingGroup) {
                    return;
                }
            }
            else if (authorityRejected) {
                return;
            }

            authorityAccepted = true;
            activationId = incomingActivationId;
            authorityRevision = incomingRevision;
            authoritySnapshotTimer = incomingTimer;
            authorityTargetIdentity = target;
            authorityAnchorCenter = anchor;
            authorityGroupCenter = groupCenter;
            authorityGroupHalfLength = groupHalfLength;
            authorityGroupWidth = groupWidth;
            authorityCutAngle = MathHelper.WrapAngle(angle);
            authorityScale = scale;
            authorityPointTravel = pointTravel;
            authorityDamage = damage;
            authorityKnockback = knockback;
            receivedGroupElapsed = groupElapsed;
            timer = Math.Max(timer, incomingTimer);
            struck = incomingStruck;
            whiffed = incomingWhiffed;
            selfCutDone = incomingSelfCut;
            groupActivated = incomingGroup;
            inheritPose = (flags & AuthorityFlags.InheritPose) != 0;
            inheritRot = MathHelper.WrapAngle(incomingInheritRot);
            Projectile.ai[0] = incomingPointMode ? PointModeMarker : target.Index;
            Projectile.ai[1] = authorityCutAngle;
            Projectile.ai[2] = authorityScale;
            Projectile.damage = damage;
            Projectile.knockBack = knockback;
            Projectile.Center = authorityAnchorCenter;
            if (incomingWhiffed && whiffStartedAt < 0) {
                whiffStartedAt = incomingTimer;
            }
        }

        private void CaptureInheritedPose() {
            Player owner = Owner;
            if (owner?.active != true) {
                inheritPose = false;
                inheritRot = 0f;
                return;
            }
            IOniComboController combo = OniBladeOccupancy.FindComboController(owner);
            inheritPose = combo != null
                && combo.TryGetBladePose(out inheritRot, out _)
                || OniBladeHandoff.TryPeek(owner, out inheritRot, out _);
            if (!float.IsFinite(inheritRot)) {
                inheritPose = false;
                inheritRot = 0f;
            }
        }

        private void InitializePresentation() {
            if (presentationInitialized) {
                return;
            }
            presentationInitialized = true;
            if (!Main.dedServ && Owner?.active == true) {
                SoundEngine.PlaySound(SoundID.Item71 with {
                    Pitch = -0.60f,
                    Volume = 0.40f,
                }, Owner.Center);
            }
        }

        private void UpdatePresentationSounds() {
            if (!drawSoundPlayed && timer >= WindupFrames + 1) {
                drawSoundPlayed = true;
                if (!Main.dedServ && Owner?.active == true) {
                    SoundEngine.PlaySound(CWRSound.KatanaSwing with {
                        Pitch = 0.70f,
                        Volume = 0.42f,
                    }, Owner.Center);
                }
            }
        }

        private void ApplyAuthorityProjectileState(NPC target) {
            Projectile.ai[0] = PointMode ? PointModeMarker
                : authorityTargetIdentity.Index;
            Projectile.ai[1] = authorityCutAngle;
            Projectile.ai[2] = authorityScale;
            //每个同步包都会把伤害截回 short，逐帧按权威值复原，两端才对得上
            Projectile.damage = authorityDamage;
            Projectile.knockBack = authorityKnockback;
            if (PointMode) {
                Projectile.Center = authorityAnchorCenter;
            }
            else if (!struck && target != null) {
                Projectile.Center = target.Center;
            }
            else if (!IsFinite(Projectile.Center)) {
                Projectile.Center = authorityAnchorCenter;
            }
        }

        private bool TryCutLocalPaper(int damage, float knockback) {
            Player owner = Owner;
            return !Main.dedServ && owner?.active == true && omokageEntryId > 0
                && OniOmokage.SeverEntry(owner, omokageEntryId,
                    authorityAnchorCenter, CutAngle, Math.Clamp(damage, 0,
                        MaxDamage), MathHelper.Clamp(knockback, 0f,
                        MaxKnockback));
        }

        private void FireTutorialEvent(NPC target) {
            if (localTutorialEventApplied || Projectile.owner != Main.myPlayer
                || Owner?.active != true || target?.active != true) {
                return;
            }
            localTutorialEventApplied = true;
            Tutorial.OnikiriTutorialEvents.FireDismemberLanded(Owner, target);
        }

        private void RejectAuthority() {
            authorityRejected = true;
            authorityAccepted = false;
            activationId = 0;
            whiffed = true;
            whiffStartedAt = 0;
            AdvanceAuthorityRevision();
            Projectile.timeLeft = Math.Min(Projectile.timeLeft, 3);
        }

        private void AdvanceAuthorityRevision() {
            authorityRevision++;
            if (authorityRevision == 0) {
                authorityRevision = 1;
            }
            SyncFromServer(Projectile);
        }

        /// <summary>
        /// 服务器不是这颗弹幕的持有者，原版只让 owner == Main.myPlayer 的一端消费 netUpdate，
        /// 服务端置位等于石沉大海（同理，服务端代持有者生成的弹幕也不会自动下发）。
        /// 权威推进与代生成都得自己补发同步包，否则客户端永远停在"未批复"。
        /// </summary>
        private static void SyncFromServer(Projectile projectile) {
            if (Main.netMode == NetmodeID.Server && projectile?.active == true) {
                NetMessage.SendData(MessageID.SyncProjectile, -1, -1, null,
                    projectile.whoAmI);
            }
        }

        private bool ValidateTutorialIdentity(NPC target) {
            bool tutorial = Tutorial.OnikiriTutorialTargetGlobal
                .TryGetTutorialIdentity(target, out int owner,
                    out int session, out uint token);
            if (tutorialTargetToken == 0) {
                return !tutorial;
            }
            if (!tutorial || owner != Projectile.owner
                || tutorialTargetOwner != owner
                || tutorialTargetSession != session
                || tutorialTargetToken != token) {
                return false;
            }
            Tutorial.OnikiriTutorialNetPlayer state = Owner
                .GetModPlayer<Tutorial.OnikiriTutorialNetPlayer>();
            return state.ServerSession == session
                && state.ServerTargetIndex == target.whoAmI;
        }

        private bool HasCompetingAuthority() {
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile other = Main.projectile[i];
                if (!other.active || other.whoAmI == Projectile.whoAmI
                    || other.owner != Projectile.owner || other.type != Type
                    || other.ModProjectile is not OniSeverStrike strike) {
                    continue;
                }
                if (strike.authorityAccepted && !strike.whiffed) {
                    return true;
                }
            }
            return false;
        }

        private static Item FindOnikiri(Player player) {
            if (player?.HeldItem?.type == OnikiriOverride.ID
                && player.HeldItem.Alives()) {
                return player.HeldItem;
            }
            if (player?.inventory == null) {
                return null;
            }
            for (int i = 0; i < player.inventory.Length; i++) {
                Item item = player.inventory[i];
                if (item?.type == OnikiriOverride.ID && item.Alives()) {
                    return item;
                }
            }
            return null;
        }

        private static OmokageEntry FindOmokageEntry(int entryId) {
            if (entryId <= 0) {
                return null;
            }
            for (int i = 0; i < OniOmokage.Entries.Count; i++) {
                OmokageEntry entry = OniOmokage.Entries[i];
                if (entry.Id == entryId && entry.IsArmed
                    && entry.Alpha >= 0.35f) {
                    return entry;
                }
            }
            return null;
        }

        private static float DistanceToHitbox(NPC npc, Vector2 point) {
            Rectangle hitbox = npc.Hitbox;
            float x = MathHelper.Clamp(point.X, hitbox.Left, hitbox.Right);
            float y = MathHelper.Clamp(point.Y, hitbox.Top, hitbox.Bottom);
            return Vector2.Distance(point, new Vector2(x, y));
        }

        private static bool IsFinite(Vector2 value)
            => float.IsFinite(value.X) && float.IsFinite(value.Y);

        private static float SanitizeAngle(float value)
            => float.IsFinite(value) ? MathHelper.WrapAngle(value) : 0f;

        private static float SanitizeScale(float value)
            => float.IsFinite(value)
                ? MathHelper.Clamp(value, OniSeverReplicationSystem.MinScale,
                    OniSeverReplicationSystem.MaxScale)
                : 1f;

        /// <summary>居合四段（纯视觉，不锁操控）、蓄=鞘位反压、闪=两帧甩出只剩残影、 残心=过冲回稳后屏息微晃</summary>
        private void UpdatePose() {
            bladePose.Update();
            if (!Owner.active || Owner.dead) {
                return;
            }

            //反噬落下后本体交给玩家肢解管线（刀已入鞘，随身体一并定格）

            if (struck && timer > SelfCutFrame) {
                bladePose.Opacity = 0f;
                return;
            }

            //残心起让位给玩家输入；演出播完亦收

            if (timer > StrikeFrame + 2
                && (OniBladeOccupancy.ComboClaims(Owner) || OniBladeOccupancy.AnyHardOccupant(Owner, Projectile))
                || timer > SheatheFrame + NotoFlickFrames + NotoFadeFrames) {
                bladePose.Opacity = 0f;
                return;
            }

            //面向落刀锚点；水平几乎重合时退回切线方向

            int facing = MathF.Cos(CutAngle) >= 0f ? 1 : -1;
            float toAnchorX = Projectile.Center.X - Owner.Center.X;
            if (MathF.Abs(toAnchorX) > 8f) {
                facing = toAnchorX > 0f ? 1 : -1;
            }
            //拔刀完成位顺切线，按朝向取不背手的那一端

            Vector2 cutDir = CutAngle.ToRotationVector2();
            float strikeRot = cutDir.X * facing >= 0f ? CutAngle : MathHelper.WrapAngle(CutAngle + MathHelper.Pi);
            float sheathRot = strikeRot - facing * 1.05f;
            var stretch = Player.CompositeArmStretchAmount.Full;

            if (whiffed) {
                UpdateWhiffPose(facing, strikeRot, sheathRot);
                return;
            }

            if (timer <= WindupFrames) {
                //蓄、鞘位再反向压一分、出刀前的那口气；

                //继承连段刀角时从当前角度顺势收拢（刀已在手，不淡入，划弧带轻残影）

                float wind = OFR.EaseOutCubic(timer / (float)WindupFrames);
                if (inheritPose) {
                    bladePose.Rotation = OniBladePose.LerpAngle(inheritRot, sheathRot - facing * 0.30f, wind);
                    bladePose.Opacity = 1f;
                    bladePose.PushSmear(0.4f);
                }
                else {
                    bladePose.Rotation = sheathRot - facing * 0.30f * wind;
                    bladePose.Opacity = MathHelper.Clamp(timer / 3f, 0f, 1f);
                }
                stretch = Player.CompositeArmStretchAmount.Quarter;
            }
            else if (timer <= StrikeFrame) {
                //闪、两帧从鞘底甩到切线带过冲，逐帧压残影

                float t = (timer - WindupFrames) / (float)DrawFlashFrames;
                float ease = 1f - (1f - t) * (1f - t) * (1f - t);
                bladePose.Rotation = OniBladePose.LerpAngle(sheathRot - facing * 0.30f
                    , strikeRot + facing * 0.16f, ease);
                bladePose.Opacity = 1f;
                bladePose.PushSmear(1f);
            }
            else if (timer <= SheatheFrame) {
                //残心、过冲回稳后屏息，微晃是唯一的动静

                float settle = MathHelper.Clamp((timer - StrikeFrame) / 6f, 0f, 1f);
                bladePose.Rotation = OniBladePose.LerpAngle(strikeRot + facing * 0.16f, strikeRot, settle)
                    + MathF.Sin(timer * 0.045f) * 0.03f * settle;
                bladePose.Opacity = 1f;
            }
            else if (timer <= SheatheFrame + NotoFlickFrames) {
                //纳刀、与引爆同帧起手，一挑入鞘、刀入鞘，目标才裂

                float t = (timer - SheatheFrame) / (float)NotoFlickFrames;
                float ease = 1f - (1f - t) * (1f - t) * (1f - t);
                bladePose.Rotation = OniBladePose.LerpAngle(strikeRot, sheathRot, ease);
                bladePose.Opacity = 1f;
                if (timer - SheatheFrame <= 3) {
                    bladePose.PushSmear(0.8f);
                }
                stretch = Player.CompositeArmStretchAmount.ThreeQuarters;
            }
            else {
                //收、持刀淡出

                bladePose.Rotation = sheathRot;
                bladePose.Opacity = 1f - (timer - SheatheFrame - NotoFlickFrames) / (float)NotoFadeFrames;
                stretch = Player.CompositeArmStretchAmount.ThreeQuarters;
            }

            bladePose.ApplyPose(Owner, Projectile, stretch);
        }

        /// <summary>空挥收势、没有残心可言，顺势快速收鞘淡出</summary>
        private void UpdateWhiffPose(int facing, float strikeRot, float sheathRot) {
            int wt = timer - StrikeFrame;
            if (wt <= WhiffSheatheFrames) {
                float ease = OFR.EaseOutCubic(wt / (float)WhiffSheatheFrames);
                bladePose.Rotation = OniBladePose.LerpAngle(strikeRot + facing * 0.16f, sheathRot, ease);
                bladePose.Opacity = 1f;
            }
            else {
                bladePose.Rotation = sheathRot;
                bladePose.Opacity = 1f - (wt - WhiffSheatheFrames) / (float)NotoFadeFrames;
            }
            bladePose.ApplyPose(Owner, Projectile);
        }

        /// <summary>遮挡层、居合持刀的实体刀与拔刀残影</summary>
        void IOverlayDrawable.DrawOverlay(SpriteBatch spriteBatch) {
            if (Main.dedServ) {
                return;
            }
            bladePose.Draw(spriteBatch, Owner);
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
