using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Restart
{
    /// <summary>Legend_Restart 按键，委托 TryRestart</summary>
    internal class CyberRestartInput : ModPlayer
    {
        public override void ProcessTriggers(Terraria.GameInput.TriggersSet triggersSet) {
            if (Player.whoAmI != Main.myPlayer) return;
            if (CWRKeySystem.Legend_Restart == null) return;
            if (!CWRKeySystem.Legend_Restart.JustPressed) return;

            //骇客时间激活期间禁止使用领域技能
            if (HackTime.Active) return;

            //领域未激活时不抢按键，留给 Halibut 等其它系统响应
            if (!Cyberspace.Active) return;

            CyberRestart.TryRestart(Player);
        }
    }
}
