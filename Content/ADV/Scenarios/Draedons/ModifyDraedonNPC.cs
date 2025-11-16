using CalamityOverhaul.Content.ADV.Scenarios.Draedons.Defeats;
using CalamityOverhaul.Content.ADV.Scenarios.Draedons.ExoMechdusaSums;
using InnoVault.GameSystem;
using Terraria;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons
{
    internal class ModifyDraedonNPC : NPCOverride
    {
        public override int TargetID => CWRID.NPC_Draedon;

        private static int timer;
        private static bool defeat;
        private static int battleStartTime;
        /// <summary>
        /// 是否等待机甲选择UI生成完毕
        /// </summary>
        public static bool AwaitSummonUIbeenGenerated;
        public override void SetProperty() {
            timer = 0;
            defeat = false;
            battleStartTime = 0;
            AwaitSummonUIbeenGenerated = false;
        }
        public override bool AI() {
            timer++;
            return true;
        }

        public static void DefeatEvent() {
            if (defeat) {
                return;
            }
            defeat = true;

            if (CWRRef.GetBossRushActive()) {
                return;//Boss Rush模式下不触发对话
            }

            if (!Main.LocalPlayer.TryGetADVSave(out var save)) {
                return;
            }

            //增加击败次数
            save.ExoMechDefeatCount++;

            //计算战斗时长（帧数）
            int battleDuration = timer - battleStartTime;

            //判断玩家当前血量百分比
            float healthPercent = Main.LocalPlayer.statLife / (float)Main.LocalPlayer.statLifeMax2;

            //未观看过第一次结束对话，播放完整结束对话
            if (!save.ExoMechEndingDialogue) {
                ScenarioManager.Reset<ExoMechEndingDialogue>();
                ScenarioManager.Start<ExoMechEndingDialogue>();
            }
            //第二次击败
            else if (save.ExoMechDefeatCount == 2 && !save.ExoMechSecondDefeat) {
                ScenarioManager.Reset<ExoMechSecondDefeat>();
                ScenarioManager.Start<ExoMechSecondDefeat>();
            }
            //第三次击败
            else if (save.ExoMechDefeatCount == 3 && !save.ExoMechThirdDefeat) {
                ScenarioManager.Reset<ExoMechThirdDefeat>();
                ScenarioManager.Start<ExoMechThirdDefeat>();
            }
            //后续击败根据战斗情况触发不同对话
            else if (save.ExoMechDefeatCount > 3) {
                //快速击败(战斗时长少于2分钟，即7200帧)
                if (battleDuration < 60 * 60 * 2) {
                    ScenarioManager.Reset<ExoMechQuickDefeat>();
                    ScenarioManager.Start<ExoMechQuickDefeat>();
                }
                //艰难战胜
                else if (healthPercent < 0.2f) {
                    ScenarioManager.Reset<ExoMechHardDefeat>();
                    ScenarioManager.Start<ExoMechHardDefeat>();
                }
            }
        }

        public override void PostAI() {
            //根据兼容模式决定是否禁用原版机甲选择UI
            //如果启用兼容模式，则不禁用原版UI；否则禁用原版UI，使用自定义选项框
            if (!ExoMechdusaSum.CompatibleMode) {
                CWRRef.SetAbleToSelectExoMech(Main.player[npc.target], false);
            }

            if (timer == 80) {
                AwaitSummonUIbeenGenerated = true;//标记开始等待生成UI
            }

            if (!VaultUtils.isServer && Main.myPlayer == npc.target) {
                //召唤机甲对话
                if (timer == 90) {
                    ScenarioManager.Reset<ExoMechdusaSum>();
                    ScenarioManager.Start<ExoMechdusaSum>();
                    battleStartTime = timer;//记录战斗开始时间
                }

                //正常战败对话
                if (CWRRef.GetDraedonDefeatTimer(npc) > 0) {
                    DefeatEvent();
                }
            }

            if (timer > 210 && CWRRef.GetDraedonDefeatTimer(npc) > 0) {
                AwaitSummonUIbeenGenerated = false;//如果已经是召唤了机甲后被打败了，重置等待UI生成标记
            }

            if (DraedonEffect.IsActive) {//哔哔完后再退场
                float maxTime = 30 + 150 * 8f + 120f;
                if (CWRRef.GetDraedonDefeatTimer(npc) > maxTime) {
                    CWRRef.SetDraedonDefeatTimer(npc, maxTime);
                }
            }
            //首先确保战败对话结束后迅速退场
            //这里 AwaitSummonUIbeenGenerated 是避免在一些极端情况下，机甲选择UI还没生成出来就开始计时然后强制退场
            else if (timer > 220 && !AwaitSummonUIbeenGenerated && !CWRRef.HasExo()) {//如果哔哔完了就快滚蛋
                float maxTime = 30 + 150 * 8f + 120f;
                if (CWRRef.GetDraedonDefeatTimer(npc) < maxTime) {
                    CWRRef.SetDraedonDefeatTimer(npc, maxTime);
                }
            }
        }
    }
}
