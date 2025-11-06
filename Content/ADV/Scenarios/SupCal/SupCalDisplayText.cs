using System.Linq;
using static InnoVault.VaultUtils;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal
{
    /* 这些是原台词
        // SCal (First Battle)
        SCalSummonText: 你享受地狱之旅么？
        SCalStartText: 真奇怪，你应该已经死了才对……
        SCalBH2Text: 距离你上次勉强才击败我的克隆体也没过多久。那玩意就是个失败品，不是么？
        SCalBH3Text: 你驾驭着强大的力量，但你使用这股力量只为了自己的私欲。
        SCalBrothersText: 你想见见我的家人吗？听上去挺可怕，不是么？
        SCalPhase2Text: 你将痛不欲生。
        SCalBH4Text: 别想着逃跑。只要你还活着，痛苦就不会离你而去。
        SCalSeekerRingText: 一个后起之人，只识杀戮与偷窃，但却以此得到力量。我想想，这让我想起了谁……？
        SCalBH5Text: 这场战斗的输赢对你而言毫无意义！那你又有什么理由干涉这一切！
        SCalSepulcher2Text: 我们两人里只有一个可以活下来，但如果那个人是你，这一切还有什么意义？！
        SCalDesparationText1: 给我停下！
        SCalDesparationText2: 如果我在这里失败，我就再无未来可言。
        SCalDesparationText3: 一旦你战胜了我，你就只剩下一条道路。
        SCalDesparationText4: 而那条道路……同样也无未来可言。
        SCalAcceptanceText1: 哪怕他抛弃了一切，他的力量也不会消失。
        SCalAcceptanceText2: 我已没有余力去怨恨他了，对你也是如此……
        SCalAcceptanceText3: 现在，一切都取决于你。
        // SCal (Rematch)
        SCalSummonTextRematch: 如果你想感受一下四级烫伤的话，你可算是找对人了。
        SCalStartTextRematch: 他日若你魂销魄散，你会介意我将你的骨头和血肉融入我的造物中吗？
        SCalBH2TextRematch: 你离胜利还差得远着呢。
        SCalBH3TextRematch: 自我上一次能在如此有趣的靶子假人身上测试我的魔法，已经过了很久了。
        SCalBrothersTextRematch: 只是单有过去形态的空壳罢了，或许在其中依然残存他们的些许灵魂也说不定。
        SCalPhase2TextRematch: 再一次，我们开始吧。
        SCalBH4TextRematch: 我挺好奇，自我们第一次交手后，你曾否在梦魇中见到过这些？
        SCalSeekerRingTextRematch: 起码你的技术没有退步。
        SCalBH5TextRematch: 这难道不令人激动么？
        SCalSepulcher2TextRematch: 注意一下，那个会自己爬的坟墓来了，这是最后一次。
        SCalDesparationText1Rematch: 了不起的表现，我认可你的胜利。
        SCalDesparationText2Rematch: 毫无疑问，你会遇见比我更加强大的敌人。
        SCalDesparationText3Rematch: 我相信你不会犯下和他一样的错误。
        SCalDesparationText4Rematch: 至于你的未来会变成什么样子，我很期待。
     */
    internal class SupCalDisplayText : ModifyDisplayText
    {
        public override bool Handle(ref string key, ref Color color) {
            string result = key.Split('.').Last();
            switch (result) {
                case "SCalSummonText":
                    Text("看样子，你是活腻歪了", color);
                    return false;
                case "SCalSummonTextRematch":
                    Text("我觉得你的行为没什么必要......", color);
                    return false;
            }
            return true;
        }
    }
}
