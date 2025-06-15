using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Items.Placeable
{
    internal class MetalMusicBox : ModItem, ICWRLoader
    {
        public override string Texture => CWRConstant.Item + "Placeable/MetalMusicBox";
        void ICWRLoader.SetupData() {
            MusicLoader.AddMusicBox(CWRMod.Instance
                , MusicLoader.GetMusicSlot("CalamityOverhaul/Assets/Sounds/Music/Metal")
                , ModContent.ItemType<MetalMusicBox>(), ModContent.TileType<MetalMusicBoxTile>());
        }
        public override void SetStaticDefaults() {
            Item.ResearchUnlockCount = 2;
            ItemID.Sets.CanGetPrefixes[Type] = false;
            ItemID.Sets.ShimmerTransformToItem[Type] = ItemID.MusicBox;
        }
        public override void SetDefaults() => Item.DefaultToMusicBox(ModContent.TileType<MetalMusicBoxTile>());
    }

    internal class MetalMusicBoxTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "Tiles/MetalMusicBoxTile";
        public override void SetStaticDefaults() {
            Main.tileFrameImportant[Type] = true;
            Main.tileObsidianKill[Type] = true;
            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.Origin = new Point16(0, 1);
            TileObjectData.newTile.LavaDeath = false;
            TileObjectData.newTile.DrawYOffset = 2;
            TileObjectData.newTile.StyleLineSkip = 2;
            TileObjectData.addTile(Type);
            TileID.Sets.DisableSmartCursor[Type] = true;
            AddMapEntry(new Color(191, 142, 111), Language.GetText("ItemName.MusicBox"));
        }

        public override void MouseOver(int i, int j) {
            Player localPlayer = Main.LocalPlayer;
            localPlayer.noThrow = 2;
            localPlayer.cursorItemIconEnabled = true;
            localPlayer.cursorItemIconID = TileLoader.GetItemDropFromTypeAndStyle(Type);
        }

        public override bool CreateDust(int i, int j, ref int type) => false;

        public override void DrawEffects(int i, int j, SpriteBatch spriteBatch, ref TileDrawInfo drawData) {
            if (Main.gamePaused || !Main.instance.IsActive || (Lighting.UpdateEveryFrame && !Main.rand.NextBool(4))) {
                return;
            }
            Tile tile = Main.tile[i, j];
            if (tile.TileFrameX == 36 && tile.TileFrameY % 36 == 0
                && (int)Main.timeForVisualEffects % 7 == 0 && Main.rand.NextBool(3)) {
                int goreType = Main.rand.Next(570, 573);
                Vector2 position = new Vector2(i * 16 + 8, j * 16 - 8);
                Vector2 velocity = new Vector2(Main.WindForVisuals * 2f, -0.5f);
                velocity.X *= 1f + Main.rand.NextFloat(-0.5f, 0.5f);
                velocity.Y *= 1f + Main.rand.NextFloat(-0.5f, 0.5f);
                if (goreType == 572) {
                    position.X -= 8f;
                }
                if (goreType == 571) {
                    position.X -= 4f;
                }
                Gore.NewGore(new EntitySource_TileUpdate(i, j), position, velocity, goreType, 0.8f);
            }
        }
    }
}
