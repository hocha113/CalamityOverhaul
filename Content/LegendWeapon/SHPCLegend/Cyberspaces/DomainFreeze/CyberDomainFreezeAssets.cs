using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.DomainFreeze
{
    /// <summary>领域冻结资源加载</summary>
    internal class CyberDomainFreezeAssets
    {
        /// <summary>能量波六角网格 fx</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect CyberFreezeWave { get; private set; }

        /// <summary>冻结实体故障+六角覆盖 fx</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect CyberFreezeEntity { get; private set; }

        /// <summary>六角能量罩 fx</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect CyberFreezeCage { get; private set; }
    }
}
