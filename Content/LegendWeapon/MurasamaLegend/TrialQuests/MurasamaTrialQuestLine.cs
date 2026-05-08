using CalamityOverhaul.Content.ADV.EntrustManager;
using System;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.MurasamaLegend.TrialQuests
{
    /// <summary>
    /// 鬼妖村正试炼线——将15段试炼注册到 <see cref="QuestManagerUI"/>，
    /// 并根据 <see cref="InWorldBossPhase.Mura_Level"/> 实时同步状态。<br/>
    /// 同时显示当前进行中的试炼和所有已完成的试炼
    /// </summary>
    internal class MurasamaTrialQuestLine : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "Legend";

        /// <summary>试炼总数（对应等级0-13的试炼，等级14为全部完成）</summary>
        private const int TRIAL_COUNT = 28;
        private const string KEY_PREFIX = "Mura_Trial_";

        #region 本地化文本

        public static LocalizedText QuestCategory { get; private set; }
        public static LocalizedText TrackerWaiting { get; private set; }
        public static LocalizedText TrackerFighting { get; private set; }
        public static LocalizedText TrackerBrief { get; private set; }

        /// <summary>每条试炼的标题</summary>
        public static LocalizedText[] TrialTitles { get; private set; }
        public static LocalizedText[] TrialSummaries { get; private set; }

        #endregion

        /// <summary>每条试炼对应需要击杀的Boss NPC type列表</summary>
        private static int[][] trialTargetNpcs;

        /// <summary>每条试炼独立的完成判定，与等级顺序解耦，以避免乱序击败后试炼仍不能完成的问题</summary>
        private static Func<bool>[] trialCompletedChecks;

        public override void SetStaticDefaults() {
            QuestCategory = this.GetLocalization(nameof(QuestCategory), () => "鬼妖村正·试炼");
            TrackerWaiting = this.GetLocalization(nameof(TrackerWaiting), () => "目标不在场，等待召唤...");
            TrackerFighting = this.GetLocalization(nameof(TrackerFighting), () => "{0}: {1:0%}");
            TrackerBrief = this.GetLocalization(nameof(TrackerBrief), () => "猎杀目标：{0}");

            TrialTitles = new LocalizedText[TRIAL_COUNT];

            //标题风格参考MGSV:TPP，以军事行动代号的口吻
            string[] defaultTitles = [
                "沙地幽影",         //0 史莱姆王
                "沙海猎杀",         //1 荒漠灾虫
                "入侵之者",         //2 克苏鲁之眼
                "腐化清除",         //3 世吞/克脑
                "寄生威胁",         //4 腐巢意志/血肉宿主
                "地牢侵破",         //5 骷髅王
                "凝胶秽神",         //6 史莱姆之神
                "通向地狱",         //7 血肉墙
                "渊海清扫",         //8 渊海灾虫
                "硫磺净炎",         //9 硫磺火元素
                "冰原桎梏",         //10 极地冰灵
                "钢铁齿轮",         //11 毁灭者
                "双瞳歼灭",         //12 双子魔眼
                "机械王颅",         //13 机械骷髅王
                "灾影行动",         //14 灾厄之影
                "丛林之花",         //15 世纪之花
                "石化阵线",         //16 石巨人
                "瘟疫清除",         //17 瘟疫使者
                "魔像破防",         //18 毁灭魔像
                "星域终结",         //19 星神游龙
                "月球坠落",         //20 月球领主
                "幻影制裁",         //21 亵渎天神
                "幽魂祛除",         //22 噬魂幽花
                "弑神之蛇",         //23 神明吞噬者
                "丛林之炎",         //24 丛林龙犽戎
                "核心终结",         //25 星流巨械
                "混沌裁决",         //26 至尊灾厄
                "原初回归",         //27 始源妖龙
            ];
            for (int i = 0; i < TRIAL_COUNT; i++) {
                int idx = i;
                TrialTitles[i] = this.GetLocalization($"Trial_{i}", () => defaultTitles[idx]);
            }

            TrialSummaries = new LocalizedText[TRIAL_COUNT];
            string[] defaultSummaries = [
                "新兵，向我证明你自己\n那个黏糊糊的蓝胖子是个好的起点\n解决它",
                "沙海之下的海妖余孽还在游荡\n追上去，终结它",
                "夜月当空，让我们去戳爆那颗在空中乱飞的大眼球，让那个伪神彻底变成瞎子",
                "伪神的残躯玷污着泰拉的大地，去解放被腐化的大陆\n砍碎那坨伪神的大脑，剁碎那条紫色蠕虫！",
                "腐化的大地还在像心脏一样颤动，血肉的寄生者还在活动，腐化的肿瘤仍旧在思考，去将他们彻底放逐！",
                "地牢门口的诅咒已经松动，我感觉到灵能在深牢中蠢动，夹杂着惨叫、哀嚎、低语\n击碎那颗大头颅",
                "你感受到了吗？那种充斥着凝胶的恶臭腐败\n将这个污秽聚合体彻底净化",
                "走进地狱，那道横亘在熔岩上方的血肉封印就是我们通向下一个时代的门\n将它撕碎",
                "硫磺之海的弃儿阻挡了我们探寻深渊的宝藏，去终结它的吞噬和游荡",
                "熔岩深处那位硫磺使者的异端之火碍眼，去将其熄灭",
                "北境苦寒之地有一头被封印的冰雪造物\n在它彻底解封之前，将其粉碎",
                "一个巨大的钢铁蠕虫挡住了我们的征途，将它剁成碎片",
                "那双机械的眼睛俯视着我们——用刃将它们刺穿",
                "骷髅领主穿上了钢铁铠甲以为能阻止我们？击碎那颗金属骷髅头",
                "那个投入混沌的女巫，她有一个畸变的克隆姊妹，杀了那个异形，以血和火焰祭刀",
                "丛林深处那株疯狂的植物已经暴走，用刃终结这段失控的生长",
                "愚蠢的蜥蜴族只会信奉这些冥顽不灵的石头，让我们把它斩为齑粉，摧毁他们的信仰",
                "蒸汽与毒气之中有个蜜蜂机械混合体，把那个肮脏的玩意儿击落",
                "那座毁灭魔像在大地上横冲直撞，将它的钢铁躯体彻底粉碎",
                "星域的蠕虫将宇宙能量汇聚于此，终结它，这片星域才能属于我们",
                "那个躲在月亮背面的伪神不过是个残缺的拼凑物\n去斩断它的触须，挖出它的心脏，用它的血来痛饮",
                "靠吸食恒星热能苟延残喘的可怜神明，它的异端之火需要彻底熄灭",
                "地牢深处的亡灵聚合体，让那些不安的灵魂彻底归寂",
                "那条傲慢的宇宙巨蟒在世界的帷幕后蠢蠢欲动，终结他的野望",
                "丛林巨龙，泰拉大陆仅存的金源龙裔，与其他异形迥然不同，它值得我们的尊重\n然而，眼下我们却需让它再次赴死，剥夺其身上的金源魄\n这些材料将被用来打造出一套无比上乘的装备",
                "那个机械教会的异端笃信自己所创造的星流泰坦胜过神明之力\n这种信念荒谬至极，让我们将那几台泰坦归还自然的状态——一堆废铁",
                "曾拥有出色灵能天赋的女巫，本应成为守护泰拉的一大助力，却早已迷失于混沌之中\n我们唯有将她放逐，将刀锋刺入她的胸膛，让忠诚之火焚尽她腐朽的灵魂",
                "我们的征服之路早已不可阻挡，破碎那黑渊之下妖龙的铠甲\n使用那终焉之石，与异形展开最终决战",
            ];
            for (int i = 0; i < TRIAL_COUNT; i++) {
                int idx = i;
                TrialSummaries[i] = this.GetLocalization($"TrialSummary_{i}", () => defaultSummaries[idx]);
            }
        }

        public override void PostSetupContent() {
            trialTargetNpcs = new int[TRIAL_COUNT][];
            //试炼0 (0→1): 史莱姆王
            trialTargetNpcs[0] = [NPCID.KingSlime];
            //试炼1 (1→2): 荒漠灾虫
            trialTargetNpcs[1] = [CWRID.NPC_DesertScourgeHead];
            //试炼2 (2→3): 克苏鲁之眼
            trialTargetNpcs[2] = [NPCID.EyeofCthulhu];
            //试炼3 (3→4): 世界吞噬者 / 克苏鲁之脑
            trialTargetNpcs[3] = [NPCID.EaterofWorldsHead, NPCID.BrainofCthulhu];
            //试炼4 (4→5): 腐巢意志 / 血肉宿主
            trialTargetNpcs[4] = [CWRID.NPC_HiveMind, CWRID.NPC_PerforatorHive];
            //试炼5 (5→6): 骷髅王
            trialTargetNpcs[5] = [NPCID.SkeletronHead];
            //试炼6 (6→7): 史莱姆之神
            trialTargetNpcs[6] = [CWRID.NPC_SlimeGodCore];
            //试炼7 (7→8): 血肉墙
            trialTargetNpcs[7] = [NPCID.WallofFlesh];
            //试炼8 (8→9): 渊海灾虫
            trialTargetNpcs[8] = [CWRID.NPC_AquaticScourgeHead];
            //试炼9 (9→10): 硫磺火元素
            trialTargetNpcs[9] = [CWRID.NPC_BrimstoneElemental];
            //试炼10 (10→11): 极地冰灵
            trialTargetNpcs[10] = [CWRID.NPC_Cryogen];
            //试炼11 (11→12): 毁灭者
            trialTargetNpcs[11] = [NPCID.TheDestroyer];
            //试炼12 (12→13): 双子魔眼
            trialTargetNpcs[12] = [NPCID.Retinazer, NPCID.Spazmatism];
            //试炼13 (13→14): 机械骷髅王
            trialTargetNpcs[13] = [NPCID.SkeletronPrime];
            //试炼14 (14→15): 灾厄之影
            trialTargetNpcs[14] = [CWRID.NPC_CalamitasClone];
            //试炼15 (15→16): 世纪之花
            trialTargetNpcs[15] = [NPCID.Plantera];
            //试炼16 (16→17): 石巨人
            trialTargetNpcs[16] = [NPCID.Golem, NPCID.GolemHead];
            //试炼17 (17→18): 瘟疫使者
            trialTargetNpcs[17] = [CWRID.NPC_PlaguebringerGoliath];
            //试炼18 (18→19): 毁灭魔像
            trialTargetNpcs[18] = [CWRID.NPC_RavagerBody];
            //试炼19 (19→20): 星神游龙
            trialTargetNpcs[19] = [CWRID.NPC_AstrumDeusHead];
            //试炼20 (20→21): 月球领主
            trialTargetNpcs[20] = [NPCID.MoonLordCore];
            //试炼21 (21→22): 亵渎天神
            trialTargetNpcs[21] = [CWRID.NPC_Providence];
            //试炼22 (22→23): 噬魂幽花
            trialTargetNpcs[22] = [CWRID.NPC_Polterghast];
            //试炼23 (23→24): 神明吞噬者
            trialTargetNpcs[23] = [CWRID.NPC_DevourerofGodsHead];
            //试炼24 (24→25): 丛林龙犽戎
            trialTargetNpcs[24] = [CWRID.NPC_Yharon];
            //试炼25 (25→26): 星流巨械（追踪血量最低的一台）
            trialTargetNpcs[25] = [CWRID.NPC_AresBody, CWRID.NPC_Apollo, CWRID.NPC_Artemis, CWRID.NPC_ThanatosHead];
            //试炼26 (26→27): 至尊灾厄
            trialTargetNpcs[26] = [CWRID.NPC_SupremeCalamitas];
            //试炼27 (27→28): 始源妖龙
            trialTargetNpcs[27] = [CWRID.NPC_PrimordialWyrmHead];

            //以下完成判定与 InWorldBossPhase.Mura_Level() 中的跳级条件一一对应，仅去除“前置全部达成”的顺序锁
            trialCompletedChecks = new Func<bool>[TRIAL_COUNT];
            trialCompletedChecks[0] = InWorldBossPhase.DownedV0;
            trialCompletedChecks[1] = InWorldBossPhase.Downed0;
            trialCompletedChecks[2] = InWorldBossPhase.DownedV1;
            trialCompletedChecks[3] = InWorldBossPhase.DownedV2;
            trialCompletedChecks[4] = () => InWorldBossPhase.Downed3.Invoke() || InWorldBossPhase.Downed4.Invoke();
            trialCompletedChecks[5] = InWorldBossPhase.DownedV4;
            trialCompletedChecks[6] = InWorldBossPhase.Downed5;
            trialCompletedChecks[7] = () => Main.hardMode;
            trialCompletedChecks[8] = InWorldBossPhase.Downed8;
            trialCompletedChecks[9] = InWorldBossPhase.Downed7;
            trialCompletedChecks[10] = InWorldBossPhase.Downed6;
            trialCompletedChecks[11] = () => NPC.downedMechBoss1;
            trialCompletedChecks[12] = () => NPC.downedMechBoss2;
            trialCompletedChecks[13] = () => NPC.downedMechBoss3;
            trialCompletedChecks[14] = InWorldBossPhase.Downed10;
            trialCompletedChecks[15] = InWorldBossPhase.VDownedV7;
            trialCompletedChecks[16] = InWorldBossPhase.DownedV7;
            trialCompletedChecks[17] = InWorldBossPhase.Downed14;
            trialCompletedChecks[18] = InWorldBossPhase.Downed15;
            trialCompletedChecks[19] = InWorldBossPhase.Downed16;
            trialCompletedChecks[20] = InWorldBossPhase.VDownedV16;
            trialCompletedChecks[21] = InWorldBossPhase.Downed19;
            trialCompletedChecks[22] = InWorldBossPhase.Downed23;
            trialCompletedChecks[23] = InWorldBossPhase.Downed27;
            trialCompletedChecks[24] = InWorldBossPhase.Downed28;
            trialCompletedChecks[25] = InWorldBossPhase.Downed29;
            trialCompletedChecks[26] = InWorldBossPhase.Downed30;
            trialCompletedChecks[27] = () => InWorldBossPhase.Downed31.Invoke() || InWorldBossPhase.Downed32.Invoke();
        }

        public override void PostUpdateEverything() {
            if (Main.dedServ || Main.gameMenu) return;

            var manager = QuestManagerUI.Instance;
            if (manager == null) return;

            bool hasWeapon = Main.LocalPlayer.HasItem(CWRID.Item_Murasama);
            int level = InWorldBossPhase.Mura_Level();

            for (int i = 0; i < TRIAL_COUNT; i++) {
                SyncTrial(manager, i, level, hasWeapon);
            }
        }

        private void SyncTrial(QuestManagerUI manager, int trialIndex, int currentLevel, bool allowCreate = true) {
            string key = KEY_PREFIX + trialIndex;

            bool isDone = (trialCompletedChecks[trialIndex]?.Invoke() == true) || (trialIndex < currentLevel);

            if (trialIndex > currentLevel) {
                //等级还未到达的试炼，即使提前打了Boss也不提前显示
                manager.UnregisterQuest(key);
            }
            else if (isDone) {
                //已完成的试炼（保留显示）
                var entry = EnsureTrialEntry(manager, trialIndex, completed: true, allowCreate: allowCreate);
                if (entry != null && entry.Status != QuestEntryStatus.Completed) {
                    manager.SetEntryStatus(key, QuestEntryStatus.Completed, 1f);
                }
            }
            else {
                //trialIndex == currentLevel 且未完成，当前进行中的试炼
                var entry = EnsureTrialEntry(manager, trialIndex, allowCreate: allowCreate);
                if (entry == null) {
                    return;
                }
                else if (entry.Status == QuestEntryStatus.Completed) {
                    manager.SetEntryStatus(key, QuestEntryStatus.Active, 0f);
                }
            }
        }

        private MurasamaTrialQuestEntry EnsureTrialEntry(QuestManagerUI manager, int trialIndex, bool completed = false, bool allowCreate = true) {
            string key = KEY_PREFIX + trialIndex;
            var entry = manager.GetEntry(key) as MurasamaTrialQuestEntry;
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

        private MurasamaTrialQuestEntry CreateTrialEntry(int trialIndex) {
            return new MurasamaTrialQuestEntry(KEY_PREFIX + trialIndex,
                TrialTitles[trialIndex], TrialSummaries[trialIndex], QuestCategory) {
                Priority = TRIAL_COUNT - trialIndex,
                EntryStyle = new PhantomEntryStyle(),
                TrackerStyle = new PhantomTrackerWidgetStyle(),
                TargetNpcTypes = trialTargetNpcs[trialIndex],
                IsCompletedCheck = trialCompletedChecks[trialIndex],
                WaitingHint = TrackerWaiting,
                FightingFormat = TrackerFighting,
                BriefFormat = TrackerBrief,
                //左侧追踪窗口仅在玩家手持鬼妖村正时显示，避免常驻打扰
                TrackerVisibilityCheck = static () => Main.LocalPlayer.GetItem().type == CWRID.Item_Murasama,
            };
        }
    }
}
