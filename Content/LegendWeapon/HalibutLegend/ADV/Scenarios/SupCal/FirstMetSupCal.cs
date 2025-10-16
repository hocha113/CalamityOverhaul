using System;
using System.Collections.Generic;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.ADV.Scenarios.SupCal
{
    internal class FirstMetSupCal : ADVScenarioBase, ILocalizedModType
    {
        public override string Key => nameof(FirstMetSupCal);
        public string LocalizationCategory => "Legend.HalibutText.ADV";
        
        public static LocalizedText Rolename1 { get; private set; }
        public static LocalizedText Rolename2 { get; private set; }
        public static LocalizedText Line0 { get; private set; }
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Choice1_1 { get; private set; }
        public static LocalizedText Choice1_2 { get; private set; }
        public static LocalizedText Choice1_3 { get; private set; }
        public static LocalizedText Response1_1 { get; private set; }
        public static LocalizedText Response1_2 { get; private set; }
        public static LocalizedText Response1_3 { get; private set; }
        
        //设置场景默认使用硫磺火风格
        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => BrimstoneDialogueBox.Instance;
        
        public override void SetStaticDefaults() {
            Rolename1 = this.GetLocalization(nameof(Rolename1), () => "???");
            Rolename2 = this.GetLocalization(nameof(Rolename2), () => "至尊灾厄");
            Line0 = this.GetLocalization(nameof(Line0), () => "你看起来状态不怎么样");
            Line1 = this.GetLocalization(nameof(Line1), () => "需要给你热热身子吗?");
            
            // 选项文本
            Choice1_1 = this.GetLocalization(nameof(Choice1_1), () => "好啊，来吧");
            Choice1_2 = this.GetLocalization(nameof(Choice1_2), () => "不必了，我自己可以");
            Choice1_3 = this.GetLocalization(nameof(Choice1_3), () => "......");
            
            // 回应文本
            Response1_1 = this.GetLocalization(nameof(Response1_1), () => "很好，有胆量。让我看看你的实力");
            Response1_2 = this.GetLocalization(nameof(Response1_2), () => "自信是好事，但不要过头了");
            Response1_3 = this.GetLocalization(nameof(Response1_3), () => "沉默吗？算了，随你");
        }
        
        protected override void Build() {
            // 注册立绘
            DialogueBoxBase.RegisterPortrait(Rolename1.Value, ADVAsset.SupCalADV);
            DialogueBoxBase.SetPortraitStyle(Rolename1.Value, silhouette: true);
            DialogueBoxBase.RegisterPortrait(Rolename2.Value, ADVAsset.SupCalADV);
            DialogueBoxBase.SetPortraitStyle(Rolename2.Value, silhouette: false);

            // 添加对话
            Add(Rolename1.Value, Line0.Value);
            Add(Rolename2.Value, Line1.Value);
            
            // 添加带选项的对话（这里对话框会暂停，等待玩家选择）
            AddWithChoices(Rolename2.Value, "那么，你的选择是？", new List<Choice> {
                new Choice(Choice1_1.Value, () => {
                    // 选择后继续对话
                    Add(Rolename2.Value, Response1_1.Value);
                    // 继续推进场景
                    DialogueUIRegistry.Current?.EnqueueDialogue(
                        Rolename2.Value, 
                        Response1_1.Value, 
                        onFinish: () => Complete()
                    );
                }),
                new Choice(Choice1_2.Value, () => {
                    Add(Rolename2.Value, Response1_2.Value);
                    DialogueUIRegistry.Current?.EnqueueDialogue(
                        Rolename2.Value, 
                        Response1_2.Value, 
                        onFinish: () => Complete()
                    );
                }),
                new Choice(Choice1_3.Value, () => {
                    Add(Rolename2.Value, Response1_3.Value);
                    DialogueUIRegistry.Current?.EnqueueDialogue(
                        Rolename2.Value, 
                        Response1_3.Value, 
                        onFinish: () => Complete()
                    );
                })
            });
        }
    }
}
