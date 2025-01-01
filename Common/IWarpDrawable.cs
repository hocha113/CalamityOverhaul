using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Common
{
    internal interface IWarpDrawable
    {
        /// <summary>
        /// 是否进行额外的自定义绘制，这层绘制不会被扭曲效果所影响
        /// </summary>
        /// <returns></returns>
        public bool CanDrawCustom() => false;
        /// <summary>
        /// 是否不使用蓝移效果，默认返回<see langword="false"/>
        /// </summary>
        /// <returns></returns>
        public bool DontUseBlueshiftEffect() => false;
        /// <summary>
        /// 一个额外的自定义绘制，这里的绘制内容不会被扭曲影响
        /// </summary>
        /// <param name="spriteBatch"></param>
        public void DrawCustom(SpriteBatch spriteBatch);
        /// <summary>
        /// 扭曲效果相关的操作
        /// </summary>
        public void Warp();
    }
}
