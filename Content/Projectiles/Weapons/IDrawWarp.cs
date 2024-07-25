using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.Projectiles.Weapons
{
    internal interface IDrawWarp
    {
        /// <summary>
        /// 是否进行<see cref="costomDraw"/>额外绘制
        /// </summary>
        /// <returns></returns>
        public bool canDraw() => false;
        /// <summary>
        /// 是否不使用蓝移效果，默认返回<see langword="false"/>
        /// </summary>
        /// <returns></returns>
        public bool noBlueshift() => false;
        /// <summary>
        /// 一个额外的自定义绘制，这里的绘制内容不会被扭曲影响
        /// </summary>
        /// <param name="spriteBatch"></param>
        public void costomDraw(SpriteBatch spriteBatch);
        public void Warp();
    }
}
