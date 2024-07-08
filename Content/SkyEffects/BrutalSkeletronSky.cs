using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ID;

namespace CalamityOverhaul.Content.SkyEffects
{
    internal class BrutalSkeletronSky : CustomSky, ILoader
    {
        void ILoader.LoadData() {
            if (CWRUtils.isServer) {
                return;
            }
            SkyManager.Instance[name] = this;
            Filters.Scene[name] = new Filter(new ScreenShaderData("FilterMiniTower").UseColor(0.15f, 0.1f, 0.1f).UseOpacity(0.3f), EffectPriority.High);
        }
        internal static string name => "CWRMod:BrutalSkeletronSky";
        private bool active;
        private float intensity;
        private float maxIntensity = 0.6f;
        public override void Activate(Vector2 position, params object[] args) {
            active = true;
            intensity = 0.01f;
        }

        public override void Deactivate(params object[] args) {
            active = false;
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            spriteBatch.Draw(CWRUtils.GetT2DValue(CWRConstant.Placeholder2)
                , new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.Black * intensity);
        }

        public override bool IsActive() {
            return active || intensity > 0;
        }

        public override void Reset() {
            active = false;
            intensity = 0.01f;
        }

        public override void Update(GameTime gameTime) {
            if (NPC.AnyNPCs(NPCID.SkeletronPrime)) {
                if (intensity < 0.3f) {
                    intensity += 0.005f;
                }
            }
            else {
                intensity -= 0.005f;
                if (intensity < 0) {
                    Deactivate();
                }
            }
        }

        public override Color OnTileColor(Color inColor) {
            return inColor;
        }
    }
}
