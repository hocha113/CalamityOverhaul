﻿using CalamityOverhaul.Content.Items.Placeable;
using CalamityOverhaul.Content.TileModules;
using CalamityOverhaul.Content.UIs.CompressorUIs;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using InnoVault.TileProcessors;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Tiles
{
    internal class DarkMatterCompressor : ModTile, ICWRLoader
    {
        public override string Texture => CWRConstant.Asset + "Tiles/" + "DarkMatterCompressor";
        public const int Width = 4;
        public const int Height = 3;
        public const int OriginOffsetX = 1;
        public const int OriginOffsetY = 1;
        public const int SheetSquare = 18;
        private int frameIndex = 1;
        private static Asset<Texture2D> tileAsset;
        private static Asset<Texture2D> tileGlowAsset;
        void ICWRLoader.LoadAsset() {
            tileAsset = ModContent.Request<Texture2D>(Texture);
            tileGlowAsset = ModContent.Request<Texture2D>(Texture + "Glow");
        }
        void ICWRLoader.UnLoadData() {
            tileAsset = null;
            tileGlowAsset = null;
        }
        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            AddMapEntry(new Color(67, 72, 81), CWRUtils.SafeGetItemName<DarkMatterCompressorItem>());
            AnimationFrameHeight = 52;
            TileObjectData.newTile.CopyFrom(TileObjectData.Style3x4);
            TileObjectData.newTile.Width = Width;
            TileObjectData.newTile.Height = Height;
            TileObjectData.newTile.Origin = new Point16(OriginOffsetX, OriginOffsetY);
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateHeights = new int[] { 16, 16, 16 };
            TileObjectData.newTile.LavaDeath = false;
            TileObjectData.addTile(Type);
        }

        public override void MouseOver(int i, int j) {
            Player player = Main.LocalPlayer;
            player.noThrow = 2;
            player.mouseInterface = true;
            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = ModContent.ItemType<DarkMatterCompressorItem>();//当玩家鼠标悬停在物块之上时，显示该物品的材质
        }

        public override bool RightClick(int i, int j) {
            if (VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                if (TileProcessorLoader.ByPositionGetTP(point, out CompressorTP compressor)) {
                    Main.LocalPlayer.CWR().CompressorContrType = compressor.WhoAmI;
                    CompressorUI.Instance.compressorEntity = compressor;
                    CompressorUI.Instance.Active = !CompressorUI.Instance.Active;
                }
                SoundEngine.PlaySound(SoundID.Chat with { Pitch = 0.3f });
            }

            return true;
        }

        public override bool CanExplode(int i, int j) => false;

        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.Electric);
            return false;
        }

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

        public override void NumDust(int i, int j, bool fail, ref int num) => num = fail ? 1 : 3;

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out CompressorTP compressor)) {
                return false;
            }

            Tile t = Main.tile[i, j];
            int frameXPos = t.TileFrameX;
            int frameYPos = t.TileFrameY;
            frameYPos += compressor.frame * (Height * SheetSquare);
            Texture2D tex = tileAsset.Value;
            Texture2D glow = tileGlowAsset.Value;
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 drawOffset = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y) + offset;
            Color drawColor = Lighting.GetColor(i, j);

            if (!t.IsHalfBlock && t.Slope == 0) {
                spriteBatch.Draw(tex, drawOffset, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
                if (compressor.drawGlow) {
                    spriteBatch.Draw(glow, drawOffset, new Rectangle(frameXPos, t.TileFrameY, 16, 16)
                    , compressor.gloaColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
                }
            }
            else if (t.IsHalfBlock) {
                spriteBatch.Draw(tex, drawOffset + Vector2.UnitY * 8f, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }

            return false;
        }
    }
}
