using CalamityOverhaul.Content.ADV.Scenarios.SupCal.End.EternalBlazingNows;
using CalamityOverhaul.Content.ADV.Scenarios.SupCal.ModifySupCalNPCs;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal.SupCalDisplayTexts
{
    internal class SupCalDisplayTextNormal : ModifyDisplayText, ILocalizedModType
    {
        #region 本地化文本字段
        //首次战斗-召唤文本
        public LocalizedText SummonText { get; private set; }
        public LocalizedText SummonRematchText { get; private set; }

        //首次战斗-开始文本
        public LocalizedText StartText { get; private set; }
        public LocalizedText StartRematchText { get; private set; }

        //BH2阶段文本
        public LocalizedText BH2Text { get; private set; }
        public LocalizedText BH2RematchText { get; private set; }

        //BH3阶段文本
        public LocalizedText BH3Text { get; private set; }
        public LocalizedText BH3RematchText { get; private set; }

        //Brothers阶段文本
        public LocalizedText BrothersText { get; private set; }
        public LocalizedText BrothersRematchText { get; private set; }

        //Phase2阶段文本
        public LocalizedText Phase2Text { get; private set; }
        public LocalizedText Phase2RematchText { get; private set; }

        //BH4阶段文本
        public LocalizedText BH4Text { get; private set; }
        public LocalizedText BH4RematchText { get; private set; }

        //SeekerRing阶段文本
        public LocalizedText SeekerRingText { get; private set; }
        public LocalizedText SeekerRingRematchText { get; private set; }

        //BH5阶段文本
        public LocalizedText BH5Text { get; private set; }
        public LocalizedText BH5RematchText { get; private set; }

        //Sepulcher2阶段文本
        public LocalizedText Sepulcher2Text { get; private set; }
        public LocalizedText Sepulcher2RematchText { get; private set; }

        //Desperation阶段文本
        public LocalizedText Desperation1Text { get; private set; }
        public LocalizedText Desperation2Text { get; private set; }
        public LocalizedText Desperation3Text { get; private set; }
        public LocalizedText Desperation4Text { get; private set; }

        //Acceptance阶段文本
        public LocalizedText Acceptance1Text { get; private set; }
        public LocalizedText Acceptance2Text { get; private set; }
        public LocalizedText Acceptance3Text { get; private set; }

        //Rematch Desperation阶段文本
        public LocalizedText Desperation1RematchText { get; private set; }
        public LocalizedText Desperation2RematchText { get; private set; }
        public LocalizedText Desperation3RematchText { get; private set; }
        public LocalizedText Desperation4RematchText { get; private set; }
        #endregion

        private void LoadLocalization() {
            //初始化本地化文本 - 使用原版台词作为占位符

            //召唤文本
            SummonText = this.GetLocalization(nameof(SummonText), () => "你享受地狱之旅么？");
            SummonRematchText = this.GetLocalization(nameof(SummonRematchText), () => "如果你想感受一下四级烫伤的话，你可算是找对人了");

            //开始文本
            StartText = this.GetLocalization(nameof(StartText), () => "真奇怪，你应该已经死了才对……");
            StartRematchText = this.GetLocalization(nameof(StartRematchText), () => "他日若你魂销魄散，你会介意我将你的骨头和血肉融入我的造物中吗？");

            //BH2阶段文本
            BH2Text = this.GetLocalization(nameof(BH2Text), () => "距离你上次勉强才击败我的克隆体也没过多久那玩意就是个失败品，不是么？");
            BH2RematchText = this.GetLocalization(nameof(BH2RematchText), () => "你离胜利还差得远着呢");

            //BH3阶段文本
            BH3Text = this.GetLocalization(nameof(BH3Text), () => "你驾驭着强大的力量，但你使用这股力量只为了自己的私欲");
            BH3RematchText = this.GetLocalization(nameof(BH3RematchText), () => "自我上一次能在如此有趣的靶子假人身上测试我的魔法，已经过了很久了");

            //Brothers阶段文本
            BrothersText = this.GetLocalization(nameof(BrothersText), () => "你想见见我的家人吗？听上去挺可怕，不是么？");
            BrothersRematchText = this.GetLocalization(nameof(BrothersRematchText), () => "只是单有过去形态的空壳罢了，或许在其中依然残存他们的些许灵魂也说不定");

            //Phase2阶段文本
            Phase2Text = this.GetLocalization(nameof(Phase2Text), () => "你将痛不欲生");
            Phase2RematchText = this.GetLocalization(nameof(Phase2RematchText), () => "再一次，我们开始吧");

            //BH4阶段文本
            BH4Text = this.GetLocalization(nameof(BH4Text), () => "别想着逃跑只要你还活着，痛苦就不会离你而去");
            BH4RematchText = this.GetLocalization(nameof(BH4RematchText), () => "我挺好奇，自我们第一次交手后，你曾否在梦魇中见到过这些？");

            //SeekerRing阶段文本
            SeekerRingText = this.GetLocalization(nameof(SeekerRingText), () => "一个后起之人，只识杀戮与偷窃，但却以此得到力量我想想，这让我想起了谁……？");
            SeekerRingRematchText = this.GetLocalization(nameof(SeekerRingRematchText), () => "起码你的技术没有退步");

            //BH5阶段文本
            BH5Text = this.GetLocalization(nameof(BH5Text), () => "这场战斗的输赢对你而言毫无意义！那你又有什么理由干涉这一切！");
            BH5RematchText = this.GetLocalization(nameof(BH5RematchText), () => "这难道不令人激动么？");

            //Sepulcher2阶段文本
            Sepulcher2Text = this.GetLocalization(nameof(Sepulcher2Text), () => "我们两人里只有一个可以活下来，但如果那个人是你，这一切还有什么意义？!");
            Sepulcher2RematchText = this.GetLocalization(nameof(Sepulcher2RematchText), () => "注意一下，那个会自己爬的坟墓来了，这是最后一次");

            //Desperation阶段文本
            Desperation1Text = this.GetLocalization(nameof(Desperation1Text), () => "给我停下！");
            Desperation2Text = this.GetLocalization(nameof(Desperation2Text), () => "如果我在这里失败，我就再无未来可言");
            Desperation3Text = this.GetLocalization(nameof(Desperation3Text), () => "一旦你战胜了我，你就只剩下一条道路");
            Desperation4Text = this.GetLocalization(nameof(Desperation4Text), () => "而那条道路……同样也无未来可言");

            //Acceptance阶段文本
            Acceptance1Text = this.GetLocalization(nameof(Acceptance1Text), () => "哪怕他抛弃了一切，他的力量也不会消失");
            Acceptance2Text = this.GetLocalization(nameof(Acceptance2Text), () => "我已没有余力去怨恨他了，对你也是如此……");
            Acceptance3Text = this.GetLocalization(nameof(Acceptance3Text), () => "现在，一切都取决于你");

            //Rematch Desperation阶段文本
            Desperation1RematchText = this.GetLocalization(nameof(Desperation1RematchText), () => "了不起的表现，我认可你的胜利");
            Desperation2RematchText = this.GetLocalization(nameof(Desperation2RematchText), () => "毫无疑问，你会遇见比我更加强大的敌人");
            Desperation3RematchText = this.GetLocalization(nameof(Desperation3RematchText), () => "我相信你不会犯下和他一样的错误");
            Desperation4RematchText = this.GetLocalization(nameof(Desperation4RematchText), () => "至于你的未来会变成什么样子，我很期待");
        }

        public override void SetStaticDefaults() {
            LoadLocalization();

            //设置动态台词
            SetDynamicDialogue("SCalSummonText", () => new DialogueOverride(SummonText, null));
            SetDynamicDialogue("SCalSummonTextRematch", () => new DialogueOverride(SummonRematchText, null));
            SetDynamicDialogue("SCalStartText", () => new DialogueOverride(StartText, null));
            SetDynamicDialogue("SCalStartTextRematch", () => new DialogueOverride(StartRematchText, null));
            SetDynamicDialogue("SCalBH2Text", () => new DialogueOverride(BH2Text, null));
            SetDynamicDialogue("SCalBH2TextRematch", () => new DialogueOverride(BH2RematchText, null));
            SetDynamicDialogue("SCalBH3Text", () => new DialogueOverride(BH3Text, null));
            SetDynamicDialogue("SCalBH3TextRematch", () => new DialogueOverride(BH3RematchText, null));
            SetDynamicDialogue("SCalBrothersText", () => new DialogueOverride(BrothersText, null));
            SetDynamicDialogue("SCalBrothersTextRematch", () => new DialogueOverride(BrothersRematchText, null));
            SetDynamicDialogue("SCalPhase2Text", () => new DialogueOverride(Phase2Text, null));
            SetDynamicDialogue("SCalPhase2TextRematch", () => new DialogueOverride(Phase2RematchText, null));
            SetDynamicDialogue("SCalBH4Text", () => new DialogueOverride(BH4Text, null));
            SetDynamicDialogue("SCalBH4TextRematch", () => new DialogueOverride(BH4RematchText, null));
            SetDynamicDialogue("SCalSeekerRingText", () => new DialogueOverride(SeekerRingText, null));
            SetDynamicDialogue("SCalSeekerRingTextRematch", () => new DialogueOverride(SeekerRingRematchText, null));
            SetDynamicDialogue("SCalBH5Text", () => new DialogueOverride(BH5Text, null));
            SetDynamicDialogue("SCalBH5TextRematch", () => new DialogueOverride(BH5RematchText, null));
            SetDynamicDialogue("SCalSepulcher2Text", () => new DialogueOverride(Sepulcher2Text, null));
            SetDynamicDialogue("SCalSepulcher2TextRematch", () => new DialogueOverride(Sepulcher2RematchText, null));
            SetDynamicDialogue("SCalDesparationText1", () => new DialogueOverride(Desperation1Text, null));
            SetDynamicDialogue("SCalDesparationText2", () => new DialogueOverride(Desperation2Text, null));
            SetDynamicDialogue("SCalDesparationText3", () => new DialogueOverride(Desperation3Text, null));
            SetDynamicDialogue("SCalDesparationText4", () => new DialogueOverride(Desperation4Text, null));
            SetDynamicDialogue("SCalAcceptanceText1", () => new DialogueOverride(Acceptance1Text, null));
            SetDynamicDialogue("SCalAcceptanceText2", () => new DialogueOverride(Acceptance2Text, null));
            SetDynamicDialogue("SCalAcceptanceText3", () => new DialogueOverride(Acceptance3Text, null));
            SetDynamicDialogue("SCalDesparationText1Rematch", () => new DialogueOverride(Desperation1RematchText, null));
            SetDynamicDialogue("SCalDesparationText2Rematch", () => new DialogueOverride(Desperation2RematchText, null));
            SetDynamicDialogue("SCalDesparationText3Rematch", () => new DialogueOverride(Desperation3RematchText, null));
            SetDynamicDialogue("SCalDesparationText4Rematch", () => new DialogueOverride(Desperation4RematchText, null));
        }

        public override bool Alive(Player player) {
            return !EbnPlayer.IsConquered(player)
                && !CWRWorld.BossRush && !ModifySupCalNPC.TrueBossRushStateByAI
                && NPC.AnyNPCs(CWRID.NPC_SupremeCalamitas);//未攻略状态下才会触发这些台词
        }
    }
}
