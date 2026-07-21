using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Teleport
{
    /// <summary>瞬移按键，Legend_Teleport 触发 TryTeleport</summary>
    internal class CyberTeleportInput : ModPlayer
    {
        public override void ProcessTriggers(Terraria.GameInput.TriggersSet triggersSet) {
            if (Player.whoAmI != Main.myPlayer) return;
            if (CWRKeySystem.Legend_Teleport == null) return;
            if (!CWRKeySystem.Legend_Teleport.JustPressed) return;

            //HackTime 中禁用
            if (HackTime.Active) return;

            //领域未激活不抢键
            if (!Cyberspace.Active) return;

            CyberTeleport.TryTeleport(Player);
        }
    }
}
