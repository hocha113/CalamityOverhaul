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

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.TeleportStations
{
    /// <summary>传送站瓦片,2x3 拱门</summary>
    internal class TeleportStationTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/TeleportStationTile";

        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            Main.tileSolidTop[Type] = true;

            AddMapEntry(new Color(70, 160, 150), VaultUtils.GetLocalizedItemName<TeleportStation>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.Width = 2;
            TileObjectData.newTile.Height = 3;
            TileObjectData.newTile.Origin = new Point16(1, 2);
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateHeights = [16, 16, 18];
            TileObjectData.newTile.LavaDeath = false;

            TileObjectData.addTile(Type);

            HitSound = SoundID.Tink;
            MineResist = 1.5f;
        }

        public override bool CanDrop(int i, int j) => false;

        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.Electric);
            return false;
        }

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

        public override void NumDust(int i, int j, bool fail, ref int num) => num = fail ? 1 : 3;

        public override void MouseOver(int i, int j) {
            Main.LocalPlayer.SetMouseOverByTile(ModContent.ItemType<TeleportStation>());
        }

        public override bool RightClick(int i, int j) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out TeleportStationTP tp)) {
                return false;
            }

            tp.RightClickByTile();
            SoundEngine.PlaySound(CWRSound.ButtonZero with { Pitch = 0.2f, Volume = 0.5f });
            return true;
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out TeleportStationTP tp)) {
                return;
            }
            if (tp.GlowIntensity > 0.05f) {
                r = 0.14f * tp.GlowIntensity;
                g = 0.32f * tp.GlowIntensity;
                b = 0.3f * tp.GlowIntensity;
            }
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out TeleportStationTP tp)) {
                return false;
            }

            //电量不足一次传送时机身压暗,全系"断电减半"状态语言
            MachineTileDraw.DrawCell(i, j, spriteBatch, Type, 3, tp.MachineData.UEvalue < TeleportStationTP.BaseCost);
            return false;
        }
    }
}
