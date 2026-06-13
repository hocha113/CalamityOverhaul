using CalamityOverhaul.Content.ADV.Scenarios.SupCal.End.EternalBlazingNows;
using CalamityOverhaul.Content.ADV.Scenarios.SupCal.ModifySupCalNPCs;
using CalamityOverhaul.Content.Items.Accessories;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal.SupCalDisplayTexts
{
    /* 这些是原台词
        //SCal (First Battle)
        SCalSummonText: 你享受地狱之旅么？
        SCalStartText: 真奇怪，你应该已经死了才对……
        SCalBH2Text: 距离你上次勉强才击败我的克隆体也没过多久那玩意就是个失败品，不是么？
        SCalBH3Text: 你驾驭着强大的力量，但你使用这股力量只为了自己的私欲
        SCalBrothersText: 你想见见我的家人吗？听上去挺可怕，不是么？
        SCalPhase2Text: 你将痛不欲生
        SCalBH4Text: 别想着逃跑只要你还活着，痛苦就不会离你而去
    ---------------------------------------------------这段台词全部屏蔽-------------------------------------------------------
        SCalSeekerRingText: 一个后起之人，只识杀戮与偷窃，但却以此得到力量我想想，这让我想起了谁……？
        SCalBH5Text: 这场战斗的输赢对你而言毫无意义！那你又有什么理由干涉这一切！
        SCalSepulcher2Text: 我们两人里只有一个可以活下来，但如果那个人是你，这一切还有什么意义？!
        SCalDesparationText1: 给我停下！
        SCalDesparationText2: 如果我在这里失败，我就再无未来可言
        SCalDesparationText3: 一旦你战胜了我，你就只剩下一条道路
        SCalDesparationText4: 而那条道路……同样也无未来可言
        SCalAcceptanceText1: 哪怕他抛弃了一切，他的力量也不会消失
        SCalAcceptanceText2: 我已没有余力去怨恨他了，对你也是如此……
        SCalAcceptanceText3: 现在，一切都取决于你
     --------------------------------------------------这段台词全部屏蔽--------------------------------------------------------
        //SCal (Rematch)
        SCalSummonTextRematch: 如果你想感受一下四级烫伤的话，你可算是找对人了
        SCalStartTextRematch: 他日若你魂销魄散，你会介意我将你的骨头和血肉融入我的造物中吗？
        SCalBH2TextRematch: 你离胜利还差得远着呢
        SCalBH3TextRematch: 自我上一次能在如此有趣的靶子假人身上测试我的魔法，已经过了很久了
        SCalBrothersTextRematch: 只是单有过去形态的空壳罢了，或许在其中依然残存他们的些许灵魂也说不定
        SCalPhase2TextRematch: 再一次，我们开始吧
        SCalBH4TextRematch: 我挺好奇，自我们第一次交手后，你曾否在梦魇中见到过这些？
        SCalSeekerRingTextRematch: 起码你的技术没有退步
        SCalBH5TextRematch: 这难道不令人激动么？
        SCalSepulcher2TextRematch: 注意一下，那个会自己爬的坟墓来了，这是最后一次
        SCalDesparationText1Rematch: 了不起的表现，我认可你的胜利
        SCalDesparationText2Rematch: 毫无疑问，你会遇见比我更加强大的敌人
        SCalDesparationText3Rematch: 我相信你不会犯下和他一样的错误
        SCalDesparationText4Rematch: 至于你的未来会变成什么样子，我很期待
     */
    internal class SupCalDisplayText : ModifyDisplayText, ILocalizedModType
    {
        #region 本地化文本字段
        //首次战斗-召唤文本
        public LocalizedText SummonWithProverbs { get; private set; }
        public LocalizedText SummonWithoutProverbs { get; private set; }
        public LocalizedText SummonRematchWithProverbs { get; private set; }
        public LocalizedText SummonRematchWithoutProverbs { get; private set; }

        //首次战斗-开始文本
        public LocalizedText StartLowHealth { get; private set; }
        public LocalizedText StartMediumHealth { get; private set; }
        public LocalizedText StartDefault { get; private set; }
        public LocalizedText StartRematchLowHealth { get; private set; }
        public LocalizedText StartRematchMediumHealth { get; private set; }
        public LocalizedText StartRematchDefault { get; private set; }

        //BH3阶段文本
        public LocalizedText BH3WithHalibut { get; private set; }
        public LocalizedText BH3WithoutHalibut { get; private set; }

        //Brothers阶段文本
        public LocalizedText BrothersWithHalibut { get; private set; }
        public LocalizedText BrothersWithoutHalibut { get; private set; }

        //Phase2阶段文本
        public LocalizedText Phase2WithHalibut { get; private set; }
        public LocalizedText Phase2WithoutHalibut { get; private set; }

        //BH4阶段文本
        public LocalizedText BH4Text { get; private set; }

        //SeekerRing阶段文本
        public LocalizedText SeekerRingText { get; private set; }

        //BH5阶段文本
        public LocalizedText BH5Text { get; private set; }

        //Sepulcher2阶段文本
        public LocalizedText Sepulcher2Text { get; private set; }

        //Desperation阶段文本
        public LocalizedText Desperation1Text { get; private set; }
        public LocalizedText Desperation2Text { get; private set; }
        public LocalizedText Desperation3Text { get; private set; }
        public LocalizedText Desperation4Text { get; private set; }

        //Acceptance阶段文本
        public LocalizedText Acceptance1Text { get; private set; }
        public LocalizedText Acceptance2Text { get; private set; }

        //故事文本
        public static LocalizedText Story1 { get; private set; }
        public static LocalizedText Story2 { get; private set; }
        public static LocalizedText Story3 { get; private set; }
        public static LocalizedText Story4 { get; private set; }
        #endregion

        private void LoadLocalization() {
            //初始化本地化文本
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
            Desperation2Text = this.GetLocalization(nameof(Desperation2Text), () => "别得意——我之前只是在陪你玩罢了");
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

        public override void SetStaticDefaults() {
            LoadLocalization();

            //设置动态台词
            SetDynamicDialogue("SCalSummonText", () => {
                var player = Main.LocalPlayer;
                if (player.TryGetModPlayer<ProverbsPlayer>(out var proverbsPlayer) && proverbsPlayer.HasProverbs) {
                    return new DialogueOverride(SummonWithProverbs, Color.Orange);
                }
                else {
                    return new DialogueOverride(SummonWithoutProverbs, Color.Yellow);
                }
            });
            SetDynamicDialogue("SCalSummonTextRematch", () => {
                var player = Main.LocalPlayer;
                if (player.TryGetModPlayer<ProverbsPlayer>(out var proverbsPlayer) && proverbsPlayer.HasProverbs) {
                    return new DialogueOverride(SummonRematchWithProverbs, Color.Orange);
                }
                else {
                    return new DialogueOverride(SummonRematchWithoutProverbs, Color.Yellow);
                }
            });
            SetDynamicDialogue("SCalStartText", () => {
                var player = Main.LocalPlayer;
                if (player.statLife < player.statLifeMax2 * 0.3f) {
                    return new DialogueOverride(StartLowHealth, Color.Orange);
                }
                else if (player.statLife < player.statLifeMax2 * 0.7f) {
                    return new DialogueOverride(StartMediumHealth, Color.Yellow);
                }
                else {
                    return new DialogueOverride(StartDefault, null);
                }
            });
            SetDynamicDialogue("SCalStartTextRematch", () => {
                var player = Main.LocalPlayer;
                if (player.statLife < player.statLifeMax2 * 0.3f) {
                    return new DialogueOverride(StartRematchLowHealth, Color.Orange);
                }
                else if (player.statLife < player.statLifeMax2 * 0.7f) {
                    return new DialogueOverride(StartRematchMediumHealth, Color.Yellow);
                }
                else {
                    return new DialogueOverride(StartRematchDefault, null);
                }
            });
            //SCalBH3Text: 你驾驭着强大的力量，但你使用这股力量只为了自己的私欲
            SetDynamicDialogue("SCalBH3Text", () => {
                var player = Main.LocalPlayer;
                if (player.TryGetHalibutPlayer(out var halibutPlayer) && halibutPlayer.HasHalubut) {
                    return new DialogueOverride(BH3WithHalibut, Color.Orange);
                }
                else {
                    return new DialogueOverride(BH3WithoutHalibut, Color.Yellow);
                }
            });
            //SCalBrothersText: 你想见见我的家人吗？听上去挺可怕，不是么？
            SetDynamicDialogue("SCalBrothersText", () => {
                var player = Main.LocalPlayer;
                if (player.TryGetHalibutPlayer(out var halibutPlayer) && halibutPlayer.HasHalubut) {
                    return new DialogueOverride(BrothersWithHalibut, Color.Orange);
                }
                else {
                    return new DialogueOverride(BrothersWithoutHalibut, Color.Yellow);
                }
            });
            //SCalPhase2Text: 你将痛不欲生
            SetDynamicDialogue("SCalPhase2Text", () => {
                var player = Main.LocalPlayer;
                if (player.TryGetHalibutPlayer(out var halibutPlayer) && halibutPlayer.HasHalubut) {
                    return new DialogueOverride(Phase2WithHalibut, Color.Orange);
                }
                else {
                    return new DialogueOverride(Phase2WithoutHalibut, Color.Yellow);
                }
            });
            //SCalBH4Text: 别想着逃跑只要你还活着，痛苦就不会离你而去
            SetDynamicDialogue("SCalBH4Text", () => {
                return new DialogueOverride(BH4Text, Color.Orange);
            });
            //SCalSeekerRingText: 一个后起之人，只识杀戮与偷窃，但却以此得到力量我想想，这让我想起了谁……？
            SetDynamicDialogue("SCalSeekerRingText", () => {
                return new DialogueOverride(SeekerRingText, Color.Orange);
            });
            //SCalBH5Text: 这场战斗的输赢对你而言毫无意义！那你又有什么理由干涉这一切！
            SetDynamicDialogue("SCalBH5Text", () => {
                return new DialogueOverride(BH5Text, Color.Orange);
            });
            //SCalSepulcher2Text: 我们两人里只有一个可以活下来，但如果那个人是你，这一切还有什么意义？!
            SetDynamicDialogue("SCalSepulcher2Text", () => {
                return new DialogueOverride(Sepulcher2Text, Color.Orange);
            });
            //SCalDesparationText1: 给我停下！
            SetDynamicDialogue("SCalDesparationText1", () => {
                return new DialogueOverride(Desperation1Text, Color.Orange);
            });
            //SCalDesparationText2: 如果我在这里失败，我就再无未来可言
            SetDynamicDialogue("SCalDesparationText2", () => {
                return new DialogueOverride(Desperation2Text, Color.Orange);
            });
            //SCalDesparationText3: 一旦你战胜了我，你就只剩下一条道路
            SetDynamicDialogue("SCalDesparationText3", () => {
                return new DialogueOverride(Desperation3Text, Color.Orange);
            });
            //SCalDesparationText4: 而那条道路……同样也无未来可言
            SetDynamicDialogue("SCalDesparationText4", () => {
                return new DialogueOverride(Desperation4Text, Color.Orange);
            });
            //SCalAcceptanceText1: 哪怕他抛弃了一切，他的力量也不会消失
            SetDynamicDialogue("SCalAcceptanceText1", () => {
                return new DialogueOverride(Acceptance1Text, Color.Orange);
            });
            //SCalAcceptanceText2: 我已没有余力去怨恨他了，对你也是如此……
            SetDynamicDialogue("SCalAcceptanceText2", () => {
                return new DialogueOverride(Acceptance2Text, Color.Orange);
            });
        }

        //需要屏蔽的原版对话key
        public static HashSet<string> BlockedDialogueKeys = new HashSet<string>()
        {
            //战败后的接受台词（屏蔽以使用自定义场景)
            "SCalAcceptanceText3",

            //重复战败台词（屏蔽以使用自定义场景)
            "SCalDesparationText1Rematch",
            "SCalDesparationText2Rematch",
            "SCalDesparationText3Rematch",
            "SCalDesparationText4Rematch"
        };

        //依据玩家是否持有比目鱼分发对应版本，避免比目鱼以陌生人身份贸然登场
        private static void StartEternalBlazingNow() {
            if (Main.LocalPlayer.HasHalibut()) {
                ScenarioManager.Reset<EternalBlazingNow>();
                ScenarioManager.Start<EternalBlazingNow>();
            }
            else {
                ScenarioManager.Reset<EternalBlazingNowNoHelen>();
                ScenarioManager.Start<EternalBlazingNowNoHelen>();
            }
        }

        public override bool PreHandle(ref string key, ref Color color) {
            //提取key的最后一部分(去除模组前缀)
            string result = key.Split('.').Last();

            if (NPC.AnyNPCs(CWRID.NPC_SupremeCalamitas) && !VaultUtils.isServer) {
                if (result == "SCalAcceptanceText3") {
                    //Boss已经进入濒死阶段，触发战败对话场景
                    StartEternalBlazingNow();
                    return false;
                }
                if (result == "SCalDesparationText4Rematch") {//如果已经是重复击杀
                    if (!EbnPlayer.OnEbn(Main.LocalPlayer)) {//但玩家因为某些原因还是没有完成BE结局
                        //就再次触发结局场景
                        StartEternalBlazingNow();
                        return false;
                    }
                }
                if (CWRMod.Instance.infernum != null && result == "SCalCongratulations") {
                    //Boss已经进入濒死阶段，触发战败对话场景
                    StartEternalBlazingNow();
                    return false;
                }
            }

            //如果是需要屏蔽的对话，返回false阻止显示
            if (BlockedDialogueKeys.Contains(result)) {
                return false;
            }

            return true;
        }

        public override bool Alive(Player player) {
            return EbnPlayer.IsConquered(player)
                && !CWRWorld.BossRush && !ModifySupCalNPC.TrueBossRushStateByAI
                && NPC.AnyNPCs(CWRID.NPC_SupremeCalamitas);//被攻略后才会触发这些台词
        }
    }
}
