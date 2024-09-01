using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Graphics.Effects;

namespace CalamityOverhaul.Content.Events
{
    internal class TungstenSky : CustomSky, ILoader
    {
        private bool isActive = false;
        private float intensity = 0f;
        private static Asset<Texture2D> back;

        void ILoader.LoadData() {
            if (Main.dedServ) {
                return;
            }
            Filters.Scene["CWRMod:TungstenSky"] = new Filter(new TungstenSkyDate("FilterMiniTower")
                .UseColor(0.5f, 0f, 0.5f).UseOpacity(0.2f), EffectPriority.VeryHigh);
            SkyManager.Instance["CWRMod:TungstenSky"] = new TungstenSky();
        }

        public override void OnLoad() {
            back = CWRUtils.GetT2DAsset(CWRConstant.Asset + "Events/TungstenRiotBackgrounds");
        }

        public override void Update(GameTime gameTime) {
            intensity = isActive ? Math.Min(1f, 0.01f + intensity) : Math.Max(0f, intensity - 0.01f);
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            if (maxDepth >= 0 && minDepth < 0) {
                spriteBatch.Draw(back.Value, Vector2.Zero, null, Color.White * intensity * 0.6f, 0, Vector2.Zero, new Vector2(2, 3), SpriteEffects.None, 0);
            }
        }

        public override void Activate(Vector2 position, params object[] args) {
            isActive = true;
        }

        public override void Deactivate(params object[] args) {
            isActive = false;
        }

        public override void Reset() {
            isActive = false;
        }

        public override bool IsActive() {
            return isActive || intensity > 0.001f;
        }

        public override Color OnTileColor(Color inColor) {
            return new Color(Vector4.Lerp(new Vector4(1f, 0.9f, 0.6f, 1f), inColor.ToVector4(), 1f - intensity));
        }
    }
}
