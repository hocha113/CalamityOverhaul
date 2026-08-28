using CalamityOverhaul.Content.Industrials.ElectricPowers.Crushers;
using CalamityOverhaul.Content.UIs.UIEffect;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Recyclers
{
    /// <summary>回收机瓦片:4×3;台上物品与状态灯等反馈覆层由 TP 在物块层上绘制</summary>
    internal class RecyclerTile : ModTile
    {
        public const int TileWidth = 4;
        public const int TileHeight = 3;
        public override string Texture => CWRConstant.Asset + "ElectricPowers/RecyclerTile";

        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            AddMapEntry(new Color(96, 116, 104), VaultUtils.GetLocalizedItemName<Recycler>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.Width = 4;
            TileObjectData.newTile.Height = 3;
            TileObjectData.newTile.Origin = new Point16(2, 2);
            TileObjectData.newTile.AnchorBottom = new AnchorData(
                AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide,
                TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateHeights = [16, 16, 16];
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.newTile.LavaDeath = false;
            TileObjectData.addTile(Type);
        }

        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.Electric);
            return false;
        }

        public override void NumDust(int i, int j, bool fail, ref int num) => num = fail ? 1 : 3;

        public override bool CanDrop(int i, int j) => false;

        public override void MouseOver(int i, int j) {
            Player player = Main.LocalPlayer;
            player.noThrow = 2;
            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = ModContent.ItemType<Recycler>();
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out RecyclerTP tp)) {
                return;
            }
            if (tp.RecData?.IsWorking == true) {
                r = 0.14f;
                g = 0.26f;
                b = 0.20f;
            }
        }

        public override bool RightClick(int i, int j) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var topLeft)) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(topLeft, out RecyclerTP tp)) {
                return false;
            }

            tp.RightClickByTile(false);
            return true;
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out RecyclerTP tp)) {
                return false;
            }

            //缺电时机身压暗,全系"断电减半"状态语言
            bool powered = tp.RecData != null && tp.RecData.UEvalue >= tp.RecData.UEPerTick;
            MachineTileDraw.DrawCell(i, j, spriteBatch, Type, 3, 16, !powered);
            return false;
        }
    }
}
