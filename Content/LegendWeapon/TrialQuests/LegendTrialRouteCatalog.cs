using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.Localization;

namespace CalamityOverhaul.Content.LegendWeapon.TrialQuests
{
    internal static class LegendTrialRouteCatalog
    {
        private static IReadOnlyList<LegendTrialDefinition> shpcProgression;
        private static IReadOnlyList<LegendTrialDefinition> halibutProgression;
        private static IReadOnlyList<LegendTrialDefinition> onikiriProgression;

        public static IReadOnlyList<LegendTrialDefinition> SHPCProgression
            => shpcProgression ??= CreateSHPC();

        public static IReadOnlyList<LegendTrialDefinition> HalibutProgression
            => halibutProgression ??= CreateHalibut();

        public static IReadOnlyList<LegendTrialDefinition> OnikiriProgression
            => onikiriProgression ??= CreateOnikiri();


        public static LegendTrialDefinition[] CreateSHPC(LocalizedText[] titles = null, LocalizedText[] summaries = null,
            LocalizedText bossRushName = null, LocalizedText eventActiveFormat = null) => [
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
            Trial("shpc.021.boss_rush", BossRush(bossRushName, eventActiveFormat), titles, summaries, 21),
        ];

        /// <summary>鬼切试炼，序列同 SHPC，键 onikiri.*</summary>
        public static LegendTrialDefinition[] CreateOnikiri(LocalizedText[] titles = null, LocalizedText[] summaries = null,
            LocalizedText bossRushName = null, LocalizedText eventActiveFormat = null) => [
            Trial("onikiri.000.eye_of_cthulhu", Npc(() => [NPCID.EyeofCthulhu], InWorldBossPhase.DownedV1), titles, summaries, 0),
            Trial("onikiri.001.evil_boss", Npc(() => [NPCID.EaterofWorldsHead, NPCID.BrainofCthulhu], InWorldBossPhase.DownedV2), titles, summaries, 1),
            Trial("onikiri.002.calamity_evil_boss", Npc(() => [CWRID.NPC_HiveMind, CWRID.NPC_PerforatorHive], () => InWorldBossPhase.Downed3.Invoke() || InWorldBossPhase.Downed4.Invoke()), titles, summaries, 2),
            Trial("onikiri.003.slime_god", Npc(() => [CWRID.NPC_SlimeGodCore], InWorldBossPhase.Downed5), titles, summaries, 3),
            Trial("onikiri.004.wall_of_flesh", Npc(() => [NPCID.WallofFlesh], () => Main.hardMode), titles, summaries, 4),
            Trial("onikiri.005.aquatic_scourge", Npc(() => [CWRID.NPC_AquaticScourgeHead], InWorldBossPhase.Downed8), titles, summaries, 5),
            Trial("onikiri.006.brimstone_elemental", Npc(() => [CWRID.NPC_BrimstoneElemental], InWorldBossPhase.Downed7), titles, summaries, 6),
            Trial("onikiri.007.destroyer", Npc(() => [NPCID.TheDestroyer], () => NPC.downedMechBoss1), titles, summaries, 7),
            Trial("onikiri.008.twins", Npc(() => [NPCID.Retinazer, NPCID.Spazmatism], () => NPC.downedMechBoss2), titles, summaries, 8),
            Trial("onikiri.009.skeletron_prime", Npc(() => [NPCID.SkeletronPrime], () => NPC.downedMechBoss3), titles, summaries, 9),
            Trial("onikiri.010.calamitas_clone", Npc(() => [CWRID.NPC_CalamitasClone], InWorldBossPhase.Downed10), titles, summaries, 10),
            Trial("onikiri.011.plantera", Npc(() => [NPCID.Plantera], InWorldBossPhase.VDownedV7), titles, summaries, 11),
            Trial("onikiri.012.golem", Npc(() => [NPCID.Golem, NPCID.GolemHead], InWorldBossPhase.DownedV7), titles, summaries, 12),
            Trial("onikiri.013.cultist", Npc(() => [NPCID.CultistBoss], InWorldBossPhase.DownedV8), titles, summaries, 13),
            Trial("onikiri.014.moon_lord", Npc(() => [NPCID.MoonLordCore], InWorldBossPhase.VDownedV16), titles, summaries, 14),
            Trial("onikiri.015.providence", Npc(() => [CWRID.NPC_Providence], InWorldBossPhase.Downed19), titles, summaries, 15),
            Trial("onikiri.016.polterghast", Npc(() => [CWRID.NPC_Polterghast], InWorldBossPhase.Downed23), titles, summaries, 16),
            Trial("onikiri.017.devourer_of_gods", Npc(() => [CWRID.NPC_DevourerofGodsHead], InWorldBossPhase.Downed27), titles, summaries, 17),
            Trial("onikiri.018.yharon", Npc(() => [CWRID.NPC_Yharon], InWorldBossPhase.Downed28), titles, summaries, 18),
            Trial("onikiri.019.exo_mechs", Npc(() => [CWRID.NPC_AresBody, CWRID.NPC_Apollo, CWRID.NPC_Artemis, CWRID.NPC_ThanatosHead], InWorldBossPhase.Downed29), titles, summaries, 19),
            Trial("onikiri.020.supreme_calamitas", Npc(() => [CWRID.NPC_SupremeCalamitas], InWorldBossPhase.Downed30), titles, summaries, 20),
            Trial("onikiri.021.boss_rush", BossRush(bossRushName, eventActiveFormat), titles, summaries, 21),
        ];

        public static LegendTrialDefinition[] CreateHalibut(LocalizedText[] titles = null, Func<int, LocalizedText> summaryProvider = null,
            LocalizedText bossRushName = null, LocalizedText eventActiveFormat = null) => [
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
                BossRush(bossRushName, eventActiveFormat)), titles, summaryProvider, 13),
        ];

        private static LegendTrialDefinition Trial(string key, ILegendTrialTarget target, LocalizedText[] titles, LocalizedText[] summaries, int index) {
            return new LegendTrialDefinition(key, target, titles?.ElementAtOrDefault(index), summaries?.ElementAtOrDefault(index));
        }

        private static LegendTrialDefinition Trial(string key, ILegendTrialTarget target, LocalizedText[] titles, Func<int, LocalizedText> summaryProvider, int index) {
            return new LegendTrialDefinition(key, target, titles?.ElementAtOrDefault(index), summaryProvider?.Invoke(index));
        }

        private static NpcLegendTrialTarget Npc(Func<int[]> npcTypeProvider, Func<bool> completedCheck)
            => new(npcTypeProvider, completedCheck);

        private static EventLegendTrialTarget BossRush(LocalizedText displayName, LocalizedText activeFormat)
            => new(displayName, activeFormat, CWRRef.GetBossRushActive, CWRRef.GetDownedBossRush, () => CWRRef.Has);

        private static CompositeLegendTrialTarget Any(params ILegendTrialTarget[] targets)
            => new(LegendTrialCompositeMode.Any, targets);

        private static CompositeLegendTrialTarget All(params ILegendTrialTarget[] targets)
            => new(LegendTrialCompositeMode.All, targets);
    }
}
