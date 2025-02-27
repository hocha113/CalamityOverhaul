using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.Generator.WindGriven
{
    internal class WindGrivenGeneratorTP : BaseGeneratorTP
    {
        public override int TargetTileID => ModContent.TileType<WindGrivenGeneratorTile>();
        private float rotition;
        private float rotSpeed;
        public override void GeneratorUpdate() {
            rotSpeed = 0.02f;
            rotition += rotSpeed;
            if (GeneratorData.UEvalue < 1000) {
                GeneratorData.UEvalue += rotSpeed * 10;
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            Texture2D blade = CWRUtils.GetT2DValue(CWRConstant.Asset + "Generator/Blade");
            Vector2 drawPos = CenterInWorld - Main.screenPosition + new Vector2(0, -46);
            Vector2 drawOrig = new Vector2(blade.Width / 2, blade.Height);
            for (int i = 0; i < 3; i++) {
                float drawRot = (MathHelper.TwoPi) / 3f * i + rotition;
                spriteBatch.Draw(blade, drawPos, null, Color.White, drawRot, drawOrig, 1, SpriteEffects.None, 0);
            }
        }
    }
}
