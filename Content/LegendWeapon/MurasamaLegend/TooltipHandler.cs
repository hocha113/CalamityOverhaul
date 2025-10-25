using CalamityOverhaul.Common;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.InWorldBossPhase;
using static CalamityOverhaul.Content.LegendWeapon.MurasamaLegend.MurasamaOverride;

namespace CalamityOverhaul.Content.LegendWeapon.MurasamaLegend
{
    internal class TooltipHandler
    {
        private static void ModifyWallSelect(int index, ref string newContent, ref string num) {
            if (index != 4) {
                return;
            }
            if (Level4 && !Main.hardMode) {
                newContent = MuraText.GetTextValue("Subtest_Text0");
                num += "-1";
            }
        }

        private static void ModifyMechBossSelect(int index, ref string newContent, ref string num) {
            if (index != 5) {
                return;
            }
            if (!Level5) {
                //没有击败全部灾三王，不提示机械三王
                return;
            }
            //根据击败的Boss数量来确定提示
            //击败了所有三个机械Boss
            if (NPC.downedMechBoss1 && NPC.downedMechBoss2 && NPC.downedMechBoss3) {
                //全部击败，不需要提示
                return;
            }
            //击败了两个Boss的情况
            if (NPC.downedMechBoss2 && NPC.downedMechBoss3 && !NPC.downedMechBoss1) {
                //击败了双子和机械统帅，还差毁灭者
                newContent = MuraText.GetTextValue("Subtest_Text1");  //提示击败毁灭者
                num += "-3";
            }
            else if (NPC.downedMechBoss1 && NPC.downedMechBoss3 && !NPC.downedMechBoss2) {
                //击败了毁灭者和机械统帅，还差双子
                newContent = MuraText.GetTextValue("Subtest_Text2");  //提示击败双子
                num += "-3";
            }
            else if (NPC.downedMechBoss1 && NPC.downedMechBoss2 && !NPC.downedMechBoss3) {
                //击败了毁灭者和双子，还差机械统帅
                newContent = MuraText.GetTextValue("Subtest_Text3");  //提示击败机械统帅
                num += "-3";
            }
            //击败了一个Boss的情况
            else if (NPC.downedMechBoss3 && !NPC.downedMechBoss1 && !NPC.downedMechBoss2) {
                //只击败了机械统帅
                newContent = MuraText.GetTextValue("Subtest_Text1");  //提示击败毁灭者（优先）
                num += "-2";
            }
            else if (NPC.downedMechBoss2 && !NPC.downedMechBoss1 && !NPC.downedMechBoss3) {
                //只击败了双子 - 这是问题所在！
                newContent = MuraText.GetTextValue("Subtest_Text1");  //提示击败机械统帅
                num += "-2";  //修复：改为-2而不是-3
            }
            else if (NPC.downedMechBoss1 && !NPC.downedMechBoss2 && !NPC.downedMechBoss3) {
                //只击败了毁灭者
                newContent = MuraText.GetTextValue("Subtest_Text2");  //提示击败双子
                num += "-2";
            }
            //一个都没击败
            else if (Level5) {
                newContent = MuraText.GetTextValue("Subtest_Text1");  //提示击败毁灭者（第一个）
                num += "-1";
            }
        }

