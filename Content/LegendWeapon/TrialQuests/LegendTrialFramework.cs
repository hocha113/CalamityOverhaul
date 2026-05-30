using CalamityOverhaul.Content.ADV.EntrustManager;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.TrialQuests
{
    internal enum LegendTrialCompositeMode
    {
        Any,
        All,
    }

    internal readonly struct LegendTrialTargetSnapshot
    {
        public readonly bool IsActive;
        public readonly float Progress;
        public readonly float DisplayRatio;
        public readonly string ActiveName;
        public readonly string StatusLine;

        public LegendTrialTargetSnapshot(bool isActive, float progress, float displayRatio, string activeName, string statusLine = "") {
            IsActive = isActive;
            Progress = MathHelper.Clamp(progress, 0f, 1f);
            DisplayRatio = MathHelper.Clamp(displayRatio, 0f, 1f);
            ActiveName = activeName ?? string.Empty;
            StatusLine = statusLine ?? string.Empty;
        }

        public static LegendTrialTargetSnapshot Inactive => new(false, 0f, 1f, string.Empty);
        public static LegendTrialTargetSnapshot Completed => new(false, 1f, 0f, string.Empty);
    }

    internal interface ILegendTrialTarget
    {
        bool IsAvailable { get; }
        bool IsCompleted { get; }
        IEnumerable<string> GetDisplayNames();
        LegendTrialTargetSnapshot GetSnapshot();
    }

    internal sealed class NpcLegendTrialTarget : ILegendTrialTarget
    {
        private readonly Func<int[]> npcTypeProvider;
        private readonly Func<bool> completedCheck;
        private readonly string fallbackName;
        private bool resolvedNpcTypes;
        private int[] cachedNpcTypes;

        public NpcLegendTrialTarget(Func<int[]> npcTypeProvider, Func<bool> completedCheck, string fallbackName = "") {
            this.npcTypeProvider = npcTypeProvider;
            this.completedCheck = completedCheck;
            this.fallbackName = fallbackName ?? string.Empty;
        }

        public bool IsAvailable => ResolveNpcTypes().Length > 0;
        public bool IsCompleted => completedCheck?.Invoke() == true;

        public IEnumerable<string> GetDisplayNames() {
            string[] names = [.. ResolveNpcTypes()
                .Select(static t => Lang.GetNPCNameValue(t))
                .Where(static n => !string.IsNullOrEmpty(n))];

            if (names.Length > 0) {
                return names;
            }
            if (!string.IsNullOrEmpty(fallbackName)) {
                return [fallbackName];
            }
            return [];
        }

        public LegendTrialTargetSnapshot GetSnapshot() {
            if (IsCompleted) {
                return LegendTrialTargetSnapshot.Completed;
            }

            int[] npcTypes = ResolveNpcTypes();
            if (npcTypes.Length == 0) {
                return LegendTrialTargetSnapshot.Inactive;
            }

            bool alive = false;
            float bestRatio = 1f;
            string bestName = string.Empty;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.lifeMax <= 0) {
                    continue;
                }
                if (Array.IndexOf(npcTypes, npc.type) < 0) {
                    continue;
                }

                alive = true;
                float ratio = (float)npc.life / npc.lifeMax;
                if (ratio < bestRatio) {
                    bestRatio = ratio;
                    bestName = Lang.GetNPCNameValue(npc.type);
                }
            }

            if (!alive) {
                return LegendTrialTargetSnapshot.Inactive;
            }

            return new LegendTrialTargetSnapshot(true, 1f - bestRatio, bestRatio, bestName);
        }

        private int[] ResolveNpcTypes() {
            if (resolvedNpcTypes) {
                return cachedNpcTypes;
            }

            int[] types = npcTypeProvider?.Invoke() ?? [];
            cachedNpcTypes = [.. types.Where(static t => t > NPCID.None).Distinct()];
            resolvedNpcTypes = true;
            return cachedNpcTypes;
        }
    }

    internal sealed class EventLegendTrialTarget : ILegendTrialTarget
    {
        private readonly string displayName;
        private readonly Func<bool> activeCheck;
        private readonly Func<bool> completedCheck;
        private readonly Func<bool> availableCheck;

        public EventLegendTrialTarget(string displayName, Func<bool> activeCheck, Func<bool> completedCheck, Func<bool> availableCheck = null) {
            this.displayName = displayName ?? string.Empty;
            this.activeCheck = activeCheck;
            this.completedCheck = completedCheck;
            this.availableCheck = availableCheck;
        }

        public bool IsAvailable => availableCheck?.Invoke() ?? true;
        public bool IsCompleted => completedCheck?.Invoke() == true;

        public IEnumerable<string> GetDisplayNames() {
            if (string.IsNullOrEmpty(displayName)) {
                return [];
            }
            return [displayName];
        }

        public LegendTrialTargetSnapshot GetSnapshot() {
            if (IsCompleted) {
                return LegendTrialTargetSnapshot.Completed;
            }
            if (activeCheck?.Invoke() == true) {
                return new LegendTrialTargetSnapshot(true, 0f, 1f, displayName, $"{displayName}: 进行中");
            }
            return LegendTrialTargetSnapshot.Inactive;
        }
    }

    internal sealed class CompositeLegendTrialTarget : ILegendTrialTarget
    {
        private readonly LegendTrialCompositeMode mode;
        private readonly ILegendTrialTarget[] targets;

        public CompositeLegendTrialTarget(LegendTrialCompositeMode mode, params ILegendTrialTarget[] targets) {
            this.mode = mode;
            this.targets = targets?.Where(static t => t != null).ToArray() ?? [];
        }

        public bool IsAvailable {
            get {
                ILegendTrialTarget[] availableTargets = AvailableTargets();
                return mode == LegendTrialCompositeMode.Any
                    ? availableTargets.Length > 0
                    : availableTargets.Length == targets.Length && targets.Length > 0;
            }
        }

        public bool IsCompleted {
            get {
                ILegendTrialTarget[] availableTargets = AvailableTargets();
                if (availableTargets.Length == 0) {
                    return false;
                }
                return mode == LegendTrialCompositeMode.Any
                    ? availableTargets.Any(static t => t.IsCompleted)
                    : availableTargets.All(static t => t.IsCompleted);
            }
        }

        public IEnumerable<string> GetDisplayNames() {
            return AvailableTargets().SelectMany(static t => t.GetDisplayNames());
        }

        public LegendTrialTargetSnapshot GetSnapshot() {
            if (IsCompleted) {
                return LegendTrialTargetSnapshot.Completed;
            }

            ILegendTrialTarget[] availableTargets = AvailableTargets();
            if (availableTargets.Length == 0) {
                return LegendTrialTargetSnapshot.Inactive;
            }

            LegendTrialTargetSnapshot[] snapshots = [.. availableTargets.Select(static t => t.GetSnapshot())];
            LegendTrialTargetSnapshot active = snapshots.FirstOrDefault(static s => s.IsActive);
            if (active.IsActive) {
                return active;
            }

            if (mode == LegendTrialCompositeMode.All && snapshots.Length > 0) {
                float progress = snapshots.Average(static s => s.Progress);
                return new LegendTrialTargetSnapshot(false, progress, 1f - progress, string.Empty);
            }

            return LegendTrialTargetSnapshot.Inactive;
        }

        private ILegendTrialTarget[] AvailableTargets() {
            return [.. targets.Where(static t => t.IsAvailable)];
        }
    }

    internal sealed class LegendTrialDefinition
    {
        public string Key { get; }
        public LocalizedText Title { get; }
        public LocalizedText Summary { get; }
        public ILegendTrialTarget Target { get; }

        public bool IsAvailable => Target?.IsAvailable == true;
        public bool IsCompleted => Target?.IsCompleted == true;

        public LegendTrialDefinition(string key, ILegendTrialTarget target, LocalizedText title = null, LocalizedText summary = null) {
            Key = key;
            Target = target;
            Title = title;
            Summary = summary;
        }
    }

    internal static class LegendTrialRouteResolver
    {
        public static IReadOnlyList<LegendTrialDefinition> GetAvailableTrials(IReadOnlyList<LegendTrialDefinition> definitions) {
            if (definitions == null || definitions.Count == 0) {
                return [];
            }
            return [.. definitions.Where(static d => d?.IsAvailable == true)];
        }

        public static int GetSequentialLevel(IReadOnlyList<LegendTrialDefinition> definitions, Func<LegendTrialDefinition, bool> isCompleted = null) {
            int level = 0;
            foreach (LegendTrialDefinition trial in GetAvailableTrials(definitions)) {
                bool completed = isCompleted?.Invoke(trial) ?? trial.IsCompleted;
                if (!completed) {
                    break;
                }
                level++;
            }
            return level;
        }

        public static int GetSequentialOriginalLevel(IReadOnlyList<LegendTrialDefinition> definitions, Func<LegendTrialDefinition, bool> isCompleted = null) {
            if (definitions == null || definitions.Count == 0) {
                return 0;
            }

            int level = 0;
            for (int i = 0; i < definitions.Count; i++) {
                LegendTrialDefinition trial = definitions[i];
                if (trial?.IsAvailable != true) {
                    continue;
                }

                bool completed = isCompleted?.Invoke(trial) ?? trial.IsCompleted;
                if (!completed) {
                    break;
                }
                level = i + 1;
            }
            return level;
        }

        public static string GetRouteSignature(IReadOnlyList<LegendTrialDefinition> definitions) {
            return string.Join("|", GetAvailableTrials(definitions).Select(static d => d.Key));
        }

        public static IEnumerable<string> GetLegacyCompletedKeys(IReadOnlyList<LegendTrialDefinition> definitions, int legacyLevel) {
            if (legacyLevel <= 0) {
                yield break;
            }

            int index = 0;
            foreach (LegendTrialDefinition trial in definitions ?? []) {
                if (index++ >= legacyLevel) {
                    yield break;
                }
                if (trial != null && !string.IsNullOrEmpty(trial.Key)) {
                    yield return trial.Key;
                }
            }
        }
    }

    internal static class LegendTrialRouteCatalog
    {
        private const string BossRushName = "终焉之战";

        private static IReadOnlyList<LegendTrialDefinition> murasamaProgression;
        private static IReadOnlyList<LegendTrialDefinition> shpcProgression;
        private static IReadOnlyList<LegendTrialDefinition> halibutProgression;

        public static IReadOnlyList<LegendTrialDefinition> MurasamaProgression
            => murasamaProgression ??= CreateMurasama();

        public static IReadOnlyList<LegendTrialDefinition> SHPCProgression
            => shpcProgression ??= CreateSHPC();

        public static IReadOnlyList<LegendTrialDefinition> HalibutProgression
            => halibutProgression ??= CreateHalibut();

        public static LegendTrialDefinition[] CreateMurasama(LocalizedText[] titles = null, LocalizedText[] summaries = null) => [
            Trial("murasama.000.king_slime", Npc(() => [NPCID.KingSlime], InWorldBossPhase.DownedV0), titles, summaries, 0),
            Trial("murasama.001.desert_scourge", Npc(() => [CWRID.NPC_DesertScourgeHead], InWorldBossPhase.Downed0), titles, summaries, 1),
            Trial("murasama.002.eye_of_cthulhu", Npc(() => [NPCID.EyeofCthulhu], InWorldBossPhase.DownedV1), titles, summaries, 2),
            Trial("murasama.003.evil_boss", Npc(() => [NPCID.EaterofWorldsHead, NPCID.BrainofCthulhu], InWorldBossPhase.DownedV2), titles, summaries, 3),
            Trial("murasama.004.calamity_evil_boss", Npc(() => [CWRID.NPC_HiveMind, CWRID.NPC_PerforatorHive], () => InWorldBossPhase.Downed3.Invoke() || InWorldBossPhase.Downed4.Invoke()), titles, summaries, 4),
            Trial("murasama.005.skeletron", Npc(() => [NPCID.SkeletronHead], InWorldBossPhase.DownedV4), titles, summaries, 5),
            Trial("murasama.006.slime_god", Npc(() => [CWRID.NPC_SlimeGodCore], InWorldBossPhase.Downed5), titles, summaries, 6),
            Trial("murasama.007.wall_of_flesh", Npc(() => [NPCID.WallofFlesh], () => Main.hardMode), titles, summaries, 7),
            Trial("murasama.008.aquatic_scourge", Npc(() => [CWRID.NPC_AquaticScourgeHead], InWorldBossPhase.Downed8), titles, summaries, 8),
            Trial("murasama.009.brimstone_elemental", Npc(() => [CWRID.NPC_BrimstoneElemental], InWorldBossPhase.Downed7), titles, summaries, 9),
            Trial("murasama.010.cryogen", Npc(() => [CWRID.NPC_Cryogen], InWorldBossPhase.Downed6), titles, summaries, 10),
            Trial("murasama.011.destroyer", Npc(() => [NPCID.TheDestroyer], () => NPC.downedMechBoss1), titles, summaries, 11),
            Trial("murasama.012.twins", Npc(() => [NPCID.Retinazer, NPCID.Spazmatism], () => NPC.downedMechBoss2), titles, summaries, 12),
            Trial("murasama.013.skeletron_prime", Npc(() => [NPCID.SkeletronPrime], () => NPC.downedMechBoss3), titles, summaries, 13),
            Trial("murasama.014.calamitas_clone", Npc(() => [CWRID.NPC_CalamitasClone], InWorldBossPhase.Downed10), titles, summaries, 14),
            Trial("murasama.015.plantera", Npc(() => [NPCID.Plantera], InWorldBossPhase.VDownedV7), titles, summaries, 15),
            Trial("murasama.016.golem", Npc(() => [NPCID.Golem, NPCID.GolemHead], InWorldBossPhase.DownedV7), titles, summaries, 16),
            Trial("murasama.017.plaguebringer", Npc(() => [CWRID.NPC_PlaguebringerGoliath], InWorldBossPhase.Downed14), titles, summaries, 17),
            Trial("murasama.018.ravager", Npc(() => [CWRID.NPC_RavagerBody], InWorldBossPhase.Downed15), titles, summaries, 18),
            Trial("murasama.019.astrum_deus", Npc(() => [CWRID.NPC_AstrumDeusHead], InWorldBossPhase.Downed16), titles, summaries, 19),
            Trial("murasama.020.moon_lord", Npc(() => [NPCID.MoonLordCore], InWorldBossPhase.VDownedV16), titles, summaries, 20),
            Trial("murasama.021.providence", Npc(() => [CWRID.NPC_Providence], InWorldBossPhase.Downed19), titles, summaries, 21),
            Trial("murasama.022.polterghast", Npc(() => [CWRID.NPC_Polterghast], InWorldBossPhase.Downed23), titles, summaries, 22),
            Trial("murasama.023.devourer_of_gods", Npc(() => [CWRID.NPC_DevourerofGodsHead], InWorldBossPhase.Downed27), titles, summaries, 23),
            Trial("murasama.024.yharon", Npc(() => [CWRID.NPC_Yharon], InWorldBossPhase.Downed28), titles, summaries, 24),
            Trial("murasama.025.exo_mechs", Npc(() => [CWRID.NPC_AresBody, CWRID.NPC_Apollo, CWRID.NPC_Artemis, CWRID.NPC_ThanatosHead], InWorldBossPhase.Downed29), titles, summaries, 25),
            Trial("murasama.026.supreme_calamitas", Npc(() => [CWRID.NPC_SupremeCalamitas], InWorldBossPhase.Downed30), titles, summaries, 26),
            Trial("murasama.027.primordial_wyrm_or_boss_rush", Any(
                Npc(() => [CWRID.NPC_PrimordialWyrmHead], InWorldBossPhase.Downed31),
                BossRush()), titles, summaries, 27),
        ];

        public static LegendTrialDefinition[] CreateSHPC(LocalizedText[] titles = null, LocalizedText[] summaries = null) => [
            Trial("shpc.000.eye_of_cthulhu", Npc(() => [NPCID.EyeofCthulhu], InWorldBossPhase.DownedV1), titles, summaries, 0),
            Trial("shpc.001.evil_boss", Npc(() => [NPCID.EaterofWorldsHead, NPCID.BrainofCthulhu], InWorldBossPhase.DownedV2), titles, summaries, 1),
            Trial("shpc.002.calamity_evil_boss", Npc(() => [CWRID.NPC_HiveMind, CWRID.NPC_PerforatorHive], () => InWorldBossPhase.Downed3.Invoke() || InWorldBossPhase.Downed4.Invoke()), titles, summaries, 2),
            Trial("shpc.003.slime_god", Npc(() => [CWRID.NPC_SlimeGodCore], InWorldBossPhase.Downed5), titles, summaries, 3),
            Trial("shpc.004.wall_of_flesh", Npc(() => [NPCID.WallofFlesh], () => Main.hardMode), titles, summaries, 4),
            Trial("shpc.005.aquatic_scourge", Npc(() => [CWRID.NPC_AquaticScourgeHead], InWorldBossPhase.Downed8), titles, summaries, 5),
            Trial("shpc.006.brimstone_elemental", Npc(() => [CWRID.NPC_BrimstoneElemental], InWorldBossPhase.Downed7), titles, summaries, 6),
            Trial("shpc.007.destroyer", Npc(() => [NPCID.TheDestroyer], () => NPC.downedMechBoss1), titles, summaries, 7),
            Trial("shpc.008.twins", Npc(() => [NPCID.Retinazer, NPCID.Spazmatism], () => NPC.downedMechBoss2), titles, summaries, 8),
            Trial("shpc.009.skeletron_prime", Npc(() => [NPCID.SkeletronPrime], () => NPC.downedMechBoss3), titles, summaries, 9),
            Trial("shpc.010.calamitas_clone", Npc(() => [CWRID.NPC_CalamitasClone], InWorldBossPhase.Downed10), titles, summaries, 10),
            Trial("shpc.011.plantera", Npc(() => [NPCID.Plantera], InWorldBossPhase.VDownedV7), titles, summaries, 11),
            Trial("shpc.012.golem", Npc(() => [NPCID.Golem, NPCID.GolemHead], InWorldBossPhase.DownedV7), titles, summaries, 12),
            Trial("shpc.013.cultist", Npc(() => [NPCID.CultistBoss], InWorldBossPhase.DownedV8), titles, summaries, 13),
            Trial("shpc.014.moon_lord", Npc(() => [NPCID.MoonLordCore], InWorldBossPhase.VDownedV16), titles, summaries, 14),
            Trial("shpc.015.providence", Npc(() => [CWRID.NPC_Providence], InWorldBossPhase.Downed19), titles, summaries, 15),
            Trial("shpc.016.polterghast", Npc(() => [CWRID.NPC_Polterghast], InWorldBossPhase.Downed23), titles, summaries, 16),
            Trial("shpc.017.devourer_of_gods", Npc(() => [CWRID.NPC_DevourerofGodsHead], InWorldBossPhase.Downed27), titles, summaries, 17),
            Trial("shpc.018.yharon", Npc(() => [CWRID.NPC_Yharon], InWorldBossPhase.Downed28), titles, summaries, 18),
            Trial("shpc.019.exo_mechs", Npc(() => [CWRID.NPC_AresBody, CWRID.NPC_Apollo, CWRID.NPC_Artemis, CWRID.NPC_ThanatosHead], InWorldBossPhase.Downed29), titles, summaries, 19),
            Trial("shpc.020.supreme_calamitas", Npc(() => [CWRID.NPC_SupremeCalamitas], InWorldBossPhase.Downed30), titles, summaries, 20),
            Trial("shpc.021.boss_rush", BossRush(), titles, summaries, 21),
        ];

        public static LegendTrialDefinition[] CreateHalibut(LocalizedText[] titles = null, Func<int, LocalizedText> summaryProvider = null) => [
            Trial("halibut.000.king_slime", Npc(() => [NPCID.KingSlime], InWorldBossPhase.DownedV0), titles, summaryProvider, 0),
            Trial("halibut.001.eye_of_cthulhu", Npc(() => [NPCID.EyeofCthulhu], InWorldBossPhase.DownedV1), titles, summaryProvider, 1),
            Trial("halibut.002.queen_bee", Npc(() => [NPCID.QueenBee], InWorldBossPhase.DownedV3), titles, summaryProvider, 2),
            Trial("halibut.003.skeletron_and_wall", Npc(() => [NPCID.SkeletronHead, NPCID.WallofFlesh], () => InWorldBossPhase.DownedV4.Invoke() && Main.hardMode), titles, summaryProvider, 3),
            Trial("halibut.004.mech_or_aquatic_scourge", Npc(() => [NPCID.TheDestroyer, NPCID.SkeletronPrime, NPCID.Retinazer, NPCID.Spazmatism, CWRID.NPC_AquaticScourgeHead], () => InWorldBossPhase.DownedV5.Invoke() || InWorldBossPhase.Downed8.Invoke()), titles, summaryProvider, 4),
            Trial("halibut.005.calamitas_or_plantera", Npc(() => [CWRID.NPC_CalamitasClone, NPCID.Plantera], () => InWorldBossPhase.Downed10.Invoke() || InWorldBossPhase.VDownedV7.Invoke()), titles, summaryProvider, 5),
            Trial("halibut.006.golem", Npc(() => [NPCID.Golem, NPCID.GolemHead], InWorldBossPhase.DownedV7), titles, summaryProvider, 6),
            Trial("halibut.007.moon_lord", Npc(() => [NPCID.MoonLordCore], InWorldBossPhase.VDownedV16), titles, summaryProvider, 7),
            Trial("halibut.008.providence", Npc(() => [CWRID.NPC_Providence], InWorldBossPhase.Downed19), titles, summaryProvider, 8),
            Trial("halibut.009.polterghast", Npc(() => [CWRID.NPC_Polterghast], InWorldBossPhase.Downed23), titles, summaryProvider, 9),
            Trial("halibut.010.devourer_of_gods", Npc(() => [CWRID.NPC_DevourerofGodsHead], InWorldBossPhase.Downed27), titles, summaryProvider, 10),
            Trial("halibut.011.yharon", Npc(() => [CWRID.NPC_Yharon], InWorldBossPhase.Downed28), titles, summaryProvider, 11),
            Trial("halibut.012.exo_mechs_and_supreme_calamitas", All(
                Npc(() => [CWRID.NPC_AresBody, CWRID.NPC_Apollo, CWRID.NPC_Artemis, CWRID.NPC_ThanatosHead], InWorldBossPhase.Downed29),
                Npc(() => [CWRID.NPC_SupremeCalamitas], InWorldBossPhase.Downed30)), titles, summaryProvider, 12),
            Trial("halibut.013.primordial_wyrm_or_boss_rush", Any(
                Npc(() => [CWRID.NPC_PrimordialWyrmHead], InWorldBossPhase.Downed31),
                BossRush()), titles, summaryProvider, 13),
        ];

        private static LegendTrialDefinition Trial(string key, ILegendTrialTarget target, LocalizedText[] titles, LocalizedText[] summaries, int index) {
            return new LegendTrialDefinition(key, target, titles?.ElementAtOrDefault(index), summaries?.ElementAtOrDefault(index));
        }

        private static LegendTrialDefinition Trial(string key, ILegendTrialTarget target, LocalizedText[] titles, Func<int, LocalizedText> summaryProvider, int index) {
            return new LegendTrialDefinition(key, target, titles?.ElementAtOrDefault(index), summaryProvider?.Invoke(index));
        }

        private static NpcLegendTrialTarget Npc(Func<int[]> npcTypeProvider, Func<bool> completedCheck)
            => new(npcTypeProvider, completedCheck);

        private static EventLegendTrialTarget BossRush()
            => new(BossRushName, CWRRef.GetBossRushActive, CWRRef.GetDownedBossRush, () => CWRRef.Has);

        private static CompositeLegendTrialTarget Any(params ILegendTrialTarget[] targets)
            => new(LegendTrialCompositeMode.Any, targets);

        private static CompositeLegendTrialTarget All(params ILegendTrialTarget[] targets)
            => new(LegendTrialCompositeMode.All, targets);
    }

    internal class LegendTrialQuestEntry : EntrustEntryData
    {
        public LegendTrialDefinition Trial { get; init; }
        public LocalizedText WaitingHint { get; init; }
        public LocalizedText FightingFormat { get; init; }
        public LocalizedText BriefFormat { get; init; }

        private LegendTrialTargetSnapshot snapshot = LegendTrialTargetSnapshot.Inactive;

        public LegendTrialQuestEntry(string key, LocalizedText title, LocalizedText summary, LocalizedText category)
            : base(key, title, summary, category) { }

        public override float GetTrackerContentTopPadding() => 5f;

        public override void OnUpdate() {
            if (Status == QuestEntryStatus.Completed || Status == QuestEntryStatus.Failed
                || Status == QuestEntryStatus.Suspended) {
                return;
            }

            if (Trial?.IsCompleted == true) {
                snapshot = LegendTrialTargetSnapshot.Completed;
                Progress = 1f;
                return;
            }

            snapshot = Trial?.Target?.GetSnapshot() ?? LegendTrialTargetSnapshot.Inactive;
            Progress = snapshot.Progress;
        }

        public override List<string> GetTrackerDetails() {
            var lines = new List<string>(2);

            string brief = BuildBrief();
            if (!string.IsNullOrEmpty(brief)) {
                lines.Add(brief);
            }

            if (!string.IsNullOrEmpty(snapshot.StatusLine)) {
                lines.Add(snapshot.StatusLine);
            }
            else if (!snapshot.IsActive) {
                lines.Add(WaitingHint?.Value ?? "...");
            }
            else {
                lines.Add(string.Format(FightingFormat?.Value ?? "{0}: {1:0%}",
                    snapshot.ActiveName, snapshot.DisplayRatio));
            }

            return lines;
        }

        private string BuildBrief() {
            string list = string.Join(" / ", Trial?.Target?.GetDisplayNames() ?? []);
            if (string.IsNullOrEmpty(list)) {
                return string.Empty;
            }

            string fmt = BriefFormat?.Value;
            return string.IsNullOrEmpty(fmt) ? list : string.Format(fmt, list);
        }
    }

    internal abstract class LegendTrialQuestLineBase : ModSystem
    {
        protected abstract string KeyPrefix { get; }
        protected abstract int LegacyTrialCount { get; }
        protected abstract LocalizedText QuestCategoryText { get; }
        protected abstract LocalizedText TrackerWaitingText { get; }
        protected abstract LocalizedText TrackerFightingText { get; }
        protected abstract LocalizedText TrackerBriefText { get; }
        protected abstract IReadOnlyList<LegendTrialDefinition> Trials { get; }
        protected abstract bool CanCreateEntries(Player player);
        protected abstract IEntrustEntryStyle CreateEntryStyle();
        protected abstract IEntrustTrackerWidgetStyle CreateTrackerStyle();
        protected abstract Func<bool> CreateTrackerVisibilityCheck();

        public override void PostUpdateEverything() {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }

            var manager = QuestManagerUI.Instance;
            if (manager == null || Trials == null) {
                return;
            }

            bool allowCreate = CanCreateEntries(Main.LocalPlayer);
            IReadOnlyList<LegendTrialDefinition> availableTrials = LegendTrialRouteResolver.GetAvailableTrials(Trials);
            int currentLevel = LegendTrialRouteResolver.GetSequentialLevel(Trials);

            for (int i = 0; i < LegacyTrialCount; i++) {
                manager.UnregisterQuest(KeyPrefix + i);
            }

            foreach (LegendTrialDefinition trial in Trials) {
                int routeIndex = IndexOfTrial(availableTrials, trial);
                SyncTrial(manager, trial, routeIndex, currentLevel, allowCreate, availableTrials.Count);
            }
        }

        private void SyncTrial(QuestManagerUI manager, LegendTrialDefinition trial, int routeIndex, int currentLevel, bool allowCreate, int routeCount) {
            string key = GetEntryKey(trial);
            if (trial == null || routeIndex < 0 || routeIndex > currentLevel) {
                manager.UnregisterQuest(key);
                return;
            }

            bool isDone = routeIndex < currentLevel || trial.IsCompleted;
            if (isDone) {
                var entry = EnsureTrialEntry(manager, trial, routeIndex, routeCount, completed: true, allowCreate: allowCreate);
                if (entry != null && entry.Status != QuestEntryStatus.Completed) {
                    manager.SetEntryStatus(key, QuestEntryStatus.Completed, 1f);
                }
            }
            else {
                var entry = EnsureTrialEntry(manager, trial, routeIndex, routeCount, allowCreate: allowCreate);
                if (entry != null && entry.Status == QuestEntryStatus.Completed) {
                    manager.SetEntryStatus(key, QuestEntryStatus.Active, 0f);
                }
            }
        }

        private LegendTrialQuestEntry EnsureTrialEntry(QuestManagerUI manager, LegendTrialDefinition trial, int routeIndex, int routeCount, bool completed = false, bool allowCreate = true) {
            string key = GetEntryKey(trial);
            var entry = manager.GetEntry(key) as LegendTrialQuestEntry;
            if (entry != null) {
                return entry;
            }
            if (!allowCreate) {
                return null;
            }

            entry = CreateTrialEntry(trial, routeIndex, routeCount);
            if (completed) {
                entry.Status = QuestEntryStatus.Completed;
                entry.Progress = 1f;
            }
            manager.RegisterQuest(entry);
            return entry;
        }

        protected virtual LegendTrialQuestEntry CreateTrialEntry(LegendTrialDefinition trial, int routeIndex, int routeCount) {
            return new LegendTrialQuestEntry(GetEntryKey(trial), trial.Title, trial.Summary, QuestCategoryText) {
                Trial = trial,
                Priority = routeCount - routeIndex,
                EntryStyle = CreateEntryStyle(),
                TrackerStyle = CreateTrackerStyle(),
                WaitingHint = TrackerWaitingText,
                FightingFormat = TrackerFightingText,
                BriefFormat = TrackerBriefText,
                TrackerVisibilityCheck = CreateTrackerVisibilityCheck(),
            };
        }

        private string GetEntryKey(LegendTrialDefinition trial) => KeyPrefix + trial.Key;

        private static int IndexOfTrial(IReadOnlyList<LegendTrialDefinition> trials, LegendTrialDefinition target) {
            for (int i = 0; i < trials.Count; i++) {
                if (ReferenceEquals(trials[i], target)) {
                    return i;
                }
            }
            return -1;
        }
    }
}
