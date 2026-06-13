using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Common
{
    internal interface IWarpDrawable
    {
        /// <summary>是否绘制不受扭曲影响的自定义层</summary>
        public bool CanDrawCustom() => false;
        /// <summary>禁用蓝移，默认 false</summary>
        public bool DontUseBlueshiftEffect() => false;
        /// <summary>扭曲管道外的自定义绘制</summary>
        /// <param name="spriteBatch"></param>
        public void DrawCustom(SpriteBatch spriteBatch);
        /// <summary>扭曲采样源绘制</summary>
        public void Warp();
    }
}
