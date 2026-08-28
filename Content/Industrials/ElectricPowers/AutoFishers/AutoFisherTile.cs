using CalamityOverhaul.Common;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.AutoFishers
{
    /// <summary>自动钓鱼机瓦片,4x5;钓竿与鱼线由 TP 在物块层上拼装</summary>
    internal class AutoFisherTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/AutoFisherTile";

        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            AddMapEntry(new Color(78, 122, 156), VaultUtils.GetLocalizedItemName<AutoFisher>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.Width = 4;
            TileObjectData.newTile.Height = 5;
            TileObjectData.newTile.Origin = new Point16(2, 4);
            TileObjectData.newTile.CoordinateHeights = [16, 16, 16, 16, 16];
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile
                | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.addTile(Type);
        }

        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.Water);
            return false;
        }

        public override bool CanDrop(int i, int j) => false;

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

        public override void MouseOver(int i, int j) => Main.LocalPlayer.SetMouseOverByTile(ModContent.ItemType<AutoFisher>());

        public override bool RightClick(int i, int j) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out AutoFisherTP tp)) {
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
            if (!TileProcessorLoader.ByPositionGetTP(point, out AutoFisherTP tp)) {
                return;
            }
            if (tp.GlowIntensity > 0.05f) {
                r = 0.12f * tp.GlowIntensity;
                g = 0.24f * tp.GlowIntensity;
                b = 0.36f * tp.GlowIntensity;
            }
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out AutoFisherTP tp)) {
                return false;
            }

            //电量不足以下一竿时机身压暗,一眼可读的断电状态
            MachineTileDraw.DrawCell(i, j, spriteBatch, Type, 5, 16, tp.MachineData.UEvalue < AutoFisherTP.CastCost);
            return false;
        }
    }
}
