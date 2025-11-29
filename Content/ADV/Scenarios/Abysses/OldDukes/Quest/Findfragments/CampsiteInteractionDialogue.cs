using CalamityOverhaul.Content.ADV.ADVChoices;
using CalamityOverhaul.Content.ADV.ADVRewardPopups;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Items;
using CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Items.OceanRaiderses;
using CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.OldDukeShops;
using System;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Quest.FindFragments
{
    /// <summary>
    /// 营地交互对话，三个选项
    /// </summary>
    internal class CampsiteInteractionDialogue : ADVScenarioBase, ILocalizedModType
    {
        public override string LocalizationCategory => "ADV";

        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => SulfseaDialogueBox.Instance;

        //角色名称
        public static LocalizedText OldDukeName { get; private set; }

        //对话台词
        public static LocalizedText GreetingLine { get; private set; }

        //选项文本
        public static LocalizedText Choice1Text { get; private set; }
        public static LocalizedText Choice2Text { get; private set; }
        public static LocalizedText Choice3Text { get; private set; }
        public static LocalizedText Choice2DisabledHint { get; private set; }

        //选项回应
        public static LocalizedText Choice1Response { get; private set; }
        public static LocalizedText Choice2Response { get; private set; }
        public static LocalizedText Choice3Response { get; private set; }

        //任务完成后的对话
        public static LocalizedText QuestCompleteLine { get; private set; }

        public override void SetStaticDefaults() {
            OldDukeName = this.GetLocalization(nameof(OldDukeName), () => "老公爵");

            GreetingLine = this.GetLocalization(nameof(GreetingLine), () => "你需要什么？");

            Choice1Text = this.GetLocalization(nameof(Choice1Text), () => "我来找你交易一下");
            Choice2Text = this.GetLocalization(nameof(Choice2Text), () => "你要的东西我都弄来了");
            Choice3Text = this.GetLocalization(nameof(Choice3Text), () => "我只是来溜达一圈");
            Choice2DisabledHint = this.GetLocalization(nameof(Choice2DisabledHint), () => "海洋残片不足");

            Choice1Response = this.GetLocalization(nameof(Choice1Response), () => "看看我这里有什么吧");
            Choice2Response = this.GetLocalization(nameof(Choice2Response), () => "很好，这些碎片足够了。这是你应得的奖励");
            Choice3Response = this.GetLocalization(nameof(Choice3Response), () => "......");

            QuestCompleteLine = this.GetLocalization(nameof(QuestCompleteLine), () => "感谢你的帮助");
        }

        protected override void Build() {
            //注册老公爵立绘
            DialogueBoxBase.RegisterPortrait(OldDukeName.Value, texture: null);
            DialogueBoxBase.SetPortraitStyle(OldDukeName.Value, silhouette: true);

            //检查任务是否已完成
            bool questCompleted = false;
            if (Main.LocalPlayer.TryGetADVSave(out var save)) {
                questCompleted = save.OldDukeFindFragmentsQuestCompleted;
            }

            if (questCompleted) {
                //任务已完成，显示简单对话
                AddWithChoices(
                    OldDukeName.Value,
                    QuestCompleteLine.Value,
                    [
                        new Choice(Choice1Text.Value, Choice1),
                        new Choice(Choice3Text.Value, Choice3),
                    ],
                    styleOverride: () => SulfseaDialogueBox.Instance,
                    choiceBoxStyle: ADVChoiceBox.ChoiceBoxStyle.Sulfsea
                );
            }
            else {
                //任务进行中，显示选项
                int fragmentCount = GetFragmentCount();
                bool hasEnoughFragments = fragmentCount >= 777;

                AddWithChoices(
                    OldDukeName.Value,
                    GreetingLine.Value,
                    [
                        new Choice(Choice1Text.Value, Choice1),
                        new Choice(Choice2Text.Value, Choice2, enabled: hasEnoughFragments, disabledHint: hasEnoughFragments ? string.Empty : Choice2DisabledHint.Value),
                        new Choice(Choice3Text.Value, Choice3),
                    ],
                    styleOverride: () => SulfseaDialogueBox.Instance,
                    choiceBoxStyle: ADVChoiceBox.ChoiceBoxStyle.Sulfsea
                );
            }
        }

        /// <summary>
        /// 获取玩家背包中的海洋残片数量
        /// </summary>
        private static int GetFragmentCount() {
            int count = 0;
            Player player = Main.LocalPlayer;
            int fragmentType = ModContent.ItemType<Oceanfragments>();

            for (int i = 0; i < player.inventory.Length; i++) {
                if (player.inventory[i].type == fragmentType) {
                    count += player.inventory[i].stack;
                }
            }

            return count;
        }

        /// <summary>
        /// 消耗海洋残片
        /// </summary>
        private static void ConsumeFragments(int amount) {
            Player player = Main.LocalPlayer;
            int fragmentType = ModContent.ItemType<Oceanfragments>();
            int remaining = amount;

            for (int i = 0; i < player.inventory.Length && remaining > 0; i++) {
                if (player.inventory[i].type == fragmentType) {
                    int toConsume = Math.Min(player.inventory[i].stack, remaining);
                    player.inventory[i].stack -= toConsume;
                    remaining -= toConsume;

                    if (player.inventory[i].stack <= 0) {
                        player.inventory[i].TurnToAir();
                    }
                }
            }
        }

        //选项1：打开商店
        private void Choice1() {
            ScenarioManager.Reset<CampsiteInteractionDialogue_Choice1>();
            ScenarioManager.Start<CampsiteInteractionDialogue_Choice1>();
            Complete();
        }

        private class CampsiteInteractionDialogue_Choice1 : ADVScenarioBase
        {
            public override string Key => nameof(CampsiteInteractionDialogue_Choice1);
            protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => SulfseaDialogueBox.Instance;

            protected override void Build() {
                Add(OldDukeName.Value, Choice1Response.Value);
            }

            protected override void OnScenarioComplete() {
                //打开商店
                OldDukeShopUI.Instance.Active = true;
                OldDukeEffect.IsActive = false;
            }
        }

        //选项2：提交海洋残片
        private void Choice2() {
            ScenarioManager.Reset<CampsiteInteractionDialogue_Choice2>();
            ScenarioManager.Start<CampsiteInteractionDialogue_Choice2>();
            Complete();
        }

        private class CampsiteInteractionDialogue_Choice2 : ADVScenarioBase
        {
            public override string Key => nameof(CampsiteInteractionDialogue_Choice2);
            protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => SulfseaDialogueBox.Instance;

            protected override void Build() {
                Add(OldDukeName.Value, Choice2Response.Value);
            }

            protected override void OnScenarioComplete() {
                //消耗海洋残片
                ConsumeFragments(777);

                //标记任务完成
                if (Main.LocalPlayer.TryGetADVSave(out var save)) {
                    save.OldDukeFindFragmentsQuestCompleted = true;
                }

                //给予奖励
                ADVRewardPopup.ShowReward(
                    ModContent.ItemType<OceanRaiders>(),
                    1,
                    "",
                    appearDuration: 24,
                    holdDuration: -1,
                    giveDuration: 16,
                    requireClick: true,
                    anchorProvider: () => {
                        var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                        if (rect == Rectangle.Empty) {
                            return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                        }
                        return new Vector2(rect.Center.X, rect.Y - 70f);
                    },
                    offset: Vector2.Zero,
                    styleProvider: () => ADVRewardPopup.RewardStyle.Sulfsea
                );

                OldDukeEffect.IsActive = false;
            }
        }

        //选项3：结束对话
        private void Choice3() {
            ScenarioManager.Reset<CampsiteInteractionDialogue_Choice3>();
            ScenarioManager.Start<CampsiteInteractionDialogue_Choice3>();
            Complete();
        }

        private class CampsiteInteractionDialogue_Choice3 : ADVScenarioBase
        {
            public override string Key => nameof(CampsiteInteractionDialogue_Choice3);
            protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => SulfseaDialogueBox.Instance;

            protected override void Build() {
                Add(OldDukeName.Value, Choice3Response.Value);
            }

            protected override void OnScenarioComplete() {
                OldDukeEffect.IsActive = false;
            }
        }

        protected override void OnScenarioStart() {
            OldDukeEffect.IsActive = true;
        }

        protected override void OnScenarioComplete() {
            OldDukeEffect.IsActive = false;
        }
    }
}
