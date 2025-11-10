using CalamityOverhaul.Content.ADV.Scenarios.SupCal.End.EternalBlazingNows;
using CalamityOverhaul.Content.Items.Accessories;
using System.Collections.Generic;
using System.Linq;
using Terraria;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal
{
    /* 这些是原台词
        // SCal (First Battle)
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
        // SCal (Rematch)
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
    internal class SupCalDisplayText : ModifyDisplayText
    {
        public override void SetStaticDefaults() {
            /*
            下面是写给画师以及其他合作进行文学的创作的朋友的：

            示例0：基础台词覆盖
            SetDialogue("SCalSummonText", "看样子，你是活腻歪了");
            SetDialogue("SCalSummonTextRematch", "我觉得你的行为没什么必要......");

            示例1：根据玩家生命值显示不同台词
            SetDynamicDialogue("SCalStartText", () => {
                var player = Main.LocalPlayer;
                if (player.statLife < player.statLifeMax2 * 0.3f) {
                    return new DialogueOverride("你看起来已经奄奄一息了", Color.Red);
                }
                else if (player.statLife < player.statLifeMax2 * 0.7f) {
                    return new DialogueOverride("受伤了？真有意思", Color.Yellow);
                }
                else {
                    return new DialogueOverride("真奇怪，你应该已经死了才对……", Color.White);
                }
            });

            示例2：使用条件构建器根据多个条件选择台词
            SetConditionalDialogue("SCalBH2Text",
                CreateConditional()
                    .When(() => Main.expertMode && Main.masterMode,
                          "在大师模式中挑战我？勇气可嘉", Color.Purple)
                    .When(() => Main.expertMode,
                          "专家模式？也不过如此", Color.Orange)
                    .When(() => Main.LocalPlayer.ZoneUnderworldHeight,
                          "在地狱深处与我战斗，倒也合适", Color.OrangeRed)
                    .Otherwise("距离你上次勉强才击败我的克隆体也没过多久那玩意就是个失败品，不是么？")
            );
             */
            SetDynamicDialogue("SCalSummonText", () => {
                var player = Main.LocalPlayer;
                if (player.TryGetModPlayer<ProverbsPlayer>(out var proverbsPlayer) && proverbsPlayer.HasProverbs) {
                    return new DialogueOverride("你竟然真的戴着那个戒指来了......既然如此，我便不会再留情", Color.Orange);
                }
                else {
                    return new DialogueOverride("哈哈哈......我期待这一刻已经很久了", Color.Yellow);
                }
            });
            SetDynamicDialogue("SCalSummonTextRematch", () => {
                var player = Main.LocalPlayer;
                if (player.TryGetModPlayer<ProverbsPlayer>(out var proverbsPlayer) && proverbsPlayer.HasProverbs) {
                    return new DialogueOverride("你还戴着那个戒指......", Color.Orange);
                }
                else {
                    return new DialogueOverride("又是你......看来你对死亡的理解还不够深刻", Color.Yellow);
                }
            });
            SetDynamicDialogue("SCalStartText", () => {
                var player = Main.LocalPlayer;
                if (player.statLife < player.statLifeMax2 * 0.3f) {
                    return new DialogueOverride("你看起来已经奄奄一息了", Color.Orange);
                }
                else if (player.statLife < player.statLifeMax2 * 0.7f) {
                    return new DialogueOverride("你的技术还有待进步", Color.Yellow);
                }
                else {
                    return new DialogueOverride("真奇怪，你应该已经死了才对……", null);
                }
            });
            SetDynamicDialogue("SCalStartTextRematch", () => {
                var player = Main.LocalPlayer;
                if (player.statLife < player.statLifeMax2 * 0.3f) {
                    return new DialogueOverride("你看起来已经奄奄一息了", Color.Orange);
                }
                else if (player.statLife < player.statLifeMax2 * 0.7f) {
                    return new DialogueOverride("受伤了？真有意思", Color.Yellow);
                }
                else {
                    return new DialogueOverride("真奇怪，你应该已经死了才对……", null);
                }
            });
            //SCalBH3Text: 你驾驭着强大的力量，但你使用这股力量只为了自己的私欲
            SetDynamicDialogue("SCalBH3Text", () => {
                var player = Main.LocalPlayer;
                if (player.TryGetHalibutPlayer(out var halibutPlayer) && halibutPlayer.HasHalubut) {
                    return new DialogueOverride("以凡人之躯驾驭这股诡异的力量，走到如此地步，你们值得敬佩", Color.Orange);
                }
                else {
                    return new DialogueOverride("你很不错，但你什么时候才能意识到，你只是在徒劳的攻击一团火焰", Color.Yellow);
                }
            });
            //SCalBrothersText: 你想见见我的家人吗？听上去挺可怕，不是么？
            SetDynamicDialogue("SCalBrothersText", () => {
                var player = Main.LocalPlayer;
                if (player.TryGetHalibutPlayer(out var halibutPlayer) && halibutPlayer.HasHalubut) {
                    return new DialogueOverride("是时候让你们见见我的家人了，他们失败在成为异类的路上，你们今后可别如此", Color.Orange);
                }
                else {
                    return new DialogueOverride("是时候让你见见我的家人了，若你今后死于他人之手，我也会将你收于此列", Color.Yellow);
                }
            });
            //SCalPhase2Text: 你将痛不欲生
            SetDynamicDialogue("SCalPhase2Text", () => {
                var player = Main.LocalPlayer;
                if (player.TryGetHalibutPlayer(out var halibutPlayer) && halibutPlayer.HasHalubut) {
                    return new DialogueOverride("你们真的不准备求饶吗？", Color.Orange);
                }
                else {
                    return new DialogueOverride("你真的不准备求饶吗？", Color.Yellow);
                }
            });
            //SCalBH4Text: 别想着逃跑只要你还活着，痛苦就不会离你而去
            SetDynamicDialogue("SCalBH4Text", () => {
                return new DialogueOverride("给我站那里别动！", Color.Orange);
            });
            //SCalSeekerRingText: 一个后起之人，只识杀戮与偷窃，但却以此得到力量我想想，这让我想起了谁……？
            SetDynamicDialogue("SCalSeekerRingText", () => {
                return new DialogueOverride("你的力量均来自身外之物，这样的你形如虚设.......这倒让我想起了那个虚伪之人", Color.Orange);
            });
            //SCalBH5Text: 这场战斗的输赢对你而言毫无意义！那你又有什么理由干涉这一切！
            SetDynamicDialogue("SCalBH5Text", () => {
                return new DialogueOverride("胜利的天平有些不确定会倒向何方了...你只需站住一会儿就可以改变现状，好吗？", Color.Orange);
            });
            //SCalSepulcher2Text: 我们两人里只有一个可以活下来，但如果那个人是你，这一切还有什么意义？!
            SetDynamicDialogue("SCalSepulcher2Text", () => {
                return new DialogueOverride("如果今天能活下来的人只有一个，你觉得我会希望是谁？", Color.Orange);
            });
            //SCalDesparationText1: 给我停下！
            SetDynamicDialogue("SCalDesparationText1", () => {
                return new DialogueOverride("给 我 老 老 实 实 站 那 里 你 个 杂 鱼", Color.Orange);
            });
            //SCalDesparationText2: 如果我在这里失败，我就再无未来可言
            SetDynamicDialogue("SCalDesparationText2", () => {
                return new DialogueOverride("别得意，我之前只是一直在让着你罢了", Color.Orange);
            });
            //SCalDesparationText3: 一旦你战胜了我，你就只剩下一条道路
            SetDynamicDialogue("SCalDesparationText3", () => {
                return new DialogueOverride("咳咳咳...我承认我今天的状态不太好", Color.Orange);
            });
            //SCalDesparationText4: 而那条道路……同样也无未来可言
            SetDynamicDialogue("SCalDesparationText4", () => {
                return new DialogueOverride("看来是我输了......能遇见你，真是幸事一件", Color.Orange);
            });
            //SCalAcceptanceText1: 哪怕他抛弃了一切，他的力量也不会消失
            SetDynamicDialogue("SCalAcceptanceText1", () => {
                return new DialogueOverride("我想这是我百年来最开心的一天...", Color.Orange);
            });
            //SCalAcceptanceText2: 我已没有余力去怨恨他了，对你也是如此……
            SetDynamicDialogue("SCalAcceptanceText2", () => {
                return new DialogueOverride("或许你真的是那个可以终结这个该死的时代的人", Color.Orange);
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

        public override bool PreHandle(ref string key, ref Color color) {
            //提取key的最后一部分(去除模组前缀)
            string result = key.Split('.').Last();

            if (result == "SCalAcceptanceText3" && NPC.AnyNPCs(CWRID.NPC_SupremeCalamitas)) {
                //Boss已经进入濒死阶段，触发战败对话场景
                if (!VaultUtils.isServer) {
                    ScenarioManager.Reset<EternalBlazingNow>();
                    ScenarioManager.Start<EternalBlazingNow>();
                }
            }

            //如果是需要屏蔽的对话，返回false阻止显示
            if (BlockedDialogueKeys.Contains(result)) {
                return false;
            }

            return true;
        }

        public override bool Alive(Player player) {
            return EbnPlayer.IsConquered(player);//被攻略后才会触发这些台词
        }
    }
}
