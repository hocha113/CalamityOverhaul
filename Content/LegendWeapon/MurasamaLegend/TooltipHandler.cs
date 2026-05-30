using CalamityOverhaul.Common;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.LegendWeapon.MurasamaLegend.MurasamaOverride;

namespace CalamityOverhaul.Content.LegendWeapon.MurasamaLegend
{
    internal class TooltipHandler
    {
        public static void SetTooltip(Item item, ref List<TooltipLine> tooltips) {
            string keyDisplay = CWRKeySystem.QuestManager_Key?.GetAssignedKeys() is { Count: > 0 } k ? k[0] : CWRLocText.Instance.Notbound.Value;
            tooltips.ReplacePlaceholder("legend_Text", CWRLocText.GetTextValue("Legend_QuestManager_Hint").Replace("{KEY}", keyDisplay), "");
            tooltips.InsertHotkeyBinding(CWRKeySystem.Murasama_TriggerKey, "[KEY1]", noneTip: CWRLocText.Instance.Notbound.Value);
            tooltips.InsertHotkeyBinding(CWRKeySystem.Murasama_DownKey, "[KEY2]", noneTip: CWRLocText.Instance.Notbound.Value);

            string text2 = CWRLocText.GetTextValue("Murasama_Text0");

            //试炼叙事文本已迁移至委托系统(MurasamaTrialQuestLine)，此处仅保留技能解锁与传奇状态
            tooltips.ReplacePlaceholder("[Lang1]", UnlockSkill1(item) ? $"[c/00ff00:{text2}]" : $"[c/808080:{CWRLocText.GetTextValue("Murasama_Text1")}]");
            tooltips.ReplacePlaceholder("[Lang2]", UnlockSkill2(item) ? $"[c/00ff00:{text2}]" : $"[c/808080:{CWRLocText.GetTextValue("Murasama_Text2")}]");
            tooltips.ReplacePlaceholder("[Lang3]", UnlockSkill3(item) ? $"[c/00ff00:{text2}]" : $"[c/808080:{CWRLocText.GetTextValue("Murasama_Text3")}]");

            int index = item.CWR()?.LegendData?.TargetLevel ?? 0;
            string num = (index + 1).ToString();
            if (index == 28) {
                num = CWRLocText.GetTextValue("Murasama_Text_Lang_End");
            }

            string text = LegendData.GetLevelTrialPreText(item.CWR(), "Murasama_Text_Lang_0", num);

            tooltips.ReplacePlaceholder("[Lang4]", text, "");
            tooltips.ReplacePlaceholder("[Text]", "", "");
        }
    }
}
