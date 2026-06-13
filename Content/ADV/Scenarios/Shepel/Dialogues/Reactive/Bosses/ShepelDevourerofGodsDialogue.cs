using CalamityOverhaul.Content.ADV.DialogueBoxs;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Dialogues.Reactive.Bosses
{
    //神明吞噬者是灾厄的重要节点，Shepel真正动容
    internal class ShepelDevourerofGodsDialogue : ShepelReactiveDialogueBase
    {
        protected override ShepelReactiveEvent HandledEvent => ShepelReactiveEvent.BossDefeated;
        protected override int TargetBossNpcType => CWRID.NPC_DevourerofGodsHead;

        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }
        public static LocalizedText Line3 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            Line1 = this.GetLocalization(nameof(Line1),
                () => "噬神者的维度轨迹已彻底消失……它以神明为食，是我记录中威胁等级最高的目标之一。");
            Line2 = this.GetLocalization(nameof(Line2),
                () => "您展现出的决断力远超我的预测模型。这份跨越生死的共同战斗数据，我会永远珍藏。");
            Line3 = this.GetLocalization(nameof(Line3),
                () => "请允许我，一直追随在如此耀眼的您的身边。");
        }

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(RoleName.Value, texture: null);
            DialogueBoxBase.SetPortraitStyle(RoleName.Value, silhouette: false);
            ShepelADVData data = Main.LocalPlayer.GetModPlayer<ADVSavePlayer>().ADVSave.Get<ShepelADVData>();
            ConsumeEvent(data);

            Add(RoleName.Value, Line1.Value,
                onStart: () => ShowPortraitWithFace(ShepelFullBodyPortrait.Face.Shocked));
            Add(RoleName.Value, Line2.Value,
                onStart: () => SetPortraitFace(ShepelFullBodyPortrait.Face.Sad));
            Add(RoleName.Value, Line3.Value,
                onStart: () => SetPortraitFace(ShepelFullBodyPortrait.Face.Serious),
                onComplete: Complete);
        }
    }
}
