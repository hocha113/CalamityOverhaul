using CalamityOverhaul.Content.ADV.ADVChoices;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Campsites;
using System;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes
{
    internal class ComeCampsiteFindMe : ADVScenarioBase, ILocalizedModType
    {
        public override string LocalizationCategory => "ADV";

        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => SulfseaDialogueBox.Instance;

        //角色名称
        public static LocalizedText OldDukeName { get; private set; }

        public static LocalizedText B1 { get; private set; }
        public static LocalizedText C1 { get; private set; }
        public static LocalizedText C2 { get; private set; }

        public override void SetStaticDefaults() {
            OldDukeName = this.GetLocalization(nameof(OldDukeName), () => "老公爵");
            B1 = this.GetLocalization(nameof(B1), () => "有事就来营地找我");
            C1 = this.GetLocalization(nameof(C1), () => "有事");
            C2 = this.GetLocalization(nameof(C2), () => "我只是钓来玩玩");
        }

        protected override void Build() {
            //注册老公爵立绘
            DialogueBoxBase.RegisterPortrait(OldDukeName.Value, ADVAsset.OldDuke, null, true);

            //任务已完成，显示简单对话
            AddWithChoices(
                OldDukeName.Value,
                B1.Value,
                [
                    new Choice(C1.Value, Choice1),
                    new Choice(C2.Value, Choice2),
                ],
                styleOverride: () => SulfseaDialogueBox.Instance,
                choiceBoxStyle: ADVChoiceBox.ChoiceBoxStyle.Sulfsea
            );
        }

        private void Choice1() {
            Main.LocalPlayer.Teleport(OldDukeCampsite.CampsitePosition + new Vector2(0, -50), 999);
            ScenarioManager.Reset<CampsiteInteractionDialogue>();
            ScenarioManager.Start<CampsiteInteractionDialogue>();
            Complete();
        }

        private void Choice2() {
            ScenarioManager.Reset<CampsiteInteractionDialogue.CampsiteInteractionDialogue_Choice3>();
            ScenarioManager.Start<CampsiteInteractionDialogue.CampsiteInteractionDialogue_Choice3>();
            Complete();
        }
    }
}
