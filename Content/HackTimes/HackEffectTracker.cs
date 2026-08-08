using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes.Scannables;
using CalamityOverhaul.Content.RAMSystems;
using CalamityOverhaul.Content.TimeFreezes;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>单个骇入效果运行时实例</summary>
    internal sealed class ActiveHackEffect
    {
        public long ActivationId;
        public QuickHackDef Hack;
        public IHackTarget Target;
        public NetworkNPCIdentity NpcIdentity;
        public int CasterIndex;
        public uint SessionId;
        public uint RequestId;
        public float PaidRamCost;
        public int Elapsed;
        public bool Active = true;
        public bool Applied;
        public bool Replicated;
        public bool RefundSettled;
        public float EffectMult = 1f;
        public int Generation;

        public int TargetIndex => Target is NpcScannable n ? n.NpcIndex : -1;
        public int TileX => Target is TileScannable t ? t.TileCoordX : -1;
        public int TileY => Target is TileScannable t ? t.TileCoordY : -1;

        public int EffectiveDuration {
            get {
                int duration = Hack?.GetDuration() ?? 0;
                if (duration <= 0) return 0;
                float multiplier = float.IsFinite(EffectMult)
                    ? MathHelper.Clamp(EffectMult, 0.1f, 1f)
                    : 1f;
                return Math.Clamp((int)(duration * multiplier), 1,
                    HackEffectTracker.MaxEffectDuration);
            }
        }
    }

    /// <summary>骇入效果权威与复制追踪器</summary>
    internal sealed class HackEffectTracker : ICWRLoader
    {
        internal const int MaxEffectDuration = 60 * 60 * 10;
        private const float KillRefundRatio = 0.5f;
        private const int MaxTombstones = 512;

        private static readonly List<ActiveHackEffect> activeEffects = [];
        private static readonly List<ActiveHackEffect> activeTileEffects = [];
        private static readonly List<ActiveHackEffect> removeBuffer = [];
        private static readonly List<ActiveHackEffect> pendingEffects = [];
        private static readonly List<NPC> groupBuffer = [];
        private static readonly HashSet<long> replicatedTombstones = [];
        private static readonly Queue<long> replicatedTombstoneOrder = [];
        private static bool updatingEffects;
        private static ulong lastUpdateFrame = ulong.MaxValue;
        //时停/时缓闸门的跨帧累加器
        private static float timeScaleCarry;

        void ICWRLoader.UnLoadData() => Reset();

        public static ActiveHackEffect ApplyNpcEffect(QuickHackDef hack,
            int targetIndex, int casterIndex) {
            if (Main.netMode == NetmodeID.MultiplayerClient || hack == null
                || targetIndex < 0 || targetIndex >= Main.maxNPCs) {
                return null;
            }
            NPC npc = Main.npc[targetIndex];
            if (!NetworkNPCIdentity.TryCapture(npc,
                out NetworkNPCIdentity identity)) {
                return null;
            }
            Player caster = ResolvePlayer(casterIndex);
            var target = new NpcScannable(targetIndex);
            if (caster == null || !hack.CanApplyTo(target, caster)) return null;

            return AddAuthorityEffect(hack, target, identity, casterIndex,
                0, 0, 0f, 0, npc.boss ? 0.5f : 1f, 0);
        }

        public static ActiveHackEffect Apply(QuickHackDef hack, int targetIndex,
            int casterIndex) => ApplyNpcEffect(hack, targetIndex, casterIndex);

        public static ActiveHackEffect ApplyTileEffect(QuickHackDef hack, int tileX,
            int tileY, int casterIndex) {
            if (Main.netMode == NetmodeID.MultiplayerClient || hack == null
                || !IsValidTile(tileX, tileY)) {
                return null;
            }
            Player caster = ResolvePlayer(casterIndex);
            var target = new TileScannable(tileX, tileY);
            if (caster == null || !hack.CanApplyTo(target, caster)) return null;

            return AddAuthorityEffect(hack, target, default, casterIndex,
                0, 0, 0f, 0, 1f, 0);
        }

        public static ActiveHackEffect ApplyToTile(QuickHackDef hack, int tileX,
            int tileY, int casterIndex)
            => ApplyTileEffect(hack, tileX, tileY, casterIndex);

        internal static ActiveHackEffect ApplyAuthorityEffect(QuickHackDef hack,
            IHackTarget target, int casterIndex, uint sessionId, uint requestId,
            float paidRamCost, long activationId) {
            if (Main.netMode == NetmodeID.MultiplayerClient || hack == null
                || target == null || !target.IsValid
                || !float.IsFinite(paidRamCost) || paidRamCost < 0f
                || paidRamCost > RamSystem.MaxMutationAmount) {
                return null;
            }

            Player caster = ResolvePlayer(casterIndex);
            if (caster == null || !hack.CanApplyTo(target, caster)) return null;

            NetworkNPCIdentity identity = default;
            float effectMult = 1f;
            if (target is NpcScannable npcTarget) {
                NPC npc = Main.npc[npcTarget.NpcIndex];
                if (!NetworkNPCIdentity.TryCapture(npc, out identity)) return null;
                effectMult = npc.boss ? 0.5f : 1f;
            }
            else if (target is TileScannable tileTarget) {
                if (!IsValidTile(tileTarget.TileCoordX, tileTarget.TileCoordY)) return null;
            }
            else if (Main.netMode == NetmodeID.SinglePlayer
                && target is IHackableTurret or IHackableSignalTower) {
            }
            else {
                return null;
            }

            return AddAuthorityEffect(hack, target, identity, casterIndex,
                sessionId, requestId, paidRamCost, activationId, effectMult, 0);
        }

        private static ActiveHackEffect AddAuthorityEffect(QuickHackDef hack,
            IHackTarget target, NetworkNPCIdentity identity, int casterIndex,
            uint sessionId, uint requestId, float paidRamCost, long activationId,
            float effectMult, int generation) {
            if (activationId == 0) {
                activationId = HackTimeNetSync.AllocateActivationId();
            }
            if (activationId <= 0 || FindEffect(activationId) != null) return null;

            var effect = new ActiveHackEffect {
                ActivationId = activationId,
                Hack = hack,
                Target = target,
                NpcIdentity = identity,
                CasterIndex = casterIndex,
                SessionId = sessionId,
                RequestId = requestId,
                PaidRamCost = paidRamCost,
                Elapsed = 0,
                Active = true,
                Applied = false,
                Replicated = false,
                EffectMult = float.IsFinite(effectMult)
                    ? MathHelper.Clamp(effectMult, 0.1f, 1f)
                    : 1f,
                Generation = Math.Clamp(generation, 0, 8),
            };
            AddEffect(effect);
            return effect;
        }

        internal static bool ApplyReplicatedEffect(long activationId,
            QuickHackDef hack, IHackTarget target, NetworkNPCIdentity npcIdentity,
            int casterIndex, uint sessionId, uint requestId, int elapsed,
            float effectMult, int generation) {
            bool validInstantTile = hack?.GetDuration() == 0
                && target is TileScannable tileTarget
                && tileTarget.TileCoordX >= 0
                && tileTarget.TileCoordX < Main.maxTilesX
                && tileTarget.TileCoordY >= 0
                && tileTarget.TileCoordY < Main.maxTilesY;
            if (Main.netMode != NetmodeID.MultiplayerClient || activationId <= 0
                || hack == null || target == null
                || !target.IsValid && !validInstantTile
                || casterIndex < 0 || casterIndex >= Main.maxPlayers
                || elapsed < 0 || elapsed > MaxEffectDuration
                || !float.IsFinite(effectMult) || effectMult <= 0f
                || effectMult > 1f || generation < 0 || generation > 8
                || replicatedTombstones.Contains(activationId)) {
                return false;
            }

            ActiveHackEffect existing = FindEffect(activationId);
            if (existing != null) {
                if (!existing.Replicated || existing.Hack.SlotIndex != hack.SlotIndex
                    || existing.CasterIndex != casterIndex
                    || !existing.Target.TargetEquals(target)) {
                    return false;
                }
                existing.Elapsed = Math.Max(existing.Elapsed, elapsed);
                existing.Hack.OnReplicatedTick(existing.Target, existing.Elapsed);
                return true;
            }

            var effect = new ActiveHackEffect {
                ActivationId = activationId,
                Hack = hack,
                Target = target,
                NpcIdentity = npcIdentity,
                CasterIndex = casterIndex,
                SessionId = sessionId,
                RequestId = requestId,
                PaidRamCost = 0f,
                Elapsed = elapsed,
                Active = true,
                Applied = true,
                Replicated = true,
                EffectMult = effectMult,
                Generation = generation,
            };
            AddEffect(effect);
            hack.OnReplicatedApply(target, elapsed);
            if (effect.EffectiveDuration > 0) {
                hack.OnReplicatedTick(target, elapsed);
            }
            return true;
        }

        internal static bool ApplyReplicatedProgress(long activationId, int elapsed) {
            if (Main.netMode != NetmodeID.MultiplayerClient || activationId <= 0
                || elapsed < 0 || elapsed > MaxEffectDuration) {
                return false;
            }
            ActiveHackEffect effect = FindEffect(activationId);
            if (effect == null || !effect.Active || !effect.Replicated) return false;
            effect.Elapsed = Math.Max(effect.Elapsed, elapsed);
            effect.Hack.OnReplicatedTick(effect.Target, effect.Elapsed);
            return true;
        }

        internal static bool RemoveReplicatedEffect(long activationId) {
            if (Main.netMode != NetmodeID.MultiplayerClient || activationId <= 0)
                return false;
            AddTombstone(activationId);
            ActiveHackEffect effect = FindEffect(activationId);
            if (effect == null || !effect.Replicated) return true;
            if (effect.Active && effect.Target?.IsValid == true) {
                effect.Hack.OnReplicatedRemove(effect.Target);
            }
            effect.Active = false;
            RemoveEffect(effect);
            return true;
        }

        internal static bool RemoveAuthorityEffect(long activationId,
            bool invokeRemove = true) {
            if (Main.netMode == NetmodeID.MultiplayerClient || activationId <= 0)
                return false;
            ActiveHackEffect effect = FindEffect(activationId);
            if (effect == null || effect.Replicated) return false;
            EndAuthorityEffect(effect, invokeRemove);
            RemoveEffect(effect);
            return true;
        }

        public static void Update() {
            ulong frame = Main.GameUpdateCount;
            if (lastUpdateFrame == frame) return;
            lastUpdateFrame = frame;

            //时停/时缓统一闸门：本帧无推进量则整表冻结，
            //不 OnApply、不 OnTick、不推进计时、不结算到期。
            //上传队列刻意不冻结，冻结中完成上传的效果挂起在此，
            //解冻后第一个推进帧统一结算
            if (TimeGear.PullFrameAdvance(ref timeScaleCarry) <= 0) {
                return;
            }

            if (Main.netMode == NetmodeID.MultiplayerClient) {
                UpdateReplicatedList(activeEffects);
                UpdateReplicatedList(activeTileEffects);
                return;
            }
            UpdateAuthorityList(activeEffects);
            UpdateAuthorityList(activeTileEffects);
        }

        public static void UpdateTileEffects() {
            //统一由 Update 在同一现实帧推进
        }

        private static void UpdateAuthorityList(List<ActiveHackEffect> effects) {
            removeBuffer.Clear();
            updatingEffects = true;
            for (int i = 0; i < effects.Count; i++) {
                ActiveHackEffect effect = effects[i];
                if (!effect.Active || effect.Replicated) {
                    removeBuffer.Add(effect);
                    continue;
                }
                if (!TryResolveTarget(effect, out NPC deadNpc)) {
                    if (deadNpc != null && deadNpc.life <= 0) {
                        RefundKilledEffect(effect, deadNpc);
                    }
                    EndAuthorityEffect(effect, effect.Applied);
                    removeBuffer.Add(effect);
                    continue;
                }

                Player caster = ResolvePlayer(effect.CasterIndex);
                if (caster == null) {
                    EndAuthorityEffect(effect, effect.Applied);
                    removeBuffer.Add(effect);
                    continue;
                }

                if (!effect.Applied) {
                    effect.Applied = true;
                    if (!effect.Hack.OnApply(effect.Target, caster)) {
                        EndAuthorityEffect(effect, false);
                        removeBuffer.Add(effect);
                        continue;
                    }
                    HackTimeNetSync.BroadcastEffectApply(effect);
                }

                int duration = effect.EffectiveDuration;
                if (duration == 0) {
                    EndAuthorityEffect(effect, false);
                    removeBuffer.Add(effect);
                    continue;
                }
                if (effect.Elapsed >= duration) {
                    EndAuthorityEffect(effect, true);
                    removeBuffer.Add(effect);
                    continue;
                }

                if (!effect.Hack.OnTick(effect.Target, effect.Elapsed)) {
                    EndAuthorityEffect(effect, true);
                    removeBuffer.Add(effect);
                    continue;
                }

                effect.Elapsed++;
                if (Main.netMode == NetmodeID.Server && effect.Elapsed % 15 == 0) {
                    HackTimeNetSync.BroadcastEffectProgress(effect);
                }
            }
            updatingEffects = false;

            for (int i = 0; i < removeBuffer.Count; i++) {
                effects.Remove(removeBuffer[i]);
            }
            if (pendingEffects.Count > 0) {
                for (int i = pendingEffects.Count - 1; i >= 0; i--) {
                    ActiveHackEffect effect = pendingEffects[i];
                    if (IsNpcEffect(effect) == ReferenceEquals(effects, activeEffects)) {
                        effects.Add(effect);
                        pendingEffects.RemoveAt(i);
                    }
                }
            }
            removeBuffer.Clear();
        }

        private static void UpdateReplicatedList(List<ActiveHackEffect> effects) {
            removeBuffer.Clear();
            for (int i = 0; i < effects.Count; i++) {
                ActiveHackEffect effect = effects[i];
                if (!effect.Active || !effect.Replicated
                    || !TryResolveTarget(effect, out _)) {
                    if (effect.Active && effect.Target?.IsValid == true)
                        effect.Hack.OnReplicatedRemove(effect.Target);
                    effect.Active = false;
                    AddTombstone(effect.ActivationId);
                    removeBuffer.Add(effect);
                    continue;
                }
                int duration = effect.EffectiveDuration;
                if (duration > 0 && effect.Elapsed < duration) effect.Elapsed++;
                if (duration > 0) {
                    effect.Hack.OnReplicatedTick(effect.Target, effect.Elapsed);
                }
            }
            for (int i = 0; i < removeBuffer.Count; i++) {
                effects.Remove(removeBuffer[i]);
            }
            removeBuffer.Clear();
        }

        private static void EndAuthorityEffect(ActiveHackEffect effect,
            bool invokeRemove) {
            if (!effect.Active) return;
            if (invokeRemove && effect.Target?.IsValid == true) {
                effect.Hack.OnRemove(effect.Target);
            }
            effect.Active = false;
            if (Main.netMode == NetmodeID.Server) {
                HackTimeNetSync.BroadcastEffectRemove(effect.ActivationId);
            }
        }

        private static bool TryResolveTarget(ActiveHackEffect effect,
            out NPC lastNpc) {
            lastNpc = null;
            if (effect.Target is NpcScannable npcTarget) {
                if (npcTarget.NpcIndex >= 0 && npcTarget.NpcIndex < Main.maxNPCs)
                    lastNpc = Main.npc[npcTarget.NpcIndex];
                if (effect.NpcIdentity.IsValid) {
                    if (!effect.NpcIdentity.TryResolve(out NPC resolved)) return false;
                    lastNpc = resolved;
                    return resolved.life > 0;
                }
                return lastNpc?.active == true && lastNpc.life > 0;
            }
            if (effect.Target is TileScannable tileTarget) {
                return IsValidTile(tileTarget.TileCoordX, tileTarget.TileCoordY);
            }
            if (Main.netMode == NetmodeID.SinglePlayer
                && effect.Target is IHackableTurret or IHackableSignalTower) {
                return effect.Target.IsValid;
            }
            return false;
        }

        private static void RefundKilledEffect(ActiveHackEffect effect, NPC target) {
            if (effect.RefundSettled || effect.PaidRamCost <= 0f
                || !float.IsFinite(effect.PaidRamCost)) {
                return;
            }
            effect.RefundSettled = true;
            Player caster = ResolvePlayer(effect.CasterIndex);
            if (caster == null) return;
            float refund = Math.Min(effect.PaidRamCost,
                Math.Max(1f, effect.PaidRamCost * KillRefundRatio));
            RamSystem.Restore(caster, refund, out _);
        }

        public static bool HasEffect<T>(int npcIndex) where T : QuickHackDef {
            return FindNpcEffect<T>(npcIndex) != null;
        }

        public static float GetEffectProgress<T>(int npcIndex) where T : QuickHackDef {
            ActiveHackEffect effect = FindNpcEffect<T>(npcIndex);
            if (effect == null) return -1f;
            int duration = effect.EffectiveDuration;
            return duration <= 0 ? 1f
                : MathHelper.Clamp(effect.Elapsed / (float)duration, 0f, 1f);
        }

        public static void GetEffects(int npcIndex,
            List<ActiveHackEffect> result) {
            result.Clear();
            AddNpcEffects(activeEffects, npcIndex, result);
            AddNpcEffects(pendingEffects, npcIndex, result);
        }

        public static ActiveHackEffect GetEffect<T>(int npcIndex)
            where T : QuickHackDef => FindNpcEffect<T>(npcIndex);

        public static IReadOnlyList<ActiveHackEffect> AllActiveEffects
            => activeEffects;

        public static void PropagateNpcEffectToGroup<T>(T hack, int rootNpcIndex,
            int casterIndex, Action<NPC> onSpread = null) where T : QuickHackDef {
            if (Main.netMode == NetmodeID.MultiplayerClient || hack == null
                || rootNpcIndex < 0 || rootNpcIndex >= Main.maxNPCs) return;
            NPC root = Main.npc[rootNpcIndex];
            if (root?.active != true) return;

            NpcGroupHelper.CollectGroup(root, groupBuffer);
            for (int i = 0; i < groupBuffer.Count; i++) {
                NPC member = groupBuffer[i];
                if (member.whoAmI == rootNpcIndex
                    || HasEffect<T>(member.whoAmI)) continue;
                ActiveHackEffect spread = ApplyNpcEffect(hack, member.whoAmI,
                    casterIndex);
                if (spread == null) continue;
                onSpread?.Invoke(member);
            }
            groupBuffer.Clear();
        }

        public static bool HasTileEffect<T>(int tileX, int tileY)
            where T : QuickHackDef {
            for (int i = 0; i < activeTileEffects.Count; i++) {
                ActiveHackEffect effect = activeTileEffects[i];
                if (effect.Active && effect.Hack is T
                    && effect.TileX == tileX && effect.TileY == tileY) return true;
            }
            return false;
        }

        public static void GetTileEffects(int tileX, int tileY,
            List<ActiveHackEffect> result) {
            result.Clear();
            for (int i = 0; i < activeTileEffects.Count; i++) {
                ActiveHackEffect effect = activeTileEffects[i];
                if (effect.Active && effect.TileX == tileX
                    && effect.TileY == tileY) result.Add(effect);
            }
        }

        public static IReadOnlyList<ActiveHackEffect> AllActiveTileEffects
            => activeTileEffects;

        internal static Player ResolveEffectCaster(QuickHackDef hack,
            IHackTarget target) {
            if (hack == null || target == null) return null;
            ActiveHackEffect effect = FindMatchingEffect(activeEffects, hack, target)
                ?? FindMatchingEffect(activeTileEffects, hack, target);
            return effect == null ? null : ResolvePlayer(effect.CasterIndex);
        }

        internal static ActiveHackEffect FindEffect(long activationId) {
            ActiveHackEffect effect = FindByActivation(activeEffects, activationId)
                ?? FindByActivation(activeTileEffects, activationId);
            if (effect != null) return effect;
            return FindByActivation(pendingEffects, activationId);
        }

        internal static void BeginReplicatedSnapshot() {
            if (Main.netMode != NetmodeID.MultiplayerClient) return;
            ClearReplicatedList(activeEffects);
            ClearReplicatedList(activeTileEffects);
            replicatedTombstones.Clear();
            replicatedTombstoneOrder.Clear();
        }

        public static void Reset() {
            ClearAllList(activeEffects);
            ClearAllList(activeTileEffects);
            pendingEffects.Clear();
            removeBuffer.Clear();
            groupBuffer.Clear();
            replicatedTombstones.Clear();
            replicatedTombstoneOrder.Clear();
            updatingEffects = false;
            lastUpdateFrame = ulong.MaxValue;
            timeScaleCarry = 0f;
        }

        private static void AddEffect(ActiveHackEffect effect) {
            if (updatingEffects) {
                pendingEffects.Add(effect);
                return;
            }
            (IsNpcEffect(effect) ? activeEffects : activeTileEffects).Add(effect);
        }

        private static void RemoveEffect(ActiveHackEffect effect) {
            activeEffects.Remove(effect);
            activeTileEffects.Remove(effect);
            pendingEffects.Remove(effect);
        }

        private static bool IsNpcEffect(ActiveHackEffect effect)
            => effect.Target is NpcScannable;

        private static bool IsValidTile(int tileX, int tileY) {
            return tileX >= 0 && tileX < Main.maxTilesX
                && tileY >= 0 && tileY < Main.maxTilesY
                && Main.tile[tileX, tileY].HasTile;
        }

        private static Player ResolvePlayer(int index) {
            if (index < 0 || index >= Main.maxPlayers) return null;
            Player player = Main.player[index];
            return player?.active == true && !player.dead ? player : null;
        }

        private static ActiveHackEffect FindNpcEffect<T>(int npcIndex)
            where T : QuickHackDef {
            for (int i = 0; i < activeEffects.Count; i++) {
                ActiveHackEffect effect = activeEffects[i];
                if (effect.Active && effect.Hack is T
                    && effect.TargetIndex == npcIndex) return effect;
            }
            for (int i = 0; i < pendingEffects.Count; i++) {
                ActiveHackEffect effect = pendingEffects[i];
                if (effect.Active && effect.Hack is T
                    && effect.TargetIndex == npcIndex) return effect;
            }
            return null;
        }

        private static void AddNpcEffects(List<ActiveHackEffect> source,
            int npcIndex, List<ActiveHackEffect> result) {
            for (int i = 0; i < source.Count; i++) {
                ActiveHackEffect effect = source[i];
                if (effect.Active && effect.TargetIndex == npcIndex)
                    result.Add(effect);
            }
        }

        private static ActiveHackEffect FindByActivation(
            List<ActiveHackEffect> source, long activationId) {
            if (activationId <= 0) return null;
            for (int i = 0; i < source.Count; i++) {
                if (source[i].ActivationId == activationId) return source[i];
            }
            return null;
        }

        private static ActiveHackEffect FindMatchingEffect(
            List<ActiveHackEffect> source, QuickHackDef hack, IHackTarget target) {
            for (int i = 0; i < source.Count; i++) {
                ActiveHackEffect effect = source[i];
                if (effect.Active && effect.Hack == hack
                    && effect.Target?.TargetEquals(target) == true) return effect;
            }
            return null;
        }

        private static void AddTombstone(long activationId) {
            if (activationId <= 0 || !replicatedTombstones.Add(activationId)) return;
            replicatedTombstoneOrder.Enqueue(activationId);
            while (replicatedTombstones.Count > MaxTombstones
                && replicatedTombstoneOrder.TryDequeue(out long expired)) {
                replicatedTombstones.Remove(expired);
            }
        }

        private static void ClearReplicatedList(List<ActiveHackEffect> effects) {
            for (int i = effects.Count - 1; i >= 0; i--) {
                ActiveHackEffect effect = effects[i];
                if (!effect.Replicated) continue;
                if (effect.Active && effect.Target?.IsValid == true)
                    effect.Hack.OnReplicatedRemove(effect.Target);
                effects.RemoveAt(i);
            }
        }

        private static void ClearAllList(List<ActiveHackEffect> effects) {
            for (int i = 0; i < effects.Count; i++) {
                ActiveHackEffect effect = effects[i];
                if (!effect.Active || effect.Target?.IsValid != true) continue;
                if (effect.Replicated)
                    effect.Hack.OnReplicatedRemove(effect.Target);
                else if (effect.Applied)
                    effect.Hack.OnRemove(effect.Target);
            }
            effects.Clear();
        }
    }
}
