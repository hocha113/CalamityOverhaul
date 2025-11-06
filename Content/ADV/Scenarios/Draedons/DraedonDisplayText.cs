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
            "DraedonExoPhase1Text1",
            "DraedonExoPhase1Text2",
            "DraedonExoPhase2Text1",
            "DraedonExoPhase2Text2",
            "DraedonExoPhase3Text1",
            "DraedonExoPhase3Text2",
            "DraedonExoPhase4Text1",
            "DraedonExoPhase4Text2",
            "DraedonExoPhase5Text1",
            "DraedonExoPhase5Text2",
            "DraedonExoPhase6Text1",
            "DraedonExoPhase6Text2",
            "DraedonExoPhase6Text3",
            "DraedonAresEnrageText",

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
            "DraedonEndKillAttemptText"
        };

        public override bool PreHandle(ref string key, ref Color color) {
            //提取key的最后一部分(去除模组前缀)
            string result = key.Split('.').Last();

            //如果是需要屏蔽的对话，返回false阻止显示
            if (BlockedDialogueKeys.Contains(result)) {
                return false;
            }

            return true;
        }
    }
}