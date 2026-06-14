using CalamityOverhaul.Content.ADV.ADVChoices;
using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.ADV.DialogueBoxs.Styles;
using CalamityOverhaul.Content.ADV.EntrustManager;
using CalamityOverhaul.Content.Items.Tools;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Helen.Quest.FishoilQuest
{
    /// <summary>鱼油提交场景，300鱼达标后询问交鱼</summary>
    internal class FishoilSubmitScenario : ADVScenarioBase, ILocalizedModType
    {
        public static LocalizedText Rolename { get; private set; }
        public static LocalizedText SubmitLine1 { get; private set; }
        public static LocalizedText SubmitLine2 { get; private set; }
        public static LocalizedText QuestionLine { get; private set; }
        public static LocalizedText ChoiceGive { get; private set; }
        public static LocalizedText ChoiceRefuse { get; private set; }
        public static LocalizedText GiveResponse { get; private set; }
        public static LocalizedText RefuseResponse { get; private set; }
        public static LocalizedText NotEnoughResponse { get; private set; }
        public static LocalizedText AlreadyCompletedResponse { get; private set; }

        private const string enjoy = " ";

        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => SeaDialogueBox.Instance;

        public override void SetStaticDefaults() {
            Rolename = this.GetLocalization(nameof(Rolename), () => "比目鱼");
            SubmitLine1 = this.GetLocalization(nameof(SubmitLine1), () => "嗯，你收集到了足够的鱼");
            SubmitLine2 = this.GetLocalization(nameof(SubmitLine2), () => "正好够我提炼一瓶鱼油的量");
            QuestionLine = this.GetLocalization(nameof(QuestionLine), () => "你要把它们交给我吗？");
            ChoiceGive = this.GetLocalization(nameof(ChoiceGive), () => "好，拿去吧");
            ChoiceRefuse = this.GetLocalization(nameof(ChoiceRefuse), () => "我再想想");
            GiveResponse = this.GetLocalization(nameof(GiveResponse), () => "很好，稍等...给你，一瓶新鲜的鱼油");
            RefuseResponse = this.GetLocalization(nameof(RefuseResponse), () => "好吧，什么时候想好了再来找我");
            NotEnoughResponse = this.GetLocalization(nameof(NotEnoughResponse), () => "嗯？鱼好像不太够，再去捕一些回来吧");
            AlreadyCompletedResponse = this.GetLocalization(nameof(AlreadyCompletedResponse), () => "鱼油已经给你了，再要的话还得等下次");

            //委托任务条目的本地化也在此初始化
            FishoilQuestEntry.InitLocalization(this);
        }

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(Rolename.Value, ADVAsset.HelenADV);
            DialogueBoxBase.SetPortraitStyle(Rolename.Value, silhouette: false);
            DialogueBoxBase.RegisterPortrait(Rolename.Value + enjoy, ADVAsset.Helen_enjoyADV);
            DialogueBoxBase.SetPortraitStyle(Rolename.Value + enjoy, silhouette: false);

            //场景启动瞬间再次校验持久化完成标志，防止重复触发
            if (FishoilQuestEntry.IsPersistentlyCompleted()) {
                Add(Rolename.Value, AlreadyCompletedResponse.Value);
                return;
            }

            //再次校验鱼数，避免触发到这一刻之间被丢/吃/转走
            if (FishoilQuestEntry.CountAvailableFish(Main.LocalPlayer) < FishoilQuestEntry.FishRequired) {
                Add(Rolename.Value, NotEnoughResponse.Value);
                return;
            }

            Add(Rolename.Value, SubmitLine1.Value);
            Add(Rolename.Value + enjoy, SubmitLine2.Value);

            AddWithChoices(Rolename.Value, QuestionLine.Value, new List<Choice> {
                new Choice(ChoiceGive.Value, OnGive),
                new Choice(ChoiceRefuse.Value, OnRefuse),
            }, choiceBoxStyle: ADVChoiceBox.ChoiceBoxStyle.Default);
        }

        /// <summary>交鱼原子提交，消耗+发奖+存档+UI，失败返 false</summary>
        private static bool TrySubmit(Player player) {
            if (player == null) return false;
            //已经完成则视为成功，不再二次发奖
            if (FishoilQuestEntry.IsPersistentlyCompleted()) return true;

            int needed = FishoilQuestEntry.FishRequired;
            if (FishoilQuestEntry.CountAvailableFish(player) < needed) return false;

            int consumed = FishoilQuestEntry.ConsumeAvailableFish(player, needed);
            if (consumed < needed) {
                //极端情况下消耗失败（理论上不应发生），仍然回退避免发空奖
                return false;
            }

            //发放鱼油奖励
            int fishoilType = ModContent.ItemType<Fishoil>();
            ADVRewardPopup.ShowReward(fishoilType, 5, "", appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                anchorProvider: () => {
                    var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                    if (rect == Rectangle.Empty) {
                        return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                    }
                    return new Vector2(rect.Center.X, rect.Y - 70f);
                }, offset: Vector2.Zero);

            //持久化完成状态
            if (player.TryGetADVSave(out var save)) {
                save.Get<HalibutADVData>().FishoilQuestCompleted = true;
                save.Get<HalibutADVData>().FishoilQuestSuspended = false;
            }
            //更新委托管理器中的条目状态
            QuestManagerUI.Instance?.SetEntryStatus(
                FishoilQuestEntry.QuestKey, QuestEntryStatus.Completed, 1f);
            return true;
        }

        private void OnGive() {
            //同步执行提交，结果决定后续播放哪条对话
            bool ok = TrySubmit(Main.LocalPlayer);
            if (ok) {
                ScenarioManager.Reset<FishoilSubmit_Give>();
                ScenarioManager.Start<FishoilSubmit_Give>();
            }
            else {
                ScenarioManager.Reset<FishoilSubmit_NotEnough>();
                ScenarioManager.Start<FishoilSubmit_NotEnough>();
            }
            Complete();
        }

        /// <summary>交鱼成功回应，副作用已在 TrySubmit 完成</summary>
        private class FishoilSubmit_Give : ADVScenarioBase
        {
            public override string Key => nameof(FishoilSubmit_Give);
            protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => SeaDialogueBox.Instance;

            protected override void Build() {
                Add(Rolename.Value + enjoy, GiveResponse.Value);
            }
        }

        /// <summary>鱼数不足兜底，只播提示不改状态</summary>
        private class FishoilSubmit_NotEnough : ADVScenarioBase
        {
            public override string Key => nameof(FishoilSubmit_NotEnough);
            protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => SeaDialogueBox.Instance;

            protected override void Build() {
                Add(Rolename.Value, NotEnoughResponse.Value);
            }
        }

        private void OnRefuse() {
            //同步置位"等待手动提交"持久化标志，
            //不依赖子场景的 OnScenarioComplete，避免任何时序间隙导致 OnUpdate 抢先重新触发问询
            if (Main.LocalPlayer.TryGetADVSave(out var save)
                && !FishoilQuestEntry.IsPersistentlyCompleted()) {
                save.Get<HalibutADVData>().FishoilQuestSuspended = true;
            }
            ScenarioManager.Reset<FishoilSubmit_Refuse>();
            ScenarioManager.Start<FishoilSubmit_Refuse>();
            Complete();
        }

        /// <summary>拒交回应，挂起标志已在 OnRefuse 写入</summary>
        private class FishoilSubmit_Refuse : ADVScenarioBase
        {
            public override string Key => nameof(FishoilSubmit_Refuse);
            protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => SeaDialogueBox.Instance;

            protected override void Build() {
                Add(Rolename.Value, RefuseResponse.Value);
            }

            protected override void OnScenarioComplete() {
                //已完成则不再改写状态
                if (FishoilQuestEntry.IsPersistentlyCompleted()) return;
                //兜底再写一次（同步路径已经写过，幂等）
                if (Main.LocalPlayer.TryGetADVSave(out var save)) {
                    save.Get<HalibutADVData>().FishoilQuestSuspended = true;
                }
            }
        }
    }
}
