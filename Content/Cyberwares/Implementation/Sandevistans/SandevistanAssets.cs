using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.Sandevistans
{
    /// <summary>斯安威斯坦着色器资源</summary>
    internal class SandevistanAssets
    {
        /// <summary>屏幕后处理 Effect</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect SandevistanScreen { get; private set; }
    }
}
