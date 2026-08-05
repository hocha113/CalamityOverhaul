using CalamityOverhaul.Content.Players;
using CalamityOverhaul.Content.Wraiths.Core;
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
        internal const int RevivalDecayDelay = 60 * 8;

        private const int SchemaVersion = 1;
        private const string SaveKey = "OnikiriWraithLoadout";
        private const float ErosionDecayPerTick = 1f / (60f * 240f);
        private const int ErosionDecayDelay = 60 * 6;
        private const float RevivalDecayPerTick = 1f / (60f * 480f);
        private const int ResourceSyncInterval = 15;

        public const float TierCrawl = 0.35f;
        public const float TierStain = 0.70f;
        public const float TierMirror = 0.95f;

        internal static readonly string[] UsableKeys = [
            ScapeGhostKey,
            HeadlessShadeKey,
            GhostHandKey,
            LanternBoyKey,
            CrimsonBrideKey,
        ];

        private sealed class MasteryState
        {
            internal float Value = 1f;
            internal bool Dormant;
        }

        private readonly Dictionary<string, MasteryState> mastery = [];
        private string equippedWraithKey = string.Empty;
        private float erosion;
        private float revival;
        private int scapeMultiplier = 2;
        private int restTicks;
        private int erosionIdleTicks;
        private int revivalIdleTicks;
        private int revivalChangedTicks;
        private int resourceSyncTicks;
        private int lastCueTier;
        private bool resourceDirty;
        private bool sessionInitialized;

        internal uint LoadoutRevision { get; private set; }
        internal uint ResourceRevision { get; private set; }
        internal string EquippedWraithKey => equippedWraithKey;
        internal bool SessionInitialized => sessionInitialized;
        public float Erosion => erosion;
        public float Revival => revival;
        public int RevivalChangedTimer => revivalChangedTicks;
        public int ScapeMultiplier => scapeMultiplier;
        public int ErosionTier => erosion >= TierMirror ? 3
            : erosion >= TierStain ? 2 : erosion >= TierCrawl ? 1 : 0;

        public override void Initialize() => ResetState();

        private void ResetState() {
            mastery.Clear();
            foreach (string key in UsableKeys) {
                mastery[key] = new MasteryState();
            }
            equippedWraithKey = string.Empty;
            erosion = 0f;
            revival = 0f;
            scapeMultiplier = 2;
            restTicks = 0;
            erosionIdleTicks = 0;
            revivalIdleTicks = 0;
            revivalChangedTicks = int.MaxValue / 2;
            resourceSyncTicks = 0;
            lastCueTier = 0;
            resourceDirty = false;
            sessionInitialized = false;
            LoadoutRevision = 0;
            ResourceRevision = 0;
        }

        internal float GetMastery(string key)
            => key != null && mastery.TryGetValue(key, out MasteryState state) ? state.Value : 0f;

        internal bool IsDormant(string key)
            => key != null && mastery.TryGetValue(key, out MasteryState state) && state.Dormant;

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
            LoadoutRevision++;
            if (Main.netMode == NetmodeID.Server) {
                WraithNet.SendStateSync(Player.whoAmI);
            }
            return true;
        }

        internal bool TryConsumeAuthority(string key, float masteryCost, float erosionCost) {
            if (!TryConsumeCore(key, masteryCost, erosionCost)) {
                return false;
            }
            MarkResourceChanged(immediate: true);
            return true;
        }

        internal bool TryCommitScapeAuthority(in WraithAbilityContext context,
            bool friendly, out bool revivalKilled) {
            revivalKilled = false;
            if (context.Player != Player || context.Definition?.Key != ScapeGhostKey
                || !TryConsumeCore(context.Definition.Key,
                    context.Definition.MasteryCost, context.Definition.ErosionCost)) {
                return false;
            }
            if (friendly) {
                scapeMultiplier = Math.Min(scapeMultiplier * 2, 32);
            }
            revival = MathHelper.Clamp(revival + 0.25f, 0f, 1f);
            revivalIdleTicks = 0;
            revivalChangedTicks = 0;
            revivalKilled = revival >= 1f;
            if (revivalKilled) {
                revival = 0f;
            }
            MarkResourceChanged(immediate: true);
            if (revivalKilled && WraithRegistry.TryGet(ScapeGhostKey, out WraithDefinition definition)) {
                WraithLethality.Kill(Player, definition, WraithSystemText.RevivalKillReason);
            }
            return true;
        }

        private bool TryConsumeCore(string key, float masteryCost, float erosionCost) {
            if (Main.netMode == NetmodeID.MultiplayerClient || !sessionInitialized
                || !mastery.TryGetValue(key, out MasteryState state) || state.Dormant
                || masteryCost <= 0f || state.Value + 0.0001f < masteryCost) {
                return false;
            }
            state.Value = MathHelper.Clamp(state.Value - masteryCost, 0f, 1f);
            if (state.Value <= WraithAbilityService.DormantThreshold) {
                state.Dormant = true;
            }
            AddErosionInternal(erosionCost);
            return true;
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
            if (resting) {
                restTicks = Math.Min(restTicks + 1, WraithAbilityService.RecoveryDelayTicks);
                if (restTicks >= WraithAbilityService.RecoveryDelayTicks) {
                    float erosionFactor = MathHelper.Lerp(1f, 0.5f, erosion);
                    float amount = WraithAbilityService.RecoveryPerSecond * erosionFactor / 60f;
                    foreach (MasteryState state in mastery.Values) {
                        if (state.Value >= 1f) {
                            continue;
                        }
                        state.Value = Math.Min(state.Value + amount, 1f);
                        if (state.Dormant && state.Value >= WraithAbilityService.WakeThreshold) {
                            state.Dormant = false;
                            immediateSync = true;
                            if (Main.netMode != NetmodeID.Server && Player.whoAmI == Main.myPlayer) {
                                PlayWakeCue();
                            }
                        }
                        changed = true;
                    }
                }
            }
            else {
                restTicks = 0;
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

            if (revivalIdleTicks < RevivalDecayDelay) {
                revivalIdleTicks++;
            }
            else if (revival > 0f) {
                revival = Math.Max(revival - RevivalDecayPerTick, 0f);
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
                MasteryState state = mastery[key];
                records.Add(new TagCompound {
                    ["Key"] = key,
                    ["Mastery"] = state.Value,
                    ["Dormant"] = state.Dormant,
                });
            }
            TagCompound stateTag = new() {
                ["Version"] = SchemaVersion,
                ["Records"] = records,
                ["Erosion"] = erosion,
                ["Revival"] = revival,
                ["ScapeMultiplier"] = scapeMultiplier,
            };
            if (!string.IsNullOrEmpty(equippedWraithKey)) {
                stateTag["EquippedWraithKey"] = equippedWraithKey;
            }
            tag[SaveKey] = stateTag;
        }

        public override void LoadData(TagCompound tag) {
            ResetState();
            if (!tag.TryGet(SaveKey, out TagCompound stateTag)
                || stateTag == null || stateTag.GetInt("Version") != SchemaVersion) {
                return;
            }

            string equipped = stateTag.GetString("EquippedWraithKey");
            if (string.IsNullOrEmpty(equipped)) {
                equipped = stateTag.GetString("Equipped");
            }
            equippedWraithKey = WraithRegistry.TryGetUsable(equipped, out _) ? equipped : string.Empty;
            if (stateTag.TryGet("Records", out List<TagCompound> records) && records != null) {
                HashSet<string> seen = [];
                foreach (TagCompound record in records) {
                    string key = record.GetString("Key");
                    if (!seen.Add(key) || !mastery.TryGetValue(key, out MasteryState entry)) {
                        continue;
                    }
                    float value = record.TryGet("Mastery", out float stored) && float.IsFinite(stored)
                        ? MathHelper.Clamp(stored, 0f, 1f) : 1f;
                    entry.Value = value;
                    entry.Dormant = value <= WraithAbilityService.DormantThreshold
                        || record.GetBool("Dormant") && value < WraithAbilityService.WakeThreshold;
                }
            }
            erosion = ReadUnitFloat(stateTag, "Erosion");
            revival = ReadUnitFloat(stateTag, "Revival");
            scapeMultiplier = SanitizeScapeMultiplier(stateTag.GetInt("ScapeMultiplier"));
            lastCueTier = ErosionTier;
        }

        private static float ReadUnitFloat(TagCompound tag, string key)
            => tag.TryGet(key, out float value) && float.IsFinite(value)
                ? MathHelper.Clamp(value, 0f, 1f) : 0f;

        internal void ExportSnapshot(out string equipped, out uint loadoutRev, out uint resourceRev,
            out float scapeMastery, out bool scapeDormant,
            out float shadeMastery, out bool shadeDormant,
            out float handMastery, out bool handDormant,
            out float lanternMastery, out bool lanternDormant,
            out float brideMastery, out bool brideDormant,
            out float erosionValue, out float revivalValue, out int multiplier,
            out int erosionIdle, out int revivalIdle) {
            equipped = equippedWraithKey;
            loadoutRev = LoadoutRevision;
            resourceRev = ResourceRevision;
            scapeMastery = GetMastery(ScapeGhostKey);
            scapeDormant = IsDormant(ScapeGhostKey);
            shadeMastery = GetMastery(HeadlessShadeKey);
            shadeDormant = IsDormant(HeadlessShadeKey);
            handMastery = GetMastery(GhostHandKey);
            handDormant = IsDormant(GhostHandKey);
            lanternMastery = GetMastery(LanternBoyKey);
            lanternDormant = IsDormant(LanternBoyKey);
            brideMastery = GetMastery(CrimsonBrideKey);
            brideDormant = IsDormant(CrimsonBrideKey);
            erosionValue = erosion;
            revivalValue = revival;
            multiplier = scapeMultiplier;
            erosionIdle = erosionIdleTicks;
            revivalIdle = revivalIdleTicks;
        }

        internal bool AcceptInitialState(string equipped,
            float scapeMastery, bool scapeDormant,
            float shadeMastery, bool shadeDormant,
            float handMastery, bool handDormant,
            float lanternMastery, bool lanternDormant,
            float brideMastery, bool brideDormant,
            float erosionValue, float revivalValue, int multiplier,
            int erosionIdle, int revivalIdle) {
            if (Main.netMode != NetmodeID.Server || sessionInitialized) {
                return false;
            }
            equippedWraithKey = WraithRegistry.TryGetUsable(equipped, out _) ? equipped : string.Empty;
            ApplyMastery(ScapeGhostKey, scapeMastery, scapeDormant);
            ApplyMastery(HeadlessShadeKey, shadeMastery, shadeDormant);
            ApplyMastery(GhostHandKey, handMastery, handDormant);
            ApplyMastery(LanternBoyKey, lanternMastery, lanternDormant);
            ApplyMastery(CrimsonBrideKey, brideMastery, brideDormant);
            erosion = SanitizeUnit(erosionValue);
            revival = SanitizeUnit(revivalValue);
            scapeMultiplier = SanitizeScapeMultiplier(multiplier);
            erosionIdleTicks = Math.Clamp(erosionIdle, 0, ErosionDecayDelay);
            revivalIdleTicks = Math.Clamp(revivalIdle, 0, RevivalDecayDelay);
            LoadoutRevision = 0;
            ResourceRevision = 0;
            sessionInitialized = true;
            return true;
        }

        internal void ApplyNetworkState(string equipped, uint loadoutRev, uint resourceRev,
            float scapeMastery, bool scapeDormant,
            float shadeMastery, bool shadeDormant,
            float handMastery, bool handDormant,
            float lanternMastery, bool lanternDormant,
            float brideMastery, bool brideDormant,
            float erosionValue, float revivalValue, int multiplier,
            int erosionIdle, int revivalIdle, bool force) {
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

            bool scapeWoke = IsDormant(ScapeGhostKey) && !scapeDormant;
            bool shadeWoke = IsDormant(HeadlessShadeKey) && !shadeDormant;
            bool handWoke = IsDormant(GhostHandKey) && !handDormant;
            bool lanternWoke = IsDormant(LanternBoyKey) && !lanternDormant;
            bool brideWoke = IsDormant(CrimsonBrideKey) && !brideDormant;
            int previousTier = ErosionTier;
            ApplyMastery(ScapeGhostKey, scapeMastery, scapeDormant);
            ApplyMastery(HeadlessShadeKey, shadeMastery, shadeDormant);
            ApplyMastery(GhostHandKey, handMastery, handDormant);
            ApplyMastery(LanternBoyKey, lanternMastery, lanternDormant);
            ApplyMastery(CrimsonBrideKey, brideMastery, brideDormant);
            erosion = SanitizeUnit(erosionValue);
            revival = SanitizeUnit(revivalValue);
            scapeMultiplier = SanitizeScapeMultiplier(multiplier);
            erosionIdleTicks = Math.Clamp(erosionIdle, 0, ErosionDecayDelay);
            revivalIdleTicks = Math.Clamp(revivalIdle, 0, RevivalDecayDelay);
            ResourceRevision = resourceRev;
            sessionInitialized = true;
            if (ErosionTier > previousTier && Player.whoAmI == Main.myPlayer) {
                PlayTierCue(ErosionTier);
            }
            if ((scapeWoke || shadeWoke || handWoke || lanternWoke || brideWoke)
                && Player.whoAmI == Main.myPlayer) {
                PlayWakeCue();
            }
        }

        private void ApplyMastery(string key, float value, bool dormant) {
            MasteryState state = mastery[key];
            state.Value = SanitizeUnit(value);
            state.Dormant = state.Value <= WraithAbilityService.DormantThreshold
                || dormant && state.Value < WraithAbilityService.WakeThreshold;
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

        private static void PlayWakeCue()
            => SoundEngine.PlaySound(SoundID.Item4 with { Pitch = -0.45f, Volume = 0.35f });
    }
}
