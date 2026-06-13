using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Common
{
    internal interface IAdditiveDrawable
    {
        /// <summary><see cref="BlendState.Additive"/> 批次，Non 层之后绘制</summary>
        /// <param name="spriteBatch"></param>
        void DrawAdditiveAfterNon(SpriteBatch spriteBatch);
    }
}
