using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Scenarios.Draedon.ExoMechdusaSums;
using System.Collections.Generic;
using System.Linq;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Draedon
{
    internal sealed class DraedonDisplayText : NarrativeDisplayText
    {
        private static readonly HashSet<string> BlockedDialogueKeys = [
            "DraedonIntroductionText1",
            "DraedonIntroductionText2",
            "DraedonIntroductionText3",
            "DraedonIntroductionText4",
            "DraedonIntroductionText5",
            "DraedonResummonText",
            "DraedonBossRushText",
            "DraedonEndText1",
            "DraedonEndText2",
            "DraedonEndText3",
            "DraedonEndText4",
            "DraedonEndText5",
            "DraedonEndText6",
            "DraedonEndText7",
            "DraedonEndText8",
            "DraedonEndText9",
            "DraedonEndKillAttemptText",
        ];

        public override bool PreHandle(ref string key, ref Color color) {
            string result = key.Split('.').Last();

            if (ExoMechdusaSum.CompatibleMode) {
                if (result == "EndOfBattle_FirstDefeat1" || result.Contains("EndOfBattle_SuccessiveDefeat")) {
                    ModifyDraedonNPC.DefeatEvent();
                }

                if (result.Contains("IntroductionMonologue")
                    || result.Contains("EndOfBattle_FirstDefeat")
                    || result.Contains("EndOfBattle_SuccessiveDefeat")
                    || CWRMod.Instance.infernum != null && result.Contains("DraedonDefeat")) {
                    return false;
                }
            }

            return !BlockedDialogueKeys.Contains(result);
        }
    }
}
