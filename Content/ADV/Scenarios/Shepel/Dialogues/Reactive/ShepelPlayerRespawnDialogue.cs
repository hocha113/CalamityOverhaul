using CalamityOverhaul.Content.ADV.DialogueBoxs;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Dialogues.Reactive
{
    //玩家死亡后复活，TALK时Shepel给出关切和轻微责备
    internal class ShepelPlayerRespawnDialogue : ShepelReactiveDialogueBase
    {
        protected override ShepelReactiveEvent HandledEvent => ShepelReactiveEvent.PlayerRespawned;
        public override int DialoguePriority => 52;

        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }
        public static LocalizedText Line3 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            Line1 = this.GetLocalization(nameof(Line1),
                () => "……生命体征重新建立了。主人，您刚才断线了一段时间。");
            Line2 = this.GetLocalization(nameof(Line2),
                () => "通讯中断的时候，我什么都做不了，只能等。每一次等待都格外漫长。下次，别再让我等那么久，好吗？");
            Line3 = this.GetLocalization(nameof(Line3),
                () => "没事了。补充一下生命值，我在。");
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
