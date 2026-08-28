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

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.PotionBeacons
{
    /// <summary>药剂弥散信标瓦片,2x2</summary>
    internal class PotionBeaconTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/PotionBeaconTile";

        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            Main.tileSolidTop[Type] = true;

            AddMapEntry(new Color(152, 112, 190), VaultUtils.GetLocalizedItemName<PotionBeacon>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.Width = 4;
            TileObjectData.newTile.Height = 4;
            TileObjectData.newTile.Origin = new Point16(2, 3);
            TileObjectData.newTile.AnchorBottom = new AnchorData(
                AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide
                , TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateHeights = [16, 16, 16, 16];
            TileObjectData.newTile.LavaDeath = false;

            TileObjectData.addTile(Type);
        }

        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.PurpleTorch);
            return false;
        }

        public override bool CanDrop(int i, int j) => false;

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

        public override void MouseOver(int i, int j) {
            Main.LocalPlayer.SetMouseOverByTile(ModContent.ItemType<PotionBeacon>());
        }

        public override bool RightClick(int i, int j) {
            if (!TileProcessorLoader.AutoPositionGetTP<PotionBeaconTP>(i, j, out var tp)) {
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
            if (!TileProcessorLoader.ByPositionGetTP(point, out PotionBeaconTP tp)) {
                return;
            }
            if (tp.GlowIntensity > 0.05f) {
                r = 0.3f * tp.GlowIntensity;
                g = 0.15f * tp.GlowIntensity;
                b = 0.4f * tp.GlowIntensity;
            }
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out PotionBeaconTP tp)) {
                return false;
            }

            //停摆时机身压暗,全系"断电减半"状态语言
            MachineTileDraw.DrawCell(i, j, spriteBatch, Type, 4, 16, !tp.WorkingActive);
            return false;
        }
    }
}
