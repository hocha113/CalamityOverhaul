using CalamityOverhaul.Content.ADV.ADVChoices;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Helen
{
    /// <summary>
    /// 海伦在接受神明吞噬者任务后的劝阻场景
    /// </summary>
    internal class HelensInterference : ADVScenarioBase, ILocalizedModType, IWorldInfo
    {
        public static int DelayTimer;

        //角色名称本地化
        public static LocalizedText Rolename { get; private set; }

        //对话文本本地化
        public static LocalizedText Line0 { get; private set; }
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }

        //第一层选项文本
        public static LocalizedText Question1 { get; private set; }
        public static LocalizedText Choice1_1 { get; private set; }
        public static LocalizedText Choice1_2 { get; private set; }
        public static LocalizedText Choice1_3 { get; private set; }

        //第二层对话和最终选择
        public static LocalizedText FinalQuestion { get; private set; }
        public static LocalizedText FinalChoice_Continue { get; private set; }
        public static LocalizedText FinalChoice_Stop { get; private set; }

        //设置场景默认使用海洋风格
        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => SeaDialogueBox.Instance;

        void IWorldInfo.OnWorldLoad() {
            DelayTimer = 0;
        }

        public override void SetStaticDefaults() {
            Rolename = this.GetLocalization(nameof(Rolename), () => "比目鱼");

            //开场对话
            Line0 = this.GetLocalization(nameof(Line0), () => "……喂，你有空吗？");
            Line1 = this.GetLocalization(nameof(Line1), () => "今天.....嗯....天气不错？");
            Line2 = this.GetLocalization(nameof(Line2), () => "(比目鱼似乎将一些东西藏了起来)");

            //第一层选项
            Question1 = this.GetLocalization(nameof(Question1), () => "......");
            Choice1_1 = this.GetLocalization(nameof(Choice1_1), () => "你在做什么？");
            Choice1_2 = this.GetLocalization(nameof(Choice1_2), () => "拿出来！");
            Choice1_3 = this.GetLocalization(nameof(Choice1_3), () => "(沉默)");

            //最终选择
            FinalQuestion = this.GetLocalization(nameof(FinalQuestion), () => "不要再往前走了.....好吗。那个女巫，她的委托，她想要的东西......我不想看到。剩下的路我们完全可以自己走");
            FinalChoice_Continue = this.GetLocalization(nameof(FinalChoice_Continue), () => "继续委托");
            FinalChoice_Stop = this.GetLocalization(nameof(FinalChoice_Stop), () => "中止委托");
        }

        protected override void Build() {
            //注册海伦立绘
            DialogueBoxBase.RegisterPortrait(Rolename.Value, ADVAsset.Helen2ADV);
            DialogueBoxBase.SetPortraitStyle(Rolename.Value, silhouette: false);

            //开场对话
            Add(Rolename.Value, Line0.Value);
            Add(Rolename.Value, Line1.Value);
            //将刻心者从玩家背包移除
            Add(" ", Line2.Value, onComplete: RemoveHeartcarverFromPlayer);

            //第一层选项
            AddWithChoices(Rolename.Value, Question1.Value, new List<Choice> {
                new Choice(Choice1_1.Value, OnChoice1),
                new Choice(Choice1_2.Value, OnChoice2),
                new Choice(Choice1_3.Value, OnChoice3)
            });
        }

        //选项1：询问
        private void OnChoice1() {
            ScenarioManager.Start<Branch_Inquiry>();
            Complete();
        }

        //选项2：愤怒
        private void OnChoice2() {
            ScenarioManager.Start<Branch_Anger>();
            Complete();
        }

        //选项3：沉默
        private void OnChoice3() {
            ScenarioManager.Start<Branch_Silence>();
            Complete();
        }

        //移除刻心者的辅助方法
        private static void RemoveHeartcarverFromPlayer() {
            Player player = Main.LocalPlayer;
            int heartcarverType = ModContent.ItemType<Heartcarver>();

            //从背包中移除所有刻心者
            for (int i = 0; i < player.inventory.Length; i++) {
                if (player.inventory[i].type == heartcarverType) {
                    player.inventory[i].TurnToAir();
                }
            }
        }

        //将刻心者归还给玩家
        private static void ReturnHeartcarverToPlayer() {
            Player player = Main.LocalPlayer;
            int heartcarverType = ModContent.ItemType<Heartcarver>();

            //寻找空位并放入刻心者
            int emptySlot = player.FindItemInInventoryOrOpenVoidBag(ItemID.None, out bool _);
            if (emptySlot >= 0) {
                player.inventory[emptySlot].SetDefaults(heartcarverType);
                player.inventory[emptySlot].stack = 1;
            }
            else {
                //如果背包满了，生成到地上
                player.QuickSpawnItem(player.GetSource_Misc("HelensInterference"), heartcarverType, 1);
            }
        }

        public override void Update(ADVSave save, HalibutPlayer halibutPlayer) {
            //检查是否接受了神明吞噬者任务
            if (!save.SupCalDoGQuestAccepted) {
                return;
            }

            //已经触发过此场景
            if (save.HelenInterferenceTriggered) {
                return;
            }

            //如果任务已完成或已拒绝，不触发
            if (save.SupCalDoGQuestReward || save.SupCalDoGQuestDeclined) {
                return;
            }

            //避免在不合适的时候触发
            if (CWRWorld.HasBoss) {
                return;
            }

            if (!halibutPlayer.HasHalubut) {
                return;
            }

            if (--DelayTimer > 0) {
                return;
            }

            if (ScenarioManager.Start<HelensInterference>()) {
                save.HelenInterferenceTriggered = true;
            }
        }

        #region 分支场景实现

        //分支1：理性沟通
        private class Branch_Inquiry : ADVScenarioBase, ILocalizedModType
        {
            public override string Key => nameof(Branch_Inquiry);
            protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => SeaDialogueBox.Instance;
            public override string LocalizationCategory => "ADV.HelensInterference";
            //本地化文本
            public static LocalizedText Line1 { get; private set; }
            public static LocalizedText Line2 { get; private set; }
            public static LocalizedText Line3 { get; private set; }
            public static LocalizedText Line4 { get; private set; }

            public override void SetStaticDefaults() {
                Line1 = this.GetLocalization(nameof(Line1), () => "我最近愈发不安");
                Line2 = this.GetLocalization(nameof(Line2), () => "那把刀，我碰到它的时候，像是听到了......有人在窃笑 ");//TODO
                Line3 = this.GetLocalization(nameof(Line3), () => "它们都像是......某种'媒介物品'");
                Line4 = this.GetLocalization(nameof(Line4), () => "我不能让你带着它走下去，至少......");
            }

            protected override void Build() {
                DialogueBoxBase.RegisterPortrait(Rolename.Value, ADVAsset.Helen2ADV);
                DialogueBoxBase.SetPortraitStyle(Rolename.Value, silhouette: false);

                Add(Rolename.Value, Line1.Value);
                Add(Rolename.Value, Line2.Value);
                Add(Rolename.Value, Line3.Value);
                Add(Rolename.Value, Line4.Value);

                //最终选择
                AddWithChoices(Rolename.Value, FinalQuestion.Value, [
                    new Choice(FinalChoice_Continue.Value, OnContinue),
                    new Choice(FinalChoice_Stop.Value, OnStop)
                ]);
            }

            private static void OnContinue() {
                ScenarioManager.Start<FinalBranch_Continue>();
            }

            private static void OnStop() {
                ScenarioManager.Start<FinalBranch_Stop>();
            }
        }

        //分支2：冲突
        private class Branch_Anger : ADVScenarioBase, ILocalizedModType
        {
            public override string Key => nameof(Branch_Anger);
            protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => SeaDialogueBox.Instance;
            public override string LocalizationCategory => "ADV.HelensInterference";
            //本地化文本
            public static LocalizedText Line1 { get; private set; }
            public static LocalizedText Line2 { get; private set; }
            public static LocalizedText Line3 { get; private set; }
            public static LocalizedText Line4 { get; private set; }

            public override void SetStaticDefaults() {
                Line1 = this.GetLocalization(nameof(Line1), () => "......你真的...信她到了这种地步？");
                Line2 = this.GetLocalization(nameof(Line2), () => "难道你没发现吗？那个女巫，她每次给你的东西都不太正常");
                Line3 = this.GetLocalization(nameof(Line3), () => "这让我回想起深渊中的某个......我碰到它......阴冷、恐怖、令人发悸...啊....我不想回忆");
                Line4 = this.GetLocalization(nameof(Line4), () => "你本该有所察觉的.......");
            }

            protected override void Build() {
                DialogueBoxBase.RegisterPortrait(Rolename.Value, ADVAsset.Helen2ADV);
                DialogueBoxBase.SetPortraitStyle(Rolename.Value, silhouette: false);

                Add(Rolename.Value, Line1.Value);
                Add(Rolename.Value, Line2.Value);
                Add(Rolename.Value, Line3.Value);
                Add(Rolename.Value, Line4.Value);

                //最终选择
                AddWithChoices(Rolename.Value, FinalQuestion.Value, new List<Choice> {
                    new Choice(FinalChoice_Continue.Value, OnContinue),
                    new Choice(FinalChoice_Stop.Value, OnStop)
                });
            }

            private static void OnContinue() {
                ScenarioManager.Start<FinalBranch_Continue>();
            }

            private static void OnStop() {
                ScenarioManager.Start<FinalBranch_Stop>();
            }
        }

        //分支3：沉默
        private class Branch_Silence : ADVScenarioBase, ILocalizedModType
        {
            public override string Key => nameof(Branch_Silence);
            protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => SeaDialogueBox.Instance;
            public override string LocalizationCategory => "ADV.HelensInterference";
            //本地化文本
            public static LocalizedText Line1 { get; private set; }
            public static LocalizedText Line2 { get; private set; }
            public static LocalizedText Line3 { get; private set; }
            public static LocalizedText Line4 { get; private set; }

            public override void SetStaticDefaults() {
                Line1 = this.GetLocalization(nameof(Line1), () => "......你沉默的时候最可怕");
                Line2 = this.GetLocalization(nameof(Line2), () => "好吧，我把刻心者藏起来了......");
                Line3 = this.GetLocalization(nameof(Line3), () => "那个女巫，来路不明，危险至极。如果那是陷阱，你也要乖乖跳进去吗？");
                Line4 = this.GetLocalization(nameof(Line4), () => "停下来，权当是......为了我");
            }

            protected override void Build() {
                DialogueBoxBase.RegisterPortrait(Rolename.Value, ADVAsset.Helen2ADV);
                DialogueBoxBase.SetPortraitStyle(Rolename.Value, silhouette: false);

                Add(Rolename.Value, Line1.Value);
                Add(Rolename.Value, Line2.Value);
                Add(Rolename.Value, Line3.Value);
                Add(Rolename.Value, Line4.Value);

                //最终选择
                AddWithChoices(Rolename.Value, FinalQuestion.Value, new List<Choice> {
                    new Choice(FinalChoice_Continue.Value, OnContinue),
                    new Choice(FinalChoice_Stop.Value, OnStop)
                });
            }

            private static void OnContinue() {
                ScenarioManager.Start<FinalBranch_Continue>();
            }

            private static void OnStop() {
                ScenarioManager.Start<FinalBranch_Stop>();
            }
        }

        //最终分支A：继续委托
        private class FinalBranch_Continue : ADVScenarioBase, ILocalizedModType
        {
            public override string Key => nameof(FinalBranch_Continue);
            protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => SeaDialogueBox.Instance;
            public override string LocalizationCategory => "ADV.HelensInterference";
            //本地化文本
            public static LocalizedText Line1 { get; private set; }
            public static LocalizedText Line2 { get; private set; }
            public static LocalizedText Line3 { get; private set; }
            public static LocalizedText Line4 { get; private set; }
            public static LocalizedText Line5 { get; private set; }
            public override void SetStaticDefaults() {
                Line1 = this.GetLocalization(nameof(Line1), () => "......好吧。我知道你会这么选");
                Line2 = this.GetLocalization(nameof(Line2), () => "......");
                Line3 = this.GetLocalization(nameof(Line3), () => "东西我会放回去，但我希望你用它的时候......谨慎一点");
                Line4 = this.GetLocalization(nameof(Line4), () => "还有那个女巫，也......");
                Line5 = this.GetLocalization(nameof(Line5), () => "我会陪你走下去......一直");
            }

            protected override void Build() {
                DialogueBoxBase.RegisterPortrait(Rolename.Value, ADVAsset.Helen2ADV);
                DialogueBoxBase.SetPortraitStyle(Rolename.Value, silhouette: false);

                Add(Rolename.Value, Line1.Value);
                Add(Rolename.Value, Line2.Value);
                Add(Rolename.Value, Line3.Value);
                Add(Rolename.Value, Line4.Value);
                Add(Rolename.Value, Line5.Value, onComplete: () => {
                    //归还刻心者
                    ReturnHeartcarverToPlayer();

                    //播放归还音效
                    SoundEngine.PlaySound(SoundID.Grab with {
                        Volume = 0.7f,
                        Pitch = 0.2f
                    }, Main.LocalPlayer.Center);
                });
            }

            protected override void OnScenarioComplete() {
                //标记选择了继续
                if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                    halibutPlayer.ADCSave.HelenInterferenceContinue = true;
                }
            }
        }

        //最终分支B：中止委托
        private class FinalBranch_Stop : ADVScenarioBase, ILocalizedModType
        {
            public override string Key => nameof(FinalBranch_Stop);
            protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => SeaDialogueBox.Instance;
            public override string LocalizationCategory => "ADV.HelensInterference";
            //本地化文本
            public static LocalizedText Line1 { get; private set; }
            public static LocalizedText Line2 { get; private set; }
            public static LocalizedText Line3 { get; private set; }
            public override void SetStaticDefaults() {
                Line1 = this.GetLocalization(nameof(Line1), () => "......你真的愿意停下来吗");
                Line2 = this.GetLocalization(nameof(Line2), () => "......");
                Line3 = this.GetLocalization(nameof(Line3), () => "我们走吧，让那个女巫下地狱去吧");
            }

            protected override void Build() {
                DialogueBoxBase.RegisterPortrait(Rolename.Value, ADVAsset.HelenADV);
                DialogueBoxBase.SetPortraitStyle(Rolename.Value, silhouette: false);

                Add(Rolename.Value, Line1.Value);
                Add(Rolename.Value, Line2.Value);
                Add(Rolename.Value, Line3.Value);
            }

            protected override void OnScenarioComplete() {
                //标记任务被拒绝
                if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                    halibutPlayer.ADCSave.SupCalDoGQuestDeclined = true;
                    halibutPlayer.ADCSave.HelenInterferenceStop = true;
                }

                //播放销毁音效
                SoundEngine.PlaySound(SoundID.NPCDeath1 with {
                    Volume = 0.6f,
                    Pitch = -0.4f
                }, Main.LocalPlayer.Center);
            }
        }

        #endregion
    }
}
