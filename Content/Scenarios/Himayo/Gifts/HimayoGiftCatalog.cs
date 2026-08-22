using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions;
using InnoVault.Narrative.Composition;
using System;
using System.Collections.Generic;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    /// <summary>
    /// 赠礼名册一项：只描述"哪场戏、系于哪些首领、递哪张拓本"。<br/>
    /// 完成位不在此登记，那是场景自己的 IsGiftCompleted / MarkGiftCompleted，只留一个读写入口
    /// </summary>
    internal sealed class HimayoGiftEntry
    {
        private readonly Func<int> rubbingItemType;
        private readonly Func<int[]> targetBossIds;
        private int[] resolvedTargetBossIds;

        public int Order { get; }
        public string MeiKey { get; }
        public Type ScenarioType { get; }
        public int RubbingItemType => rubbingItemType();
        public int[] TargetBossIds => resolvedTargetBossIds ??= targetBossIds() ?? [];

        public HimayoGiftEntry(int order, string meiKey, Type scenarioType, Func<int> rubbingItemType,
            Func<int[]> targetBossIds) {
            Order = order;
            MeiKey = meiKey;
            ScenarioType = scenarioType;
            this.rubbingItemType = rubbingItemType;
            this.targetBossIds = targetBossIds;
        }
    }

    internal static class HimayoGiftCatalog
    {
        public const int GiftCount = 22;

        private static readonly HimayoGiftEntry[] entries = [
            Gift<HimayoEyeOfCthulhuGift, OniMeiRubbingHigekiri>(0, nameof(MeiHigekiri),
                () => [NPCID.EyeofCthulhu]),
            Gift<HimayoEvilBossGift, OniMeiRubbingChihi>(1, nameof(MeiChihi),
                () => [NPCID.EaterofWorldsHead, NPCID.BrainofCthulhu]),
            Gift<HimayoCalamityEvilGift, OniMeiRubbingKazehi>(2, nameof(MeiKazehi),
                () => [CWRID.NPC_HiveMind, CWRID.NPC_PerforatorHive]),
            Gift<HimayoSlimeGodGift, OniMeiRubbingTodohi>(3, nameof(MeiTodohi),
                () => [CWRID.NPC_SlimeGodCore]),
            Gift<HimayoWallOfFleshGift, OniMeiRubbingTomokiri>(4, nameof(MeiTomokiri),
                () => [NPCID.WallofFlesh]),
            Gift<HimayoAquaticScourgeGift, OniMeiRubbingFudo>(5, nameof(MeiFudo),
                () => [CWRID.NPC_AquaticScourgeHead]),
            Gift<HimayoBrimstoneElementalGift, OniMeiRubbingKogehi>(6, nameof(MeiKogehi),
                () => [CWRID.NPC_BrimstoneElemental]),
            Gift<HimayoDestroyerGift, OniMeiRubbingTessetsu>(7, nameof(MeiTessetsu),
                () => [NPCID.TheDestroyer]),
            Gift<HimayoTwinsGift, OniMeiRubbingIkiai>(8, nameof(MeiIkiai),
                () => [NPCID.Retinazer, NPCID.Spazmatism]),
            Gift<HimayoSkeletronPrimeGift, OniMeiRubbingKyushu>(9, nameof(MeiKyushu),
                () => [NPCID.SkeletronPrime]),
            Gift<HimayoCalamitasCloneGift, OniMeiRubbingKarikiri>(10, nameof(MeiKarikiri),
                () => [CWRID.NPC_CalamitasClone]),
            Gift<HimayoPlanteraGift, OniMeiRubbingShiorihi>(11, nameof(MeiShiorihi),
                () => [NPCID.Plantera]),
            Gift<HimayoGolemGift, OniMeiRubbingShibori>(12, nameof(MeiShibori),
                () => [NPCID.Golem, NPCID.GolemHead]),
            Gift<HimayoCultistGift, OniMeiRubbingKanhi>(13, nameof(MeiKanhi),
                () => [NPCID.CultistBoss]),
            Gift<HimayoMoonLordGift, OniMeiRubbingShishinoko>(14, nameof(MeiShishinoko),
                () => [NPCID.MoonLordCore]),
            Gift<HimayoProvidenceGift, OniMeiRubbingKurikara>(15, nameof(MeiKurikara),
                () => [CWRID.NPC_Providence]),
            Gift<HimayoPolterghastGift, OniMeiRubbingShiohi>(16, nameof(MeiShiohi),
                () => [CWRID.NPC_Polterghast]),
            Gift<HimayoDevourerOfGodsGift, OniMeiRubbingKyoko>(17, nameof(MeiKyoko),
                () => [CWRID.NPC_DevourerofGodsHead]),
            Gift<HimayoYharonGift, OniMeiRubbingYoen>(18, nameof(MeiYoen),
                () => [CWRID.NPC_Yharon]),
            Gift<HimayoExoMechsGift, OniMeiRubbingChinmei>(19, nameof(MeiChinmei),
                () => [CWRID.NPC_AresBody, CWRID.NPC_Apollo, CWRID.NPC_Artemis, CWRID.NPC_ThanatosHead]),
            Gift<HimayoSupremeCalamitasGift, OniMeiRubbingMokukiri>(20, nameof(MeiMokukiri),
                () => [CWRID.NPC_SupremeCalamitas]),
            Gift<HimayoBossRushGift, OniMeiRubbingAshidome>(21, nameof(MeiAshidome),
                () => []),
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

        private static HimayoGiftEntry Gift<TScenario, TRubbing>(int order, string meiKey,
            Func<int[]> targetBossIds)
            where TScenario : HimayoBossGiftNarrative
            where TRubbing : OniMeiRubbingItem
            => new(order, meiKey, typeof(TScenario), () => ModContent.ItemType<TRubbing>(), targetBossIds);

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

    internal static class HimayoGiftComposerExtensions
    {
        /// <summary>
        /// 递拓本：走框架自带的奖励弹窗与发放服务（优先进背包，装不下才落地），<br/>
        /// 不做成败判定，拓本能否进包与"这场戏演过了"无关
        /// </summary>
        public static NarrativeComposer GiftReward(this NarrativeComposer composer, string giftKey)
            => HimayoGiftCatalog.TryGet(giftKey, out HimayoGiftEntry entry)
                ? composer.Reward(entry.RubbingItemType, title: string.Empty)
                : composer;
    }
}
