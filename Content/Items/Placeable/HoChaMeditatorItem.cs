using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Items.Placeable
{
    internal class HoChaMeditatorItem : ModItem
    {
        public override string Texture => CWRConstant.Item + "Placeable/HoChaMeditatorItem";
        public override void SetDefaults() {
            Item.width = 28;
            Item.height = 20;
            Item.maxStack = 9999;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.value = Item.buyPrice(0, 0, 20, 0);
            Item.rare = ItemRarityID.Green;
            Item.consumable = true;
            Item.createTile = ModContent.TileType<HoChaMeditator>();
        }
    }

    internal class HoChaMeditator : ModTile
    {
        public override string Texture => CWRConstant.Asset + "Tiles/" + "HoChaMeditator";
        [VaultLoaden(CWRConstant.Asset + "Tiles/" + "HoChaMeditator")]
        private static Asset<Texture2D> tileAsset = null;
        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            Main.tileFrameImportant[Type] = true;
            DustType = DustID.Stone;
            AddMapEntry(new Color(58, 61, 61), VaultUtils.GetLocalizedItemName<HoChaMeditatorItem>());
            TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);
            TileObjectData.newTile.Width = 9;
            TileObjectData.newTile.Height = 10;
            TileObjectData.newTile.Origin = new Point16(4, 7);
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateHeights = [16, 16, 16, 16, 16, 16, 16, 16, 16, 16];
            TileObjectData.newTile.LavaDeath = false;
            TileObjectData.addTile(Type);
        }

        public override void MouseOver(int i, int j) => Main.LocalPlayer.SetMouseOverByTile<HoChaMeditatorItem>();

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            Tile t = Main.tile[i, j];
            int frameXPos = t.TileFrameX;
            int frameYPos = t.TileFrameY;

            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out HoChaMeditatorTP tp)) {
                return false;
            }

            frameYPos += (tp.Left ? 0 : 1) * 180;
            Texture2D tex = tileAsset.Value;
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 drawOffset = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y) + offset;
            Color drawColor = Lighting.GetColor(i, j);

            if (!t.IsHalfBlock && t.Slope == 0) {
                spriteBatch.Draw(tex, drawOffset, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
            else if (t.IsHalfBlock) {
                spriteBatch.Draw(tex, drawOffset + Vector2.UnitY * 8f, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
            return false;
        }
    }

    internal class HoChaMeditatorTP : TileProcessor
    {
        public override int TargetTileID => ModContent.TileType<HoChaMeditator>();
        public bool Left;
        public override void SaveData(TagCompound tag) {
            tag.Add("_Left", Left);
        }

        public override void LoadData(TagCompound tag) {
            if (!tag.TryGet("_Left", out Left)) {
                Left = false;
            }
        }

        public override void SendData(ModPacket data) {
            data.Write(Left);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            Left = reader.ReadBoolean();
        }

        public override void Update() {
            if (InScreen) {
                return;
            }

            Player player = VaultUtils.FindClosestPlayer(CenterInWorld, int.MaxValue);
            if (player == null) {
                return;
            }

            Left = player.Center.To(CenterInWorld).X > 0;
        }
    }
}
