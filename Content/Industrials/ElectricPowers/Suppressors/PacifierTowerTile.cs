using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Suppressors
{
    /// <summary>宁静力场发生器瓦片,3x5;复用特斯拉塔瓦片贴图(仅常规形态帧)并施加冷绿色调</summary>
    internal class PacifierTowerTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/TeslaElectromagneticTowerTile";

        [VaultLoaden(CWRConstant.Asset + "ElectricPowers/TeslaElectromagneticTowerTileGlow")]
        internal static Asset<Texture2D> tileGlowAsset = null;

        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            Main.tileSolidTop[Type] = true;

            AddMapEntry(new Color(96, 176, 128), VaultUtils.GetLocalizedItemName<PacifierTower>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.Width = 3;
            TileObjectData.newTile.Height = 5;
            TileObjectData.newTile.Origin = new Point16(2, 4);
            TileObjectData.newTile.AnchorBottom = new AnchorData(
                AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide
                , TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateHeights = [16, 16, 16, 16, 16];
            TileObjectData.newTile.LavaDeath = false;

            TileObjectData.addTile(Type);
        }

        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.JungleSpore);
            return false;
        }

        public override bool CanDrop(int i, int j) => false;

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

        public override void MouseOver(int i, int j) {
            Main.LocalPlayer.SetMouseOverByTile(ModContent.ItemType<PacifierTower>());
        }

        public override bool RightClick(int i, int j) {
            if (!TileProcessorLoader.AutoPositionGetTP<PacifierTowerTP>(i, j, out var tp)) {
                return false;
            }
            tp.ToggleField();
            return true;
        }

        public override void HitWire(int i, int j) {
            if (!TileProcessorLoader.AutoPositionGetTP<PacifierTowerTP>(i, j, out var tp)) {
                return;
            }
            tp.ToggleField();
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out PacifierTowerTP tp)) {
                return;
            }
            if (tp.GlowIntensity > 0.05f) {
                r = 0.12f * tp.GlowIntensity;
                g = 0.32f * tp.GlowIntensity;
                b = 0.18f * tp.GlowIntensity;
            }
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out PacifierTowerTP tp)) {
                return false;
            }

            Tile t = Main.tile[i, j];
            int frameXPos = t.TileFrameX;
            int frameYPos = t.TileFrameY;
            Texture2D tex = TextureAssets.Tile[Type].Value;
            Texture2D glow = tileGlowAsset.Value;
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 drawOffset = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y) + offset;
            //共用特斯拉塔贴图,乘上系列色调区分机种
            Color drawColor = Lighting.GetColor(i, j).MultiplyRGB(PacifierTower.Tint);

            if (!tp.SuppressActive) {
                drawColor.R /= 2;
                drawColor.G /= 2;
                drawColor.B /= 2;
                drawColor.A = 255;
            }

            Color glowColor = PacifierTower.Tint * (0.3f + tp.GlowIntensity * 0.7f);

            if (!t.IsHalfBlock && t.Slope == 0) {
                spriteBatch.Draw(tex, drawOffset, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
                spriteBatch.Draw(glow, drawOffset, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , glowColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
            else if (t.IsHalfBlock) {
                spriteBatch.Draw(tex, drawOffset + Vector2.UnitY * 8f, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
                spriteBatch.Draw(glow, drawOffset + Vector2.UnitY * 8f, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , glowColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
            return false;
        }
    }
}
