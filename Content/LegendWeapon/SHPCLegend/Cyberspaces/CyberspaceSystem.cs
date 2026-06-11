using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Banish;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.DomainFreeze;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Restart;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Teleport;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces
{
    /// <summary>
    /// 赛博空间各子系统的逻辑驱动器
    /// <br/>这些 Update 原先挂在 <see cref="CyberspaceRender.UpdateBySystem"/>（InnoVault RenderHandle）上，
    /// 但该钩子不会在专用服务器上运行，导致多人模式下服务端的冻结/放逐计时永不推进：
    /// NPC 在服务端被永久冻结、放逐永不真正抹除、状态列表无限堆积
    /// <br/>挂到 <see cref="ModSystem.PostUpdateEverything"/> 后客户端与服务端同步推进，
    /// 服务端成为冻结时长与放逐抹除的权威端；粒子/音效等演出仍由各处的
    /// <c>Main.dedServ</c> / <c>VaultUtils.isServer</c> 守卫保证只在客户端发生
    /// </summary>
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