        private static void ModifyGolemSelect(int index, ref string newContent, ref string num) {
            if (index != 7) {
                return;
            }
            if (!Level7) {
                //没有击败石巨人，不提示石后boss
                return;
            }
            else if (Downed14.Invoke() && Downed16.Invoke() && !Downed15.Invoke()) {
                newContent = MuraText.GetTextValue("Subtest_Text5");
                num += "-3";
            }
            else if (Downed15.Invoke() && Downed16.Invoke() && !Downed14.Invoke()) {
                newContent = MuraText.GetTextValue("Subtest_Text4");
                num += "-3";
            }
            else if (Downed15.Invoke() && !Downed14.Invoke()) {
                newContent = MuraText.GetTextValue("Subtest_Text4");
                num += "-2";
            }
            else if (Downed16.Invoke() && !Downed14.Invoke()) {
                newContent = MuraText.GetTextValue("Subtest_Text4");
                num += "-2";
            }
            else if (Downed15.Invoke()) {
                newContent = MuraText.GetTextValue("Subtest_Text6");
                num += "-3";
            }
            else if (Downed14.Invoke()) {
                newContent = MuraText.GetTextValue("Subtest_Text5");
                num += "-2";
            }
            else if (Level7) {
                newContent = MuraText.GetTextValue("Subtest_Text4");
                num += "-1";
            }

        }
        private static void ModifyAfterMoonSelect(int index, ref string newContent, ref string num) {
            if (index != 9) {
                return;
            }
            else if (Downed23.Invoke()) {
                newContent = MuraText.GetTextValue("Subtest_Text8");
                num += "-1";
            }
            else if (Level9) {
                newContent = MuraText.GetTextValue("Subtest_Text7");
                num += "-1";
            }
        }
        private static void ModifyAfterMechSelect(int index, ref string newContent, ref string num) {
            if (index != 6) {
                return;
            }
            else if (VDownedV7.Invoke()) {
                newContent = MuraText.GetTextValue("Subtest_Text10");
                num += "-1";
            }
            else if (Level6) {
                newContent = MuraText.GetTextValue("Subtest_Text9");
                num += "-1";
            }
        }

        public static void SetTooltip(Item item, ref List<TooltipLine> tooltips) {
            tooltips.InsertHotkeyBinding(CWRKeySystem.Murasama_TriggerKey, "[KEY1]", noneTip: CWRLocText.Instance.Notbound.Value);
            tooltips.InsertHotkeyBinding(CWRKeySystem.Murasama_DownKey, "[KEY2]", noneTip: CWRLocText.Instance.Notbound.Value);
            
            int index = Mura_Level();
            string newContent = index >= 0 && index <= 14 ? CWRLocText.GetTextValue($"Murasama_TextDictionary_Content_{index}") : "ERROR";
            string text2 = CWRLocText.GetTextValue("Murasama_Text0");
            string text3;
            string text4;
            if (CWRServerConfig.Instance.WeaponEnhancementSystem) {
                string num = (index + 1).ToString();
                if (index == 14) {
                    num = CWRLocText.GetTextValue("Murasama_Text_Lang_End");
                }
                ModifyWallSelect(index, ref newContent, ref num);
                ModifyMechBossSelect(index, ref newContent, ref num);
                ModifyGolemSelect(index, ref newContent, ref num);
                ModifyAfterMoonSelect(index, ref newContent, ref num);
                ModifyAfterMechSelect(index, ref newContent, ref num);
                text3 = LegendData.GetLevelTrialPreText(item.CWR(), "Murasama_Text_Lang_0", num);
                text4 = CWRLocText.GetTextValue("Murasama_No_legend_Content_3");
            }
            else {
                text3 = "";
                text4 = CWRLocText.GetTextValue("Murasama_No_legend_Content_4");
                newContent = Level11 ? CWRLocText.GetTextValue("Murasama_No_legend_Content_2") : CWRLocText.GetTextValue("Murasama_No_legend_Content_1");
            }

            Color newColor = Color.Lerp(Color.IndianRed, Color.White, 0.5f + (float)Math.Sin(Main.GlobalTimeWrappedHourly) * 0.5f);
            tooltips.ReplacePlaceholder("[Text]", VaultUtils.FormatColorTextMultiLine(newContent, newColor), "");

            tooltips.ReplacePlaceholder("[Lang1]", UnlockSkill1(item) ? $"[c/00ff00:{text2}]" : $"[c/808080:{CWRLocText.GetTextValue("Murasama_Text1")}]");
            tooltips.ReplacePlaceholder("[Lang2]", UnlockSkill2(item) ? $"[c/00ff00:{text2}]" : $"[c/808080:{CWRLocText.GetTextValue("Murasama_Text2")}]");
            tooltips.ReplacePlaceholder("[Lang3]", UnlockSkill3(item) ? $"[c/00ff00:{text2}]" : $"[c/808080:{CWRLocText.GetTextValue("Murasama_Text3")}]");

            tooltips.ReplacePlaceholder("[Lang4]", text3);
            tooltips.ReplacePlaceholder("legend_Text", text4);
        }
    }
}
