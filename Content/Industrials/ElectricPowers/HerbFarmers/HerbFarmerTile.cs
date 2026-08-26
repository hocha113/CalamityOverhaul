using CalamityOverhaul.Common;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.HerbFarmers
{
    /// <summary>草药农场机瓦片,2x2</summary>
    internal class HerbFarmerTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/HerbFarmerTile";

        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            AddMapEntry(new Color(170, 160, 70), VaultUtils.GetLocalizedItemName<HerbFarmer>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.Width = 2;
            TileObjectData.newTile.Height = 2;
            TileObjectData.newTile.Origin = new Point16(0, 1);
            TileObjectData.newTile.CoordinateHeights = [16, 18];
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile
                | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.addTile(Type);
        }

        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.Grass);
            return false;
        }

        public override bool CanDrop(int i, int j) => false;

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

        public override void MouseOver(int i, int j) => Main.LocalPlayer.SetMouseOverByTile(ModContent.ItemType<HerbFarmer>());

        public override bool RightClick(int i, int j) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out HerbFarmerTP tp)) {
                return false;
            }

            tp.OpenUI();
            SoundEngine.PlaySound(CWRSound.ButtonZero with { Pitch = 0.2f, Volume = 0.5f });
            return true;
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out HerbFarmerTP tp)) {
                return;
            }
            if (tp.GlowIntensity > 0.05f) {
                r = 0.28f * tp.GlowIntensity;
                g = 0.3f * tp.GlowIntensity;
                b = 0.1f * tp.GlowIntensity;
            }
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out HerbFarmerTP tp)) {
                return false;
            }

            //缺电时机身压暗,全系"断电减半"状态语言
            MachineTileDraw.DrawCell(i, j, spriteBatch, Type, 2, tp.MachineData.UEvalue < HerbFarmerTP.PlantCost);
            return false;
        }
    }
}
