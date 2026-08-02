using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using InnoVault.Narrative.Styling;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    internal sealed class HimayoGiftEntry
    {
        private readonly Func<int> rubbingItemType;
        private readonly Func<int[]> targetBossIds;
        private readonly Func<HimayoGiftStoryData, bool> completed;
        private readonly Action<HimayoGiftStoryData, bool> setCompleted;
        private int[] resolvedTargetBossIds;

        public int Order { get; }
        public string MeiKey { get; }
        public Type ScenarioType { get; }
        public int RubbingItemType => rubbingItemType();
        public int[] TargetBossIds => resolvedTargetBossIds ??= targetBossIds() ?? [];

        public HimayoGiftEntry(int order, string meiKey, Type scenarioType, Func<int> rubbingItemType,
            Func<int[]> targetBossIds,
            Func<HimayoGiftStoryData, bool> completed, Action<HimayoGiftStoryData, bool> setCompleted) {
            Order = order;
            MeiKey = meiKey;
            ScenarioType = scenarioType;
            this.rubbingItemType = rubbingItemType;
            this.targetBossIds = targetBossIds;
            this.completed = completed;
            this.setCompleted = setCompleted;
        }

        public bool IsCompleted(HimayoGiftStoryData data) => data != null && completed(data);

        public void SetCompleted(HimayoGiftStoryData data, bool value) {
            if (data != null) {
                setCompleted(data, value);
            }
        }
    }

    internal static class HimayoGiftCatalog
    {
        public const int GiftCount = 22;

        private static readonly HimayoGiftEntry[] entries = [
            Gift<HimayoEyeOfCthulhuGift, OniMeiRubbingHigekiri>(0, nameof(MeiHigekiri),
                () => [NPCID.EyeofCthulhu],
                d => d.EyeOfCthulhuGift, (d, value) => d.EyeOfCthulhuGift = value),
            Gift<HimayoEvilBossGift, OniMeiRubbingChihi>(1, nameof(MeiChihi),
                () => [NPCID.EaterofWorldsHead, NPCID.BrainofCthulhu],
                d => d.EvilBossGift, (d, value) => d.EvilBossGift = value),
            Gift<HimayoCalamityEvilGift, OniMeiRubbingKazehi>(2, nameof(MeiKazehi),
                () => [CWRID.NPC_HiveMind, CWRID.NPC_PerforatorHive],
                d => d.CalamityEvilGift, (d, value) => d.CalamityEvilGift = value),
            Gift<HimayoSlimeGodGift, OniMeiRubbingTodohi>(3, nameof(MeiTodohi),
                () => [CWRID.NPC_SlimeGodCore],
                d => d.SlimeGodGift, (d, value) => d.SlimeGodGift = value),
            Gift<HimayoWallOfFleshGift, OniMeiRubbingTomokiri>(4, nameof(MeiTomokiri),
                () => [NPCID.WallofFlesh],
                d => d.WallOfFleshGift, (d, value) => d.WallOfFleshGift = value),
            Gift<HimayoAquaticScourgeGift, OniMeiRubbingFudo>(5, nameof(MeiFudo),
                () => [CWRID.NPC_AquaticScourgeHead],
                d => d.AquaticScourgeGift, (d, value) => d.AquaticScourgeGift = value),
            Gift<HimayoBrimstoneElementalGift, OniMeiRubbingKogehi>(6, nameof(MeiKogehi),
                () => [CWRID.NPC_BrimstoneElemental],
                d => d.BrimstoneElementalGift, (d, value) => d.BrimstoneElementalGift = value),
            Gift<HimayoDestroyerGift, OniMeiRubbingTessetsu>(7, nameof(MeiTessetsu),
                () => [NPCID.TheDestroyer],
                d => d.DestroyerGift, (d, value) => d.DestroyerGift = value),
            Gift<HimayoTwinsGift, OniMeiRubbingIkiai>(8, nameof(MeiIkiai),
                () => [NPCID.Retinazer, NPCID.Spazmatism],
                d => d.TwinsGift, (d, value) => d.TwinsGift = value),
            Gift<HimayoSkeletronPrimeGift, OniMeiRubbingKyushu>(9, nameof(MeiKyushu),
                () => [NPCID.SkeletronPrime],
                d => d.SkeletronPrimeGift, (d, value) => d.SkeletronPrimeGift = value),
            Gift<HimayoCalamitasCloneGift, OniMeiRubbingKarikiri>(10, nameof(MeiKarikiri),
                () => [CWRID.NPC_CalamitasClone],
                d => d.CalamitasCloneGift, (d, value) => d.CalamitasCloneGift = value),
            Gift<HimayoPlanteraGift, OniMeiRubbingShiorihi>(11, nameof(MeiShiorihi),
                () => [NPCID.Plantera],
                d => d.PlanteraGift, (d, value) => d.PlanteraGift = value),
            Gift<HimayoGolemGift, OniMeiRubbingShibori>(12, nameof(MeiShibori),
                () => [NPCID.Golem, NPCID.GolemHead],
                d => d.GolemGift, (d, value) => d.GolemGift = value),
            Gift<HimayoCultistGift, OniMeiRubbingKanhi>(13, nameof(MeiKanhi),
                () => [NPCID.CultistBoss],
                d => d.CultistGift, (d, value) => d.CultistGift = value),
            Gift<HimayoMoonLordGift, OniMeiRubbingShishinoko>(14, nameof(MeiShishinoko),
                () => [NPCID.MoonLordCore],
                d => d.MoonLordGift, (d, value) => d.MoonLordGift = value),
            Gift<HimayoProvidenceGift, OniMeiRubbingKurikara>(15, nameof(MeiKurikara),
                () => [CWRID.NPC_Providence],
                d => d.ProvidenceGift, (d, value) => d.ProvidenceGift = value),
            Gift<HimayoPolterghastGift, OniMeiRubbingShiohi>(16, nameof(MeiShiohi),
                () => [CWRID.NPC_Polterghast],
                d => d.PolterghastGift, (d, value) => d.PolterghastGift = value),
            Gift<HimayoDevourerOfGodsGift, OniMeiRubbingKyoko>(17, nameof(MeiKyoko),
                () => [CWRID.NPC_DevourerofGodsHead],
                d => d.DevourerOfGodsGift, (d, value) => d.DevourerOfGodsGift = value),
            Gift<HimayoYharonGift, OniMeiRubbingYoen>(18, nameof(MeiYoen),
                () => [CWRID.NPC_Yharon],
                d => d.YharonGift, (d, value) => d.YharonGift = value),
            Gift<HimayoExoMechsGift, OniMeiRubbingChinmei>(19, nameof(MeiChinmei),
                () => [CWRID.NPC_AresBody, CWRID.NPC_Apollo, CWRID.NPC_Artemis, CWRID.NPC_ThanatosHead],
                d => d.ExoMechsGift, (d, value) => d.ExoMechsGift = value),
            Gift<HimayoSupremeCalamitasGift, OniMeiRubbingMokukiri>(20, nameof(MeiMokukiri),
                () => [CWRID.NPC_SupremeCalamitas],
                d => d.SupremeCalamitasGift, (d, value) => d.SupremeCalamitasGift = value),
            Gift<HimayoBossRushGift, OniMeiRubbingAshidome>(21, nameof(MeiAshidome),
                () => [],
                d => d.BossRushGift, (d, value) => d.BossRushGift = value),
        ];

        private static readonly Dictionary<string, HimayoGiftEntry> byKey = CreateKeyMap();
        private static readonly Dictionary<Type, HimayoGiftEntry> byScenarioType = CreateScenarioMap();

        static HimayoGiftCatalog() {
            if (entries.Length != GiftCount) {
                throw new InvalidOperationException($"Himayo gift catalog count is {entries.Length}, expected {GiftCount}.");
            }
            for (int i = 0; i < entries.Length; i++) {
                if (entries[i].Order != i) {
                    throw new InvalidOperationException(
                        $"Himayo gift catalog order {entries[i].Order} is invalid at index {i}.");
                }
            }
        }

        public static IReadOnlyList<HimayoGiftEntry> All => entries;

        public static bool TryGet(string key, out HimayoGiftEntry entry) {
            if (key != null && byKey.TryGetValue(key, out entry)) {
                return true;
            }
            entry = null;
            return false;
        }

        public static bool TryGet(Type scenarioType, out HimayoGiftEntry entry) {
            if (scenarioType != null && byScenarioType.TryGetValue(scenarioType, out entry)) {
                return true;
            }
            entry = null;
            return false;
        }

        public static bool TryResolveKey(string key, out HimayoGiftEntry entry) {
            if (TryGet(key, out entry)) {
                return true;
            }
            for (int i = 0; i < entries.Length; i++) {
                if (string.Equals(entries[i].MeiKey, key, StringComparison.OrdinalIgnoreCase)) {
                    entry = entries[i];
                    return true;
                }
            }
            entry = null;
            return false;
        }

        public static void Sanitize(HimayoGiftStoryData data) {
            if (data == null) {
                return;
            }

            HashSet<string> seen = new(StringComparer.Ordinal);
            List<string> clean = [];
            if (data.PendingGiftKeys != null) {
                for (int i = 0; i < data.PendingGiftKeys.Count && clean.Count < GiftCount; i++) {
                    string key = data.PendingGiftKeys[i];
                    if (TryGet(key, out HimayoGiftEntry entry) && !entry.IsCompleted(data) && seen.Add(entry.MeiKey)) {
                        clean.Add(entry.MeiKey);
                    }
                }
            }
            clean.Sort((left, right) => byKey[left].Order.CompareTo(byKey[right].Order));
            data.PendingGiftKeys = clean;

            bool evilGiftPending = clean.Contains(nameof(MeiChihi)) && !data.EvilBossGift;
            if (!evilGiftPending || data.EvilBossGiftBossId != NPCID.EaterofWorldsHead
                && data.EvilBossGiftBossId != NPCID.BrainofCthulhu) {
                data.EvilBossGiftBossId = 0;
            }
        }

        public static bool IsTargetBoss(int npcType) {
            if (npcType <= 0) {
                return false;
            }
            for (int i = 0; i < entries.Length; i++) {
                int[] ids = entries[i].TargetBossIds;
                for (int j = 0; j < ids.Length; j++) {
                    if (ids[j] > 0 && ids[j] == npcType) {
                        return true;
                    }
                }
            }
            return false;
        }

        public static bool IsWorldConditionMet(HimayoGiftEntry entry) {
            if (entry == null) {
                return false;
            }

            return entry.MeiKey switch {
                nameof(MeiHigekiri) => NPC.downedBoss1,
                nameof(MeiChihi) => NPC.downedBoss2,
                nameof(MeiKazehi) => CWRRef.GetDownedHiveMind() || CWRRef.GetDownedPerforator(),
                nameof(MeiTodohi) => CWRRef.GetDownedSlimeGod(),
                nameof(MeiTomokiri) => NPC.downedBoss3,
                nameof(MeiFudo) => CWRRef.GetDownedAquaticScourge(),
                nameof(MeiKogehi) => CWRRef.GetDownedBrimstoneElemental(),
                nameof(MeiTessetsu) => NPC.downedMechBoss1,
                nameof(MeiIkiai) => NPC.downedMechBoss2,
                nameof(MeiKyushu) => NPC.downedMechBoss3,
                nameof(MeiKarikiri) => CWRRef.GetDownedCalamitasClone(),
                nameof(MeiShiorihi) => NPC.downedPlantBoss,
                nameof(MeiShibori) => NPC.downedGolemBoss,
                nameof(MeiKanhi) => NPC.downedAncientCultist,
                nameof(MeiShishinoko) => NPC.downedMoonlord,
                nameof(MeiKurikara) => CWRRef.GetDownedProvidence(),
                nameof(MeiShiohi) => CWRRef.GetDownedPolterghast(),
                nameof(MeiKyoko) => CWRRef.GetDownedDoG(),
                nameof(MeiYoen) => CWRRef.GetDownedYharon(),
                nameof(MeiChinmei) => CWRRef.GetDownedExoMechs(),
                nameof(MeiMokukiri) => CWRRef.GetDownedCalamitas(),
                nameof(MeiAshidome) => CWRRef.GetDownedBossRush(),
                _ => false,
            };
        }

        private static HimayoGiftEntry Gift<TScenario, TRubbing>(int order, string meiKey,
            Func<int[]> targetBossIds,
            Func<HimayoGiftStoryData, bool> completed, Action<HimayoGiftStoryData, bool> setCompleted)
            where TScenario : HimayoBossGiftNarrative
            where TRubbing : OniMeiRubbingItem
            => new(order, meiKey, typeof(TScenario), () => ModContent.ItemType<TRubbing>(),
                targetBossIds, completed, setCompleted);

        private static Dictionary<string, HimayoGiftEntry> CreateKeyMap() {
            Dictionary<string, HimayoGiftEntry> map = new(StringComparer.Ordinal);
            for (int i = 0; i < entries.Length; i++) {
                map.Add(entries[i].MeiKey, entries[i]);
            }
            return map;
        }

        private static Dictionary<Type, HimayoGiftEntry> CreateScenarioMap() {
            Dictionary<Type, HimayoGiftEntry> map = [];
            for (int i = 0; i < entries.Length; i++) {
                map.Add(entries[i].ScenarioType, entries[i]);
            }
            return map;
        }
    }

    internal sealed class HimayoGiftRewardPayload : PopupPayload
    {
        private readonly string giftKey;

        public HimayoGiftRewardPayload(string giftKey) {
            this.giftKey = giftKey;
            Title = string.Empty;
        }

        public override int IconItemType
            => HimayoGiftCatalog.TryGet(giftKey, out HimayoGiftEntry entry) ? entry.RubbingItemType : 0;

        public override void OnClaimed(Player player) {
            if (HimayoStorySync.TryClaimGift(player, giftKey)) {
                NarrativeAudioDefaults.Play(NarrativeAudioDefaults.RewardGrant);
            }
        }
    }

    internal static class HimayoGiftComposerExtensions
    {
        public static NarrativeComposer GiftReward(this NarrativeComposer composer, string giftKey)
            => composer.Popup(new HimayoGiftRewardPayload(giftKey), blocking: true);
    }
}
