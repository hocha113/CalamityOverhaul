using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Common
{
    internal interface IAdditiveDrawable
    {
        /// <summary>
        /// 用<see cref="BlendState.Additive"/>来绘制，但是在绘制Non层后进行
        /// </summary>
        /// <param name="spriteBatch"></param>
        void DrawAdditiveAfterNon(SpriteBatch spriteBatch);
    }
}
