using CalamityOverhaul.Content.Narrative.Data;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using CalamityOverhaul.Content.Narrative.Runtime;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative.Scenarios.OldDuke
{
    internal static class OldDukeStorySync
    {
        public static OldDukeStoryData Story => Get(Main.LocalPlayer);

        public static OldDukeStoryData Get(Player player) => player.GetModPlayer<StoryPlayer>().Get<OldDukeStoryData>();

        public static OldDukeInteractionState GetState(Player player) => Get(player).OldDukeState;

        public static void SetState(Player player, OldDukeInteractionState state) {
            Get(player).OldDukeState = state;
            if (player == Main.LocalPlayer) {
                OldDukeEffect.Send();
            }
        }

        public static void MarkMetIfNeeded(Player player) {
            if (GetState(player) == OldDukeInteractionState.NotMet) {
                SetState(player, OldDukeInteractionState.Met);
            }
        }

        public static bool Read(Func<OldDukeStoryData, bool> story, Func<OldDukeStoryData, bool> legacy) {
            if (story(Story)) {
                return true;
            }

            return legacy(Story);
        }

        public static void Write(Action<OldDukeStoryData> story, Action<OldDukeStoryData> legacy) {
            story(Story);
            legacy(Story);
        }

        public static bool IsAnyScenarioActive() {
            return NarrativeRouter.IsActive<FirstMetOldDuke>()
                || NarrativeRouter.IsActive<ComeCampsiteFindMe>()
                || NarrativeRouter.IsActive<CampsiteInteractionDialogue>()
                || NarrativeRouter.IsActive<CampsiteChatDialogue>()
                || NarrativeRouter.IsActive<Quest.FindFragments.FirstCampsiteDialogue>();
        }
    }
}
