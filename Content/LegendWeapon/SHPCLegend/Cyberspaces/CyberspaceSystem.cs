using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Banish;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.DomainFreeze;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Restart;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Teleport;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces
{
    /// <summary>赛博空间 System，PostUpdateEverything 推进子系统</summary>
    internal class CyberspaceSystem : ModSystem
    {
        public override void PostUpdateEverything() {
            Cyberspace.Update();
            CyberBanish.Update();
            CyberBossExecution.Update();
            CyberDomainFreeze.Update();
            CyberTeleport.Update();
            CyberRestart.Update();
        }

        public override void ClearWorld() => ResetAll();

        internal static void ResetAll() {
            Cyberspace.Reset();
            CyberBanish.Reset();
            CyberBossExecution.Reset();
            CyberDomainFreeze.Reset();
            CyberTeleport.Reset();
            CyberRestart.Reset();
        }
    }
}
