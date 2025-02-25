using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Generator
{
    internal class ThermalGeneratorTP : TileProcessor
    {
        public override int TargetTileID => ModContent.TileType<ThermalGeneratorTile>();
        public int frame;
        public override void Update() {
            CWRUtils.ClockFrame(ref frame, 5, 6);
        }

        public override void Draw(SpriteBatch spriteBatch) {
            base.Draw(spriteBatch);
        }
    }
}
