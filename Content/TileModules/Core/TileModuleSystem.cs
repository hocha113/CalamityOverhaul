using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.Map;
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

            foreach (BaseTileModule module in TileModuleLoader.TileModulesList) {
                TileModuleLoader.ModuleIDHanderModuleHasNumInWorld[module.ModuleID] = 0;
            }

            foreach (BaseTileModule module in TileModuleLoader.TileModuleInWorld) {
                if (!module.Active) {
                    continue;
                }

                TileModuleLoader.ModuleIDHanderModuleHasNumInWorld[module.ModuleID]++;

                module.Tile = CWRUtils.GetTile(module.Position.X, module.Position.Y);
                if (module.IsDaed()) {
                    module.Kill();
                    continue;
                }
                module.Update();
            }

            foreach (BaseTileModule module in TileModuleLoader.TileModulesList) {
                if (module.GetInWorldHasNum() > 0) {
                    module.SingleInstanceUpdate();
                }
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
                    Vector2 drawPos = module.PosInWorld - Main.screenPosition;
                    Main.EntitySpriteDraw(CWRUtils.GetT2DValue(CWRConstant.Placeholder2), drawPos
                    , new Rectangle(0, 0, 16, 16), Color.Red * 0.6f, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
                    Utils.DrawBorderStringFourWay(Main.spriteBatch, FontAssets.ItemStack.Value
                        , $"Name:{module.GetType().Name} \nID:{module.ModuleID} \nwhoAmi:{module.WhoAmI}"
                        , drawPos.X + 0, drawPos.Y - 70, Color.AliceBlue, Color.Black, Vector2.Zero, 1f);
                }
            }

            Main.spriteBatch.End();
        }
    }
}
