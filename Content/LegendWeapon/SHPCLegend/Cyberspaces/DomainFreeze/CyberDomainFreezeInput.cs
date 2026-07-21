using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.DomainFreeze
{
    /// <summary>领域冻结按键，CyberFreeze_Key 触发</summary>
    internal class CyberDomainFreezeInput : ModPlayer
    {
        public override void ProcessTriggers(Terraria.GameInput.TriggersSet triggersSet) {
            if (Player.whoAmI != Main.myPlayer) return;
            //HackTime 中禁用
            if (HackTime.Active) return;
            if (CWRKeySystem.CyberFreeze_Key != null && CWRKeySystem.CyberFreeze_Key.JustPressed) {
                CyberDomainFreeze.TriggerFreeze(Player);
            }
        }
    }
}
