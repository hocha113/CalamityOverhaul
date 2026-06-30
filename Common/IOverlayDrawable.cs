using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Common
{
    /// <summary>
    /// 在<see cref="IPrimitiveDrawable"/>与<see cref="IAdditiveDrawable"/>之后以正常Alpha混合绘制，
    /// 用于需要稳定遮挡顶点/加色特效（不被其盖过）的精灵体，例如握持武器本体
    /// </summary>
    internal interface IOverlayDrawable
    {
        /// <summary><see cref="BlendState.AlphaBlend"/> 批次，Primitive/Additive 层之后绘制</summary>
        /// <param name="spriteBatch"></param>
        void DrawOverlay(SpriteBatch spriteBatch);
    }
}
