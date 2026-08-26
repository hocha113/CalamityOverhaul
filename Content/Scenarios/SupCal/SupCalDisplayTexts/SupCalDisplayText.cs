using CalamityOverhaul.Content.Items.Accessories;
using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Scenarios.SupCal.End.EternalBlazingNow;
using CalamityOverhaul.Content.Scenarios.SupCal.ModifySupCalNPCs;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.SupCal.SupCalDisplayTexts
{
    internal sealed class SupCalDisplayText : NarrativeDisplayText, ILocalizedModType
    {
        public override string LocalizationCategory => "ADV.SupCal";

        public LocalizedText SummonWithProverbs { get; private set; }
        public LocalizedText SummonWithoutProverbs { get; private set; }
        public LocalizedText SummonRematchWithProverbs { get; private set; }
        public LocalizedText SummonRematchWithoutProverbs { get; private set; }
        public LocalizedText StartLowHealth { get; private set; }
        public LocalizedText StartMediumHealth { get; private set; }
        public LocalizedText StartDefault { get; private set; }
        public LocalizedText StartRematchLowHealth { get; private set; }
        public LocalizedText StartRematchMediumHealth { get; private set; }
        public LocalizedText StartRematchDefault { get; private set; }
        public LocalizedText BH3WithHalibut { get; private set; }
        public LocalizedText BH3WithoutHalibut { get; private set; }
        public LocalizedText BrothersWithHalibut { get; private set; }
        public LocalizedText BrothersWithoutHalibut { get; private set; }
        public LocalizedText Phase2WithHalibut { get; private set; }
        public LocalizedText Phase2WithoutHalibut { get; private set; }
        public LocalizedText BH4Text { get; private set; }
        public LocalizedText SeekerRingText { get; private set; }
        public LocalizedText BH5Text { get; private set; }
        public LocalizedText Sepulcher2Text { get; private set; }
        public LocalizedText Desperation1Text { get; private set; }
        public LocalizedText Desperation2Text { get; private set; }
        public LocalizedText Desperation3Text { get; private set; }
        public LocalizedText Desperation4Text { get; private set; }
        public LocalizedText Acceptance1Text { get; private set; }
        public LocalizedText Acceptance2Text { get; private set; }

        public static LocalizedText Story1 { get; private set; }
        public static LocalizedText Story2 { get; private set; }
        public static LocalizedText Story3 { get; private set; }
        public static LocalizedText Story4 { get; private set; }

        private static readonly HashSet<string> BlockedDialogueKeys = [
            "SCalAcceptanceText3",
            "SCalDesparationText1Rematch",
            "SCalDesparationText2Rematch",
            "SCalDesparationText3Rematch",
            "SCalDesparationText4Rematch"
        ];

        public override void SetStaticDefaults() {
            LoadLocalization();

            SetDynamicDialogue("SCalSummonText", () => Main.LocalPlayer.TryGetModPlayer(out ProverbsPlayer proverbsPlayer) && proverbsPlayer.HasProverbs
                ? new DialogueOverride(SummonWithProverbs, Color.Orange)
                : new DialogueOverride(SummonWithoutProverbs, Color.Yellow));
            SetDynamicDialogue("SCalSummonTextRematch", () => Main.LocalPlayer.TryGetModPlayer(out ProverbsPlayer proverbsPlayer) && proverbsPlayer.HasProverbs
                ? new DialogueOverride(SummonRematchWithProverbs, Color.Orange)
                : new DialogueOverride(SummonRematchWithoutProverbs, Color.Yellow));
            SetDynamicDialogue("SCalStartText", () => {
                Player player = Main.LocalPlayer;
                if (player.statLife < player.statLifeMax2 * 0.3f) {
                    return new DialogueOverride(StartLowHealth, Color.Orange);
                }
                if (player.statLife < player.statLifeMax2 * 0.7f) {
                    return new DialogueOverride(StartMediumHealth, Color.Yellow);
                }
                return new DialogueOverride(StartDefault);
            });
            SetDynamicDialogue("SCalStartTextRematch", () => {
                Player player = Main.LocalPlayer;
                if (player.statLife < player.statLifeMax2 * 0.3f) {
                    return new DialogueOverride(StartRematchLowHealth, Color.Orange);
                }
                if (player.statLife < player.statLifeMax2 * 0.7f) {
                    return new DialogueOverride(StartRematchMediumHealth, Color.Yellow);
                }
                return new DialogueOverride(StartRematchDefault);
            });
            SetDynamicDialogue("SCalBH3Text", () => HasHalibut()
                ? new DialogueOverride(BH3WithHalibut, Color.Orange)
                : new DialogueOverride(BH3WithoutHalibut, Color.Yellow));
            SetDynamicDialogue("SCalBrothersText", () => HasHalibut()
                ? new DialogueOverride(BrothersWithHalibut, Color.Orange)
                : new DialogueOverride(BrothersWithoutHalibut, Color.Yellow));
            SetDynamicDialogue("SCalPhase2Text", () => HasHalibut()
                ? new DialogueOverride(Phase2WithHalibut, Color.Orange)
                : new DialogueOverride(Phase2WithoutHalibut, Color.Yellow));
            SetDynamicDialogue("SCalBH4Text", () => new DialogueOverride(BH4Text, Color.Orange));
            SetDynamicDialogue("SCalSeekerRingText", () => new DialogueOverride(SeekerRingText, Color.Orange));
            SetDynamicDialogue("SCalBH5Text", () => new DialogueOverride(BH5Text, Color.Orange));
            SetDynamicDialogue("SCalSepulcher2Text", () => new DialogueOverride(Sepulcher2Text, Color.Orange));
            SetDynamicDialogue("SCalDesparationText1", () => new DialogueOverride(Desperation1Text, Color.Orange));
            SetDynamicDialogue("SCalDesparationText2", () => new DialogueOverride(Desperation2Text, Color.Orange));
            SetDynamicDialogue("SCalDesparationText3", () => new DialogueOverride(Desperation3Text, Color.Orange));
            SetDynamicDialogue("SCalDesparationText4", () => new DialogueOverride(Desperation4Text, Color.Orange));
            SetDynamicDialogue("SCalAcceptanceText1", () => new DialogueOverride(Acceptance1Text, Color.Orange));
            SetDynamicDialogue("SCalAcceptanceText2", () => new DialogueOverride(Acceptance2Text, Color.Orange));
        }

        private void LoadLocalization() {
            SummonWithProverbs = this.GetLocalization(nameof(SummonWithProverbs), () => "你竟然真的戴着那个戒指来了……既然如此，我便不会再留情");
            SummonWithoutProverbs = this.GetLocalization(nameof(SummonWithoutProverbs), () => "哈哈哈……这一刻，我已经期待许久");
            SummonRematchWithProverbs = this.GetLocalization(nameof(SummonRematchWithProverbs), () => "你还戴着那个戒指……");
            SummonRematchWithoutProverbs = this.GetLocalization(nameof(SummonRematchWithoutProverbs), () => "又是你……看来你对死亡的理解还不够深刻");
            StartLowHealth = this.GetLocalization(nameof(StartLowHealth), () => "你看起来已经奄奄一息了");
            StartMediumHealth = this.GetLocalization(nameof(StartMediumHealth), () => "你的技术还有待进步");
            StartDefault = this.GetLocalization(nameof(StartDefault), () => "真奇怪，你应该已经死了才对……");
            StartRematchLowHealth = this.GetLocalization(nameof(StartRematchLowHealth), () => "你看起来已经奄奄一息了");
            StartRematchMediumHealth = this.GetLocalization(nameof(StartRematchMediumHealth), () => "受伤了？真有意思");
            StartRematchDefault = this.GetLocalization(nameof(StartRematchDefault), () => "真奇怪，你应该已经死了才对……");
            BH3WithHalibut = this.GetLocalization(nameof(BH3WithHalibut), () => "以凡人之躯驾驭这种诡异的力量……走到如今，你们确实值得尊敬");
            BH3WithoutHalibut = this.GetLocalization(nameof(BH3WithoutHalibut), () => "你很不错，但你什么时候才能意识到，你只是在徒劳的攻击一团火焰");
            BrothersWithHalibut = this.GetLocalization(nameof(BrothersWithHalibut), () => "是时候让你们见见我的家人了，他们失败在成为异类的路上，你们今后可别如此");
            BrothersWithoutHalibut = this.GetLocalization(nameof(BrothersWithoutHalibut), () => "是时候让你见见我的家人了。若你日后死于某人的手下，我会将你的灵魂收于此处");
            Phase2WithHalibut = this.GetLocalization(nameof(Phase2WithHalibut), () => "你们真的不打算求饶吗？");
            Phase2WithoutHalibut = this.GetLocalization(nameof(Phase2WithoutHalibut), () => "你真的不准备求饶？");
            BH4Text = this.GetLocalization(nameof(BH4Text), () => "给我站在那里别动！");
            SeekerRingText = this.GetLocalization(nameof(SeekerRingText), () => "你的力量皆非己出。失去外物，你什么都不是……真像极了那个虚伪之人");
            BH5Text = this.GetLocalization(nameof(BH5Text), () => "胜利的天平有些不确定会倒向何方了...你只需站住一会儿就可以改变现状，好吗？");
            Sepulcher2Text = this.GetLocalization(nameof(Sepulcher2Text), () => "如果今天只能活一个，你觉得我会希望是谁？");
            Desperation1Text = this.GetLocalization(nameof(Desperation1Text), () => "给 我 老 老 实 实 站 那 里，杂 鱼");
            Desperation2Text = this.GetLocalization(nameof(Desperation2Text), () => "别得意，我之前只是在陪你玩罢了");
            Desperation3Text = this.GetLocalization(nameof(Desperation3Text), () => "咳……咳……我承认，今天的状态不太好");
            Desperation4Text = this.GetLocalization(nameof(Desperation4Text), () => "看来是我输了……遇见你，真是幸事一件");
            Acceptance1Text = this.GetLocalization(nameof(Acceptance1Text), () => "这是我百年来最开心的一天……");
            Acceptance2Text = this.GetLocalization(nameof(Acceptance2Text), () => "或许，你真的能终结这个该死的时代");
            Story1 = this.GetLocalization(nameof(Story1), () => @"弩身的木料来自一口埋在地下百年的漆黑棺材，有股洗不掉的土腥味。
在那下面，它用于压制我也难以处理的恐怖。
面对那些东西，单纯的毁灭毫无意义，它们杀不死。
射出楔子，把它们钉死在地上，使其重新沉寂。
至于为什么要先把弩扔过去？
嗯……不先让棺椁的气息沾染客人，这口棺材怎么知道该收殓谁呢？");
            Story2 = this.GetLocalization(nameof(Story2), () => @"这破刀很钝，因为它不是用来割肉的。
在我还是学徒的时候，老师的心脏上长了张怎么也闭不上的嘴。
他后来死了，那颗心却还在跳，每跳一次，嘴尖叫一次，周围的人就少一个。
为了活命，我必须在他下一次心跳之前，把心脏挖出来。
现在的它，相当渴望再听到那剜心的声音。");
            Story3 = this.GetLocalization(nameof(Story3), () => @"刀里面掺了大量的黄金，外面缠着硫火，为了锁住刀刃上那六只不安分的鬼手。
这三个蠢货生前把彼此肢解，再拼接在一起，以此来延缓自身的复苏，
结果变成了一团只会掐人的鬼物。
握紧了哦，一旦硫磺火的压制失效，它们第一个捏断的就是拿刀人的脖子。
这是它们所剩不多的本能。");
            Story4 = this.GetLocalization(nameof(Story4), () => @"凡人以为这是魔法，但我称之为借用。
这本书里，每一页都封存着曾让整座城邦陷入死寂的恐怖。");
        }

        public override bool PreHandle(ref string key, ref Color color) {
            string result = key.Split('.').Last();
            if (NPC.AnyNPCs(CWRID.NPC_SupremeCalamitas) && !VaultUtils.isServer) {
                if (result == "SCalAcceptanceText3"
                    || result == "SCalDesparationText4Rematch" && !EbnState.OnEbn(Main.LocalPlayer)
                    || CWRMod.Instance.infernum != null && result == "SCalCongratulations") {
                    StartEternalBlazingNow();
                    return false;
                }
            }

            return !BlockedDialogueKeys.Contains(result);
        }

        public override bool Alive(Player player)
            => EbnState.IsConquered(player)
                && !CWRWorld.BossRush
                && !ModifySupCalNPC.TrueBossRushStateByAI
                && NPC.AnyNPCs(CWRID.NPC_SupremeCalamitas);

        private static bool HasHalibut()
            => Main.LocalPlayer.TryGetHalibutPlayer(out var halibutPlayer) && halibutPlayer.HasHalubut;

        private static void StartEternalBlazingNow() {
            if (Main.LocalPlayer.HasHalibut()) {
                NarrativeRouter.Begin<EternalBlazingNow>();
            }
            else {
                NarrativeRouter.Begin<EternalBlazingNowNoHelen>();
            }
        }
    }
}
