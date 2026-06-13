using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>骇客时间着色器资源</summary>
    internal class HackTimeAssets
    {
        /// <summary>屏幕后处理着色器</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect HackTimeScreen { get; private set; }

        /// <summary>NPC 高亮着色器</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect HackTimeNPCHighlight { get; private set; }

        /// <summary>灵异目标高亮着色器</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect HackWraithHighlight { get; private set; }

        /// <summary>炮台电路故障着色器</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect HackTurretCircuitFault { get; private set; }
    }
}
