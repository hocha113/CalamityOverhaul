using CalamityMod;
using CalamityMod.Particles;
using CalamityMod.Projectiles.Magic;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Placeable;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Tiles
{
    internal class InfiniteToilet : ModTile
    {
        public override string Texture => CWRConstant.Asset + "Tiles/" + "InfiniteToilet";
        public override bool IsLoadingEnabled(Mod mod) {
            return false;
        }
        public override void SetStaticDefaults() {
            RegisterItemDrop(ModContent.ItemType<InfiniteToiletItem>());
            Main.tileFrameImportant[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            TileObjectData.newTile.Width = 2;
            TileObjectData.newTile.Height = 3;
            TileObjectData.newTile.CoordinateHeights = new int[] { 16, 16, 16 };
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.newTile.Direction = TileObjectDirection.PlaceLeft;
            TileObjectData.newTile.StyleHorizontal = true;
            TileObjectData.newTile.Origin = new Point16(1, 1);
            TileObjectData.newTile.UsesCustomCanPlace = true;
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop, 2, 0);
            TileObjectData.newTile.LavaDeath = false;
            TileObjectData.newAlternate.CopyFrom(TileObjectData.newTile);
            TileObjectData.newAlternate.Direction = TileObjectDirection.PlaceRight;
            TileObjectData.addAlternate(1);
            TileObjectData.addTile(Type);
            AddToArray(ref TileID.Sets.RoomNeeds.CountsAsChair);
            AddMapEntry(new Color(191, 142, 111), Language.GetText("MapObject.Toilet"));
            TileID.Sets.CanBeSatOnForNPCs[Type] = true;
            TileID.Sets.CanBeSatOnForPlayers[Type] = true;
            TileID.Sets.HasOutlines[Type] = true;
            AdjTiles = new int[] { TileID.Chairs };
        }

        public override bool CanExplode(int i, int j) => false;

        public override bool CreateDust(int i, int j, ref int type) => false;

        public override void NumDust(int i, int j, bool fail, ref int num) => num = fail ? 1 : 3;

        public override void ModifySittingTargetInfo(int i, int j, ref TileRestingInfo info) => CalamityUtils.ChairSitInfo(i, j, ref info, 40, true, shitter: true);

        public override bool RightClick(int i, int j) => CalamityUtils.ChairRightClick(i, j);

        public override void MouseOver(int i, int j) => CalamityUtils.ChairMouseOver(i, j, ModContent.ItemType<InfiniteToiletItem>(), true);

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => settings.player.IsWithinSnappngRangeToTile(i, j, PlayerSittingHelper.ChairSittingMaxDistance);

        public override void HitWire(int i, int j) {
            Tile tile = Main.tile[i, j];

            int spawnX = i;
            int spawnY = j - tile.TileFrameY % 40 / 18;

            Wiring.SkipWire(spawnX, spawnY);
            Wiring.SkipWire(spawnX, spawnY + 1);

            if (Wiring.CheckMech(spawnX, spawnY, 60)) {
                for (int n = 0; n < Main.rand.Next(3, 6); n++) {
                    Vector2 pos = new Vector2(spawnX, spawnY) * 16;
                    Vector2 vr = Main.rand.NextVector2Unit() * Main.rand.Next(13, 17);
                    Projectile.NewProjectile(Wiring.GetProjectileSource(spawnX, spawnY), spawnX * 16 + 8, spawnY * 16 + 12, vr.X, vr.Y, ModContent.ProjectileType<RainbowComet>(), 9999, 9, Main.LocalPlayer.whoAmI);
                    for (int z = 0; z < 333; z++) {
                        Vector2 vector = vr * Main.rand.Next(1, 17);
                        float slp = Main.rand.NextFloat(0.5f, 0.9f);
                        GeneralParticleHandler.SpawnParticle(new FlareShine(pos + Main.rand.NextVector2Unit() * 13, vector, Color.White, Main.DiscoColor
                            , 0f, new Vector2(0.6f, 1f) * slp, new Vector2(1.5f, 2.7f) * slp, 20 + Main.rand.Next(6), 0f, 3f, 0f, Main.rand.Next(7) * 2));
                    }
                }
            }
        }
    }
}
