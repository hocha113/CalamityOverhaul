using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;

namespace CalamityOverhaul.Content.SkyEffects
{
    internal class PGBolt : ILoader
    {
        public Vector2 Position;
        public float Depth;
        public int Life;
        public bool IsAlive;
        private static Asset<Texture2D> boltAsset;
        private static Asset<Texture2D> flashAsset;
        void ILoader.LoadAsset() {
            boltAsset = CWRUtils.GetT2DAsset("CalamityOverhaul/Assets/Sky/PGBolt");
            flashAsset = CWRUtils.GetT2DAsset("CalamityOverhaul/Assets/Sky/PGFlash");
        }
        void ILoader.UnLoad() {
            boltAsset = null;
            flashAsset = null;
        }

        public void Draw(SpriteBatch spriteBatch, Rectangle rectangle, Vector2 value3, float intensity, float scale, float minDepth, float maxDepth) {
            if (IsAlive && Depth > minDepth && Depth < maxDepth) {
                Vector2 value4 = new Vector2(1f / Depth, 0.9f / Depth);
                Vector2 position = (Position - value3) * value4 + value3 - Main.screenPosition;
                if (rectangle.Contains((int)position.X, (int)position.Y)) {
                    Texture2D texture = boltAsset.Value;
                    int life = Life;
                    if (life > 26 && life % 2 == 0) {
                        texture = flashAsset.Value;
                    }
                    float scale2 = life / 30f;
                    spriteBatch.Draw(texture, position, null, Color.White * scale * scale2 * intensity
                        , 0f, Vector2.Zero, value4.X * 5f, SpriteEffects.None, 0f);
                }
            }
        }
    }
}
