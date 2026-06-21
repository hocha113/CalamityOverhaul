using CalamityOverhaul.Content.Items.Accessories;
using CalamityOverhaul.Content.Items.Magic.Pandemoniums;
using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Scenarios.SupCal.End.EternalBlazingNow;
using CalamityOverhaul.Content.Scenarios.SupCal.ModifySupCalNPCs;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.SupCal.SupCalDisplayTexts
{
    internal sealed class SupCalDisplayTextByBossRush : NarrativeDisplayText, ILocalizedModType
    {
        public LocalizedText SummonBossRush { get; private set; }
        public LocalizedText SummonBossRushWithProverbs { get; private set; }
        public LocalizedText StartBossRush { get; private set; }
        public LocalizedText BH2BossRush { get; private set; }
        public LocalizedText BH3BossRush { get; private set; }
        public LocalizedText BrothersBossRush { get; private set; }
        public LocalizedText Phase2BossRush { get; private set; }
        public LocalizedText BH4BossRush { get; private set; }
        public LocalizedText SeekerRingBossRush { get; private set; }
        public LocalizedText BH5BossRush { get; private set; }
        public LocalizedText Sepulcher2BossRush { get; private set; }
        public LocalizedText Desperation1BossRush { get; private set; }
        public LocalizedText Desperation2BossRush { get; private set; }
        public LocalizedText Desperation3BossRush { get; private set; }
        public LocalizedText Desperation4BossRush { get; private set; }
        public LocalizedText Acceptance1BossRush { get; private set; }
        public LocalizedText Acceptance2BossRush { get; private set; }

        public override void SetStaticDefaults() {
            LoadLocalization();

            SetDynamicDialogue("SCalSummonText", () => Main.LocalPlayer.TryGetModPlayer(out ProverbsPlayer proverbsPlayer) && proverbsPlayer.HasProverbs
                ? new DialogueOverride(SummonBossRushWithProverbs, Color.Orange)
                : new DialogueOverride(SummonBossRush, Color.Orange));
            SetDynamicDialogue("SCalStartText", () => new DialogueOverride(StartBossRush, Color.Orange));
            SetDynamicDialogue("SCalBH2Text", () => new DialogueOverride(BH2BossRush, Color.Orange));
            SetDynamicDialogue("SCalBH3Text", () => new DialogueOverride(BH3BossRush, Color.Orange));
            SetDynamicDialogue("SCalBrothersText", () => new DialogueOverride(BrothersBossRush, Color.Orange));
            SetDynamicDialogue("SCalPhase2Text", () => new DialogueOverride(Phase2BossRush, Color.Orange));
            SetDynamicDialogue("SCalBH4Text", () => new DialogueOverride(BH4BossRush, Color.OrangeRed));
            SetDynamicDialogue("SCalSeekerRingText", () => new DialogueOverride(SeekerRingBossRush, Color.Orange));
            SetDynamicDialogue("SCalBH5Text", () => new DialogueOverride(BH5BossRush, Color.Orange));
            SetDynamicDialogue("SCalSepulcher2Text", () => new DialogueOverride(Sepulcher2BossRush, Color.Orange));
            SetDynamicDialogue("SCalDesparationText1", () => new DialogueOverride(Desperation1BossRush, Color.Orange));
            SetDynamicDialogue("SCalDesparationText2", () => new DialogueOverride(Desperation2BossRush, Color.Orange));
            SetDynamicDialogue("SCalDesparationText3", () => new DialogueOverride(Desperation3BossRush, Color.Orange));
            SetDynamicDialogue("SCalDesparationText4", () => new DialogueOverride(Desperation4BossRush, Color.Orange));
            SetDynamicDialogue("SCalAcceptanceText1", () => new DialogueOverride(Acceptance1BossRush, Color.Orange));
            SetDynamicDialogue("SCalAcceptanceText2", () => new DialogueOverride(Acceptance2BossRush, Color.Orange));
            SetDynamicDialogue("SCalDesparationText1Rematch", () => new DialogueOverride(Desperation1BossRush, Color.Orange));
            SetDynamicDialogue("SCalDesparationText2Rematch", () => new DialogueOverride(Desperation2BossRush, Color.Orange));
            SetDynamicDialogue("SCalDesparationText3Rematch", () => new DialogueOverride(Desperation3BossRush, Color.Orange));
            SetDynamicDialogue("SCalDesparationText4Rematch", () => new DialogueOverride(Desperation4BossRush, Color.Orange));
        }

        private void LoadLocalization() {
            SummonBossRush = this.GetLocalization(nameof(SummonBossRush), () => "……你身上的气息和记忆，让我想起了早已死去的那个人");
            SummonBossRushWithProverbs = this.GetLocalization(nameof(SummonBossRushWithProverbs), () => "那枚戒指，连我这样的残影，也感到了它的颤动呢");
            StartBossRush = this.GetLocalization(nameof(StartBossRush), () => "我不过是金源魄的灵异产物，但有的事情，我也必须完成");
            BH2BossRush = this.GetLocalization(nameof(BH2BossRush), () => "你的实力确实不差，但还不够");
            BH3BossRush = this.GetLocalization(nameof(BH3BossRush), () => "往日之影……早已化作灰烬，随风散在你走过的道路上，呵呵……");
            BrothersBossRush = this.GetLocalization(nameof(BrothersBossRush), () => "这些灵魂……是影子的影子。越是复制，越显得悲哀，不是吗？");
            Phase2BossRush = this.GetLocalization(nameof(Phase2BossRush), () => "即便是被复制的生命，也有需要拼尽全力的理由");
            BH4BossRush = this.GetLocalization(nameof(BH4BossRush), () => "站住！");
            SeekerRingBossRush = this.GetLocalization(nameof(SeekerRingBossRush), () => "你的力量，与她记忆中的那个人太相似了，是巧合吗？");
            BH5BossRush = this.GetLocalization(nameof(BH5BossRush), () => "我的存在，就是为了迎接今日这场战斗");
            Sepulcher2BossRush = this.GetLocalization(nameof(Sepulcher2BossRush), () => "最后的试炼就在此刻");
            Desperation1BossRush = this.GetLocalization(nameof(Desperation1BossRush), () => "即使只是残片……");
            Desperation2BossRush = this.GetLocalization(nameof(Desperation2BossRush), () => "别以为胜利已定");
            Desperation3BossRush = this.GetLocalization(nameof(Desperation3BossRush), () => "呵……影子的寿命，本就脆弱到可笑……");
            Desperation4BossRush = this.GetLocalization(nameof(Desperation4BossRush), () => "你赢了……但她的道路，还远远没有结束");
            Acceptance1BossRush = this.GetLocalization(nameof(Acceptance1BossRush), () => "这大概，就是我的最终归宿吧");
            Acceptance2BossRush = this.GetLocalization(nameof(Acceptance2BossRush), () => "祝你好运，杂鱼");
        }

        public override bool PreHandle(ref string key, ref Color color) {
            string result = key.Split('.').Last();
            if (result == "SCalAcceptanceText3" && !VaultUtils.isClient) {
                VaultUtils.SpwanItem(new EntitySource_WorldEvent("BOSSRUSH"), new Item(ModContent.ItemType<Pandemonium>()));
                return false;
            }

            return true;
        }

        public override bool Alive(Player player)
            => ModifySupCalNPC.SetAIState()
                && EbnState.OnEbn(player)
                && ModifySupCalNPC.TrueBossRushStateByAI
                && NPC.AnyNPCs(CWRID.NPC_SupremeCalamitas);
    }
}
