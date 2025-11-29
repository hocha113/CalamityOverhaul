using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Items.OceanRaiderses
{
    internal class OceanRaidersTile : ModTile
    {
        public override string Texture => CWRConstant.Item_Placeable + "OceanRaidersTile";
        [VaultLoaden(CWRConstant.Item_Placeable + "OceanRaidersTileGlow")]
        public static Asset<Texture2D> tileGlowAsset = null;

        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            AddMapEntry(new Color(67, 142, 191), VaultUtils.GetLocalizedItemName<OceanRaiders>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);
            TileObjectData.newTile.Width = 11;
            TileObjectData.newTile.Height = 6;
            TileObjectData.newTile.Origin = new Point16(5, 5);
            TileObjectData.newTile.AnchorBottom = new AnchorData(
                AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide,
                TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateHeights = [16, 16, 16, 16, 16, 16];
            TileObjectData.newTile.LavaDeath = false;
            TileObjectData.addTile(Type);
        }

        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.Water);
            return false;
        }

        public override bool CanDrop(int i, int j) => false;

        public override void MouseOver(int i, int j) {
            Main.LocalPlayer.SetMouseOverByTile(ModContent.ItemType<OceanRaiders>());
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out OceanRaidersTP machine)) {
                return false;
            }

            Tile t = Main.tile[i, j];
            int frameXPos = t.TileFrameX;
            int frameYPos = t.TileFrameY + machine.frame * 18 * 6;
            Texture2D tex = TextureAssets.Tile[Type].Value;
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 drawOffset = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y) + offset;
            Color drawColor = Lighting.GetColor(i, j);

            if (machine.MachineData.UEvalue < OceanRaidersTP.consumeUE) {
                drawColor.R /= 2;
                drawColor.G /= 2;
                drawColor.B /= 2;
                drawColor.A = 255;
            }

            if (!t.IsHalfBlock && t.Slope == 0) {
                spriteBatch.Draw(tex, drawOffset, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);

                if (machine.isWorking && tileGlowAsset != null) {
                    Color glowColor = Color.White * machine.glowIntensity;
                    spriteBatch.Draw(tileGlowAsset.Value, drawOffset, new Rectangle(frameXPos, t.TileFrameY, 16, 16)
                        , glowColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
                }
            }
            else if (t.IsHalfBlock) {
                spriteBatch.Draw(tex, drawOffset + Vector2.UnitY * 8f, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
            return false;
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            if (VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                if (TileProcessorLoader.ByPositionGetTP(point, out OceanRaidersTP machine)) {
                    if (machine.isWorking) {
                        float intensity = machine.glowIntensity * 0.6f;
                        r = 0.2f * intensity;
                        g = 0.6f * intensity;
                        b = 0.8f * intensity;
                    }
                }
            }
        }
    }
}
