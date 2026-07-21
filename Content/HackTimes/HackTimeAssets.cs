using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>骇客时间着色器资源</summary>
    internal class HackTimeAssets
    {
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect HackTimeScreen { get; private set; }

        [VaultLoaden(CWRConstant.Effects)]
        public static Effect HackTimeNPCHighlight { get; private set; }

        [VaultLoaden(CWRConstant.Effects)]
        public static Effect HackWraithHighlight { get; private set; }

        [VaultLoaden(CWRConstant.Effects)]
        public static Effect HackTurretCircuitFault { get; private set; }
    }
}
