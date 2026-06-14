using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.UIs.StorageUIs
{
    /// <summary>
    /// 箱子UI视觉特效接口，不同主题完全不同的粒子和特效
    /// </summary>
    internal interface IChestEffects
    {
        /// <summary>更新所有粒子和特效</summary>
        void UpdateParticles(bool isActive, Vector2 panelPosition, int panelWidth, int panelHeight);
        /// <summary>绘制所有特效</summary>
        void DrawEffects(SpriteBatch spriteBatch, float uiAlpha);
        /// <summary>清空所有特效</summary>
        void Clear();
    }
}
