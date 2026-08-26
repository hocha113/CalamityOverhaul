using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Scenarios.SupCal.End.EternalBlazingNow;
using CalamityOverhaul.Content.Scenarios.SupCal.ModifySupCalNPCs;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.SupCal.SupCalDisplayTexts
{
    internal sealed class SupCalDisplayTextNormal : NarrativeDisplayText, ILocalizedModType
    {
        public override string LocalizationCategory => "ADV.SupCal";

        public LocalizedText SummonText { get; private set; }
        public LocalizedText SummonRematchText { get; private set; }
        public LocalizedText StartText { get; private set; }
        public LocalizedText StartRematchText { get; private set; }
        public LocalizedText BH2Text { get; private set; }
        public LocalizedText BH2RematchText { get; private set; }
        public LocalizedText BH3Text { get; private set; }
        public LocalizedText BH3RematchText { get; private set; }
        public LocalizedText BrothersText { get; private set; }
        public LocalizedText BrothersRematchText { get; private set; }
        public LocalizedText Phase2Text { get; private set; }
        public LocalizedText Phase2RematchText { get; private set; }
        public LocalizedText BH4Text { get; private set; }
        public LocalizedText BH4RematchText { get; private set; }
        public LocalizedText SeekerRingText { get; private set; }
        public LocalizedText SeekerRingRematchText { get; private set; }
        public LocalizedText BH5Text { get; private set; }
        public LocalizedText BH5RematchText { get; private set; }
        public LocalizedText Sepulcher2Text { get; private set; }
        public LocalizedText Sepulcher2RematchText { get; private set; }
        public LocalizedText Desperation1Text { get; private set; }
        public LocalizedText Desperation2Text { get; private set; }
        public LocalizedText Desperation3Text { get; private set; }
        public LocalizedText Desperation4Text { get; private set; }
        public LocalizedText Acceptance1Text { get; private set; }
        public LocalizedText Acceptance2Text { get; private set; }
        public LocalizedText Acceptance3Text { get; private set; }
        public LocalizedText Desperation1RematchText { get; private set; }
        public LocalizedText Desperation2RematchText { get; private set; }
        public LocalizedText Desperation3RematchText { get; private set; }
        public LocalizedText Desperation4RematchText { get; private set; }

        public override void SetStaticDefaults() {
            LoadLocalization();
            SetDynamicDialogue("SCalSummonText", () => new DialogueOverride(SummonText));
            SetDynamicDialogue("SCalSummonTextRematch", () => new DialogueOverride(SummonRematchText));
            SetDynamicDialogue("SCalStartText", () => new DialogueOverride(StartText));
            SetDynamicDialogue("SCalStartTextRematch", () => new DialogueOverride(StartRematchText));
            SetDynamicDialogue("SCalBH2Text", () => new DialogueOverride(BH2Text));
            SetDynamicDialogue("SCalBH2TextRematch", () => new DialogueOverride(BH2RematchText));
            SetDynamicDialogue("SCalBH3Text", () => new DialogueOverride(BH3Text));
            SetDynamicDialogue("SCalBH3TextRematch", () => new DialogueOverride(BH3RematchText));
            SetDynamicDialogue("SCalBrothersText", () => new DialogueOverride(BrothersText));
            SetDynamicDialogue("SCalBrothersTextRematch", () => new DialogueOverride(BrothersRematchText));
            SetDynamicDialogue("SCalPhase2Text", () => new DialogueOverride(Phase2Text));
            SetDynamicDialogue("SCalPhase2TextRematch", () => new DialogueOverride(Phase2RematchText));
            SetDynamicDialogue("SCalBH4Text", () => new DialogueOverride(BH4Text));
            SetDynamicDialogue("SCalBH4TextRematch", () => new DialogueOverride(BH4RematchText));
            SetDynamicDialogue("SCalSeekerRingText", () => new DialogueOverride(SeekerRingText));
            SetDynamicDialogue("SCalSeekerRingTextRematch", () => new DialogueOverride(SeekerRingRematchText));
            SetDynamicDialogue("SCalBH5Text", () => new DialogueOverride(BH5Text));
            SetDynamicDialogue("SCalBH5TextRematch", () => new DialogueOverride(BH5RematchText));
            SetDynamicDialogue("SCalSepulcher2Text", () => new DialogueOverride(Sepulcher2Text));
            SetDynamicDialogue("SCalSepulcher2TextRematch", () => new DialogueOverride(Sepulcher2RematchText));
            SetDynamicDialogue("SCalDesparationText1", () => new DialogueOverride(Desperation1Text));
            SetDynamicDialogue("SCalDesparationText2", () => new DialogueOverride(Desperation2Text));
            SetDynamicDialogue("SCalDesparationText3", () => new DialogueOverride(Desperation3Text));
            SetDynamicDialogue("SCalDesparationText4", () => new DialogueOverride(Desperation4Text));
            SetDynamicDialogue("SCalAcceptanceText1", () => new DialogueOverride(Acceptance1Text));
            SetDynamicDialogue("SCalAcceptanceText2", () => new DialogueOverride(Acceptance2Text));
            SetDynamicDialogue("SCalAcceptanceText3", () => new DialogueOverride(Acceptance3Text));
            SetDynamicDialogue("SCalDesparationText1Rematch", () => new DialogueOverride(Desperation1RematchText));
            SetDynamicDialogue("SCalDesparationText2Rematch", () => new DialogueOverride(Desperation2RematchText));
            SetDynamicDialogue("SCalDesparationText3Rematch", () => new DialogueOverride(Desperation3RematchText));
            SetDynamicDialogue("SCalDesparationText4Rematch", () => new DialogueOverride(Desperation4RematchText));
        }

        private void LoadLocalization() {
            SummonText = this.GetLocalization(nameof(SummonText), () => "这硫磺火海……即便是厉鬼也会感到灼痛吧？");
            SummonRematchText = this.GetLocalization(nameof(SummonRematchText), () => "证明给我看，给我活下去");
            StartText = this.GetLocalization(nameof(StartText), () => "凡人的躯壳在高温下总是如此脆弱……或者，你能给我点惊喜？");
            StartRematchText = this.GetLocalization(nameof(StartRematchText), () => "还没被烧尽吗？那便再来一次，直到把你锻造成钢，或者化为灰");
            BH2Text = this.GetLocalization(nameof(BH2Text), () => "还没有在烈火中崩溃……你比我想象的要耐烧");
            BH2RematchText = this.GetLocalization(nameof(BH2RematchText), () => "我承认你有些胆量，但你的水准还差得远着呢");
            BH3Text = this.GetLocalization(nameof(BH3Text), () => "你跨越千里寻来，就为了让这副皮囊感受痛楚？");
            BH3RematchText = this.GetLocalization(nameof(BH3RematchText), () => "上一次降下这种灾厄，已经是上个世纪的事了，你也要试试么？");
            BrothersText = this.GetLocalization(nameof(BrothersText), () => "他们曾为了那条路燃尽了一切……现在，让这些灰烬来称量你吧");
            BrothersRematchText = this.GetLocalization(nameof(BrothersRematchText), () => "或许只是些许灵魂的空壳罢了，但对付你轻而易举");
            Phase2Text = this.GetLocalization(nameof(Phase2Text), () => "接下来……火焰将不再受控。别死得太快");
            Phase2RematchText = this.GetLocalization(nameof(Phase2RematchText), () => "再一次，我们开始吧");
            BH4Text = this.GetLocalization(nameof(BH4Text), () => "别想着逃出我的手心!");
            BH4RematchText = this.GetLocalization(nameof(BH4RematchText), () => "我很好奇，我们的第一次交手，是否让你长了记性？");
            SeekerRingText = this.GetLocalization(nameof(SeekerRingText), () => "你是怎么躲开的？情况不应该这样……给我停下!");
            SeekerRingRematchText = this.GetLocalization(nameof(SeekerRingRematchText), () => "起码我的眼光没有看错……");
            BH5Text = this.GetLocalization(nameof(BH5Text), () => "我承认刚刚的战斗只不过是小打小闹，现在我将全力以赴！");
            BH5RematchText = this.GetLocalization(nameof(BH5RematchText), () => "这难道不令人激动么？");
            Sepulcher2Text = this.GetLocalization(nameof(Sepulcher2Text), () => "如果我们之中只有一个人可以活下来，你觉得我会希望是谁？");
            Sepulcher2RematchText = this.GetLocalization(nameof(Sepulcher2RematchText), () => "注意了，那个会自己爬的坟墓来了……它正渴望着新的住客");
            Desperation1Text = this.GetLocalization(nameof(Desperation1Text), () => "给我停下！");
            Desperation2Text = this.GetLocalization(nameof(Desperation2Text), () => "如果我在这里失败，那又有什么意义！");
            Desperation3Text = this.GetLocalization(nameof(Desperation3Text), () => "你的路已经有人走到了尽头，那是一条死路……你凭什么认为你能不同？！");
            Desperation4Text = this.GetLocalization(nameof(Desperation4Text), () => "若你只是重复他的疯狂，最终也不过是这些余烬中的一捧新灰罢了！");
            Acceptance1Text = this.GetLocalization(nameof(Acceptance1Text), () => "我把自己烧成了这副非人的模样，只为寻找那一线生机");
            Acceptance2Text = this.GetLocalization(nameof(Acceptance2Text), () => "唉……我已没有余力站起来了……");
            Acceptance3Text = this.GetLocalization(nameof(Acceptance3Text), () => "若你成为我这般异类……或许真的能开辟出……我也未曾见过的未来");
            Desperation1RematchText = this.GetLocalization(nameof(Desperation1RematchText), () => "了不起的表现，你发挥的很好");
            Desperation2RematchText = this.GetLocalization(nameof(Desperation2RematchText), () => "毫无疑问，你的实力每时每刻都在增长");
            Desperation3RematchText = this.GetLocalization(nameof(Desperation3RematchText), () => "我多么希望你不会重蹈我们的覆辙");
            Desperation4RematchText = this.GetLocalization(nameof(Desperation4RematchText), () => "如果你能走出一条新的路，一切都还有希望");
        }

        public override bool Alive(Player player)
            => !EbnState.IsConquered(player)
                && !CWRWorld.BossRush
                && !ModifySupCalNPC.TrueBossRushStateByAI
                && NPC.AnyNPCs(CWRID.NPC_SupremeCalamitas);
    }
}
