using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Common
{
    internal interface IAdditiveDrawable
    {
        /// <summary><see cref="BlendState.Additive"/> 批次，Non 层之后</summary>
        void DrawAdditiveAfterNon(SpriteBatch spriteBatch);
    }
}
