using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.UIs.StorageUIs
{
    /// <summary>箱子UI主题特效契约</summary>
    internal interface IChestEffects
    {
        void UpdateParticles(bool isActive, Vector2 panelPosition, int panelWidth, int panelHeight);
        void DrawEffects(SpriteBatch spriteBatch, float uiAlpha);
        void Clear();
    }
}
