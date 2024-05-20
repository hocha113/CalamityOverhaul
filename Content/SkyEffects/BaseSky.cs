using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.SkyEffects
{
    internal abstract class BaseSky : CustomSky
    {
        bool active;
        protected float intensity;
        protected float maxIntensity = 0.6f;
        public virtual void SetSkye() {

        }

        public override void Activate(Vector2 position, params object[] args) {
            active = true;
            intensity = 0.01f;
            SetSkye();
        }

        public override void Deactivate(params object[] args) {
            active = false;
        }

        public virtual void DoDraw(SpriteBatch spriteBatch, Rectangle mainRectangle, float intensity, float minDepth, float maxDepth) {

        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            DoDraw(spriteBatch, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), intensity, minDepth, maxDepth);
        }

        public override bool IsActive() {
            return active;
        }

        public override void Reset() {
            active = false;
            intensity = 0.01f;
        }

        public virtual bool CheckActive() {
            return false;
        }

        public override void Update(GameTime gameTime) {
            if (CheckActive()) {
                if (intensity < maxIntensity)
                    intensity += 0.01f;
            }
            else {
                intensity -= 0.01f;
                if (intensity < 0)
                    Deactivate();
            }
        }

        public override Color OnTileColor(Color inColor) {
            return inColor * (1f - intensity);
        }
    }
}
