using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Banish
{
    /// <summary>放逐资源</summary>
    internal class CyberBanishAssets
    {
        /// <summary>放逐 NPC 故障着色器</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect CyberBanishNPC { get; private set; }
    }
}
