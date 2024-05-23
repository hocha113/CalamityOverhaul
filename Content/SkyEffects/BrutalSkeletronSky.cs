using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ID;

namespace CalamityOverhaul.Content.SkyEffects
{
    internal class BrutalSkeletronSky : CustomSky, ISetupData
    {
        void ISetupData.LoadData() {
            if (CWRUtils.isServer) {
                return;
            }
            SkyManager.Instance["CWRMod:BrutalSkeletronSky"] = this;
        }

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
                , new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), new Color(60, 220, Main.DiscoR) * intensity);
        }

        public override bool IsActive() {
            return active;
        }

        public override void Reset() {
            active = false;
            intensity = 0.01f;
        }

        public override void Update(GameTime gameTime) {
            if (NPC.AnyNPCs(NPCID.SkeletronPrime)) {
                if (intensity < maxIntensity) {
                    intensity += 0.01f;
                }
            }
            else {
                intensity -= 0.01f;
                if (intensity < 0) {
                    Deactivate();
                }
            }
        }

        public override Color OnTileColor(Color inColor) {
            return inColor * (1f - intensity);
        }
    }
}
