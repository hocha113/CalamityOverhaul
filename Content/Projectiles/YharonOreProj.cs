using CalamityMod.Tiles.Ores;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles
{
    internal class YharonOreProj : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.penetrate = -1;
            Projectile.hostile = true;
            Projectile.friendly = true;
            Projectile.timeLeft = 30;
        }

        public override void OnKill(int timeLeft) {
            Texture2D value = CWRUtils.GetT2DValue(CWRConstant.Asset + "Tiles/YharonOre");
            if (Projectile.ai[0] != 0) {
                value = CWRUtils.GetT2DValue(CWRConstant.Asset + "Tiles/YharonOre2");
            }
            Color[] colors = new Color[value.Width * value.Height];
            value.GetData(colors);
            Vector2 projPos = Projectile.position / 16 - new Vector2(value.Width, value.Height) / 2;
            ushort auricOre = (ushort)ModContent.TileType<AuricOre>();
            for (int i = 0; i < colors.Length; i++) {
                Color color = colors[i];
                if (color.R == 0) {
                    Vector2 pos = new Vector2(i % value.Width, i / value.Width) + projPos;
                    Tile tile = CWRUtils.GetTile(pos);
                    if (tile == null) {
                        continue;
                    }
                    if (tile.HasTile) {
                        continue;
                    }
                    tile.HasTile = true;
                    tile.LiquidAmount = 0;
                    tile.TileType = auricOre;
                    WorldGen.SquareTileFrame((int)pos.X, (int)pos.Y);
                }
            }
        }
    }
}
