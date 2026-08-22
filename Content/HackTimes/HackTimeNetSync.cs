using CalamityOverhaul.Content.HackTimes.BossParts;
using CalamityOverhaul.Content.HackTimes.CircuitNodes;
using CalamityOverhaul.Content.HackTimes.Protocols;
using CalamityOverhaul.Content.HackTimes.Scannables;
using CalamityOverhaul.Content.RAMSystems;
using CalamityOverhaul.Content.TimeFreezes;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes
{
    internal enum HackNetOperation : byte
    {
        Request,
        QueueState,
        EffectApply,
        EffectProgress,
        EffectRemove,
        //新操作一律追加在末尾，已发布的编号是线上格式的一部分
        OwnedSnapshot,
        PointCue,
        //== PvP（玩家目标）批：收发全在 PvP/PlayerHackNet，这里只占号 ==
        //攻击方 → 服务端：玩家扫描探针（限频 1/60f/人）
        ScanProbe,
        //服务端 → 请求者：探针回包（防御/RAM 段位/义体/协议数）
        ScanProbeReply,
        //服务端 → 防守方：来袭上传通告（被骇横幅数据源，45f TTL 自清）
        DefenderNotice,
        //服务端 → 防守方：授予施加（防守方本机结算的入口）
        DefenderApply,
        //防守方 → 服务端：施加回执（Applied/Rejected + 可选真值载荷）
        DefenderReceipt,
        //服务端 → 全员：per-defender 在册效果快照（影子时钟，观众表现数据源）
        PlayerEffectState,
        //服务端 → 全员：效果移除（带原因字节，HUD 按它区分碎裂/淡出）
        PlayerEffectRemove,
        //防守方 → 服务端：周期对账（300f，审计痕不是执法）
        DefenderLedgerReport,
        //服务端 → 回溯施术者：被点亮的攻击方标记
        TracebackResult,
        //服务端 → 攻击方：反制警报（被回溯/目标失联/被拒绝）
        PvPAlert,
    }

    /// <summary>
    /// 落点表现的种类。<br/>
    /// 有些协议在 <c>OnApply</c> 里就把目标改没了（掉落物被挪走、弹幕被击杀），
    /// 而 EffectApply 广播排在 <c>OnApply</c> 之后，远端要么取不到落点要么身份已失效，
    /// 表现只能靠这条自带座标的旁路补
    /// </summary>
    internal enum HackPointCue : byte
    {
        ItemRecall,
        DataPurge,
        Exorcise,
    }

    internal enum HackRequestResultCode : byte
    {
        Success,
        InvalidSession,
        ConflictingRequest,
        ExpiredRequest,
        InvalidPlayer,
        InvalidProtocol,
        InvalidTarget,
        UnsupportedTarget,
        Unavailable,
        InsufficientRam,
        QueueFull,
        InvalidPayload,
        ProtocolLocked,
        //== PvP 准入拒绝码（HackPvPRules.CanTarget 逐条映射，尾部追加） ==
        //服务端总开关关闭
        PvPDisabled,
        //双方 hostile 不满足（单向 hostile 不可选中）
        NotHostile,
        //同一支非零队伍互相免疫
        SameTeam,
        //防守方复活保护窗口内
        SpawnProtected,
        //同 (攻击方,防守方) 对的落地冷却未过
        PairCooldown,
        //叠加上限（全局 ≤3 / 同对 ≤2）
        StackLimit,
        //PvP 距离越界（上传期重验对它单独给 45f 宽限；
        //不复用 InvalidPayload：那是 claim 一致性校验的拒绝码）
        OutOfRange,
    }

    /// <summary>
    /// 目标的跨端身份。新种类一律追加可选参数，别改前四个位置
    /// 现有构造点全按位置传参
    /// </summary>
    internal readonly record struct HackNetworkTarget(
        HackTargetKind Kind,
        NetworkNPCIdentity NpcIdentity,
        int TileX,
        int TileY,
        NetworkProjectileIdentity ProjIdentity = default,
        int ItemIndex = -1,
        int ItemType = ItemID.None,
        int SelfPlayerIndex = -1,
        CircuitActorKey ActorKey = default,
        int PlayerIndex = -1)
    {
        internal bool IsSerializable => Kind switch {
            HackTargetKind.Npc => NpcIdentity.IsValid,
            //部件复用 NPC 身份负载
            HackTargetKind.BossPart => NpcIdentity.IsValid,
            //液体格与物块格共用同一对座标；容器身份 = 箱子锚点格
            HackTargetKind.Tile or HackTargetKind.Water or HackTargetKind.Container
                => TileX >= 0 && TileX < Main.maxTilesX
                    && TileY >= 0 && TileY < Main.maxTilesY,
            HackTargetKind.Projectile => ProjIdentity.IsValid,
            HackTargetKind.Item => ItemIndex >= 0 && ItemIndex < Main.maxItems
                && ItemType > ItemID.None && ItemType < ItemLoader.ItemCount,
            //零负载恒可写（kind 本身即身份）；能否解析是 TryResolve 的事
            HackTargetKind.SelfRig => true,
            HackTargetKind.World => true,
            HackTargetKind.Turret or HackTargetKind.SignalTower => ActorKey.IsValid,
            //玩家身份 = 槽位索引。不需要 generation：槽位复用只发生在断线→新人加入
            //之间（分钟级不是帧级），且执行期全量重验 + 授予账 name 双检兜底
            //（论证：HACKTIME-PVP-DESIGN §2.2）
            HackTargetKind.Player => PlayerIndex >= 0 && PlayerIndex < Main.maxPlayers,
            _ => false,
        };

        internal bool TryResolve(out IHackTarget target) {
            target = null;
            if (!IsSerializable) return false;
            if (Kind == HackTargetKind.Npc) {
                if (!NpcIdentity.TryResolve(out NPC npc)) return false;
                target = new NpcScannable(npc.whoAmI);
                return true;
            }
            if (Kind == HackTargetKind.BossPart) {
                if (!NpcIdentity.TryResolve(out NPC partNpc)) return false;
                //解析瞬间已不是部件（本体刚死）就按目标丢失处理，别退化成普通 NPC 目标
                if (!BossPartResolver.TryGetPart(partNpc, out _)) return false;
                target = new BossPartScannable(partNpc.whoAmI);
                return true;
            }
            if (Kind == HackTargetKind.Tile) {
                target = new TileScannable(TileX, TileY);
                return true;
            }
            if (Kind == HackTargetKind.Water) {
                target = new WaterScannable(TileX, TileY);
                return true;
            }
            if (Kind == HackTargetKind.Projectile) {
                //弹幕槽位各端不同，必须靠 owner+identity 反查本机槽
                if (!ProjIdentity.TryResolve(out Projectile projectile)) return false;
                target = new ProjectileScannable(projectile.whoAmI);
                return true;
            }
            if (Kind == HackTargetKind.Item) {
                Item item = Main.item[ItemIndex];
                if (item?.active != true || item.IsAir || item.type != ItemType) {
                    return false;
                }
                target = new ItemScannable(ItemIndex);
                return true;
            }
            if (Kind == HackTargetKind.SelfRig) {
                //SelfPlayerIndex 不上线，由收包方回填；未回填（-1）的实例解析失败
                if (SelfPlayerIndex < 0 || SelfPlayerIndex >= Main.maxPlayers) return false;
                Player player = Main.player[SelfPlayerIndex];
                if (player?.active != true || player.dead) return false;
                target = new SelfRigScannable(SelfPlayerIndex);
                return true;
            }
            if (Kind == HackTargetKind.Container) {
                //锚点上箱子还在才算解析成功（被挖走/炸掉视作目标丢失）
                if (!ContainerScannable.IsContainerAnchorAt(TileX, TileY)) {
                    return false;
                }
                target = new ContainerScannable(TileX, TileY);
                return true;
            }
            if (Kind == HackTargetKind.World) {
                target = new WorldScannable();
                return true;
            }
            if (Kind == HackTargetKind.Player) {
                Player player = Main.player[PlayerIndex];
                if (player?.active != true || player.dead || player.ghost) return false;
                target = new PlayerScannable(PlayerIndex);
                return true;
            }
            if (Kind is HackTargetKind.Turret or HackTargetKind.SignalTower) {
                //Actor 会同步到客户端，槽位+代+类型直接解析
                if (!ActorKey.TryResolve(out var actor)) return false;
                if (Kind == HackTargetKind.Turret && actor is IHackableTurret turret) {
                    target = turret;
                    return true;
                }
                if (Kind == HackTargetKind.SignalTower && actor is IHackableSignalTower tower) {
                    target = tower;
                    return true;
                }
                return false;
            }
            return false;
        }

        internal static bool TryCapture(IHackTarget target,
            out HackNetworkTarget identity) {
            identity = default;
            //部件必须先于 NpcScannable 判定：BossPartScannable 是它的子类，
            //放在基类分支后面永远被截走、上线成 Kind=Npc
            if (target is BossPartScannable partTarget) {
                if (partTarget.NpcIndex < 0 || partTarget.NpcIndex >= Main.maxNPCs
                    || !NetworkNPCIdentity.TryCapture(Main.npc[partTarget.NpcIndex],
                        out NetworkNPCIdentity partIdentity)) {
                    return false;
                }
                identity = new HackNetworkTarget(HackTargetKind.BossPart,
                    partIdentity, -1, -1);
                return true;
            }
            if (target is NpcScannable npcTarget) {
                if (npcTarget.NpcIndex < 0 || npcTarget.NpcIndex >= Main.maxNPCs
                    || !NetworkNPCIdentity.TryCapture(Main.npc[npcTarget.NpcIndex],
                        out NetworkNPCIdentity npcIdentity)) {
                    return false;
                }
                identity = new HackNetworkTarget(HackTargetKind.Npc,
                    npcIdentity, -1, -1);
                return true;
            }
            if (target is TileScannable tileTarget) {
                int x = tileTarget.TileCoordX;
                int y = tileTarget.TileCoordY;
                if (x < 0 || x >= Main.maxTilesX || y < 0
                    || y >= Main.maxTilesY || !Main.tile[x, y].HasTile) {
                    return false;
                }
                identity = new HackNetworkTarget(HackTargetKind.Tile,
                    default, x, y);
                return true;
            }
            if (target is WaterScannable waterTarget) {
                int x = waterTarget.TileCoordX;
                int y = waterTarget.TileCoordY;
                if (x < 0 || x >= Main.maxTilesX || y < 0
                    || y >= Main.maxTilesY || Main.tile[x, y].LiquidAmount == 0) {
                    return false;
                }
                identity = new HackNetworkTarget(HackTargetKind.Water,
                    default, x, y);
                return true;
            }
            if (target is ProjectileScannable projTarget) {
                if (projTarget.ProjectileIndex < 0
                    || projTarget.ProjectileIndex >= Main.maxProjectiles
                    || !NetworkProjectileIdentity.TryCapture(
                        Main.projectile[projTarget.ProjectileIndex],
                        out NetworkProjectileIdentity projIdentity)) {
                    return false;
                }
                identity = new HackNetworkTarget(HackTargetKind.Projectile,
                    default, -1, -1, projIdentity);
                return true;
            }
            if (target is ItemScannable itemTarget) {
                int index = itemTarget.ItemIndex;
                if (index < 0 || index >= Main.maxItems) return false;
                Item item = Main.item[index];
                if (item?.active != true || item.IsAir) return false;
                //掉落物槽位在联机里是全局同步的，index+type 足够认人
                identity = new HackNetworkTarget(HackTargetKind.Item,
                    default, -1, -1, default, index, item.type);
                return true;
            }
            if (target is SelfRigScannable selfTarget) {
                //刻意不按 Main.myPlayer 闸：服务端广播效果时也要能捕获（服务端 myPlayer = 255，
                //闸了就是 BroadcastEffectApply 静默失败、队友永远看不到效果的经典坑）。
                //"只能是自己"由悬停探测（只产本机玩家）与请求回填（只回填发起者）共同保证
                if (selfTarget.PlayerIndex < 0 || selfTarget.PlayerIndex >= Main.maxPlayers) {
                    return false;
                }
                identity = new HackNetworkTarget(HackTargetKind.SelfRig,
                    default, -1, -1, SelfPlayerIndex: selfTarget.PlayerIndex);
                return true;
            }
            if (target is ContainerScannable containerTarget) {
                int x = containerTarget.AnchorX;
                int y = containerTarget.AnchorY;
                if (!ContainerScannable.IsContainerAnchorAt(x, y)) return false;
                identity = new HackNetworkTarget(HackTargetKind.Container,
                    default, x, y);
                return true;
            }
            if (target is WorldScannable) {
                //零负载：kind 即身份
                identity = new HackNetworkTarget(HackTargetKind.World,
                    default, -1, -1);
                return true;
            }
            if (target is PlayerScannable playerTarget) {
                Player player = playerTarget.ResolvePlayer();
                if (player == null || player.dead || player.ghost) return false;
                identity = new HackNetworkTarget(HackTargetKind.Player,
                    default, -1, -1, PlayerIndex: playerTarget.PlayerIndex);
                return true;
            }
            if (target is IHackableTurret turretTarget
                && CircuitActorKey.TryCapture(turretTarget.AsActor, out CircuitActorKey turretKey)) {
                identity = new HackNetworkTarget(HackTargetKind.Turret,
                    default, -1, -1, ActorKey: turretKey);
                return true;
            }
            if (target is IHackableSignalTower towerTarget
                && CircuitActorKey.TryCapture(towerTarget.AsActor, out CircuitActorKey towerKey)) {
                identity = new HackNetworkTarget(HackTargetKind.SignalTower,
                    default, -1, -1, ActorKey: towerKey);
                return true;
            }
            return false;
        }
    }

    internal readonly record struct HackQueueSnapshotRecord(
        int PlayerIndex,
        uint SessionId,
        uint RequestId,
        int SlotIndex,
        HackQueueState State,
        int Elapsed,
        int UploadFrames,
        long ActivationId,
        HackNetworkTarget Target);

    internal readonly record struct HackEffectSnapshotRecord(
        long ActivationId,
        int CasterIndex,
        uint SessionId,
        uint RequestId,
        int SlotIndex,
        int Elapsed,
        float EffectMult,
        int Generation,
        HackNetworkTarget Target);

    /// <summary>骇入请求、队列与效果复制</summary>
    internal static class HackTimeNetSync
    {
        internal const ushort RamOperationId = 48;
        //HACK32：WriteTarget 的 kind 升 ushort，线格式变了，快照版本随之 +1
        private const byte SnapshotVersion = 2;
        private const int MaxUploadsPerPlayer = 32;
        private const int MaxSnapshotUploads = 2048;
        private const int MaxSnapshotEffects = 4096;
        private const int MaxUploadFrames = 60 * 60;
        private const float MaxTargetDistance = 6400f;
        private const float MaxClaimError = 512f;

        private sealed class HackEffectPendingSource { }
        private sealed class HackQueuePendingSource { }

        private static readonly Dictionary<long, int> pendingProgress = [];
        private static long nextActivationId;
        private static ulong lastAuthorityUpdateFrame = ulong.MaxValue;

        internal static bool TryRequestQueue(QuickHackDef hack, IHackTarget target,
            out uint sessionId, out uint requestId) {
            sessionId = 0;
            requestId = 0;
            if (hack == null || target == null || !target.IsValid
                || hack.SlotIndex < 0 || hack.SlotIndex >= QuickHackDef.Count
                || (hack.SupportedTargets & (target.TargetType?.Kind
                    ?? HackTargetKind.None)) == 0) {
                return false;
            }
            if (Main.myPlayer < 0 || Main.myPlayer >= Main.maxPlayers) return false;
            Player player = Main.player[Main.myPlayer];
            if (player?.active != true || player.dead
                || !HackProtocolOwned.Owns(player, hack)
                || !RamSystem.TryAllocateRequest(player, out RamRequestToken token)) {
                return false;
            }
            sessionId = token.SessionId;
            requestId = token.RequestId;

            if (Main.netMode == NetmodeID.SinglePlayer) {
                return ExecuteAuthorityRequest(player, token, hack, target,
                    default, target.WorldCenter, -1);
            }
            if (Main.netMode != NetmodeID.MultiplayerClient
                || !HackNetworkTarget.TryCapture(target,
                    out HackNetworkTarget targetIdentity)
                || !IsFiniteVector(target.WorldCenter)) {
                return false;
            }

            ModPacket packet = NewPacket(HackNetOperation.Request);
            packet.Write(token.SessionId);
            packet.Write(token.RequestId);
            packet.Write((ushort)hack.SlotIndex);
            WriteTarget(packet, targetIdentity);
            packet.Write(target.WorldCenter.X);
            packet.Write(target.WorldCenter.Y);
            packet.Send();
            //本地预扣显示，与服务器同式估算成本；收到回执或超时后对账
            //（玩家目标不吃无限权限，预扣照登记，与权威侧的例外口径一致）
            if (!HackTime.InfiniteHack || target is PlayerScannable) {
                int predictedCost = Math.Clamp(
                    HackCostEvaluator.GetActualCost(hack, target, player),
                    1, (int)RamSystem.MaxMutationAmount);
                player.GetModPlayer<RAMPlayer>()
                    .RegisterPredictedDebit(token.RequestId, predictedCost);
            }
            return true;
        }

        public static void HandleApplyPacket(BinaryReader reader, int whoAmI) {
            if (reader == null) return;
            try {
                HackNetOperation operation = (HackNetOperation)reader.ReadByte();
                switch (operation) {
                    case HackNetOperation.Request:
                        HandleRequest(reader, whoAmI);
                        break;
                    case HackNetOperation.QueueState:
                        HandleQueueState(reader);
                        break;
                    case HackNetOperation.EffectApply:
                        HandleEffectApply(reader);
                        break;
                    case HackNetOperation.EffectProgress:
                        HandleEffectProgress(reader);
                        break;
                    case HackNetOperation.EffectRemove:
                        HandleEffectRemove(reader);
                        break;
                    case HackNetOperation.OwnedSnapshot:
                        HandleOwnedSnapshot(reader, whoAmI);
                        break;
                    case HackNetOperation.PointCue:
                        HandlePointCue(reader);
                        break;
                    default:
                        //PvP（玩家目标）批的操作全部分发给 PlayerHackNet
                        PvP.PlayerHackNet.Handle(operation, reader, whoAmI);
                        break;
                }
            } catch (EndOfStreamException) {
            } catch (IOException) {
            }
        }

        #region 协议持有快照

        /// <summary>
        /// 本机持有集上报权威端。持有集归客户端所有，服务端只是拿到校验输入，
        /// 所以这里是单向上行，没有回执也没有下发
        /// </summary>
        internal static void SendOwnedSnapshot(Player player) {
            if (Main.netMode != NetmodeID.MultiplayerClient || player == null
                || player.whoAmI != Main.myPlayer
                || !player.TryGetModPlayer(out HackTimePlayer htp)) {
                return;
            }
            HackProtocolOwned.EnsureSeed(htp);

            List<int> indices = [];
            foreach (string fullName in htp.OwnedProtocols) {
                QuickHackDef hack = QuickHackDef.GetByFullName(fullName);
                if (hack != null && hack.SlotIndex >= 0 && hack.SlotIndex < QuickHackDef.Count) {
                    indices.Add(hack.SlotIndex);
                }
            }

            ModPacket packet = NewPacket(HackNetOperation.OwnedSnapshot);
            packet.Write((ushort)indices.Count);
            for (int i = 0; i < indices.Count; i++) {
                packet.Write((ushort)indices[i]);
            }
            packet.Send();
        }

        private static void HandleOwnedSnapshot(BinaryReader reader, int whoAmI) {
            //先把负载读干净再做守卫：CWRNetWork 让所有 NetHandle 串行共用同一个 reader，
            //提前 return 会把剩余条目留在流里，同包后续分支全部错位
            int count = reader.ReadUInt16();
            List<QuickHackDef> hacks = [];
            for (int i = 0; i < count; i++) {
                int slotIndex = reader.ReadUInt16();
                QuickHackDef hack = QuickHackDef.GetByIndex(slotIndex);
                if (hack != null) {
                    hacks.Add(hack);
                }
            }

            //刻意不要求 player.active：上行发生在客户端进世界那一帧，
            //那时玩家还没在服务端生成，卡这一条会把整局的持有校验静默关掉。
            //这里只写该玩家自己的 ModPlayer，没有别的落点
            if (Main.netMode != NetmodeID.Server || !IsValidPlayerIndex(whoAmI)
                || count > QuickHackDef.Count) {
                return;
            }
            HackProtocolOwned.ApplyNetworkSnapshot(Main.player[whoAmI], hacks);
        }

        #endregion

        private static void HandleRequest(BinaryReader reader, int whoAmI) {
            uint sessionId = reader.ReadUInt32();
            uint requestId = reader.ReadUInt32();
            int slotIndex = reader.ReadUInt16();
            //先把负载读干净再做守卫：CWRNetWork 让所有 NetHandle 串行共用同一个 reader，
            //目标非法就提前 return 会把这 8 字节留在流里，同包后续分支全部错位
            bool targetValid = TryReadTarget(reader, out HackNetworkTarget identity);
            Vector2 claimedCenter = new(reader.ReadSingle(), reader.ReadSingle());
            if (!targetValid || Main.netMode != NetmodeID.Server
                || !IsValidPlayerIndex(whoAmI))
                return;
            Player player = Main.player[whoAmI];
            if (player?.active != true || requestId == 0) {
                RamNet.SendStateSnapshot(player, whoAmI);
                return;
            }

            RamRequestDisposition disposition = RamSystem.ClassifyRequest(player,
                sessionId, requestId, RamOperationId,
                out RamRequestResult previous);
            if (disposition == RamRequestDisposition.Replay) {
                ResendRequestState(player, sessionId, requestId, whoAmI);
                RamNet.SendRequestResult(player, previous, whoAmI);
                return;
            }
            if (disposition == RamRequestDisposition.Invalid) {
                RamNet.SendStateSnapshot(player, whoAmI);
                return;
            }
            if (disposition != RamRequestDisposition.New) {
                HackRequestResultCode code = disposition
                    == RamRequestDisposition.Conflict
                    ? HackRequestResultCode.ConflictingRequest
                    : HackRequestResultCode.ExpiredRequest;
                RamNet.SendRejectedRequest(player, sessionId, requestId,
                    RamOperationId, (byte)code, whoAmI);
                return;
            }

            if (identity.Kind == HackTargetKind.SelfRig) {
                //自体目标恒以请求发起者回填，线上不携带玩家索引，
                //"替别人骇"在结构上不可表达，不需要任何反作弊校验
                identity = identity with { SelfPlayerIndex = whoAmI };
            }
            if (!identity.TryResolve(out IHackTarget target)) {
                CompleteRequest(player, new RamRequestToken(sessionId, requestId),
                    HackRequestResultCode.InvalidTarget, 0f, whoAmI);
                return;
            }
            QuickHackDef hack = QuickHackDef.GetByIndex(slotIndex);
            ExecuteAuthorityRequest(player,
                new RamRequestToken(sessionId, requestId), hack, target,
                identity, claimedCenter, whoAmI);
        }

        private static bool ExecuteAuthorityRequest(Player player,
            RamRequestToken request, QuickHackDef hack, IHackTarget target,
            HackNetworkTarget identity, Vector2 claimedCenter,
            int responseClient) {
            if (Main.netMode == NetmodeID.MultiplayerClient || player?.active != true
                || player.dead || !request.IsValid) return false;

            HackRequestResultCode failure = ValidateAuthorityRequest(player, hack,
                target, identity, claimedCenter);
            if (failure != HackRequestResultCode.Success) {
                //客户端只会看到"点了没反应"，服务端不说是哪一条被拒就得靠通读源码去猜。
                //正常游戏里拒绝很少见，一行日志换一句可诊断的报告
                if (Main.netMode == NetmodeID.Server) {
                    CWRMod.Instance.Logger.Info(
                        $"[HackTime] rejected {player.name}'s "
                        + $"{hack?.Name ?? "<null protocol>"} on "
                        + $"{target?.GetType().Name ?? "<null target>"}: {failure}");
                }
                CompleteRequest(player, request, failure, 0f, responseClient);
                SendQueueState(player.whoAmI, request.SessionId,
                    request.RequestId, hack?.SlotIndex ?? -1,
                    HackQueueState.Waiting, 0, 1, 0, identity,
                    accepted: false, responseClient);
                return false;
            }

            HackTimeAuthorityPlayer state = player
                .GetModPlayer<HackTimeAuthorityPlayer>();
            state.BindSession(request.SessionId);
            if (state.Uploads.Count >= MaxUploadsPerPlayer) {
                CompleteRequest(player, request, HackRequestResultCode.QueueFull,
                    0f, responseClient);
                SendQueueState(player.whoAmI, request.SessionId,
                    request.RequestId, hack.SlotIndex, HackQueueState.Waiting,
                    0, hack.UploadTime, 0, identity, accepted: false,
                    responseClient);
                return false;
            }
            for (int i = 0; i < state.Uploads.Count; i++) {
                AuthorityHackUpload queued = state.Uploads[i];
                if (queued.SlotIndex == hack.SlotIndex
                    && queued.Target?.TargetEquals(target) == true) {
                    CompleteRequest(player, request,
                        HackRequestResultCode.Unavailable, 0f, responseClient);
                    SendQueueState(player.whoAmI, request.SessionId,
                        request.RequestId, hack.SlotIndex,
                        HackQueueState.Waiting, 0, hack.UploadTime, 0,
                        identity, accepted: false, responseClient);
                    return false;
                }
            }

            int cost = Math.Clamp(HackCostEvaluator.GetActualCost(hack, target,
                player),
                1, (int)RamSystem.MaxMutationAmount);
            float paid = 0f;
            //无限权限是 PvE 演出资产：对玩家目标无效，照常扣 RAM（进 PvP 就是无限骚扰）
            bool infiniteAuthority = HackTime.InfiniteHackAuthority
                && target is not PlayerScannable;
            if (!infiniteAuthority && !RamSystem.TryConsume(player, cost, out paid)) {
                CompleteRequest(player, request,
                    HackRequestResultCode.InsufficientRam, 0f, responseClient);
                SendQueueState(player.whoAmI, request.SessionId,
                    request.RequestId, hack.SlotIndex, HackQueueState.Waiting,
                    0, hack.UploadTime, 0, identity, accepted: false,
                    responseClient);
                return false;
            }

            var upload = new AuthorityHackUpload {
                SessionId = request.SessionId,
                RequestId = request.RequestId,
                SlotIndex = hack.SlotIndex,
                Target = target,
                TargetIdentity = identity,
                PaidRamCost = paid,
                Elapsed = 0,
                //提权窗口：上传时间 ×0.6（面板显示侧走同一个折算口，读数才对得上）
                UploadFrames = Math.Clamp(
                    PrivilegeEscalateState.ApplyUploadTime(hack.UploadTime, player),
                    1, MaxUploadFrames),
                State = state.Uploads.Count == 0
                    ? HackQueueState.Uploading
                    : HackQueueState.Waiting,
                ActivationId = 0,
            };
            state.Uploads.Add(upload);

            if (!CompleteRequest(player, request, HackRequestResultCode.Success,
                paid, responseClient)) {
                state.Uploads.Remove(upload);
                if (paid > 0f) RamSystem.Restore(player, paid, out _);
                return false;
            }
            SendQueueState(player.whoAmI, upload.SessionId, upload.RequestId,
                upload.SlotIndex, upload.State, upload.Elapsed,
                upload.UploadFrames, upload.ActivationId, identity,
                accepted: true, responseClient);
            return true;
        }

        private static HackRequestResultCode ValidateAuthorityRequest(Player player,
            QuickHackDef hack, IHackTarget target, HackNetworkTarget identity,
            Vector2 claimedCenter) {
            if (player == null || !player.active || player.dead)
                return HackRequestResultCode.InvalidPlayer;
            if (hack == null || hack.SlotIndex < 0
                || hack.SlotIndex >= QuickHackDef.Count
                || hack.UploadTime <= 0 || hack.UploadTime > MaxUploadFrames
                || hack.RamCost < 0 || hack.RamCost > RamSystem.MaxMutationAmount) {
                return HackRequestResultCode.InvalidProtocol;
            }
            if (!HasProtocolAuthority(player, hack))
                return HackRequestResultCode.ProtocolLocked;
            if (target == null || !target.IsValid || !target.IsHackable)
                return HackRequestResultCode.InvalidTarget;
            HackTargetKind kind = target.TargetType?.Kind ?? HackTargetKind.None;
            if ((hack.SupportedTargets & kind) == 0)
                return HackRequestResultCode.UnsupportedTarget;
            if (Main.netMode == NetmodeID.Server
                && (!identity.IsSerializable || identity.Kind != kind)) {
                return HackRequestResultCode.InvalidTarget;
            }
            if (kind == HackTargetKind.SelfRig) {
                //自体目标：距离恒 0、claim 无安全含义（线上格式没有玩家索引，伪造不出"别人"），
                //几何校验整块免除；恒等断言，解析产物必须就是请求者本人（回填语义的兜底）
                if ((target as SelfRigScannable)?.PlayerIndex != player.whoAmI) {
                    return HackRequestResultCode.InvalidTarget;
                }
            }
            else if (kind == HackTargetKind.Player) {
                //PvP 准入：成对谓词全量重验（与客户端预检同一个函数，谓词只写一份）；
                //距离子句按攻击方宣称座标（claim 一致性先行，误差 ≤ MaxClaimError）
                Player defender = (target as PlayerScannable)?.ResolvePlayer();
                if (defender == null) return HackRequestResultCode.InvalidTarget;
                if (!IsFiniteVector(claimedCenter)
                    || Vector2.DistanceSquared(defender.Center, claimedCenter)
                        > MaxClaimError * MaxClaimError
                    || Vector2.DistanceSquared(player.Center, claimedCenter)
                        > PvP.HackPvPRules.MaxDistance * PvP.HackPvPRules.MaxDistance) {
                    return HackRequestResultCode.InvalidPayload;
                }
                if (!PvP.HackPvPRules.CanTarget(player, defender,
                    out HackRequestResultCode denied)) {
                    return denied;
                }
            }
            else {
                Vector2 center = target.WorldCenter;
                //World 目标无实体锚点：反查实例的 WorldCenter 是兜底值，
                //取施术者宣称的天空座标做距离校验（协议写入不消费该座标）
                if (kind == HackTargetKind.World && IsFiniteVector(claimedCenter)) {
                    center = claimedCenter;
                }
                //claimedCenter 一致性校验是反伪造面，保持无条件；
                //提权窗口只放行玩家↔目标距离那一子句
                if (!IsFiniteVector(center) || !IsFiniteVector(claimedCenter)
                    || Vector2.DistanceSquared(center, claimedCenter)
                        > MaxClaimError * MaxClaimError
                    || (!PrivilegeEscalateState.BypassRangeGate(player)
                        && Vector2.DistanceSquared(player.Center, center)
                            > MaxTargetDistance * MaxTargetDistance)) {
                    return HackRequestResultCode.InvalidPayload;
                }
            }
            if (!HackTimeAccess.CanUse(player) || !hack.CanApplyTo(target, player))
                return HackRequestResultCode.Unavailable;
            return HackRequestResultCode.Success;
        }

        /// <summary>
        /// 持有校验。服务端在收到该玩家快照前一律放行，进世界时快照与首个请求存在竞态，
        /// 无脑拒会在联机下全员误杀。持有集本就是客户端自报的，这里不是反作弊面
        /// </summary>
        private static bool HasProtocolAuthority(Player player, QuickHackDef hack) {
            if (hack.UnlockedByDefault) {
                return true;
            }
            if (Main.netMode == NetmodeID.Server
                && (!player.TryGetModPlayer(out HackTimePlayer htp)
                    || !htp.OwnedSnapshotReceived)) {
                return true;
            }
            return HackProtocolOwned.Owns(player, hack);
        }

        private static bool CompleteRequest(Player player, RamRequestToken request,
            HackRequestResultCode code, float paid, int responseClient) {
            if (!RamSystem.CompleteRequest(player, request, RamOperationId,
                (byte)code, paid, out RamRequestResult result)) {
                if (Main.netMode == NetmodeID.Server && responseClient >= 0)
                    RamNet.SendStateSnapshot(player, responseClient);
                return false;
            }
            if (Main.netMode == NetmodeID.Server && responseClient >= 0)
                RamNet.SendRequestResult(player, result, responseClient);
            return true;
        }

        internal static void UpdateAuthority() {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            ulong frame = Main.GameUpdateCount;
            if (lastAuthorityUpdateFrame == frame) return;
            lastAuthorityUpdateFrame = frame;

            for (int playerIndex = 0; playerIndex < Main.maxPlayers; playerIndex++) {
                Player player = Main.player[playerIndex];
                if (player?.active != true) continue;
                HackTimeAuthorityPlayer state = player
                    .GetModPlayer<HackTimeAuthorityPlayer>();
                RAMPlayer ram = player.GetModPlayer<RAMPlayer>();
                if (!ram.ProfileInitialized || ram.SessionId == 0) continue;
                state.BindSession(ram.SessionId);
                UpdatePlayerUploads(player, state);
            }
        }

        private static void UpdatePlayerUploads(Player player,
            HackTimeAuthorityPlayer state) {
            bool hasUploading = false;
            for (int i = 0; i < state.Uploads.Count; i++) {
                AuthorityHackUpload upload = state.Uploads[i];
                QuickHackDef hack = QuickHackDef.GetByIndex(upload.SlotIndex);
                if (hack == null || !TryResolveUploadTarget(upload,
                    out IHackTarget target)) {
                    //Player 目标中途失联（死亡/掉线）：按非自愿取消口径退半
                    //（其余种类维持原语义，NPC 击杀退款走 RefundKilledEffect）
                    if (upload.TargetIdentity.Kind == HackTargetKind.Player
                        && upload.PaidRamCost > 0f) {
                        RamSystem.Restore(player, upload.PaidRamCost * 0.5f, out _);
                    }
                    SendQueueState(player.whoAmI, upload.SessionId,
                        upload.RequestId, upload.SlotIndex, upload.State,
                        upload.Elapsed, upload.UploadFrames, 0,
                        upload.TargetIdentity, accepted: false, player.whoAmI);
                    state.Uploads.RemoveAt(i--);
                    continue;
                }
                upload.Target = target;
                if (upload.State == HackQueueState.Waiting && !hasUploading)
                    upload.State = HackQueueState.Uploading;
                if (upload.State != HackQueueState.Uploading) continue;
                hasUploading = true;
                //Player 目标（PvP）分流：上传期准入重验、攻防双端进度播报、
                //完成后走 DefenderApply 授予管线，不进 HackEffectTracker
                //（防守方客户端才是自己状态的合法写入者）
                if (upload.TargetIdentity.Kind == HackTargetKind.Player) {
                    if (PvP.PlayerHackAuthority.TickUpload(player, state, upload)) {
                        state.Uploads.Remove(upload);
                        i--;
                        hasUploading = false;
                    }
                    continue;
                }
                upload.Elapsed = Math.Min(upload.Elapsed + 1,
                    upload.UploadFrames);
                if (upload.Elapsed % 15 == 0 && Main.netMode == NetmodeID.Server) {
                    SendQueueState(player.whoAmI, upload.SessionId,
                        upload.RequestId, upload.SlotIndex, upload.State,
                        upload.Elapsed, upload.UploadFrames, 0,
                        upload.TargetIdentity, accepted: true, player.whoAmI);
                }
                if (upload.Elapsed < upload.UploadFrames) continue;

                long activationId = AllocateActivationId();
                ActiveHackEffect effect = HackEffectTracker.ApplyAuthorityEffect(
                    hack, target, player.whoAmI, upload.SessionId,
                    upload.RequestId, upload.PaidRamCost, activationId);
                upload.State = HackQueueState.Completed;
                upload.ActivationId = effect?.ActivationId ?? 0;
                SendQueueState(player.whoAmI, upload.SessionId,
                    upload.RequestId, upload.SlotIndex, upload.State,
                    upload.UploadFrames, upload.UploadFrames,
                    upload.ActivationId, upload.TargetIdentity,
                    accepted: effect != null, player.whoAmI);
                state.Uploads.RemoveAt(i--);
                hasUploading = false;
            }
        }

        private static bool TryResolveUploadTarget(AuthorityHackUpload upload,
            out IHackTarget target) {
            target = null;
            if (upload.TargetIdentity.IsSerializable)
                return upload.TargetIdentity.TryResolve(out target);
            if (Main.netMode == NetmodeID.SinglePlayer
                && upload.Target?.IsValid == true) {
                target = upload.Target;
                return true;
            }
            return false;
        }

        internal static long AllocateActivationId() {
            do {
                nextActivationId++;
                if (nextActivationId <= 0) nextActivationId = 1;
            }
            while (HackEffectTracker.FindEffect(nextActivationId) != null);
            return nextActivationId;
        }

        internal static void BroadcastEffectApply(ActiveHackEffect effect,
            int toWho = -1) {
            if (Main.netMode != NetmodeID.Server
                || !TryCreateEffectRecord(effect,
                    out HackEffectSnapshotRecord record)) return;
            ModPacket packet = NewPacket(HackNetOperation.EffectApply);
            WriteEffectRecord(packet, record);
            packet.Send(toWho);
        }

        internal static void BroadcastEffectProgress(ActiveHackEffect effect) {
            if (Main.netMode != NetmodeID.Server || effect == null
                || effect.ActivationId <= 0 || effect.Elapsed < 0
                || effect.Elapsed > HackEffectTracker.MaxEffectDuration) return;
            ModPacket packet = NewPacket(HackNetOperation.EffectProgress);
            packet.Write(effect.ActivationId);
            packet.Write(effect.Elapsed);
            packet.Send();
        }

        internal static void BroadcastEffectRemove(long activationId) {
            if (Main.netMode != NetmodeID.Server || activationId <= 0) return;
            ModPacket packet = NewPacket(HackNetOperation.EffectRemove);
            packet.Write(activationId);
            packet.Send();
        }

        /// <summary>目标已被改掉的协议，用自带座标的旁路把表现补到各客户端</summary>
        internal static void BroadcastPointCue(HackPointCue cue, int casterIndex,
            Vector2 point) {
            if (Main.netMode != NetmodeID.Server || !IsValidPlayerIndex(casterIndex)
                || !IsFiniteVector(point)) {
                return;
            }
            ModPacket packet = NewPacket(HackNetOperation.PointCue);
            packet.Write((byte)cue);
            packet.Write((byte)casterIndex);
            packet.Write(point.X);
            packet.Write(point.Y);
            packet.Send();
        }

        private static void HandlePointCue(BinaryReader reader) {
            HackPointCue cue = (HackPointCue)reader.ReadByte();
            int casterIndex = reader.ReadByte();
            Vector2 point = new(reader.ReadSingle(), reader.ReadSingle());
            if (Main.netMode != NetmodeID.MultiplayerClient
                || !IsValidPlayerIndex(casterIndex) || !IsFiniteVector(point)) {
                return;
            }
            switch (cue) {
                case HackPointCue.ItemRecall:
                    Protocols.ItemRecall.PlayRecallTrail(casterIndex, point);
                    break;
                case HackPointCue.DataPurge:
                    Protocols.DataPurge.PlayPurgeCue(point);
                    break;
                case HackPointCue.Exorcise:
                    Protocols.Exorcise.PlayEraseCue(point);
                    break;
            }
        }

        private static void HandleQueueState(BinaryReader reader) {
            bool accepted = reader.ReadBoolean();
            if (!TryReadQueueRecord(reader, out HackQueueSnapshotRecord record)
                || Main.netMode != NetmodeID.MultiplayerClient) return;
            ApplyReplicatedQueueRecord(record, accepted);
        }

        private static void HandleEffectApply(BinaryReader reader) {
            if (!TryReadEffectRecord(reader, out HackEffectSnapshotRecord record)
                || Main.netMode != NetmodeID.MultiplayerClient) return;
            ApplyReplicatedEffectRecord(record);
        }

        private static void HandleEffectProgress(BinaryReader reader) {
            long activationId = reader.ReadInt64();
            int elapsed = reader.ReadInt32();
            if (Main.netMode != NetmodeID.MultiplayerClient || activationId <= 0
                || elapsed < 0 || elapsed > HackEffectTracker.MaxEffectDuration)
                return;
            if (!HackEffectTracker.ApplyReplicatedProgress(activationId, elapsed)) {
                if (pendingProgress.Count < Main.maxNPCs + Main.maxProjectiles)
                    pendingProgress[activationId] = elapsed;
            }
        }

        private static void HandleEffectRemove(BinaryReader reader) {
            long activationId = reader.ReadInt64();
            if (Main.netMode != NetmodeID.MultiplayerClient || activationId <= 0)
                return;
            pendingProgress.Remove(activationId);
            TimeControlReplicationSystem.Cancel<HackEffectPendingSource>(activationId);
            HackEffectTracker.RemoveReplicatedEffect(activationId);
        }

        //PvP 批的上传分流（PlayerHackAuthority）复用同一条队列播报通道，故 internal
        internal static void SendQueueState(int playerIndex, uint sessionId,
            uint requestId, int slotIndex, HackQueueState state, int elapsed,
            int uploadFrames, long activationId, HackNetworkTarget target,
            bool accepted, int toWho) {
            if (Main.netMode != NetmodeID.Server || toWho < 0
                || toWho >= Main.maxPlayers || playerIndex < 0
                || playerIndex >= Main.maxPlayers || sessionId == 0
                || requestId == 0 || slotIndex < 0
                || slotIndex >= QuickHackDef.Count || !target.IsSerializable)
                return;
            var record = new HackQueueSnapshotRecord(playerIndex, sessionId,
                requestId, slotIndex, state, Math.Clamp(elapsed, 0, MaxUploadFrames),
                Math.Clamp(uploadFrames, 1, MaxUploadFrames), activationId, target);
            ModPacket packet = NewPacket(HackNetOperation.QueueState);
            packet.Write(accepted);
            WriteQueueRecord(packet, record);
            packet.Send(toWho);
        }

        private static void ResendRequestState(Player player, uint sessionId,
            uint requestId, int toWho) {
            HackTimeAuthorityPlayer state = player
                .GetModPlayer<HackTimeAuthorityPlayer>();
            for (int i = 0; i < state.Uploads.Count; i++) {
                AuthorityHackUpload upload = state.Uploads[i];
                if (upload.SessionId != sessionId
                    || upload.RequestId != requestId) continue;
                SendQueueState(player.whoAmI, sessionId, requestId,
                    upload.SlotIndex, upload.State, upload.Elapsed,
                    upload.UploadFrames, upload.ActivationId,
                    upload.TargetIdentity, accepted: true, toWho);
                return;
            }
            ActiveHackEffect effect = FindEffectByRequest(player.whoAmI,
                sessionId, requestId);
            if (effect != null) BroadcastEffectApply(effect, toWho);
        }

        private static ActiveHackEffect FindEffectByRequest(int casterIndex,
            uint sessionId, uint requestId) {
            IReadOnlyList<ActiveHackEffect> npcEffects
                = HackEffectTracker.AllActiveEffects;
            for (int i = 0; i < npcEffects.Count; i++) {
                ActiveHackEffect effect = npcEffects[i];
                if (effect.Active && effect.CasterIndex == casterIndex
                    && effect.SessionId == sessionId
                    && effect.RequestId == requestId) return effect;
            }
            IReadOnlyList<ActiveHackEffect> tileEffects
                = HackEffectTracker.AllActiveTileEffects;
            for (int i = 0; i < tileEffects.Count; i++) {
                ActiveHackEffect effect = tileEffects[i];
                if (effect.Active && effect.CasterIndex == casterIndex
                    && effect.SessionId == sessionId
                    && effect.RequestId == requestId) return effect;
            }
            return null;
        }

        internal static bool WriteSnapshot(BinaryWriter writer) {
            if (writer == null || Main.netMode == NetmodeID.MultiplayerClient)
                return false;
            var uploads = new List<HackQueueSnapshotRecord>();
            for (int i = 0; i < Main.maxPlayers
                && uploads.Count < MaxSnapshotUploads; i++) {
                Player player = Main.player[i];
                if (player?.active != true) continue;
                HackTimeAuthorityPlayer state = player
                    .GetModPlayer<HackTimeAuthorityPlayer>();
                for (int j = 0; j < state.Uploads.Count
                    && uploads.Count < MaxSnapshotUploads; j++) {
                    AuthorityHackUpload upload = state.Uploads[j];
                    if (!upload.TargetIdentity.IsSerializable) continue;
                    uploads.Add(new HackQueueSnapshotRecord(i,
                        upload.SessionId, upload.RequestId, upload.SlotIndex,
                        upload.State, upload.Elapsed, upload.UploadFrames,
                        upload.ActivationId, upload.TargetIdentity));
                }
            }

            var effects = new List<HackEffectSnapshotRecord>();
            CollectEffectRecords(HackEffectTracker.AllActiveEffects, effects);
            CollectEffectRecords(HackEffectTracker.AllActiveTileEffects, effects);
            if (effects.Count > MaxSnapshotEffects) return false;

            writer.Write(SnapshotVersion);
            writer.Write((ushort)uploads.Count);
            for (int i = 0; i < uploads.Count; i++)
                WriteQueueRecord(writer, uploads[i]);
            writer.Write((ushort)effects.Count);
            for (int i = 0; i < effects.Count; i++)
                WriteEffectRecord(writer, effects[i]);
            return true;
        }

        internal static bool ReadSnapshot(BinaryReader reader) {
            if (reader == null) return false;
            try {
                if (reader.ReadByte() != SnapshotVersion) return false;
                int uploadCount = reader.ReadUInt16();
                if (uploadCount < 0 || uploadCount > MaxSnapshotUploads)
                    return false;
                var uploads = new List<HackQueueSnapshotRecord>(uploadCount);
                for (int i = 0; i < uploadCount; i++) {
                    if (!TryReadQueueRecord(reader,
                        out HackQueueSnapshotRecord record)) return false;
                    uploads.Add(record);
                }
                int effectCount = reader.ReadUInt16();
                if (effectCount < 0 || effectCount > MaxSnapshotEffects)
                    return false;
                var effects = new List<HackEffectSnapshotRecord>(effectCount);
                for (int i = 0; i < effectCount; i++) {
                    if (!TryReadEffectRecord(reader,
                        out HackEffectSnapshotRecord record)) return false;
                    effects.Add(record);
                }
                if (Main.netMode != NetmodeID.MultiplayerClient) return true;

                HackTimeUI.Instance?.Queue?.Clear();
                HackEffectTracker.BeginReplicatedSnapshot();
                pendingProgress.Clear();
                TimeControlReplicationSystem.CancelAll<HackQueuePendingSource>();
                TimeControlReplicationSystem.CancelAll<HackEffectPendingSource>();
                for (int i = 0; i < uploads.Count; i++)
                    ApplyReplicatedQueueRecord(uploads[i], accepted: true);
                for (int i = 0; i < effects.Count; i++)
                    ApplyReplicatedEffectRecord(effects[i]);
                return true;
            } catch (EndOfStreamException) {
                return false;
            } catch (IOException) {
                return false;
            }
        }

        private static void ApplyReplicatedQueueRecord(
            HackQueueSnapshotRecord record, bool accepted) {
            if (record.PlayerIndex != Main.myPlayer) return;
            HackQueueRenderer queue = HackTimeUI.Instance?.Queue;
            if (queue == null) return;
            if (!accepted) {
                queue.RemoveRequest(record.SessionId, record.RequestId);
                return;
            }
            QuickHackDef hack = QuickHackDef.GetByIndex(record.SlotIndex);
            if (hack == null) return;
            void Apply(IHackTarget target) {
                queue.Enqueue(hack, record.SlotIndex, target, 0,
                    record.SessionId, record.RequestId);
                float progress = record.UploadFrames > 0
                    ? record.Elapsed / (float)record.UploadFrames
                    : 0f;
                queue.ApplyAuthorityState(record.SessionId, record.RequestId,
                    record.SlotIndex, record.State, progress,
                    record.ActivationId, accepted: true);
            }
            if (record.Target.TryResolve(out IHackTarget target)) {
                Apply(target);
                return;
            }
            //部件记录同样带 NpcIdentity，照走迟到 NPC 的挂起解析
            if (record.Target.Kind is not HackTargetKind.Npc
                and not HackTargetKind.BossPart) return;
            long pendingId = unchecked(((long)record.SessionId << 32)
                | record.RequestId);
            int remaining = Math.Max(1, record.UploadFrames - record.Elapsed);
            TimeControlReplicationSystem.ResolveOrQueueNPC<HackQueuePendingSource>(
                pendingId, record.Target.NpcIdentity, remaining,
                npc => Apply(new NpcScannable(npc.whoAmI)));
        }

        private static void ApplyReplicatedEffectRecord(
            HackEffectSnapshotRecord record) {
            QuickHackDef hack = QuickHackDef.GetByIndex(record.SlotIndex);
            if (hack == null) return;
            void Apply(IHackTarget target) {
                int elapsed = record.Elapsed;
                if (pendingProgress.Remove(record.ActivationId,
                    out int pendingElapsed)) {
                    elapsed = Math.Max(elapsed, pendingElapsed);
                }
                HackEffectTracker.ApplyReplicatedEffect(record.ActivationId,
                    hack, target, record.Target.NpcIdentity,
                    record.CasterIndex, record.SessionId, record.RequestId,
                    elapsed, record.EffectMult, record.Generation);
                if (record.CasterIndex == Main.myPlayer)
                    HackTimeUI.Instance?.Queue?.RemoveRequest(record.SessionId,
                        record.RequestId);
            }
            if (record.Target.TryResolve(out IHackTarget target)) {
                Apply(target);
                return;
            }
            //部件记录同样带 NpcIdentity，照走迟到 NPC 的挂起解析；
            //挂起回调造普通 NpcScannable 即可，复制端只跑表现，Replicated 钩子经 TryNpc 解包
            if (record.Target.Kind is not HackTargetKind.Npc
                and not HackTargetKind.BossPart) return;
            int duration = Math.Clamp((int)(hack.GetDuration()
                * record.EffectMult), 0, HackEffectTracker.MaxEffectDuration);
            int remaining = Math.Max(1, duration - record.Elapsed);
            TimeControlReplicationSystem.ResolveOrQueueNPC<HackEffectPendingSource>(
                record.ActivationId, record.Target.NpcIdentity, remaining,
                npc => Apply(new NpcScannable(npc.whoAmI)));
        }

        private static void CollectEffectRecords(
            IReadOnlyList<ActiveHackEffect> source,
            List<HackEffectSnapshotRecord> destination) {
            for (int i = 0; i < source.Count
                && destination.Count < MaxSnapshotEffects; i++) {
                ActiveHackEffect effect = source[i];
                if (!effect.Active || !effect.Applied || effect.Replicated
                    || effect.EffectiveDuration <= 0) continue;
                if (TryCreateEffectRecord(effect,
                    out HackEffectSnapshotRecord record)) destination.Add(record);
            }
        }

        private static bool TryCreateEffectRecord(ActiveHackEffect effect,
            out HackEffectSnapshotRecord record) {
            record = default;
            if (effect == null || effect.ActivationId <= 0 || effect.Hack == null
                || effect.Hack.SlotIndex < 0
                || effect.Hack.SlotIndex >= QuickHackDef.Count
                || effect.CasterIndex < 0 || effect.CasterIndex >= Main.maxPlayers
                || effect.Elapsed < 0
                || effect.Elapsed > HackEffectTracker.MaxEffectDuration
                || !float.IsFinite(effect.EffectMult)
                || effect.EffectMult <= 0f || effect.EffectMult > 1f
                || effect.Generation < 0 || effect.Generation > 8) return false;
            HackNetworkTarget target;
            if (effect.Target is TileScannable tileTarget) {
                int tileX = tileTarget.TileCoordX;
                int tileY = tileTarget.TileCoordY;
                if (tileX < 0 || tileX >= Main.maxTilesX
                    || tileY < 0 || tileY >= Main.maxTilesY) return false;
                target = new HackNetworkTarget(HackTargetKind.Tile,
                    default, tileX, tileY);
            }
            else if (!HackNetworkTarget.TryCapture(effect.Target, out target)) {
                return false;
            }
            record = new HackEffectSnapshotRecord(effect.ActivationId,
                effect.CasterIndex, effect.SessionId, effect.RequestId,
                effect.Hack.SlotIndex, effect.Elapsed, effect.EffectMult,
                effect.Generation, target);
            return true;
        }

        private static void WriteQueueRecord(BinaryWriter writer,
            in HackQueueSnapshotRecord record) {
            writer.Write((byte)record.PlayerIndex);
            writer.Write(record.SessionId);
            writer.Write(record.RequestId);
            writer.Write((ushort)record.SlotIndex);
            writer.Write((byte)record.State);
            writer.Write(record.Elapsed);
            writer.Write(record.UploadFrames);
            writer.Write(record.ActivationId);
            WriteTarget(writer, record.Target);
        }

        private static bool TryReadQueueRecord(BinaryReader reader,
            out HackQueueSnapshotRecord record) {
            record = default;
            int playerIndex = reader.ReadByte();
            uint sessionId = reader.ReadUInt32();
            uint requestId = reader.ReadUInt32();
            int slotIndex = reader.ReadUInt16();
            HackQueueState state = (HackQueueState)reader.ReadByte();
            int elapsed = reader.ReadInt32();
            int uploadFrames = reader.ReadInt32();
            long activationId = reader.ReadInt64();
            if (!TryReadTarget(reader, out HackNetworkTarget target)) return false;
            if (!IsValidPlayerIndex(playerIndex) || sessionId == 0
                || requestId == 0 || slotIndex < 0
                || slotIndex >= QuickHackDef.Count
                || (int)state < (int)HackQueueState.Waiting
                || (int)state > (int)HackQueueState.Completed
                || elapsed < 0 || elapsed > MaxUploadFrames
                || uploadFrames <= 0 || uploadFrames > MaxUploadFrames
                || elapsed > uploadFrames || activationId < 0) return false;
            if (target.Kind == HackTargetKind.SelfRig) {
                //自体目标零负载，玩家索引按记录归属回填
                target = target with { SelfPlayerIndex = playerIndex };
            }
            record = new HackQueueSnapshotRecord(playerIndex, sessionId,
                requestId, slotIndex, state, elapsed, uploadFrames,
                activationId, target);
            return true;
        }

        private static void WriteEffectRecord(BinaryWriter writer,
            in HackEffectSnapshotRecord record) {
            writer.Write(record.ActivationId);
            writer.Write((byte)record.CasterIndex);
            writer.Write(record.SessionId);
            writer.Write(record.RequestId);
            writer.Write((ushort)record.SlotIndex);
            writer.Write(record.Elapsed);
            writer.Write(record.EffectMult);
            writer.Write((byte)record.Generation);
            WriteTarget(writer, record.Target);
        }

        private static bool TryReadEffectRecord(BinaryReader reader,
            out HackEffectSnapshotRecord record) {
            record = default;
            long activationId = reader.ReadInt64();
            int casterIndex = reader.ReadByte();
            uint sessionId = reader.ReadUInt32();
            uint requestId = reader.ReadUInt32();
            int slotIndex = reader.ReadUInt16();
            int elapsed = reader.ReadInt32();
            float effectMult = reader.ReadSingle();
            int generation = reader.ReadByte();
            if (!TryReadTarget(reader, out HackNetworkTarget target)) return false;
            if (activationId <= 0 || !IsValidPlayerIndex(casterIndex)
                || slotIndex < 0 || slotIndex >= QuickHackDef.Count
                || elapsed < 0 || elapsed > HackEffectTracker.MaxEffectDuration
                || !float.IsFinite(effectMult) || effectMult <= 0f
                || effectMult > 1f || generation < 0 || generation > 8)
                return false;
            if (target.Kind == HackTargetKind.SelfRig) {
                //自体目标零负载，玩家索引按效果施术者回填
                target = target with { SelfPlayerIndex = casterIndex };
            }
            record = new HackEffectSnapshotRecord(activationId, casterIndex,
                sessionId, requestId, slotIndex, elapsed, effectMult,
                generation, target);
            return true;
        }

        /// <summary>
        /// 目标身份的线上格式：两字节 kind + 该 kind 自己的定长负载。<br/>
        /// 加新 kind 时读写两侧必须同时改，且读侧要先把负载吃干净再校验
        /// </summary>
        private static void WriteTarget(BinaryWriter writer,
            in HackNetworkTarget target) {
            //kind 是 ushort：BossPart=256/SelfRig=512/Container=1024/World=2048
            //都超出 byte 表达范围，(byte)256 == 0 会静默变成 None
            writer.Write((ushort)target.Kind);
            if (target.Kind == HackTargetKind.Npc) {
                target.NpcIdentity.Write(writer);
            }
            else if (target.Kind == HackTargetKind.BossPart) {
                //部件负载与 Npc 完全同款
                target.NpcIdentity.Write(writer);
            }
            else if (target.Kind is HackTargetKind.Tile or HackTargetKind.Water
                or HackTargetKind.Container) {
                //容器身份 = 箱子锚点格，复用格子座标负载
                writer.Write(target.TileX);
                writer.Write(target.TileY);
            }
            else if (target.Kind == HackTargetKind.Projectile) {
                target.ProjIdentity.Write(writer);
            }
            else if (target.Kind == HackTargetKind.Item) {
                writer.Write((short)target.ItemIndex);
                writer.Write(target.ItemType);
            }
            else if (target.Kind is HackTargetKind.Turret or HackTargetKind.SignalTower) {
                target.ActorKey.Write(writer);
            }
            else if (target.Kind == HackTargetKind.Player) {
                writer.Write((byte)target.PlayerIndex);
            }
            //SelfRig 与 World 零负载：kind 本身即身份，不写任何字节
        }

        private static bool TryReadTarget(BinaryReader reader,
            out HackNetworkTarget target) {
            target = default;
            HackTargetKind kind = (HackTargetKind)reader.ReadUInt16();
            if (kind == HackTargetKind.Npc) {
                if (!NetworkNPCIdentity.TryRead(reader,
                    out NetworkNPCIdentity identity)) return false;
                target = new HackNetworkTarget(kind, identity, -1, -1);
                return true;
            }
            if (kind == HackTargetKind.BossPart) {
                //定长负载一次读完，无错位风险
                if (!NetworkNPCIdentity.TryRead(reader,
                    out NetworkNPCIdentity partIdentity)) return false;
                target = new HackNetworkTarget(kind, partIdentity, -1, -1);
                return true;
            }
            if (kind is HackTargetKind.Tile or HackTargetKind.Water
                or HackTargetKind.Container) {
                //先读满两项再判界，提前 return 会把字节留在共用的流里
                int tileX = reader.ReadInt32();
                int tileY = reader.ReadInt32();
                if (tileX < 0 || tileX >= Main.maxTilesX
                    || tileY < 0 || tileY >= Main.maxTilesY) return false;
                target = new HackNetworkTarget(kind, default, tileX, tileY);
                return true;
            }
            if (kind == HackTargetKind.Projectile) {
                if (!NetworkProjectileIdentity.TryRead(reader,
                    out NetworkProjectileIdentity projIdentity)) return false;
                target = new HackNetworkTarget(kind, default, -1, -1, projIdentity);
                return true;
            }
            if (kind == HackTargetKind.Item) {
                //先读满两项再判，提前 return 会把剩下的字节留在共用的流里
                int itemIndex = reader.ReadInt16();
                int itemType = reader.ReadInt32();
                if (itemIndex < 0 || itemIndex >= Main.maxItems
                    || itemType <= ItemID.None
                    || itemType >= ItemLoader.ItemCount) return false;
                target = new HackNetworkTarget(kind, default, -1, -1,
                    default, itemIndex, itemType);
                return true;
            }
            if (kind == HackTargetKind.SelfRig) {
                //自体目标零负载：线上只有 kind，玩家索引由收包方按请求/记录上下文回填
                target = new HackNetworkTarget(kind, default, -1, -1);
                return true;
            }
            if (kind == HackTargetKind.World) {
                //零负载：没有要吃的字节，锚点是不是天空交给 TryResolve
                target = new HackNetworkTarget(kind, default, -1, -1);
                return true;
            }
            if (kind is HackTargetKind.Turret or HackTargetKind.SignalTower) {
                //定长 10 字节负载，TryRead 内部先吃干净再按 IsValid 校验
                if (!CircuitActorKey.TryRead(reader, out CircuitActorKey actorKey)) return false;
                target = new HackNetworkTarget(kind, default, -1, -1,
                    ActorKey: actorKey);
                return true;
            }
            if (kind == HackTargetKind.Player) {
                //先读满 1 字节再校验（共用流纪律）
                int playerIndex = reader.ReadByte();
                if (playerIndex >= Main.maxPlayers) return false;
                target = new HackNetworkTarget(kind, default, -1, -1,
                    PlayerIndex: playerIndex);
                return true;
            }
            return false;
        }

        private static ModPacket NewPacket(HackNetOperation operation) {
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.HackProtocolApply);
            packet.Write((byte)operation);
            return packet;
        }

        /// <summary>
        /// 仅清理静态同步态。玩家上传队列由 <see cref="HackTimeAuthorityPlayer"/>
        /// 在 Initialize / PlayerDisconnect 中自清，勿在 OnWorldUnload 等时机
        /// 遍历 GetModPlayer（此时 modPlayers 可能已失效）。
        /// </summary>
        internal static void Reset() {
            nextActivationId = 0;
            lastAuthorityUpdateFrame = ulong.MaxValue;
            pendingProgress.Clear();
            TimeControlReplicationSystem.CancelAll<HackQueuePendingSource>();
            TimeControlReplicationSystem.CancelAll<HackEffectPendingSource>();
        }

        private static bool IsValidPlayerIndex(int index)
            => index >= 0 && index < Main.maxPlayers;

        private static bool IsFiniteVector(Vector2 value)
            => float.IsFinite(value.X) && float.IsFinite(value.Y);
    }
}
