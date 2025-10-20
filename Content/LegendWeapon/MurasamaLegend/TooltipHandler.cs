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
            //击败了史莱姆之神但是没有击败肉山
            if (Level4 && !Main.hardMode) {
                newContent = MuraText.GetTextValue("Subtest_Text0");
                num += "-1";
            }
            else {
                num += "-0";
            }
        }

        private static void ModifyMechBossSelect(int index, ref string newContent, ref string num) {
            if (index != 5) {
                return;
            }//index是5，执行到这里说明刚进入困难模式
            string levelContent = "";

            if (!Level5) {
                return;
            }

            //如果已经击败了灾厄三王，下面枚举判断所有机械Boss的选择
            do {
                if (!NPC.downedMechBoss1) {//毁灭者
                    newContent = MuraText.GetTextValue("Subtest_Text1");
                    levelContent = "-1";
                    break;
                }
                if (!NPC.downedMechBoss2) {//双子魔眼
                    newContent = MuraText.GetTextValue("Subtest_Text2");
                    levelContent = "-2";
                    break;
                }
                if (!NPC.downedMechBoss3) {//机械统帅
                    newContent = MuraText.GetTextValue("Subtest_Text3");
                    levelContent = "-3";
                    break;
                }
            } while (false);
            num += levelContent;
        }
        private static void ModifyGolemSelect(int index, ref string newContent, ref string num) {
            if (index != 7) {
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
