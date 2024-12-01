using CalamityOverhaul.Content.Items.Materials;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Tiles
{
    internal class PestilenceIngotTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "Tiles/" + "PestilenceIngotTile";
        public override void SetStaticDefaults() {
            AddMapEntry(new Color(121, 89, 9), CWRUtils.SafeGetItemName<PestilenceIngot>());
            Main.tileShine[Type] = 1100;
            Main.tileSolid[Type] = true;
            Main.tileSolidTop[Type] = true;
            Main.tileFrameImportant[Type] = true;
            TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);
            TileObjectData.newTile.StyleHorizontal = true;
            TileObjectData.newTile.LavaDeath = false;
            TileObjectData.addTile(Type);
        }

        public override void RandomUpdate(int i, int j) {
            Vector2 Center = new Vector2(i, j) * 16 + new Vector2(8, 8);
            for (int o = 0; o < Main.rand.Next(7, 13); o++) {
                NPC.NewNPC(new EntitySource_WorldEvent(), (int)Center.X, (int)Center.Y, NPCID.Bee);
            }
        }

        public override bool CreateDust(int i, int j, ref int type) {
            return false;
        }

        public override void NumDust(int i, int j, bool fail, ref int num) {
            for (int o = 0; o < Main.rand.Next(13); o++) {
                int dust = Dust.NewDust(new Vector2(i, j) * 16, 16, 16, DustID.GemEmerald
                    , Main.rand.NextFloat(-1, 1), Main.rand.NextFloat(-1, 1));
                Main.dust[dust].noGravity = true;
            }
        }
    }
}
