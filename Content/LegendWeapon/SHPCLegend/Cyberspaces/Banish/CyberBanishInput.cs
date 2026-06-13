using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Banish
{
    /// <summary>放逐按键，领域激活时放逐光标下目标</summary>
    internal class CyberBanishInput : ModPlayer
    {
        public override void ProcessTriggers(Terraria.GameInput.TriggersSet triggersSet) {
            if (Player.whoAmI != Main.myPlayer) return;
            //骇客时间激活期间禁止使用领域技能
            if (HackTime.Active) return;
            if (CWRKeySystem.CyberBanish_Key != null && CWRKeySystem.CyberBanish_Key.JustPressed) {
                CyberBanish.BanishAtCursor();
            }
        }
    }
}
