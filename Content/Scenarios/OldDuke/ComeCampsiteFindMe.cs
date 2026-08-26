using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Scenarios.OldDuke.Campsites;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldDuke
{
    internal sealed class ComeCampsiteFindMe : NarrativeScenario, ILocalizedModType
    {
        private const string DoneLabel = "done";

        public string LocalizationCategory => "ADV.OldDuke";

        public static LocalizedText B1 { get; private set; }
        public static LocalizedText C1 { get; private set; }
        public static LocalizedText C2 { get; private set; }
        public static LocalizedText B1_NO { get; private set; }

        public override StyleId DefaultStyle => "Sulfsea";

        public override void SetStaticDefaults() {
            B1 = this.GetLocalization(nameof(B1), () => "……有空就来喝杯茶吧");
            B1_NO = this.GetLocalization(nameof(B1_NO), () => "你先找来我这里吧，认一下路");
            C1 = this.GetLocalization(nameof(C1), () => "有正事找你");
            C2 = this.GetLocalization(nameof(C2), () => "我只是想钓着玩玩……");
        }

        protected override void Build(NarrativeComposer n) {
            bool campsiteQuestActive = OldDukeStorySync.Read(
                d => d.OldDukeCooperationAccepted
                    && !d.OldDukeFirstCampsiteDialogueCompleted,
                d => d.OldDukeCooperationAccepted
                    && !d.OldDukeFirstCampsiteDialogueCompleted)
                && OldDukeCampsite.IsGenerated;

            if (campsiteQuestActive) {
                n.Say("OldDuke", B1_NO.Value).End();
                return;
            }

            n.Choice("OldDuke", B1.Value, c => c
                .Option("business", C1.Value, NarrativeTarget.Goto(DoneLabel), onSelect: () => {
                    OldDukeCampsite.TeleportToCampsite(Main.LocalPlayer);
                    NarrativeRouter.Begin<CampsiteInteractionDialogue>();
                })
                .Option("stroll", C2.Value, NarrativeTarget.Goto(DoneLabel), onSelect: () => {
                    CampsiteInteractionDialogue.EntryMode = CampsiteInteractionDialogue.InteractionEntryMode.StrollEnd;
                    NarrativeRouter.Begin<CampsiteInteractionDialogue>();
                }))
             .Label(DoneLabel)
             .End();
        }
    }
}
