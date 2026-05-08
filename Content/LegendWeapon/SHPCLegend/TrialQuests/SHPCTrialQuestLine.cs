using CalamityOverhaul.Content.ADV.EntrustManager;
using CalamityOverhaul.Content.ADV.Scenarios.Shepel;
using System;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.TrialQuests
{
    internal class SHPCTrialQuestLine : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "Legend";

        private const int TRIAL_COUNT = 22;
        private const string KEY_PREFIX = "SHPC_Trial_";

        #region 本地化文本

        public static LocalizedText QuestCategory { get; private set; }
        public static LocalizedText TrackerWaiting { get; private set; }
        public static LocalizedText TrackerFighting { get; private set; }
        public static LocalizedText TrackerBrief { get; private set; }
        public static LocalizedText[] TrialTitles { get; private set; }
        public static LocalizedText[] TrialSummaries { get; private set; }

        #endregion

        private static int[][] trialTargetNpcs;

        /// <summary>每条试炼独立的完成判定，与等级顺序解耦，以避免乱序击败后试炼仍不能完成的问题</summary>
        private static Func<bool>[] trialCompletedChecks;

        public override void SetStaticDefaults() {
            QuestCategory = this.GetLocalization(nameof(QuestCategory), () => "SHPC·试炼");
            TrackerWaiting = this.GetLocalization(nameof(TrackerWaiting), () => "目标不在场，等待召唤...");
            TrackerFighting = this.GetLocalization(nameof(TrackerFighting), () => "{0}: {1:0%}");
            TrackerBrief = this.GetLocalization(nameof(TrackerBrief), () => "采集目标：{0}");

            TrialTitles = new LocalizedText[TRIAL_COUNT];
            string[] defaultTitles = [
                "眼部解剖",     //0 克苏鲁之眼
                "生化样本",     //1 世界吞噬者/克苏鲁之脑
                "腐血追猎",     //2 腐巢意志/血肉宿主
                "污秽提纯",     //3 史莱姆之神
                "封印突破",     //4 血肉墙
                "深海污染",     //5 渊海灾虫
                "硫火采样",     //6 硫磺火元素
                "机械蠕虫",     //7 毁灭者
                "双眼拆解",     //8 双子魔眼
                "钢铁颅骨",     //9 机械骷髅王
                "灾影分析",     //10 灾厄之影
                "生态考察",     //11 世纪之花
                "远古科技",     //12 石巨人
                "信息封锁",     //13 邪教徒
                "月背秘密",     //14 月球领主
                "地核探险",     //15 亵渎天神
                "幽魂观测",     //16 噬魂幽花
                "神域入侵",     //17 神明吞噬者
                "获取龙羽",     //18 丛林龙犽戎
                "造物主访问",   //19 星流巨械
                "女巫审计",     //20 至尊灾厄
                "终焉大战",     //21 终焉之战
            ];
            for (int i = 0; i < TRIAL_COUNT; i++) {
                int idx = i;
                TrialTitles[i] = this.GetLocalization($"Trial_{i}", () => defaultTitles[idx]);
            }

            TrialSummaries = new LocalizedText[TRIAL_COUNT];
            string[] defaultSummaries = [
                "朋友，复兴文明的任务终于落在了我两手上...第一个目标是那只大眼球，我需要解剖它，看看它是如何实现反重力的",
                "无论那是盘踞腐土的巨虫，还是支配血肉的大脑，去切下它们的核心",
                "邪恶生态再次涌现新的造物——污秽聚合的腐巢意志，或是嗜血成群的血肉宿主，去清除掉它",
                "凝胶居然衍生出了神？让我们对它来一次彻底的提纯分离，看看它还能剩下什么",
                "一道横亘在地狱的血肉长墙，我们需要用足够的火力轰穿这道有机屏障",
                "硫磺之海的巨虫已经浮出水面，把它的吞噬器官拆下来",
                "熔岩深处的硫磺使者正在扩散高热反应，熄灭它的核心火焰",
                "第一台机械目标是一条巨型钢铁蠕虫，把它切成可回收废料",
                "第二个机械目标是那对空中镜像眼球，逐个拆除它们的武装模块",
                "最后一台机械目标戴着旧王的颅骨，把那颗金属头颅轰碎",
                "那个女巫的克隆体在游荡，用它来校准SHPC的灾厄反应模型",
                "丛林地下有几朵妖艳的大花苞已经盛开，我们需要去采集实验资料",
                "神庙深处的远古机器人等待着一次充能启动，顺带逆向出远古科技",
                "地牢门口那群狂热的信徒正在举行某种古老的仪式，打断他们",
                "月亮背面的秘密将被我们知晓，世界将回到原来的样子",
                "寄生在地核中的神明注意到了我们，去取得它的热能利用数据",
                "地牢的怨灵聚集成了庞大的共生体，记录它，然后让它归于沉寂",
                "可以确定它不是碳基生命，它吞噬神明，但我们不是神",
                "世界上仅存的龙裔，哇这太酷了！带我去看看，我要得到它的羽毛",
                "是时候拜访我的造物主了，把星流巨械的控制核心带回来",
                "那个女巫的存在让我感到困扰，终止她的混沌实验",
                "大混战！曾经被我们击败过的敌人联合了起来，准备向我们发起总攻",
            ];
            for (int i = 0; i < TRIAL_COUNT; i++) {
                int idx = i;
                TrialSummaries[i] = this.GetLocalization($"TrialSummary_{i}", () => defaultSummaries[idx]);
            }
        }

        public override void PostSetupContent() {
            trialTargetNpcs = new int[TRIAL_COUNT][];
            //试炼0 (等级0→1): 克苏鲁之眼
            trialTargetNpcs[0] = [NPCID.EyeofCthulhu];
            //试炼1 (等级1→2): 世界吞噬者 / 克苏鲁之脑
            trialTargetNpcs[1] = [NPCID.EaterofWorldsHead, NPCID.BrainofCthulhu];
            //试炼2 (等级2→3): 腐巢意志 / 血肉宿主
            trialTargetNpcs[2] = [CWRID.NPC_HiveMind, CWRID.NPC_PerforatorHive];
            //试炼3 (等级3→4): 史莱姆之神
            trialTargetNpcs[3] = [CWRID.NPC_SlimeGodCore];
            //试炼4 (等级4→5): 血肉墙
            trialTargetNpcs[4] = [NPCID.WallofFlesh];
            //试炼5 (等级5→6): 渊海灾虫
            trialTargetNpcs[5] = [CWRID.NPC_AquaticScourgeHead];
            //试炼6 (等级6→7): 硫磺火元素
            trialTargetNpcs[6] = [CWRID.NPC_BrimstoneElemental];
            //试炼7 (等级7→8): 毁灭者
            trialTargetNpcs[7] = [NPCID.TheDestroyer];
            //试炼8 (等级8→9): 双子魔眼
            trialTargetNpcs[8] = [NPCID.Retinazer, NPCID.Spazmatism];
            //试炼9 (等级9→10): 机械骷髅王
            trialTargetNpcs[9] = [NPCID.SkeletronPrime];
            //试炼10 (等级10→11): 灾厄之影
            trialTargetNpcs[10] = [CWRID.NPC_CalamitasClone];
            //试炼11 (等级11→12): 世纪之花
            trialTargetNpcs[11] = [NPCID.Plantera];
            //试炼12 (等级12→13): 石巨人
            trialTargetNpcs[12] = [NPCID.Golem, NPCID.GolemHead];
            //试炼13 (等级13→14): 邪教徒
            trialTargetNpcs[13] = [NPCID.CultistBoss];
            //试炼14 (等级14→15): 月球领主
            trialTargetNpcs[14] = [NPCID.MoonLordCore];
            //试炼15 (等级15→16): 亵渎天神
            trialTargetNpcs[15] = [CWRID.NPC_Providence];
            //试炼16 (等级16→17): 噬魂幽花
            trialTargetNpcs[16] = [CWRID.NPC_Polterghast];
            //试炼17 (等级17→18): 神明吞噬者
            trialTargetNpcs[17] = [CWRID.NPC_DevourerofGodsHead];
            //试炼18 (等级18→19): 丛林龙犽戎
            trialTargetNpcs[18] = [CWRID.NPC_Yharon];
            //试炼19 (等级19→20): 星流巨械（追踪血量最低的一台）
            trialTargetNpcs[19] = [CWRID.NPC_AresBody, CWRID.NPC_Apollo,
                CWRID.NPC_Artemis, CWRID.NPC_ThanatosHead];
            //试炼20 (等级20→21): 至尊灾厄
            trialTargetNpcs[20] = [CWRID.NPC_SupremeCalamitas];
            //试炼21 (等级21→22): 终焉之战，无可追踪的固定NPC，完成后等级直接变22
            trialTargetNpcs[21] = [];

            //以下完成判定与 InWorldBossPhase.SHPC_Level() 中的跳级条件一一对应，仅去除“前置全部达成”的顺序锁
            trialCompletedChecks = new Func<bool>[TRIAL_COUNT];
            trialCompletedChecks[0] = InWorldBossPhase.DownedV1;
            trialCompletedChecks[1] = InWorldBossPhase.DownedV2;
            trialCompletedChecks[2] = () => InWorldBossPhase.Downed3.Invoke() || InWorldBossPhase.Downed4.Invoke();
            trialCompletedChecks[3] = InWorldBossPhase.Downed5;
            trialCompletedChecks[4] = () => Main.hardMode;
            trialCompletedChecks[5] = InWorldBossPhase.Downed8;
            trialCompletedChecks[6] = InWorldBossPhase.Downed7;
            trialCompletedChecks[7] = () => NPC.downedMechBoss1;
            trialCompletedChecks[8] = () => NPC.downedMechBoss2;
            trialCompletedChecks[9] = () => NPC.downedMechBoss3;
            trialCompletedChecks[10] = InWorldBossPhase.Downed10;
            trialCompletedChecks[11] = InWorldBossPhase.VDownedV7;
            trialCompletedChecks[12] = InWorldBossPhase.DownedV7;
            trialCompletedChecks[13] = InWorldBossPhase.DownedV8;
            trialCompletedChecks[14] = InWorldBossPhase.VDownedV16;
            trialCompletedChecks[15] = InWorldBossPhase.Downed19;
            trialCompletedChecks[16] = InWorldBossPhase.Downed23;
            trialCompletedChecks[17] = InWorldBossPhase.Downed27;
            trialCompletedChecks[18] = InWorldBossPhase.Downed28;
            trialCompletedChecks[19] = InWorldBossPhase.Downed29;
            trialCompletedChecks[20] = InWorldBossPhase.Downed30;
            trialCompletedChecks[21] = InWorldBossPhase.Downed32;
        }

        public override void PostUpdateEverything() {
            if (Main.dedServ || Main.gameMenu) return;

            var manager = QuestManagerUI.Instance;
            if (manager == null) return;

            bool canStart = FirstMetShepel.CanStartSHPCTrialQuests(Main.LocalPlayer);
            int level = InWorldBossPhase.SHPC_Level();

            for (int i = 0; i < TRIAL_COUNT; i++) {
                SyncTrial(manager, i, level, canStart);
            }
        }

        private void SyncTrial(QuestManagerUI manager, int trialIndex, int currentLevel, bool allowCreate = true) {
            string key = KEY_PREFIX + trialIndex;

            bool isDone = (trialCompletedChecks[trialIndex]?.Invoke() == true) || (trialIndex < currentLevel);

            if (trialIndex > currentLevel) {
                manager.UnregisterQuest(key);
            }
            else if (isDone) {
                var entry = EnsureTrialEntry(manager, trialIndex, completed: true, allowCreate: allowCreate);
                if (entry != null && entry.Status != QuestEntryStatus.Completed) {
                    manager.SetEntryStatus(key, QuestEntryStatus.Completed, 1f);
                }
            }
            else {
                var entry = EnsureTrialEntry(manager, trialIndex, allowCreate: allowCreate);
                if (entry == null) {
                    return;
                }
                else if (entry.Status == QuestEntryStatus.Completed) {
                    manager.SetEntryStatus(key, QuestEntryStatus.Active, 0f);
                }
            }
        }

        private SHPCTrialQuestEntry EnsureTrialEntry(QuestManagerUI manager, int trialIndex, bool completed = false, bool allowCreate = true) {
            string key = KEY_PREFIX + trialIndex;
            var entry = manager.GetEntry(key) as SHPCTrialQuestEntry;
            if (entry != null) return entry;
            if (!allowCreate) return null;

            entry = CreateTrialEntry(trialIndex);
            if (completed) {
                entry.Status = QuestEntryStatus.Completed;
                entry.Progress = 1f;
            }
            manager.RegisterQuest(entry);
            return entry;
        }

        private SHPCTrialQuestEntry CreateTrialEntry(int trialIndex) {
            return new SHPCTrialQuestEntry(KEY_PREFIX + trialIndex,
                TrialTitles[trialIndex], TrialSummaries[trialIndex], QuestCategory) {
                Priority = TRIAL_COUNT - trialIndex,
                EntryStyle = new SHPCEntryStyle(),
                TrackerStyle = new SHPCTrackerWidgetStyle(),
                TargetNpcTypes = trialTargetNpcs[trialIndex],
                IsCompletedCheck = trialCompletedChecks[trialIndex],
                WaitingHint = TrackerWaiting,
                FightingFormat = TrackerFighting,
                BriefFormat = TrackerBrief,
                //左侧追踪窗口仅在玩家手持SHPC时显示，避免常驻打扰
                TrackerVisibilityCheck = static () => Main.LocalPlayer.GetItem().type == SHPCOverride.ID,
            };
        }
    }
}
