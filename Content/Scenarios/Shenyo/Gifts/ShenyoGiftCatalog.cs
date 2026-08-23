using System;
using System.Collections.Generic;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Shenyo.Gifts
{
    /// <summary>
    /// 沉宴礼物名册一项：只描述"哪场戏、序号对应沉宴试炼线第几关、系于哪些首领"。<br/>
    /// 完成位不在此登记，那是场景自己的 IsGiftCompleted / MarkGiftCompleted，只留一个读写入口
    /// </summary>
    internal sealed class ShenyoGiftEntry
    {
        private readonly Func<int[]> targetBossIds;
        private int[] resolvedTargetBossIds;

        public int Order { get; }
        public string Id { get; }
        public Type ScenarioType { get; }
        public int[] TargetBossIds => resolvedTargetBossIds ??= targetBossIds() ?? [];

        public ShenyoGiftEntry(int order, string id, Type scenarioType, Func<int[]> targetBossIds) {
            Order = order;
            Id = id;
            ScenarioType = scenarioType;
            this.targetBossIds = targetBossIds;
        }
    }

    /// <summary>
    /// 沉宴礼物线的24个条目，顺序与首领绑定严格对照
    /// <see cref="LegendWeapon.TrialQuests.LegendTrialRouteCatalog.CreateKikasa"/> 的24关试炼<br/>
    /// 触发能否播放不在此判定，交由 <see cref="ShenyoBossGiftNarrative.CanSpawned"/> 直接读试炼线的完成位
    /// </summary>
    internal static class ShenyoGiftCatalog
    {
        public const int GiftCount = 24;

        private static readonly ShenyoGiftEntry[] entries = [
            Gift<ShenyoKingSlimeGift>(0, "KingSlime", () => [NPCID.KingSlime]),
            Gift<ShenyoEyeOfCthulhuGift>(1, "EyeOfCthulhu", () => [NPCID.EyeofCthulhu]),
            Gift<ShenyoEvilBossGift>(2, "EvilBoss", () => [NPCID.EaterofWorldsHead, NPCID.BrainofCthulhu]),
            Gift<ShenyoCalamityEvilGift>(3, "CalamityEvil", () => [CWRID.NPC_HiveMind, CWRID.NPC_PerforatorHive]),
            Gift<ShenyoQueenBeeOrDeerclopsGift>(4, "QueenBeeOrDeerclops", () => [NPCID.QueenBee, NPCID.Deerclops]),
            Gift<ShenyoSkeletronGift>(5, "Skeletron", () => [NPCID.SkeletronHead]),
            Gift<ShenyoSlimeGodGift>(6, "SlimeGod", () => [CWRID.NPC_SlimeGodCore]),
            Gift<ShenyoWallOfFleshGift>(7, "WallOfFlesh", () => [NPCID.WallofFlesh]),
            Gift<ShenyoQueenSlimeGift>(8, "QueenSlime", () => [NPCID.QueenSlimeBoss]),
            Gift<ShenyoAquaticScourgeGift>(9, "AquaticScourge", () => [CWRID.NPC_AquaticScourgeHead]),
            Gift<ShenyoMechsGift>(10, "Mechs", () =>
                [NPCID.TheDestroyer, NPCID.Retinazer, NPCID.Spazmatism, NPCID.SkeletronPrime]),
            Gift<ShenyoPlanteraGift>(11, "Plantera", () => [NPCID.Plantera]),
            Gift<ShenyoLeviathanGift>(12, "Leviathan", () => [CWRID.NPC_Leviathan, CWRID.NPC_Anahita]),
            Gift<ShenyoGolemGift>(13, "Golem", () => [NPCID.Golem, NPCID.GolemHead]),
            Gift<ShenyoDukeFishronGift>(14, "DukeFishron", () => [NPCID.DukeFishron]),
            Gift<ShenyoEmpressGift>(15, "Empress", () => [NPCID.HallowBoss]),
            Gift<ShenyoCultistGift>(16, "Cultist", () => [NPCID.CultistBoss]),
            Gift<ShenyoMoonLordGift>(17, "MoonLord", () => [NPCID.MoonLordCore]),
            Gift<ShenyoPolterghastGift>(18, "Polterghast", () => [CWRID.NPC_Polterghast]),
            Gift<ShenyoOldDukeGift>(19, "OldDuke", () => [CWRID.NPC_OldDuke]),
            Gift<ShenyoDevourerOfGodsGift>(20, "DevourerOfGods", () => [CWRID.NPC_DevourerofGodsHead]),
            Gift<ShenyoYharonGift>(21, "Yharon", () => [CWRID.NPC_Yharon]),
            Gift<ShenyoExoAndSCalGift>(22, "ExoAndSCal", () =>
                [CWRID.NPC_AresBody, CWRID.NPC_Apollo, CWRID.NPC_Artemis, CWRID.NPC_ThanatosHead, CWRID.NPC_SupremeCalamitas]),
            //终章双路径：BossRush本身不是击杀事件，靠Tracker的边沿检测；始源妖龙走正常击杀路径
            Gift<ShenyoBossRushGift>(23, "BossRush", () => [CWRID.NPC_PrimordialWyrmHead]),
        ];

        private static readonly Dictionary<string, ShenyoGiftEntry> byId = CreateIdMap();
        private static readonly Dictionary<Type, ShenyoGiftEntry> byScenarioType = CreateScenarioMap();

        static ShenyoGiftCatalog() {
            if (entries.Length != GiftCount) {
                throw new InvalidOperationException($"Shenyo gift catalog count is {entries.Length}, expected {GiftCount}.");
            }
            for (int i = 0; i < entries.Length; i++) {
                if (entries[i].Order != i) {
                    throw new InvalidOperationException(
                        $"Shenyo gift catalog order {entries[i].Order} is invalid at index {i}.");
                }
            }
        }

        public static IReadOnlyList<ShenyoGiftEntry> All => entries;

        public static bool TryGet(string id, out ShenyoGiftEntry entry) {
            if (id != null && byId.TryGetValue(id, out entry)) {
                return true;
            }
            entry = null;
            return false;
        }

        public static bool TryGet(Type scenarioType, out ShenyoGiftEntry entry) {
            if (scenarioType != null && byScenarioType.TryGetValue(scenarioType, out entry)) {
                return true;
            }
            entry = null;
            return false;
        }

        private static ShenyoGiftEntry Gift<TScenario>(int order, string id, Func<int[]> targetBossIds)
            where TScenario : ShenyoBossGiftNarrative
            => new(order, id, typeof(TScenario), targetBossIds);

        private static Dictionary<string, ShenyoGiftEntry> CreateIdMap() {
            Dictionary<string, ShenyoGiftEntry> map = new(StringComparer.Ordinal);
            for (int i = 0; i < entries.Length; i++) {
                map.Add(entries[i].Id, entries[i]);
            }
            return map;
        }

        private static Dictionary<Type, ShenyoGiftEntry> CreateScenarioMap() {
            Dictionary<Type, ShenyoGiftEntry> map = [];
            for (int i = 0; i < entries.Length; i++) {
                map.Add(entries[i].ScenarioType, entries[i]);
            }
            return map;
        }
    }
}
