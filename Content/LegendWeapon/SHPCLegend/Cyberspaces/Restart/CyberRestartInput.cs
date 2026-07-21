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

            //HackTime 中禁用
            if (HackTime.Active) return;

            //领域未激活不抢键
            if (!Cyberspace.Active) return;

            CyberRestart.TryRestart(Player);
        }
    }
}
