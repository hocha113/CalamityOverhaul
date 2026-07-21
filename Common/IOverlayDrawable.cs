using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Common
{
    /// <summary>
    /// Primitive/Additive 之后的 AlphaBlend 层，压在顶点与加色之上（如握持本体）
    /// </summary>
    internal interface IOverlayDrawable
    {
        /// <summary><see cref="BlendState.AlphaBlend"/> 批次，Primitive/Additive 之后</summary>
        void DrawOverlay(SpriteBatch spriteBatch);
    }
}
