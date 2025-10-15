using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.ADV.Scenario
{
    internal class FirstMet : ADVScenarioBase
    {
        public override string Key => nameof(FirstMet);
        private bool rewardGiven;
        protected override void Build() {
            DialogueBoxBase.RegisterPortrait("？？？", ADVAsset.HeadADV);
            DialogueBoxBase.SetPortraitStyle("？？？", silhouette: true);
            DialogueBoxBase.RegisterPortrait("哈利鲈特", ADVAsset.HeadADV);
            DialogueBoxBase.SetPortraitStyle("哈利鲈特", silhouette: false);
            Add("？？？", "你是谁？");
            Add("？？？", "你为什么会在这里？");
            Add("？？？", "你是来救我的？");
            Add("？？？", "谢谢你！");
            Add("？？？", "我叫做哈利鲈特，是一条鱼。");
            Add("哈利鲈特", "我被困在这个地方很久了，能见到人类真是太好了！");
            Add("哈利鲈特", "你能帮我出去吗？");
            Add("哈利鲈特", "我知道一个地方，那里有一条通往外界的路。");
            Add("哈利鲈特", "但是路上有很多危险的怪物，我需要你的帮助。");
            Add("哈利鲈特", "我们一起去吧！");
        }

        public override void PreProcessSegment(DialogueBoxBase.DialoguePreProcessArgs args) {
            //第四段结束时给予奖励 Index=3 (0-based)
            if (args.Index == 3 && !rewardGiven) {
                rewardGiven = true;
                ADVRewardPopup.ShowReward(ItemID.Bass, 1, "", appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                    anchorProvider: () => {
                        var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                        if (rect == Rectangle.Empty) {
                            return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                        }
                        return new Vector2(rect.Center.X, rect.Y - 70f);
                    }, offset: Vector2.Zero);
            }
        }
    }
}