using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;

namespace CalamityOverhaul.Common
{
    /// <summary>
    /// 全局着色器资源装载点
    /// <br/>仅负责通过 <see cref="VaultLoadenAttribute"/> 自动加载 <c>Assets/Effects</c> 下的着色器
    /// <br/>所有运行时渲染逻辑请放在专门的 <c>RenderHandle</c> 子类中（例如 <see cref="Render.WarpEffectRender"/>）
    /// </summary>
    [VaultLoaden(CWRConstant.Effects)]
    public static class EffectLoader
    {
        public static Asset<Effect> PowerSFShader { get; set; }
        public static Asset<Effect> WarpShader { get; set; }
        public static Asset<Effect> NeutronRing { get; set; }
        public static Asset<Effect> NeutronWarp { get; set; }
        public static Asset<Effect> PrimeHalo { get; set; }
        public static Asset<Effect> DestroyerThermalOutline { get; set; }
        public static Asset<Effect> KnifeRendering { get; set; }
        public static Asset<Effect> KnifeDistortion { get; set; }
        public static Asset<Effect> GradientTrail { get; set; }
        public static Asset<Effect> DeductDraw { get; set; }
        public static Asset<Effect> Crystal { get; set; }
        public static Asset<Effect> AccretionDisk { get; set; }
        public static Asset<Effect> FlattenedDisk { get; set; }
        public static Asset<Effect> BlackHole { get; set; }
        public static Asset<Effect> GammaRayBeam { get; set; }
        public static Asset<Effect> DropPodFlame { get; set; }
        public static Asset<Effect> DropPodShockwave { get; set; }
        public static Asset<Effect> DropPodHeatHaze { get; set; }
        public static Asset<Effect> CyberShockwave { get; set; }
        public static Asset<Effect> CyberRestartField { get; set; }
        public static Asset<Effect> CyberBoundaryRing { get; set; }
        public static Asset<Effect> CyberGlitchBolt { get; set; }
        public static Asset<Effect> CyberRiftSlash { get; set; }
        public static Asset<Effect> CyberReform { get; set; }
        public static Asset<Effect> CyberTraceBeam { get; set; }
        public static Asset<Effect> CyberEnergyOrb { get; set; }
        public static Asset<Effect> CyberDetonation { get; set; }
        public static Asset<Effect> CyberDataArc { get; set; }
        public static Asset<Effect> CyberspaceField { get; set; }
        public static Asset<Effect> CyberPanel { get; set; }
        public static Asset<Effect> CyberDomainPanel { get; set; }
        public static Asset<Effect> SHPCModPanel { get; set; }
        public static Asset<Effect> CyberpunkItemFilter { get; set; }
        public static Asset<Effect> HotwindPanel { get; set; }
        public static Asset<Effect> DraedonPanel { get; set; }
        public static Asset<Effect> ForestPanel { get; set; }
        public static Asset<Effect> NotifBadge { get; set; }
        public static Asset<Effect> ShepelGlitch { get; set; }
        public static Asset<Effect> SeaDomainField { get; set; }
        public static Asset<Effect> OceanCurrentTrail { get; set; }
        public static Asset<Effect> OceanWaterBlob { get; set; }
        public static Asset<Effect> ElysiumHalo { get; set; }
        public static Asset<Effect> ElysiumStaff { get; set; }
        public static Asset<Effect> SerpentTrail { get; set; }
        public static Asset<Effect> CelestialStar { get; set; }
        public static Asset<Effect> BrimstoneDomain { get; set; }
        public static Asset<Effect> BrimstoneBlastWave { get; set; }
        public static Asset<Effect> KingSlimeRoyalAura { get; set; }
        public static Asset<Effect> KingSlimeShockwave { get; set; }
        public static Asset<Effect> KingSlimeRoyalBeam { get; set; }
        public static Asset<Effect> KingSlimeBloodWing { get; set; }
        public static Asset<Effect> CosmicCrescent { get; set; }
        public static Asset<Effect> WitchBrimstoneDomain { get; set; }
        public static Asset<Effect> CelestialDomain { get; set; }
        public static Asset<Effect> ProverbsGhostDomain { get; set; }
        public static Asset<Effect> RevelationPlague { get; set; }
        public static Asset<Effect> VoidPortal { get; set; }
        public static Asset<Effect> AbandonedPortalPanel { get; set; }
        public static Asset<Effect> VoidSuction { get; set; }
        public static Asset<Effect> VoidArrival { get; set; }
        public static Asset<Effect> CyberBossBar { get; set; }
        public static Asset<Effect> HackRamArc { get; set; }
        public static Asset<Effect> SHPCCoreOrb { get; set; }
        public static Asset<Effect> CyberwareRadialPanel { get; set; }
        public static Asset<Effect> CyberwarePanel { get; set; }
        public static Asset<Effect> CyberwareBulletTime { get; set; }
        public static Asset<Effect> ThermalPanel { get; set; }
        public static Asset<Effect> ThermalBar { get; set; }
        public static Asset<Effect> ThermalHeatHaze { get; set; }
        public static Asset<Effect> VoidColonySky { get; set; }
        public static Asset<Effect> VoidFog { get; set; }
        public static Asset<Effect> VoidTimeShift { get; set; }
        public static Asset<Effect> GlitchHead { get; set; }
        public static Asset<Effect> ArchitectureWarp { get; set; }
        public static Asset<Effect> VoidLaserCannon { get; set; }
        public static Asset<Effect> SignalTowerLightning { get; set; }
        public static Asset<Effect> SignalTowerElectrified { get; set; }
        public static Asset<Effect> SignalTowerVirusBroadcast { get; set; }
        public static Asset<Effect> SignalTowerHoverOutline { get; set; }
        public static Asset<Effect> DecryptionPanelBackground { get; set; }
        public static Asset<Effect> BreachMatrixAxisHighlight { get; set; }
        public static Asset<Effect> GatlinTracer { get; set; }
        public static Asset<Effect> GatlinImpactBurst { get; set; }
        public static Asset<Effect> BrimstoneDialogueBox { get; set; }
        public static Asset<Effect> SeaDialogueBox { get; set; }
        public static Asset<Effect> EntrustGuideCard { get; set; }
        public static Asset<Effect> MurasamaPhantomPanel { get; set; }
        public static Asset<Effect> CybCourseSky { get; set; }
        public static Asset<Effect> CybCourseLoading { get; set; }
        public static Asset<Effect> CybCourseEntryReveal { get; set; }
        public static Asset<Effect> VoidColonyLoading { get; set; }
    }
}
