using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI;
using System;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Helen
{
    /// <summary>
    /// 比目鱼鱼油获取提示与任务创建场景
    /// </summary>
    internal class FishoilQuestScenario : ADVScenarioBase, ILocalizedModType
    {
        public override string Key => nameof(FishoilQuestScenario);
        public string LocalizationCategory => "Legend.HalibutText.ADV";

        //触发控制
        public static bool Spwand; //外部可置 true 来允许尝试触发
        private static bool scenarioStarted; //已进入对话(等待玩家选择)
        private static int spawnDelayTimer; //延迟计时器
        private const int BassNeedThreshold = 10; //需要的初始鲈鱼数量门槛

        public static LocalizedText Rolename { get; private set; }
        public static LocalizedText Line0 { get; private set; }
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }
        public static LocalizedText Line3 { get; private set; }
        public static LocalizedText Line4 { get; private set; }
        public static LocalizedText ChoiceAccept { get; private set; }
        public static LocalizedText ChoiceDecline { get; private set; }

        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => SeaDialogueBox.Instance;

        public override void SetStaticDefaults() {
            Rolename = this.GetLocalization(nameof(Rolename), () => "比目鱼");
            Line0 = this.GetLocalization(nameof(Line0), () => "你最近好像捕到了不少普通的鱼");
            Line1 = this.GetLocalization(nameof(Line1), () => "给我一批做实验，我可以提炼一瓶新鲜的鱼油");
            Line2 = this.GetLocalization(nameof(Line2), () => "过程不难但很枯燥");
            Line3 = this.GetLocalization(nameof(Line3), () => "鱼油很有潜力,比你想的更有用");
            Line4 = this.GetLocalization(nameof(Line4), () => "愿意吗?");
            ChoiceAccept = this.GetLocalization(nameof(ChoiceAccept), () => "可以");
            ChoiceDecline = this.GetLocalization(nameof(ChoiceDecline), () => "没兴趣");
        }

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(Rolename.Value, ADVAsset.Helen_solemnADV);
            DialogueBoxBase.SetPortraitStyle(Rolename.Value, silhouette: false);

            Add(Rolename.Value, Line0.Value);
            Add(Rolename.Value, Line1.Value);
            Add(Rolename.Value, Line2.Value);
            Add(Rolename.Value, Line3.Value);
            AddWithChoices(Rolename.Value, Line4.Value, new System.Collections.Generic.List<Choice>{
                new Choice(ChoiceAccept.Value, OnAccept, enabled: true),
                new Choice(ChoiceDecline.Value, OnDecline, enabled: true)
            });
        }

        private void OnAccept() {
            if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                halibutPlayer.ADCSave.FishoilQuestAccepted = true;
                FishoilQuestUI.Instance.OpenPersistent();
            }
            scenarioStarted = false; //结束占用
            Complete();
        }

        private void OnDecline() {
            if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                halibutPlayer.ADCSave.FishoilQuestDeclined = true;
            }
            scenarioStarted = false;
            Complete();
        }

        public override void Update(ADVSave save, HalibutPlayer halibutPlayer) {
            //必须击败蜂后后才有兴趣让你收集最普通的鱼
            if (!NPC.downedQueenBee) {
                Spwand = false;
                return;
            }

            if (!save.FirstMet) {
                return;
            }

            //已经完成或拒绝就不再尝试
            if (save.FishoilQuestAccepted || save.FishoilQuestDeclined) {
                return;
            }

            //对话已经开始(正在选项中)时不重复尝试
            if (scenarioStarted) {
                return;
            }

            Player player = halibutPlayer.Player;
            int bassCount = 0;
            for (int i = 0; i < player.inventory.Length; i++) {
                if (player.inventory[i].type == Terraria.ID.ItemID.Bass) {
                    bassCount += player.inventory[i].stack;
                    if (bassCount >= BassNeedThreshold) {
                        break;
                    }
                }
            }

            //未达到门槛
            if (bassCount < BassNeedThreshold) {
                return;
            }

            //首次达到门槛时设置延迟
            if (!Spwand) {
                Spwand = true;
                spawnDelayTimer = Main.rand.Next(11, 63);
            }

            //延迟倒计时
            if (spawnDelayTimer > 0) {
                spawnDelayTimer--;
                return;
            }

            //排除混乱事件
            if (VaultUtils.IsInvasion()) {
                return;
            }

            if (ScenarioManager.Start<FishoilQuestScenario>()) {
                scenarioStarted = true; //防止重复
                Spwand = false; //消耗标记
                if (!HalibutUIHead.Instance.Open) HalibutUIHead.Instance.Open = true;
            }
        }
    }
}
