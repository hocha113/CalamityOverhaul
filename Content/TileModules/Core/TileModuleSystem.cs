using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.TileModules.Core
{
    internal class TileModuleSystem : ModSystem
    {
        public override void OnWorldUnload() {
            foreach (BaseTileModule module in TileModuleLoader.TileModuleInWorld) {
                if (!module.Active) {
                    continue;
                }
                module.UnLoadInWorld();
            }
        }

        public override void PostUpdateEverything() {
            if (TileModuleLoader.TileModuleInWorld.Count <= 0) {
                return;
            }

            foreach (BaseTileModule module in TileModuleLoader.TileModuleInWorld) {
                if (!module.Active) {
                    continue;
                }
                module.Tile = CWRUtils.GetTile(module.Position.X, module.Position.Y);
                if (module.IsDaed()) {
                    module.Kill();
                    continue;
                }
                module.Update();
            }
        }

        public override void PostDrawTiles() {
            if (TileModuleLoader.TileModuleInWorld.Count <= 0) {
                return;
            }

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState
                , DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            foreach (BaseTileModule module in TileModuleLoader.TileModuleInWorld) {
                if (!module.Active) {
                    continue;
                }

                module.Draw(Main.spriteBatch);

                if (CWRServerConfig.Instance.TileModuleBosSizeDraw) {
                    Main.EntitySpriteDraw(CWRUtils.GetT2DValue(CWRConstant.Placeholder2), module.PosInWorld - Main.screenPosition
                    , new Rectangle(0, 0, 16, 16), Color.Red * 0.6f, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
                }
            }

            Main.spriteBatch.End();
        }
    }
}
