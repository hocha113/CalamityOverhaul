using CalamityMod;
using CalamityMod.Items.Placeables.Furniture;
using CalamityMod.Particles;
using CalamityMod.Projectiles.Magic;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Placeable;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
        public const int NextStyleHeight = 40;
        public override bool IsLoadingEnabled(Mod mod) {
            if (!CWRServerConfig.Instance.AddExtrasContent) {
                return false;
            }
            return base.IsLoadingEnabled(mod);
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
            TileObjectData.newTile.DrawYOffset = 4;
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
            AddMapEntry(new Color(121, 102, 111), Language.GetText("MapObject.Toilet"));
            TileID.Sets.CanBeSatOnForNPCs[Type] = true;
            TileID.Sets.CanBeSatOnForPlayers[Type] = true;
            TileID.Sets.HasOutlines[Type] = true;
            AdjTiles = new int[] { TileID.Chairs };
        }

        public override bool CanExplode(int i, int j) => false;

        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.TerraBlade, 0f, 0f, 1, Main.DiscoColor, 1f);
            return false;
        }

        public override void NumDust(int i, int j, bool fail, ref int num) {
            num = fail ? 1 : 3;
        }

        //public override void PostDraw(int i, int j, SpriteBatch spriteBatch) {
        //    int xFrameOffset = Main.tile[i, j].TileFrameX;
        //    int yFrameOffset = Main.tile[i, j].TileFrameY;
        //    Texture2D glowmask = ModContent.Request<Texture2D>(Texture + "_Highlight").Value;
        //    Vector2 drawOffest = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
        //    Vector2 drawPosition = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y) + drawOffest;
        //    Color drawColour = Main.DiscoColor;
        //    Tile trackTile = Main.tile[i, j];
        //    //if (!trackTile.IsHalfBlock && trackTile.Slope == 0)
        //        //spriteBatch.Draw(glowmask, drawPosition, new Rectangle(xFrameOffset, yFrameOffset, 18, 18), drawColour, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
        //    //else if (trackTile.IsHalfBlock)
        //        //spriteBatch.Draw(glowmask, drawPosition + new Vector2(0f, 8f), new Rectangle(xFrameOffset, yFrameOffset, 18, 8), drawColour, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
        //}

        public override void ModifySittingTargetInfo(int i, int j, ref TileRestingInfo info) => CalamityUtils.ChairSitInfo(i, j, ref info, NextStyleHeight, true, shitter: true);

        public override bool RightClick(int i, int j) => CalamityUtils.ChairRightClick(i, j);

        public override void MouseOver(int i, int j) => CalamityUtils.ChairMouseOver(i, j, ModContent.ItemType<InfiniteToiletItem>(), true);

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) {
            return settings.player.IsWithinSnappngRangeToTile(i, j, PlayerSittingHelper.ChairSittingMaxDistance);
        }

        public override void HitWire(int i, int j)
        {
            Tile tile = Main.tile[i, j];

            int spawnX = i;
            int spawnY = j - (tile.TileFrameY % NextStyleHeight) / 18;

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
