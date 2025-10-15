using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.ADV.Scenario
{
    internal class FirstMet : ADVScenarioBase, ILocalizedModType
    {
        public override string Key => nameof(FirstMet);
        public static LocalizedText Rolename1 { get; private set; }
        public static LocalizedText Rolename2 { get; private set; }
        public string LocalizationCategory => "Legend.HalibutText.ADV";
        public override void SetStaticDefaults() {
            Rolename1 = this.GetLocalization(nameof(Rolename1), () => "???");
            Rolename2 = this.GetLocalization(nameof(Rolename2), () => "比目鱼");
        }

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(Rolename1.Value, ADVAsset.HeadADV);
            DialogueBoxBase.SetPortraitStyle(Rolename1.Value, silhouette: true);
            DialogueBoxBase.RegisterPortrait(Rolename2.Value, ADVAsset.HeadADV);
            DialogueBoxBase.SetPortraitStyle(Rolename2.Value, silhouette: false);
            Add(Rolename1.Value, "先生，你的钓鱼技法还有很大进步空间");
            Add(Rolename2.Value, "你可以叫我比目鱼博士，或者就叫我比目鱼");
            Add(Rolename2.Value, "很感谢你的鱼钩把我从水里拽了出来，不然我会一直处于死机状态直到真正死去");
            Add(Rolename2.Value, "这是你本来应该钓上来的鱼，我抓住了它，作为见面礼");
            Add(Rolename2.Value, "......");
            Add(Rolename2.Value, "你想问我为什么在这里吗?");
            Add(Rolename2.Value, "我所在的硫磺海大学筹备了一场深渊的科考行动");
            Add(Rolename2.Value, "最后回来的只有我一个人");
            Add(Rolename2.Value, "我们在深渊下面遭遇了无法理解的存在，那是一种......无法被杀死的恐怖");
            Add(Rolename2.Value, "我碰巧从一棵珊瑚树下得到了一只诡异的眼睛，将它吞进了体内");
            Add(Rolename2.Value, "借助它的力量所开启的领域，我逃离了深渊，然后就随机出现在了现在这片水域里");
            Add(Rolename2.Value, "眼睛的力量很诡异，我在使用后便陷入了沉睡，所以我也不知道过去多久了");
            Add(Rolename2.Value, "你看起来是泰拉人，我想你可以带上我");
            Add(Rolename2.Value, "你帮我解决体内那些眼球的复苏问题，我帮你征服这片陆地，如何?");
            Add(Rolename2.Value, "如果没有问题，就先从研究这条鱼开始吧");
        }

        public override void PreProcessSegment(DialogueBoxBase.DialoguePreProcessArgs args) {
            if (args.Index == 3) {
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