using CalamityOverhaul.Content.ADV.Scenarios.Draedons.ExoMechdusaSums;
using System.Collections.Generic;
using System.Linq;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons
{
    /*
        // Draedon (introduction)
        DraedonIntroductionText1: 你知道吗？这一刻已经等了太久了。
        DraedonIntroductionText2: 我对一切未知感到着迷，但最让我着迷的莫过于你的本质。
        DraedonIntroductionText3: 我将会向你展示，我那些超越神明的造物。
        DraedonIntroductionText4: 而你，则将在战斗中向我展示你的本质。
        DraedonIntroductionText5: 现在，选择吧。
        DraedonResummonText: 做出你的选择。
        DraedonBossRushText: 做出你的选择。你有20秒的时间。
        // Draedon (Exo Mechs mid-battle dialogue)
        DraedonExoPhase1Text1: 时间与知识的积累带来的不断改进，正是我作品精华所在。
        DraedonExoPhase1Text2: 接近完美的方法，除此再无。
        DraedonExoPhase2Text1: 很好，很好，你的表现水平完全就在误差范围之内。
        DraedonExoPhase2Text2: 这着实令人满意。接下来我们将进入下一个测试环节。
        DraedonExoPhase3Text1: 自我第一次到得知你的存在，我就一直在研究你的战斗，并以此让我的机械变得更强。
        DraedonExoPhase3Text2: 就算是现在，我依然在检测你的行动。一切都在我的计算之内。
        DraedonExoPhase4Text1: 有趣，十分有趣。
        DraedonExoPhase4Text2: 就算是面对更为困难的挑战，你仍可以稳步推进。
        DraedonExoPhase5Text1: 我依然无法理解你的本质，这样下去可不行。
        DraedonExoPhase5Text2: ……我一向追求完美，可惜造化弄人，这一定是我犯下的第一个错误。
        DraedonExoPhase6Text1: 荒谬至极。
        DraedonExoPhase6Text2: 我不会再让那些无用的计算干扰我对这场战斗的观察了。
        DraedonExoPhase6Text3: 我将向你展示，我终极造物的全部威力。
        DraedonAresEnrageText: 真是愚昧，你是无法逃跑的。
        // Draedon (ending)
        DraedonEndText1: 一个未知因素——你，是一个特异点。
        DraedonEndText2: 你对这片大地和它的历史而言，只是外来之人，就和我一样。
        DraedonEndText3: ……很抱歉，但在看了这样一场“展示”之后，我必须得花点时间整理我的思绪。
        DraedonEndText4: 迄今为止喷洒的血液已经让这片大陆变得陈腐无比，毫无生气。
        DraedonEndText5: 你也挥洒了自己的鲜血，但这可能足以带来一个新的时代……是什么，我不知道。但那一定是我所渴望看到的时代。
        DraedonEndText6: 现在，你想要接触那位暴君。可惜我无法帮到你。
        DraedonEndText7: 这并非出自怨恨，毕竟从一开始，我的目标就只有观察刚才的这一场战斗。
        DraedonEndText8: 但你过去也成功过，所以你最后会找到办法的。
        DraedonEndText9: 我必须尊重并承认你的胜利，但现在，我得把注意力放回到我的机械上了。
        DraedonEndKillAttemptText: ……你的行为没什么必要。
     */
    /*
        IntroductionMonologue1: 你知道吗？这一刻已经等了太久了。
		IntroductionMonologue2: 我对一切未知感到着迷，但最让我着迷的莫过于你的本质。
		IntroductionMonologue3: 我将会向你展示，我那些超越神明的造物。
		IntroductionMonologue4: 而你，则将在战斗中向我展示你的本质。
		IntroductionMonologue5: 现在，选择吧。
		IntroductionMonologueBrief: 做出你的选择。
		ExoMechChoiceResponse1: 很好。你的飞行能力将在实验开始后得到提升。
		ExoMechChoiceResponse2: 它们很快就会到来。
		Interjection1: 嗯……
		Interjection2_Thermal_Minor: 直至目前，你看起来只是受到了轻微的热能灼伤。
		Interjection2_Thermal_Major: 直至目前，你看起来已经受到了严重的热能灼伤。
		Interjection2_Thermal_NearLethal: 直至目前，你看起来已经受到了近乎致命的热能灼伤。
		Interjection2_Plasma_Minor: 直至目前，你看起来只是受到了轻微的等离子灼伤。
		Interjection2_Plasma_Major: 直至目前，你看起来已经受到了严重的等离子灼伤。
		Interjection2_Plasma_NearLethal: 直至目前，你看起来已经受到了近乎致命的等离子灼伤。
		Interjection2_Electricity_Minor: 直至目前，你看起来只是因电击受到了轻微的神经损伤。
		Interjection2_Electricity_Major: 直至目前，你看起来已经因电击受到了严重的神经损伤。
		Interjection2_Electricity_NearLethal: 直至目前，你看起来已经因电击受到了近乎致命的神经损伤。
		Interjection2_Internal_Minor: 直至目前，你看起来只是受到了轻微的内脏损伤。
		Interjection2_Internal_Major: 直至目前，你看起来已经受到了严重的内脏损伤。
		Interjection2_Internal_NearLethal: 直至目前，你看起来已经受到了近乎致命的内脏损伤。
		Interjection2_BluntForceTrauma_Minor: 直至目前，你看起来只是受到了轻微的钝器创伤。
		Interjection2_BluntForceTrauma_Major: 直至目前，你看起来已经受到了严重的钝器创伤。
		Interjection2_BluntForceTrauma_NearLethal: 直至目前，你看起来已经受到了近乎致命的钝器创伤。
		Interjection2_Undamaged: 着实令人着迷，你看起来没有受到任何伤害。
		Interjection3: 不错的成果。
		Interjection4: 现在，为下一阶段的测试，我们将把变量重置为对照值。
		Interjection5: 现在，做好准备。
		Interjection6: 我剩余的几台机械很快就会到来。
		Interjection7: 难以置信。
		Interjection8: 迄今为止，你给我提供的所有数据均为无价之宝。
		Interjection9: 现在，是时候进入测试的最终阶段了。
		Interjection10: 我已经命令我的最后一台机械以最大火力运行。
		Interjection10_Plural: 我已经命令我的最后两台机械以最大火力运行。
		Interjection11: 祝你好运。
		EndOfBattle_FirstDefeat1: 出乎意料！
		EndOfBattle_FirstDefeat2: 这一结果远远超越了我预想中的期望。
		EndOfBattle_FirstDefeat3: 我很快就要离开了，你我的这次会面仍有许多需要探讨的部分。不过在此之前……
		EndOfBattle_FirstDefeat4: 拿上这个吧，这是对你愿意付出时间的感谢。
		EndOfBattle_FirstDefeat5: 如果你想再次和我的机械战斗，你应当知道如何使用密码破译器。
		EndOfBattle_FirstDefeat6: 那么，再会。
		EndOfBattle_SuccessiveDefeat1: 又一场实验顺利完成。
		EndOfBattle_SuccessiveDefeat2: 那么，让我分析一下结果……
		EndOfBattle_SuccessiveDefeat3_Perfect: 出乎意料！你对这场战斗的投入、耐心、谨慎与技巧，我都已作充分记录。
		EndOfBattle_SuccessiveDefeat3_Excellent: 你拥有优异的天赋，如果再谨慎些许，一个完美的结果将触手可及。
		EndOfBattle_SuccessiveDefeat3_Good: 这次的成果是有用的。
		EndOfBattle_SuccessiveDefeat3_Acceptable: 这次的成果虽然平庸，但还算有用。
		EndOfBattle_SuccessiveDefeat3_Bad: 这些数据几乎不能使用。
		EndOfBattle_SuccessiveDefeat3_WhyDidYouMeltTheBoss: 这些数据对我而言毫无用处。
		EndOfBattle_SuccessiveDefeat4_Perfect: 这些数据拥有极高的品质。
		EndOfBattle_SuccessiveDefeat4_Excellent: 也许不久之后，你就能够拥有突破性的见解？
		EndOfBattle_SuccessiveDefeat4_ImproveFightTime: 如果你想再次与我的机械战斗，我建议你进行一场更为持久的战斗。
		EndOfBattle_SuccessiveDefeat4_ImproveAggression: 如果你想再次与我的机械战斗，我建议你采用风险更高的战斗方式。
		EndOfBattle_SuccessiveDefeat4_ImproveBuffs: 如果你想再次与我的机械战斗，我建议你减少一些功能性消耗品的使用量。
		EndOfBattle_SuccessiveDefeat4_ImproveHitCounter: 如果你想再次与我的机械战斗，我建议你提升一下你的躲避能力。
		EndOfBattle_SuccessiveDefeat4_Bad: 更为精细的战斗方式将更有助于深入分析战斗。
		EndOfBattle_SuccessiveDefeat4_WhyDidYouMeltTheBoss: 你的战斗时长太短了，短到我都无法得出任何有深意的结论。
		EndOfBattle_SuccessiveDefeat5: 我期待着下一场战斗。
		EndOfBattle_FirstDefeatReconBodyKill1: 有趣……
		EndOfBattle_FirstDefeatReconBodyKill2: 我并非没有将你“易怒”的这一数据点考虑在内。
		EndOfBattle_FirstDefeatReconBodyKill3: 不管怎么样，正如我所说的……
		Death: 精准落在我的计算范围之内。
		PlayerDeathAtAmusingTime: 很不巧，不过仍然落在我的计算范围之内。
		Error: 这个现实的结构被撕裂了。
     */
    /// <summary>
    /// 嘉登对话文本修改器，用于屏蔽原版对话并使用自定义ADV系统
    /// </summary>
    internal class DraedonDisplayText : ModifyDisplayText
    {
        //需要屏蔽的原版嘉登对话key
        public static HashSet<string> BlockedDialogueKeys = new HashSet<string>()
        {
            //介绍对话
            "DraedonIntroductionText1",
            "DraedonIntroductionText2",
            "DraedonIntroductionText3",
            "DraedonIntroductionText4",
            "DraedonIntroductionText5",
            "DraedonResummonText",
            "DraedonBossRushText",

            //战斗中期对话
            //"DraedonExoPhase1Text1",
            //"DraedonExoPhase1Text2",
            //"DraedonExoPhase2Text1",
            //"DraedonExoPhase2Text2",
            //"DraedonExoPhase3Text1",
            //"DraedonExoPhase3Text2",
            //"DraedonExoPhase4Text1",
            //"DraedonExoPhase4Text2",
            //"DraedonExoPhase5Text1",
            //"DraedonExoPhase5Text2",
            //"DraedonExoPhase6Text1",
            //"DraedonExoPhase6Text2",
            //"DraedonExoPhase6Text3",
            //"DraedonAresEnrageText",

            //结束对话
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
        };

        public override bool PreHandle(ref string key, ref Color color) {
            //提取key的最后一部分(去除模组前缀)
            string result = key.Split('.').Last();

            if (ExoMechdusaSum.CompatibleMode) {
                if (result == "EndOfBattle_FirstDefeat1") {
                    ModifyDraedonNPC.DefeatEvent();
                }
                else if (result.Contains("EndOfBattle_SuccessiveDefeat")) {
                    ModifyDraedonNPC.DefeatEvent();
                }
                //你妈的嘉登介绍对话也放过吧求求了
                if (result.Contains("IntroductionMonologue")
                    || result.Contains("EndOfBattle_FirstDefeat")
                    || result.Contains("EndOfBattle_SuccessiveDefeat")) {
                    return false;//这些本地化全部屏蔽
                }
                //这个是炼狱的
                if (CWRMod.Instance.infernum != null && result.Contains("DraedonDefeat")) {
                    return false;//这些本地化全部屏蔽
                }
            }

            //如果是需要屏蔽的对话，返回false阻止显示
            if (BlockedDialogueKeys.Contains(result)) {
                return false;
            }

            return true;
        }
    }
}