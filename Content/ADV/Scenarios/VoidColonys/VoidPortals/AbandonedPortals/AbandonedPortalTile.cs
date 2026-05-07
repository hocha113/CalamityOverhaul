using InnoVault.TileProcessors;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.ADV.Scenarios.VoidColonys.VoidPortals.AbandonedPortals
{
    internal class AbandonedPortalTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "ADV/VoidColony/AbandonedPortalTile";
        public const int Width = 26;
        public const int Height = 19;

        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = false;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            Main.tileFrameImportant[Type] = true;
            AddMapEntry(new Color(80, 160, 200));

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.Width = Width;
            TileObjectData.newTile.Height = Height;
            TileObjectData.newTile.Origin = new Point16(0, 0);
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.None, 0, 0);
            TileObjectData.newTile.CoordinateHeights = new int[Height];
            for (int i = 0; i < Height; i++) {
                TileObjectData.newTile.CoordinateHeights[i] = 16;
            }
            TileObjectData.newTile.LavaDeath = false;
            TileObjectData.newTile.WaterDeath = false;
            TileObjectData.addTile(Type);
        }

        public override bool CanExplode(int i, int j) => false;

        public override bool CreateDust(int i, int j, ref int type) => false;

        public override void NumDust(int i, int j, bool fail, ref int num) => num = 0;

        public override bool CanKillTile(int i, int j, ref bool blockDamaged) => false;

        public override bool RightClick(int i, int j) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out AbandonedPortalTP tp)) {
                return false;
            }
            AbandonedPortalSession.Open(tp);
            Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuOpen);
            return true;
        }

        public override void KillMultiTile(int i, int j, int frameX, int frameY) {
            AbandonedPortalSession.Close();
        }

        public override bool PreDraw(int i, int j, Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch) {
            return false;
        }
    }
}
