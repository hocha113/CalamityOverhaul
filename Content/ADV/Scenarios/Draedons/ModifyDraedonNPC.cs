using CalamityOverhaul.Content.ADV.Scenarios.Draedons.Defeats;
using CalamityOverhaul.Content.ADV.Scenarios.Draedons.ExoMechdusaSums;
using InnoVault.GameSystem;
using Terraria;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons
{
    internal class ModifyDraedonNPC : NPCOverride
    {
        public override int TargetID => CWRID.NPC_Draedon;

        private int timer;
        private bool defeat;
        private int battleStartTime;
        public override bool AI() {
            timer++;
            return true;
        }

        public override void PostAI() {
            //禁用原版机甲选择UI
            CWRRef.SetAbleToSelectExoMech(Main.player[npc.target], false);

            if (!VaultUtils.isServer && Main.myPlayer == npc.target) {
                //召唤机甲对话
                if (timer == 90) {
                    ScenarioManager.Reset<ExoMechdusaSum>();
                    ScenarioManager.Start<ExoMechdusaSum>();
                    battleStartTime = timer;//记录战斗开始时间
                }

                //正常战败对话
                if (CWRRef.GetDraedonDefeatTimer(npc) > 0 && !defeat) {
                    defeat = true;

                    if (Main.LocalPlayer.TryGetADVSave(out var save)) {
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
                }

                if (DraedonEffect.IsActive) {//哔哔完后再退场
                    float maxTime = 30 + 150 * 8f + 120f;
                    if (CWRRef.GetDraedonDefeatTimer(npc) > maxTime) {
                        CWRRef.SetDraedonDefeatTimer(npc, maxTime);
                    }
                }
            }
        }
    }
}
