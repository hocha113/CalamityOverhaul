using CalamityOverhaul.Content.ADV.ADVChoices;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.ADV.DialogueBoxs.Styles;
using CalamityOverhaul.Content.ADV.EntrustManager;
using System;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Helen.Quest.FishoilQuest
{
    /// <summary>
    /// 比目鱼鱼油获取提示与任务创建场景
    /// </summary>
    internal class FishoilQuestScenario : ADVScenarioBase, ILocalizedModType, IWorldInfo
    {
        //触发控制
        public static bool Spwand;//外部可置 true 来允许尝试触发
        private static bool scenarioStarted;//已进入对话(等待玩家选择)
        private static int spawnDelayTimer;//延迟计时器

        //鱼类需求总量(所有候选鱼的总和达到此数量才会触发)
        private const int FishNeedThreshold = 10;

        //可计入的普通鱼ID表
        internal static readonly int[] CandidateFishTypes = [
            ItemID.Bass,
            ItemID.Trout,
            ItemID.Salmon,
            ItemID.Tuna,
            ItemID.RedSnapper,
            ItemID.NeonTetra,
            ItemID.Damselfish,
            ItemID.ArmoredCavefish,
            ItemID.Hemopiranha,//血腥生态常见
            ItemID.Ebonkoi,//腐化生态常见
            ItemID.SpecularFish,//洞穴/地下水域常见
            ItemID.Prismite//稀有
        ];

        public static LocalizedText Rolename { get; private set; }
        public static LocalizedText Line0 { get; private set; }
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }
        public static LocalizedText Line3 { get; private set; }
        public static LocalizedText Line4 { get; private set; }
        public static LocalizedText ChoiceAccept { get; private set; }
        public static LocalizedText ChoiceDecline { get; private set; }

        private const string enjoy = " ";

        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => SeaDialogueBox.Instance;

        void IWorldInfo.OnWorldLoad() {
            Spwand = false;
            scenarioStarted = false;
            spawnDelayTimer = 0;
        }

        public override void SetStaticDefaults() {
            Rolename = this.GetLocalization(nameof(Rolename), () => "比目鱼");
            Line0 = this.GetLocalization(nameof(Line0), () => "你最近好像捕到了不少普通的鱼");
            Line1 = this.GetLocalization(nameof(Line1), () => "给我一批做实验,我可以提炼一瓶新鲜的鱼油");
            Line2 = this.GetLocalization(nameof(Line2), () => "过程不难但很枯燥");
            Line3 = this.GetLocalization(nameof(Line3), () => "鱼油很有潜力,比你想的更有用");
            Line4 = this.GetLocalization(nameof(Line4), () => "愿意吗?");
            ChoiceAccept = this.GetLocalization(nameof(ChoiceAccept), () => "可以");
            ChoiceDecline = this.GetLocalization(nameof(ChoiceDecline), () => "没兴趣");
        }

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(Rolename.Value, ADVAsset.HelenADV);
            DialogueBoxBase.SetPortraitStyle(Rolename.Value, silhouette: false);
            DialogueBoxBase.RegisterPortrait(Rolename.Value + enjoy, ADVAsset.Helen_enjoyADV);
            DialogueBoxBase.SetPortraitStyle(Rolename.Value + enjoy, silhouette: false);
            Add(Rolename.Value, Line0.Value);
            Add(Rolename.Value, Line1.Value);
            Add(Rolename.Value, Line2.Value);
            Add(Rolename.Value + enjoy, Line3.Value);
            AddWithChoices(Rolename.Value, Line4.Value, new System.Collections.Generic.List<Choice> {
                new Choice(ChoiceAccept.Value, OnAccept, enabled: true),
                new Choice(ChoiceDecline.Value, OnDecline, enabled: true)
            });
        }

        private void OnAccept() {
            if (Main.LocalPlayer.TryGetADVSave(out var save)) {
                save.Get<HalibutADVData>().FishoilQuestAccepted = true;
            }
            //注册到委托管理系统
            RegisterQuestEntry();
            scenarioStarted = false;
            Complete();
        }

        /// <summary>
        /// 将鱼油任务注册到委托管理器，如已存在则跳过。
        /// notify 为 false 时以 Tracked 状态静默注册，适用于存档重载的恢复路径，
        /// 避免每次进存档都触发"新委托"弹窗
        /// </summary>
        internal static void RegisterQuestEntry(bool completed = false, bool notify = true) {
            var manager = QuestManagerUI.Instance;
            if (manager == null) return;
            if (manager.GetEntry(FishoilQuestEntry.QuestKey) != null) return;
            var entry = FishoilQuestEntry.Create();
            if (completed) {
                entry.Status = QuestEntryStatus.Completed;
                entry.Progress = 1f;
            }
            else if (!notify) {
                //恢复路径：直接置为 Tracked，RegisterQuest 不会触发任何通知
                entry.Status = QuestEntryStatus.Tracked;
                entry.IsNew = false;
            }
            manager.RegisterQuest(entry);
        }

        /// <summary>
        /// 同步委托管理器中鱼油条目的注册与状态，
        /// 未接受 → 移除条目；已接受 → 确保注册；已完成 → 标记完成；
        /// 注：已挂起（FishoilQuestSuspended）不再切换 UI 状态为 Suspended，
        /// 因为挂起的条目会被侧边栏隐藏，改为通过条目内的提交按钮重新询问
        /// </summary>
        private static void SyncQuestEntry(ADVSave save) {
            var manager = QuestManagerUI.Instance;
            if (manager == null) return;

            //未接受任务 → 确保条目不存在
            if (!save.Get<HalibutADVData>().FishoilQuestAccepted) {
                manager.UnregisterQuest(FishoilQuestEntry.QuestKey);
                return;
            }

            //已接受 → 确保条目存在，notify: false 表示恢复路径，不弹"新委托"通知
            bool completed = save.Get<HalibutADVData>().FishoilQuestCompleted;
            RegisterQuestEntry(completed, notify: false);
            var entry = manager.GetEntry(FishoilQuestEntry.QuestKey);
            if (entry == null) return;

            //已完成 → 直接赋值恢复状态，避免 SetEntryStatus 触发"委托完成"通知
            if (completed && entry.Status != QuestEntryStatus.Completed) {
                entry.Status = QuestEntryStatus.Completed;
                entry.Progress = 1f;
                manager.MarkFilterDirty();
            }
        }

        private void OnDecline() {
            if (Main.LocalPlayer.TryGetADVSave(out var save)) {
                save.Get<HalibutADVData>().FishoilQuestDeclined = true;
            }
            scenarioStarted = false;
            Complete();
        }

        public override void Update(ADVSave save, Player player) {
            //同步委托管理器条目状态（参照 SupCalQuestLine.SyncQuest 模式）
            SyncQuestEntry(save);

            if (!NPC.downedQueenBee) {
                Spwand = false;
                return;
            }
            if (!save.Get<HalibutADVData>().FirstMet) {
                return;
            }
            if (save.Get<HalibutADVData>().FishoilQuestAccepted || save.Get<HalibutADVData>().FishoilQuestDeclined) {
                return;
            }
            if (scenarioStarted) {
                return;
            }

            int totalFishCount = 0;
            //统计所有候选鱼的总数量
            for (int i = 0; i < player.inventory.Length; i++) {
                Item item = player.inventory[i];
                if (item != null && item.stack > 0 && CandidateFishTypes.Contains(item.type)) {
                    totalFishCount += item.stack;
                    if (totalFishCount >= FishNeedThreshold) {
                        break;
                    }
                }
            }

            if (totalFishCount < FishNeedThreshold) {
                return;
            }

            if (!Spwand) {
                Spwand = true;
                spawnDelayTimer = Main.rand.Next(60, 160);//随机延迟保证自然感
            }
            if (spawnDelayTimer > 0) {
                spawnDelayTimer--;
                return;
            }
            //避免在不合适的时候触发
            if (VaultUtils.IsInvasion() || CWRWorld.HasBoss) {
                return;
            }

            if (ScenarioManager.Start<FishoilQuestScenario>()) {
                scenarioStarted = true;
                Spwand = false;
            }
        }
    }
}
