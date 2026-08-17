using CalamityOverhaul.Content.Players;
using CalamityOverhaul.Content.Wraiths.Core;
using CalamityOverhaul.Content.Wraiths.Deaths;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Wraiths.Runtime
{
    internal sealed class WraithPlayer : ModPlayer
    {
        internal const string ScapeGhostKey = "ScapeGhost";
        internal const string HeadlessShadeKey = "HeadlessShade";
        internal const string GhostHandKey = "GhostHand";
        internal const string LanternBoyKey = "LanternBoy";
        internal const string CrimsonBrideKey = "CrimsonBride";
        internal const string GhostRainKey = "GhostRain";

        private const int SchemaVersion = 3;
        private const string SaveKey = "OnikiriWraithLoadout";
        /// <summary>结印槽位数：内三角三位</summary>
        internal const int SlotCount = 3;
        /// <summary>互相催醒：一只鬼涨槽时，同场其他鬼按这个比例跟着醒</summary>
        private const float CrossWakeFactor = 0.35f;
        /// <summary>替死泄压：替身死一次，同场其他鬼各降这么多复苏</summary>
        private const float ScapeRelief = 0.15f;
        private const float ErosionDecayPerTick = 1f / (60f * 240f);
        private const int ErosionDecayDelay = 60 * 6;
        //持刀怠速衰减：该鬼 6 秒未涨复苏后开始，满→零约 240 秒
        internal const int HeldIdleDelayTicks = 60 * 6;
        private const float HeldDecayPerTick = 1f / (60f * 240f);
        //休息衰减：役鬼位空或未持鬼切持续 3 秒后开始，满→零约 48 秒
        internal const int RestDelayTicks = 60 * 3;
        private const float RestDecayPerTick = 1f / (60f * 48f);
        private const int ResourceSyncInterval = 15;

        public const float TierCrawl = 0.35f;
        public const float TierStain = 0.70f;
        public const float TierMirror = 0.95f;

        //复苏低语阈值：初动 / 将醒 / 临界
        public const float RevivalStirLine = 0.50f;
        public const float RevivalRiseLine = 0.80f;
        public const float RevivalBrinkLine = 0.95f;
        //复苏危险区：HUD 常显与危态反馈从这里开始
        public const float RevivalDangerLine = 0.70f;

        internal static readonly string[] UsableKeys = [
            ScapeGhostKey,
            HeadlessShadeKey,
            GhostHandKey,
            LanternBoyKey,
            CrimsonBrideKey,
            GhostRainKey,
        ];

        private sealed class RevivalState
        {
            internal float Value;
            internal int IdleTicks = int.MaxValue / 2;
        }

        private readonly Dictionary<string, RevivalState> revival = [];
        private readonly Dictionary<string, int> lastRevivalCueTiers = [];
        private readonly string[] equipped = new string[SlotCount];
        private float erosion;
        private int scapeMultiplier = 2;
        private int restTicks;
        private int erosionIdleTicks;
        private int revivalChangedTicks;
        private int resourceSyncTicks;
        private int lastCueTier;
        private bool resourceDirty;
        private bool sessionInitialized;

        internal uint LoadoutRevision { get; private set; }
        internal uint ResourceRevision { get; private set; }
        internal bool SessionInitialized => sessionInitialized;
        public float Erosion => erosion;
        public int RevivalChangedTimer => revivalChangedTicks;
        public int ScapeMultiplier => scapeMultiplier;
        public int ErosionTier => erosion >= TierMirror ? 3
            : erosion >= TierStain ? 2 : erosion >= TierCrawl ? 1 : 0;

        /// <summary>该槽的役鬼 Key；空槽为空串。</summary>
        internal string SlotKey(int slot)
            => slot >= 0 && slot < SlotCount ? equipped[slot] ?? string.Empty : string.Empty;

        /// <summary>该鬼是否在任一结印槽中。</summary>
        internal bool IsEquipped(string key) => SlotOf(key) >= 0;

        /// <summary>该鬼所在槽号；不在槽中返回 -1。</summary>
        internal int SlotOf(string key) {
            if (string.IsNullOrEmpty(key)) {
                return -1;
            }
            for (int i = 0; i < SlotCount; i++) {
                if (equipped[i] == key) {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>在场役鬼数（非空槽计数）。</summary>
        internal int EquippedCount {
            get {
                int count = 0;
                for (int i = 0; i < SlotCount; i++) {
                    if (!string.IsNullOrEmpty(equipped[i])) {
                        count++;
                    }
                }
                return count;
            }
        }

        /// <summary>在场役鬼 Key（按槽序，跳过空槽）。</summary>
        internal IEnumerable<string> EquippedKeys {
            get {
                for (int i = 0; i < SlotCount; i++) {
                    if (!string.IsNullOrEmpty(equipped[i])) {
                        yield return equipped[i];
                    }
                }
            }
        }

        /// <summary>在场役鬼里最高的那条复苏；空盘为 0。HUD 与状态行读它。</summary>
        public float EquippedRevival => GetRevival(HighestRevivalKey);

        /// <summary>盘上离夺身最近的那只；空盘为空串。</summary>
        internal string HighestRevivalKey {
            get {
                string best = string.Empty;
                float max = -1f;
                for (int i = 0; i < SlotCount; i++) {
                    string key = equipped[i];
                    if (string.IsNullOrEmpty(key)) {
                        continue;
                    }
                    float value = GetRevival(key);
                    if (value > max) {
                        max = value;
                        best = key;
                    }
                }
                return best;
            }
        }

        public override void Initialize() => ResetState();

        private void ResetState() {
            revival.Clear();
            lastRevivalCueTiers.Clear();
            foreach (string key in UsableKeys) {
                revival[key] = new RevivalState();
                lastRevivalCueTiers[key] = 0;
            }
            Array.Fill(equipped, string.Empty);
            erosion = 0f;
            scapeMultiplier = 2;
            restTicks = 0;
            erosionIdleTicks = 0;
            revivalChangedTicks = int.MaxValue / 2;
            resourceSyncTicks = 0;
            lastCueTier = 0;
            resourceDirty = false;
            sessionInitialized = false;
            LoadoutRevision = 0;
            ResourceRevision = 0;
        }

        internal float GetRevival(string key)
            => key != null && revival.TryGetValue(key, out RevivalState state) ? state.Value : 0f;

        /// <summary>按当前值重置该鬼的低语阶，避免装备瞬间或衰减后补播。</summary>
        private void SyncCueTier(string key) {
            if (!string.IsNullOrEmpty(key) && revival.ContainsKey(key)) {
                lastRevivalCueTiers[key] = GetRevivalTier(GetRevival(key));
            }
        }

#if DEBUG
        /// <summary>调试：把该鬼复苏推到"再役使一次就满"，用于验收 HUD 预警态。</summary>
        internal void DebugPrimeRevival(string key) {
            if (Main.netMode == NetmodeID.MultiplayerClient
                || !revival.TryGetValue(key, out RevivalState state)
                || !WraithRegistry.TryGetUsable(key, out WraithDefinition definition)) {
                return;
            }
            state.Value = MathHelper.Clamp(1f - definition.RevivalCost * 0.5f, 0f, 0.999f);
            state.IdleTicks = 0;
            if (IsEquipped(key)) {
                revivalChangedTicks = 0;
            }
            SyncCueTier(key);
            MarkResourceChanged(immediate: true);
        }
#endif

        public static int GetRevivalTier(float value) => value >= RevivalBrinkLine ? 3
            : value >= RevivalRiseLine ? 2 : value >= RevivalStirLine ? 1 : 0;

        /// <summary>
        /// 权威端写一个结印槽。key 为空即卸下该槽；
        /// 同一只鬼不得占两槽，写入前先清它原来那格
        /// </summary>
        internal bool TrySetSlotAuthority(int slot, string key,
            uint expectedRevision = uint.MaxValue) {
            if (Main.netMode == NetmodeID.MultiplayerClient || !sessionInitialized
                || slot < 0 || slot >= SlotCount
                || expectedRevision != uint.MaxValue && expectedRevision != LoadoutRevision) {
                return false;
            }
            string next = string.IsNullOrEmpty(key) ? string.Empty : key;
            if (!string.IsNullOrEmpty(next) && !WraithRegistry.TryGetUsable(next, out _)) {
                return false;
            }
            int existing = SlotOf(next);
            if (existing == slot) {
                return true;
            }
            if (existing >= 0) {
                equipped[existing] = string.Empty;
            }
            equipped[slot] = next;
            restTicks = 0;
            SyncCueTier(next);
            LoadoutRevision++;
            if (Main.netMode == NetmodeID.Server) {
                WraithNet.SendStateSync(Player.whoAmI);
            }
            return true;
        }

        internal bool TryChargeAuthority(string key, float revivalGain, float erosionCost) {
            if (!TryChargeCore(key, revivalGain, erosionCost, out string seizeKey)) {
                return false;
            }
            MarkResourceChanged(immediate: true);
            if (seizeKey != null) {
                BeginRevivalSeizure(seizeKey);
            }
            return true;
        }

        internal bool TryCommitScapeAuthority(in WraithAbilityContext context,
            bool friendly, out bool revivalKilled) {
            revivalKilled = false;
            if (context.Player != Player || context.Definition?.Key != ScapeGhostKey
                || !TryChargeCore(ScapeGhostKey, context.Definition.RevivalCost,
                    context.Definition.ErosionCost, out string seizeKey)) {
                return false;
            }
            if (friendly) {
                scapeMultiplier = Math.Min(scapeMultiplier * 2, 32);
            }
            //替死泄压：替身把这一劫连带同场其他鬼一起骗过去
            RelieveCovenRevival(ScapeGhostKey);
            revivalKilled = seizeKey == ScapeGhostKey;
            MarkResourceChanged(immediate: true);
            if (seizeKey != null) {
                BeginRevivalSeizure(seizeKey);
            }
            return true;
        }

        /// <summary>
        /// 结算一次役使：推进主鬼复苏，并按互相催醒带动同场其他鬼。
        /// 一次结算可能同时推满多槽，全部清零，但只挑一只启动夺身——演出一次只能有一个
        /// </summary>
        private bool TryChargeCore(string key, float revivalGain, float erosionCost,
            out string seizeKey) {
            seizeKey = null;
            if (Main.netMode == NetmodeID.MultiplayerClient || !sessionInitialized
                || revivalGain <= 0f || !revival.ContainsKey(key)
                || WraithRevivalDeath.IsSeized(Player)) {
                return false;
            }

            PushRevival(key, revivalGain, ref seizeKey);
            //互相催醒：同场的鬼被吵醒，一起往夺身爬
            float crossGain = revivalGain * CrossWakeFactor;
            if (crossGain > 0f) {
                for (int i = 0; i < SlotCount; i++) {
                    string other = equipped[i];
                    if (!string.IsNullOrEmpty(other) && other != key) {
                        PushRevival(other, crossGain, ref seizeKey);
                    }
                }
            }
            //主鬼优先夺身；主鬼没满才轮到被催醒的那只
            if (GetRevival(key) >= 1f) {
                seizeKey = key;
            }
            AddErosionInternal(erosionCost);
            return true;
        }

        /// <summary>推一只鬼的复苏槽并处理低语；满格的记进 seizeKey 备选。</summary>
        private void PushRevival(string key, float gain, ref string seizeKey) {
            if (!revival.TryGetValue(key, out RevivalState state)) {
                return;
            }
            state.Value = MathHelper.Clamp(state.Value + gain, 0f, 1f);
            state.IdleTicks = 0;
            revivalChangedTicks = 0;
            int tier = GetRevivalTier(state.Value);
            lastRevivalCueTiers.TryGetValue(key, out int lastTier);
            if (tier > lastTier && Main.netMode != NetmodeID.Server
                && Player.whoAmI == Main.myPlayer) {
                PlayRevivalCue(tier);
            }
            lastRevivalCueTiers[key] = Math.Max(lastTier, tier);
            seizeKey ??= state.Value >= 1f ? key : null;
        }

        /// <summary>替死泄压：同场其他鬼各降一档复苏，给多鬼一个正向反馈。</summary>
        private void RelieveCovenRevival(string sourceKey) {
            for (int i = 0; i < SlotCount; i++) {
                string other = equipped[i];
                if (string.IsNullOrEmpty(other) || other == sourceKey
                    || !revival.TryGetValue(other, out RevivalState state)) {
                    continue;
                }
                state.Value = Math.Max(state.Value - ScapeRelief, 0f);
                SyncCueTier(other);
            }
        }

        /// <summary>复苏满格：厉鬼夺身。所有满格槽一并归零，随后由演出走向死亡。</summary>
        private void BeginRevivalSeizure(string key) {
            foreach (KeyValuePair<string, RevivalState> pair in revival) {
                if (pair.Value.Value < 1f) {
                    continue;
                }
                pair.Value.Value = 0f;
                pair.Value.IdleTicks = 0;
                lastRevivalCueTiers[pair.Key] = 0;
            }
            MarkResourceChanged(immediate: true);
            WraithRevivalDeath.StartSeizure(Player, key);
        }

        private void AddErosionInternal(float amount) {
            if (amount <= 0f) {
                return;
            }
            int previousTier = ErosionTier;
            erosion = MathHelper.Clamp(erosion + amount, 0f, 1f);
            erosionIdleTicks = 0;
            if (Main.netMode != NetmodeID.Server && Player.whoAmI == Main.myPlayer
                && ErosionTier > previousTier) {
                PlayTierCue(ErosionTier);
            }
            lastCueTier = ErosionTier;
        }

        internal static int SanitizeScapeMultiplier(int value) {
            value = Math.Clamp(value, 2, 32);
            int sanitized = 2;
            while (sanitized < value) {
                sanitized *= 2;
            }
            return Math.Min(sanitized, 32);
        }

        private void MarkResourceChanged(bool immediate = false) {
            ResourceRevision++;
            resourceDirty = true;
            if (immediate && Main.netMode == NetmodeID.Server) {
                WraithNet.SendStateSync(Player.whoAmI);
                resourceDirty = false;
                resourceSyncTicks = 0;
            }
        }

        private void UpdateAuthority() {
            if (!sessionInitialized || Player.dead || Main.gamePaused) {
                return;
            }

            bool changed = false;
            bool immediateSync = false;
            bool resting = EquippedCount == 0 || !WraithAbilityService.IsOnikiriHeld(Player);
            restTicks = resting ? Math.Min(restTicks + 1, RestDelayTicks) : 0;

            //侵蚀减缓复苏衰减：满侵蚀时衰减速度是无侵蚀时的一半
            float erosionFactor = MathHelper.Lerp(1f, 0.5f, erosion);
            bool restDecay = resting && restTicks >= RestDelayTicks;
            foreach (KeyValuePair<string, RevivalState> pair in revival) {
                RevivalState state = pair.Value;
                state.IdleTicks = Math.Min(state.IdleTicks + 1, int.MaxValue - 1);
                if (state.Value <= 0f) {
                    continue;
                }
                float rate = restDecay ? RestDecayPerTick
                    : !resting && state.IdleTicks >= HeldIdleDelayTicks ? HeldDecayPerTick : 0f;
                if (rate <= 0f) {
                    continue;
                }
                state.Value = Math.Max(state.Value - rate * erosionFactor, 0f);
                //衰减跌出阈值后允许该鬼的低语再次触发
                lastRevivalCueTiers[pair.Key] = Math.Min(
                    lastRevivalCueTiers.GetValueOrDefault(pair.Key), GetRevivalTier(state.Value));
                changed = true;
            }

            if (erosionIdleTicks < ErosionDecayDelay) {
                erosionIdleTicks++;
            }
            else if (erosion > 0f) {
                int previousTier = ErosionTier;
                erosion = Math.Max(erosion - ErosionDecayPerTick, 0f);
                immediateSync |= ErosionTier != previousTier;
                lastCueTier = Math.Min(lastCueTier, ErosionTier);
                changed = true;
            }

            if (changed) {
                MarkResourceChanged(immediateSync);
            }
            if (Main.netMode == NetmodeID.Server && resourceDirty
                && ++resourceSyncTicks >= ResourceSyncInterval) {
                WraithNet.SendStateSync(Player.whoAmI);
                resourceDirty = false;
                resourceSyncTicks = 0;
            }
        }

        private void UpdateEquippedAbility() {
            if (Main.dedServ || Player.whoAmI != Main.myPlayer) {
                return;
            }
            for (int i = 0; i < SlotCount; i++) {
                string key = equipped[i];
                if (!string.IsNullOrEmpty(key)
                    && WraithAbilityService.TryResolve(Player, key,
                        out WraithAbilityContext context)) {
                    context.Definition.Ability?.Update(in context);
                }
            }
        }

        public override void PostUpdate() {
            revivalChangedTicks = Math.Min(revivalChangedTicks + 1, int.MaxValue - 1);
            WraithNet.UpdatePending(Player);
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                UpdateAuthority();
            }
            UpdateEquippedAbility();
        }

        public override void OnHurt(Player.HurtInfo info) {
            if (Player.dead || Player.statLife <= 0 || info.Damage < Player.statLife) {
                return;
            }
            Player.TryGetOverride(out PlayerDeath playerDeath);
            if (Main.netMode == NetmodeID.MultiplayerClient && Player.whoAmI == Main.myPlayer) {
                playerDeath?.NoteLocalLethalHurt(info);
            }
            else if (Main.netMode != NetmodeID.MultiplayerClient) {
                playerDeath?.NoteServerLethalHurt(info);
            }
        }

        public override void PostHurt(Player.HurtInfo info) {
            if (!VaultUtils.isServer) {
                Player.TryGetOverride(out PlayerDeath playerDeath);
                playerDeath?.ClearLethalHurt();
            }
        }

        public override void UpdateDead() {
            Player.TryGetOverride(out PlayerDeath playerDeath);
            playerDeath?.ClearScapeSession();
            playerDeath?.ClearLethalHurt();
        }

        public override void OnRespawn() {
            Player.TryGetOverride(out PlayerDeath playerDeath);
            playerDeath?.ClearScapeSession();
            playerDeath?.ClearLethalHurt();
        }

        public override void OnEnterWorld() {
            restTicks = 0;
            resourceSyncTicks = 0;
            resourceDirty = false;
            lastCueTier = ErosionTier;
            SyncAllCueTiers();
            Player.TryGetOverride(out PlayerDeath playerDeath);
            playerDeath?.ClearScapeSession();
            playerDeath?.ClearLethalHurt();
            if (Main.netMode == NetmodeID.MultiplayerClient && Player.whoAmI == Main.myPlayer) {
                sessionInitialized = false;
                WraithNet.SendInitialState(this);
            }
            else if (Main.netMode == NetmodeID.SinglePlayer) {
                sessionInitialized = true;
            }
        }

        public override void PlayerDisconnect() {
            sessionInitialized = false;
            Player.TryGetOverride(out PlayerDeath playerDeath);
            playerDeath?.ClearScapeSession();
            playerDeath?.ClearLethalHurt();
        }

        public override void SyncPlayer(int toWho, int fromWho, bool newPlayer) {
            if (Main.netMode == NetmodeID.Server && sessionInitialized) {
                WraithNet.SendStateSync(Player.whoAmI, toWho);
            }
        }

        public override void SaveData(TagCompound tag) {
            List<TagCompound> records = [];
            foreach (string key in UsableKeys) {
                records.Add(new TagCompound {
                    ["Key"] = key,
                    ["Revival"] = revival[key].Value,
                });
            }
            List<string> slots = [];
            for (int i = 0; i < SlotCount; i++) {
                slots.Add(equipped[i] ?? string.Empty);
            }
            TagCompound stateTag = new() {
                ["Version"] = SchemaVersion,
                ["Records"] = records,
                ["EquippedSlots"] = slots,
                ["Erosion"] = erosion,
                ["ScapeMultiplier"] = scapeMultiplier,
            };
            tag[SaveKey] = stateTag;
        }

        public override void LoadData(TagCompound tag) {
            ResetState();
            if (!tag.TryGet(SaveKey, out TagCompound stateTag) || stateTag == null) {
                return;
            }
            int version = stateTag.GetInt("Version");
            if (version != SchemaVersion && version != 2 && version != 1) {
                return;
            }

            if (version >= SchemaVersion
                && stateTag.TryGet("EquippedSlots", out List<string> slots) && slots != null) {
                for (int i = 0; i < SlotCount && i < slots.Count; i++) {
                    TryOccupySlot(i, slots[i]);
                }
            }
            else {
                //v1/v2 迁移：唯一役鬼落到第一格，其余两格空着
                string legacy = stateTag.GetString("EquippedWraithKey");
                if (string.IsNullOrEmpty(legacy)) {
                    legacy = stateTag.GetString("Equipped");
                }
                TryOccupySlot(0, legacy);
            }

            if (version == 1) {
                //v1 迁移：驾驭度/休眠废弃，六鬼复苏从零开始；旧共享复苏归入替死鬼
                revival[ScapeGhostKey].Value = ReadUnitFloat(stateTag, "Revival");
            }
            else if (stateTag.TryGet("Records", out List<TagCompound> records) && records != null) {
                HashSet<string> seen = [];
                foreach (TagCompound record in records) {
                    string key = record.GetString("Key");
                    if (!seen.Add(key) || !revival.TryGetValue(key, out RevivalState entry)) {
                        continue;
                    }
                    entry.Value = record.TryGet("Revival", out float stored) && float.IsFinite(stored)
                        ? MathHelper.Clamp(stored, 0f, 1f) : 0f;
                }
            }
            erosion = ReadUnitFloat(stateTag, "Erosion");
            scapeMultiplier = SanitizeScapeMultiplier(stateTag.GetInt("ScapeMultiplier"));
            lastCueTier = ErosionTier;
            SyncAllCueTiers();
        }

        /// <summary>读档/网络落位：过白名单，且不让同一只鬼占两格。</summary>
        private void TryOccupySlot(int slot, string key) {
            if (slot < 0 || slot >= SlotCount || string.IsNullOrEmpty(key)
                || !WraithRegistry.TryGetUsable(key, out _) || SlotOf(key) >= 0) {
                return;
            }
            equipped[slot] = key;
        }

        private void SyncAllCueTiers() {
            foreach (string key in UsableKeys) {
                lastRevivalCueTiers[key] = GetRevivalTier(GetRevival(key));
            }
        }

        private static float ReadUnitFloat(TagCompound tag, string key)
            => tag.TryGet(key, out float value) && float.IsFinite(value)
                ? MathHelper.Clamp(value, 0f, 1f) : 0f;

        internal WraithResourceSnapshot ExportResourceSnapshot() {
            WraithResourceSnapshot snapshot = new() {
                Revival = new float[UsableKeys.Length],
                Erosion = erosion,
                Multiplier = scapeMultiplier,
                ErosionIdle = erosionIdleTicks,
            };
            for (int i = 0; i < UsableKeys.Length; i++) {
                snapshot.Revival[i] = revival[UsableKeys[i]].Value;
            }
            return snapshot;
        }

        private void ApplySnapshotValues(in WraithResourceSnapshot snapshot) {
            for (int i = 0; i < UsableKeys.Length; i++) {
                revival[UsableKeys[i]].Value = SanitizeUnit(snapshot.Revival[i]);
            }
            erosion = SanitizeUnit(snapshot.Erosion);
            scapeMultiplier = SanitizeScapeMultiplier(snapshot.Multiplier);
            erosionIdleTicks = Math.Clamp(snapshot.ErosionIdle, 0, ErosionDecayDelay);
        }

        /// <summary>三槽 Key 快照（含空槽），供网络写包。</summary>
        internal string[] ExportSlots() {
            string[] slots = new string[SlotCount];
            for (int i = 0; i < SlotCount; i++) {
                slots[i] = equipped[i] ?? string.Empty;
            }
            return slots;
        }

        private void ApplySlots(string[] slots) {
            Array.Fill(equipped, string.Empty);
            if (slots == null) {
                return;
            }
            for (int i = 0; i < SlotCount && i < slots.Length; i++) {
                TryOccupySlot(i, slots[i]);
            }
        }

        internal bool AcceptInitialState(string[] slots, in WraithResourceSnapshot snapshot) {
            if (Main.netMode != NetmodeID.Server || sessionInitialized) {
                return false;
            }
            ApplySlots(slots);
            ApplySnapshotValues(in snapshot);
            SyncAllCueTiers();
            LoadoutRevision = 0;
            ResourceRevision = 0;
            sessionInitialized = true;
            return true;
        }

        internal void ApplyNetworkState(string[] slots, uint loadoutRev, uint resourceRev,
            in WraithResourceSnapshot snapshot, bool force) {
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                return;
            }
            if (force || loadoutRev >= LoadoutRevision) {
                ApplySlots(slots);
                LoadoutRevision = loadoutRev;
            }
            if (!force && resourceRev < ResourceRevision) {
                sessionInitialized = true;
                return;
            }

            int previousErosionTier = ErosionTier;
            float previousEquipped = EquippedRevival;
            int previousRevivalTier = GetRevivalTier(previousEquipped);
            ApplySnapshotValues(in snapshot);
            ResourceRevision = resourceRev;
            sessionInitialized = true;
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            if (ErosionTier > previousErosionTier) {
                PlayTierCue(ErosionTier);
            }
            float current = EquippedRevival;
            if (current > previousEquipped + 0.0001f) {
                revivalChangedTicks = 0;
            }
            int tier = GetRevivalTier(current);
            if (!force && tier > previousRevivalTier) {
                PlayRevivalCue(tier);
            }
            SyncAllCueTiers();
        }

        private static float SanitizeUnit(float value)
            => float.IsFinite(value) ? MathHelper.Clamp(value, 0f, 1f) : 0f;

        private void PlayTierCue(int tier) {
            var line = tier switch {
                1 => WraithSystemText.ErosionCrawl,
                2 => WraithSystemText.ErosionStain,
                _ => WraithSystemText.ErosionMirror,
            };
            VaultUtils.Text(line.Value, new Color(140, 120, 165));
            SoundEngine.PlaySound(SoundID.NPCDeath6 with {
                Pitch = -0.7f + tier * 0.15f,
                Volume = 0.35f
            });
            Player.CWR()?.GetScreenShake(1.5f + tier);
        }

        /// <summary>复苏低语：只在向上跨越阈值时短促播放一次。</summary>
        private void PlayRevivalCue(int tier) {
            var line = tier switch {
                1 => WraithSystemText.RevivalStir,
                2 => WraithSystemText.RevivalRise,
                _ => WraithSystemText.RevivalBrink,
            };
            VaultUtils.Text(line.Value, new Color(158, 44, 54));
            SoundEngine.PlaySound(SoundID.Zombie103 with {
                Pitch = -0.75f + tier * 0.12f,
                Volume = 0.32f,
                MaxInstances = 1,
            });
            if (tier >= 3) {
                Player.CWR()?.GetScreenShake(2.5f);
            }
        }
    }
}
