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

        private const int SchemaVersion = 2;
        private const string SaveKey = "OnikiriWraithLoadout";
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
        private string equippedWraithKey = string.Empty;
        private float erosion;
        private int scapeMultiplier = 2;
        private int restTicks;
        private int erosionIdleTicks;
        private int revivalChangedTicks;
        private int resourceSyncTicks;
        private int lastCueTier;
        private int lastRevivalCueTier;
        private bool resourceDirty;
        private bool sessionInitialized;

        internal uint LoadoutRevision { get; private set; }
        internal uint ResourceRevision { get; private set; }
        internal string EquippedWraithKey => equippedWraithKey;
        internal bool SessionInitialized => sessionInitialized;
        public float Erosion => erosion;
        public int RevivalChangedTimer => revivalChangedTicks;
        public int ScapeMultiplier => scapeMultiplier;
        public int ErosionTier => erosion >= TierMirror ? 3
            : erosion >= TierStain ? 2 : erosion >= TierCrawl ? 1 : 0;

        /// <summary>当前役鬼的复苏值；空役鬼位为 0。</summary>
        public float EquippedRevival => GetRevival(equippedWraithKey);

        public override void Initialize() => ResetState();

        private void ResetState() {
            revival.Clear();
            foreach (string key in UsableKeys) {
                revival[key] = new RevivalState();
            }
            equippedWraithKey = string.Empty;
            erosion = 0f;
            scapeMultiplier = 2;
            restTicks = 0;
            erosionIdleTicks = 0;
            revivalChangedTicks = int.MaxValue / 2;
            resourceSyncTicks = 0;
            lastCueTier = 0;
            lastRevivalCueTier = 0;
            resourceDirty = false;
            sessionInitialized = false;
            LoadoutRevision = 0;
            ResourceRevision = 0;
        }

        internal float GetRevival(string key)
            => key != null && revival.TryGetValue(key, out RevivalState state) ? state.Value : 0f;

        public static int GetRevivalTier(float value) => value >= RevivalBrinkLine ? 3
            : value >= RevivalRiseLine ? 2 : value >= RevivalStirLine ? 1 : 0;

        internal bool TrySetEquippedAuthority(string key, uint expectedRevision = uint.MaxValue) {
            if (Main.netMode == NetmodeID.MultiplayerClient || !sessionInitialized
                || expectedRevision != uint.MaxValue && expectedRevision != LoadoutRevision) {
                return false;
            }
            string next = string.IsNullOrEmpty(key) ? string.Empty : key;
            if (!string.IsNullOrEmpty(next) && !WraithRegistry.TryGetUsable(next, out _)) {
                return false;
            }
            if (equippedWraithKey == next) {
                return true;
            }
            equippedWraithKey = next;
            restTicks = 0;
            //换鬼后按新鬼当前进度重置低语阶，避免装备瞬间补播
            lastRevivalCueTier = GetRevivalTier(GetRevival(next));
            LoadoutRevision++;
            if (Main.netMode == NetmodeID.Server) {
                WraithNet.SendStateSync(Player.whoAmI);
            }
            return true;
        }

        internal bool TryChargeAuthority(string key, float revivalGain, float erosionCost) {
            if (!TryChargeCore(key, revivalGain, erosionCost, out bool revivalFull)) {
                return false;
            }
            MarkResourceChanged(immediate: true);
            if (revivalFull) {
                BeginRevivalSeizure(key);
            }
            return true;
        }

        internal bool TryCommitScapeAuthority(in WraithAbilityContext context,
            bool friendly, out bool revivalKilled) {
            revivalKilled = false;
            if (context.Player != Player || context.Definition?.Key != ScapeGhostKey
                || !TryChargeCore(ScapeGhostKey, context.Definition.RevivalCost,
                    context.Definition.ErosionCost, out revivalKilled)) {
                return false;
            }
            if (friendly) {
                scapeMultiplier = Math.Min(scapeMultiplier * 2, 32);
            }
            MarkResourceChanged(immediate: true);
            if (revivalKilled) {
                BeginRevivalSeizure(ScapeGhostKey);
            }
            return true;
        }

        private bool TryChargeCore(string key, float revivalGain, float erosionCost,
            out bool revivalFull) {
            revivalFull = false;
            if (Main.netMode == NetmodeID.MultiplayerClient || !sessionInitialized
                || revivalGain <= 0f || !revival.TryGetValue(key, out RevivalState state)
                || WraithRevivalDeath.IsSeized(Player)) {
                return false;
            }
            state.Value = MathHelper.Clamp(state.Value + revivalGain, 0f, 1f);
            state.IdleTicks = 0;
            if (key == equippedWraithKey) {
                revivalChangedTicks = 0;
                int tier = GetRevivalTier(state.Value);
                if (tier > lastRevivalCueTier && Main.netMode != NetmodeID.Server
                    && Player.whoAmI == Main.myPlayer) {
                    PlayRevivalCue(tier);
                }
                lastRevivalCueTier = Math.Max(lastRevivalCueTier, tier);
            }
            AddErosionInternal(erosionCost);
            revivalFull = state.Value >= 1f;
            return true;
        }

        /// <summary>复苏满格：厉鬼夺身。槽先归零，随后交由夺身演出走向死亡。</summary>
        private void BeginRevivalSeizure(string key) {
            if (!revival.TryGetValue(key, out RevivalState state)) {
                return;
            }
            state.Value = 0f;
            state.IdleTicks = 0;
            if (key == equippedWraithKey) {
                lastRevivalCueTier = 0;
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
            bool resting = string.IsNullOrEmpty(equippedWraithKey)
                || !WraithAbilityService.IsOnikiriHeld(Player);
            restTicks = resting ? Math.Min(restTicks + 1, RestDelayTicks) : 0;

            //侵蚀减缓复苏衰减：满侵蚀时衰减速度是无侵蚀时的一半
            float erosionFactor = MathHelper.Lerp(1f, 0.5f, erosion);
            bool restDecay = resting && restTicks >= RestDelayTicks;
            foreach (RevivalState state in revival.Values) {
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
                changed = true;
            }
            if (changed) {
                //衰减跌出阈值后允许低语再次触发
                lastRevivalCueTier = Math.Min(lastRevivalCueTier,
                    GetRevivalTier(GetRevival(equippedWraithKey)));
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
            if (Main.dedServ || Player.whoAmI != Main.myPlayer
                || string.IsNullOrEmpty(equippedWraithKey)
                || !WraithAbilityService.TryResolve(Player, equippedWraithKey,
                    out WraithAbilityContext context)) {
                return;
            }
            context.Definition.Ability?.Update(in context);
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
            lastRevivalCueTier = GetRevivalTier(EquippedRevival);
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
            TagCompound stateTag = new() {
                ["Version"] = SchemaVersion,
                ["Records"] = records,
                ["Erosion"] = erosion,
                ["ScapeMultiplier"] = scapeMultiplier,
            };
            if (!string.IsNullOrEmpty(equippedWraithKey)) {
                stateTag["EquippedWraithKey"] = equippedWraithKey;
            }
            tag[SaveKey] = stateTag;
        }

        public override void LoadData(TagCompound tag) {
            ResetState();
            if (!tag.TryGet(SaveKey, out TagCompound stateTag) || stateTag == null) {
                return;
            }
            int version = stateTag.GetInt("Version");
            if (version != SchemaVersion && version != 1) {
                return;
            }

            string equipped = stateTag.GetString("EquippedWraithKey");
            if (string.IsNullOrEmpty(equipped)) {
                equipped = stateTag.GetString("Equipped");
            }
            equippedWraithKey = WraithRegistry.TryGetUsable(equipped, out _) ? equipped : string.Empty;
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
            lastRevivalCueTier = GetRevivalTier(EquippedRevival);
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

        internal bool AcceptInitialState(string equipped, in WraithResourceSnapshot snapshot) {
            if (Main.netMode != NetmodeID.Server || sessionInitialized) {
                return false;
            }
            equippedWraithKey = WraithRegistry.TryGetUsable(equipped, out _) ? equipped : string.Empty;
            ApplySnapshotValues(in snapshot);
            lastRevivalCueTier = GetRevivalTier(EquippedRevival);
            LoadoutRevision = 0;
            ResourceRevision = 0;
            sessionInitialized = true;
            return true;
        }

        internal void ApplyNetworkState(string equipped, uint loadoutRev, uint resourceRev,
            in WraithResourceSnapshot snapshot, bool force) {
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                return;
            }
            if (force || loadoutRev >= LoadoutRevision) {
                equippedWraithKey = WraithRegistry.TryGetUsable(equipped, out _) ? equipped : string.Empty;
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
            lastRevivalCueTier = tier;
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
