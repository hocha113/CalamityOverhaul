using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.LegendWeapon.MurasamaLegend.MurasamaOverride;

namespace CalamityOverhaul.Content.LegendWeapon.MurasamaLegend
{
    internal class TooltipHandler
    {
        public static void SetTooltip(Item item, ref List<TooltipLine> tooltips) {
            string keyDisplay = CWRKeySystem.QuestManager_Key?.GetAssignedKeys() is { Count: > 0 } k ? k[0] : CWRKeySystem.Notbound.Value;
            tooltips.ReplacePlaceholder("legend_Text", LegendUpgradeManagerSystem.QuestManagerHint.Value.Replace("{KEY}", keyDisplay), "");
            tooltips.InsertHotkeyBinding(CWRKeySystem.Murasama_TriggerKey, "[KEY1]", noneTip: CWRKeySystem.Notbound.Value);
            tooltips.InsertHotkeyBinding(CWRKeySystem.Murasama_DownKey, "[KEY2]", noneTip: CWRKeySystem.Notbound.Value);

            string text2 = Text0.Value;

            //试炼叙事文本已迁移至委托系统(MurasamaTrialQuestLine)，此处仅保留技能解锁与传奇状态
            tooltips.ReplacePlaceholder("[Lang1]", UnlockSkill1(item) ? $"[c/00ff00:{text2}]" : $"[c/808080:{Text1.Value.Replace("[Unhook]", Skill1Unhook.ToString())}]");
            tooltips.ReplacePlaceholder("[Lang2]", UnlockSkill2(item) ? $"[c/00ff00:{text2}]" : $"[c/808080:{Text1.Value.Replace("[Unhook]", Skill2Unhook.ToString())}]");
            tooltips.ReplacePlaceholder("[Lang3]", UnlockSkill3(item) ? $"[c/00ff00:{text2}]" : $"[c/808080:{Text1.Value.Replace("[Unhook]", Skill3Unhook.ToString())}]");

            int index = item.CWR()?.LegendData?.TargetLevel ?? 0;
            string num = (index + 1).ToString();
            if (index == 28) {
                num = LegendUpgradeManagerSystem.TrialPassed.Value;
            }

            string text = LegendData.GetLevelTrialPreText(item.CWR(), Text_Lang_0, num);

            tooltips.ReplacePlaceholder("[Lang4]", text, "");
            tooltips.ReplacePlaceholder("[Text]", "", "");
        }
    }
}
