using CalamityOverhaul.Content.ADV.Scenarios.Draedons.PQCDs;
using CalamityOverhaul.Content.ADV.Scenarios.Draedons.Tzeentch;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using System;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.Quest.DeploySignaltowers
{
    /// <summary>
    /// 任务完成场景
    /// </summary>
    internal class QuestCompleteScenario : ADVScenarioBase, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";
        public override string Key => nameof(QuestCompleteScenario);

        //角色名称本地化
        public static LocalizedText DraedonName { get; private set; }

        //对话台词
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }
        public static LocalizedText Line3 { get; private set; }
        public static LocalizedText Line4 { get; private set; }
        public static LocalizedText Line5 { get; private set; }

        //设置场景默认使用嘉登科技风格
        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => DraedonDialogueBox.Instance;

        private const string red = " ";
        private const string alt = " " + " ";

        public override void SetStaticDefaults() {
            DraedonName = this.GetLocalization(nameof(DraedonName), () => "嘉登");

            Line1 = this.GetLocalization(nameof(Line1), () => "量子纠缠网络已完全建立，所有节点运行稳定");
            Line2 = this.GetLocalization(nameof(Line2), () => "数据传输延迟达到了预期的普朗克时间级别，完美符合理论预测");
            Line3 = this.GetLocalization(nameof(Line3), () => "你的效率让我印象深刻。有了这个网络，我能够实时监控整个星系的异常波动");
            Line4 = this.GetLocalization(nameof(Line4), () => "作为报酬，我会开放部分高级科技的访问权限给你");
            Line5 = this.GetLocalization(nameof(Line5), () => "如果有需要，随时可以通过量子通讯网络联系我");
        }

        private static void Give(int id, int num) {
            ADVRewardPopup.ShowReward(id, num, "", appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                anchorProvider: () => {
                    var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                    if (rect == Rectangle.Empty) {
                        return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                    }
                    return new Vector2(rect.Center.X, rect.Y - 70f);
                }, offset: Vector2.Zero
                , styleProvider: () => ADVRewardPopup.RewardStyle.Draedon);
        }

        protected override void OnScenarioStart() {
            DraedonEffect.IsActive = true;
            DraedonEffect.Send();
        }

        protected override void OnScenarioComplete() {
            DraedonEffect.IsActive = false;
            DraedonEffect.Send();

            //标记任务完成保存
            if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                halibutPlayer.ADCSave.DeploySignaltowerQuestCompleted = true;
            }
            //开启与变节者的后续对话
            FirstMetTzeentch.Open();
        }

        protected override void Build() {
            //注册嘉登立绘
            DialogueBoxBase.RegisterPortrait(DraedonName.Value, ADVAsset.Draedon2ADV, silhouette: false);
            DialogueBoxBase.RegisterPortrait(DraedonName.Value + red, ADVAsset.Draedon2RedADV, silhouette: false);
            DialogueBoxBase.RegisterPortrait(DraedonName.Value + alt, ADVAsset.DraedonADV, silhouette: false);

            //构建对话流程
            Add(DraedonName.Value + red, Line1.Value);
            Add(DraedonName.Value + alt, Line2.Value);
            Add(DraedonName.Value, Line3.Value);
            Add(DraedonName.Value, Line4.Value, onStart: () => Give(ModContent.ItemType<PQCD>(), 1));
            Add(DraedonName.Value, Line5.Value);
        }
    }
}
