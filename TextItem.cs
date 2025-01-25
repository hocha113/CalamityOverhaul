using CalamityMod.Items;
using CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul
{
    //internal class TestProj : ModProjectile, IDeductDrawble
    //{
    //    public override string Texture => "CalamityOverhaul/icon";
    //    public override void SetDefaults() {
    //        Projectile.width = Projectile.height = 66;
    //    }

    //    public override bool PreDraw(ref Color lightColor) {
    //        return false;
    //    }

    //    public void DeductDraw(SpriteBatch spriteBatch) {
    //        Texture2D value = CWRAsset.Placeholder_150.Value;
    //        spriteBatch.Draw(value, Projectile.Center - Main.screenPosition, null, Color.White, 0, value.Size() / 2, 111, SpriteEffects.None, 0);
    //    }

    //    public void PreDrawTureBody(SpriteBatch spriteBatch) {
    //        Texture2D value = TextureAssets.Projectile[Type].Value;
    //        spriteBatch.Draw(value, Projectile.Center - Main.screenPosition, null, Color.White, 0, value.Size() / 2, 1, SpriteEffects.None, 0);
    //    }
    //}

    internal class TextItem : ModItem
    {
        public override string Texture => "CalamityOverhaul/icon";

        //private bool old;
        public override bool IsLoadingEnabled(Mod mod) {
            return true;
        }

        public override void SetDefaults() {
            Item.width = 80;
            Item.height = 80;
            Item.damage = 9999;
            Item.DamageType = DamageClass.Default;
            Item.useAnimation = Item.useTime = 13;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 2.25f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.shootSpeed = 8f;
            Item.shoot = ProjectileID.PurificationPowder;
            Item.value = CalamityGlobalItem.RarityYellowBuyPrice;
            Item.rare = ItemRarityID.Yellow;
        }

        public override void UpdateInventory(Player player) {
            //player.velocity.Domp();
            //bool news = player.PressKey(false);
            //if (news && !old) {
            //    player.QuickSpawnItem(player.parent(), Main.HoverItem, Main.HoverItem.stack);
            //}
            //old = news;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI) {
            Texture2D value = CWRUtils.GetT2DValue(CWRConstant.Item_Melee + "WastelandFang");
            Effect effect = CWRUtils.GetEffectValue("DeductDraw");
            Rectangle deductRec = new Rectangle(0, 0, value.Width, value.Height / 2);
            effect.CurrentTechnique.Passes[0].Apply();
            effect.Parameters["topLeft"].SetValue(deductRec.TopLeft());
            effect.Parameters["width"].SetValue(deductRec.Width);
            effect.Parameters["height"].SetValue(deductRec.Height);
            effect.Parameters["textureSize"].SetValue(value.Size());
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(default, BlendState.AlphaBlend, Main.DefaultSamplerState, default, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            Main.spriteBatch.Draw(value, Item.position - Main.screenPosition, null, Color.White, Main.GameUpdateCount * 0.1f, value.Size() / 2, 1, SpriteEffects.FlipVertically, 0);
            Main.spriteBatch.ResetBlendState();
            return false;
        }

        public override void HoldItem(Player player) {
        }

        private void WriteTile(BinaryWriter writer, Tile tile, Point offsetPoint) {
            writer.Write(offsetPoint.X);
            writer.Write(offsetPoint.Y);
            writer.Write(tile.WallType);
            writer.Write(tile.TileType);
            writer.Write(tile.TileFrameX);
            writer.Write(tile.TileFrameY);
            writer.Write(tile.HasTile);
            writer.Write((byte)tile.Slope);
        }

        private void SetTile(BinaryReader reader) {
            int tilePosX = reader.ReadInt32() + 3720;
            int tilePosY = reader.ReadInt32() + 400;
            ushort wallType = reader.ReadUInt16();
            ushort tileType = reader.ReadUInt16();
            short frameX = reader.ReadInt16();
            short frameY = reader.ReadInt16();
            bool hasTile = reader.ReadBoolean();
            byte slope = reader.ReadByte();
            Tile tile = Main.tile[tilePosX, tilePosY];
            if (wallType > 1) {
                tile.WallType = wallType;
                tile.LiquidAmount = 255;
            }

            tile.HasTile = hasTile;
            tile.Slope = (SlopeType)slope;

            if (tileType > 0) {
                tile.TileType = tileType;
            }
            tile.TileFrameX = frameX;
            tile.TileFrameY = frameY;
            CWRUtils.SafeSquareTileFrame(tilePosX, tilePosY);
        }

        public override bool? UseItem(Player player) {
            Projectile.NewProjectile(player.GetSource_FromAI(), player.Center, new Vector2(0, 0)
                            , ModContent.ProjectileType<SetPosingStarm>(), 0, 2, -1, 0, 0);
            //Point startPoint = new Point(1720, 400);
            //Point endPoint = new Point(1720, 400);
            //int heiget = 2000;
            //int wid = 1400;
            //using (BinaryWriter writer = new BinaryWriter(File.Open("D:\\TileWorldData\\structure.dat", FileMode.Create))) {
            //    for (int x = 0; x < wid; x++) {
            //        for (int y = 0; y < heiget; y++) {
            //            Point offsetPoint = new Point(x, y);
            //            WriteTile(writer, Main.tile[startPoint.X + x, startPoint.Y + y], offsetPoint);
            //        }
            //    }
            //}
            //Point point = new Point((int)Main.MouseWorld.X / 16, (int)Main.MouseWorld.Y / 16);
            //point.Domp();

            //using (BinaryReader reader = new BinaryReader(File.Open("D:\\TileWorldData\\structure.dat", FileMode.Open))) {
            //    for (int x = 0; x < wid; x++) {
            //        for (int y = 0; y < heiget; y++) {
            //            SetTile(reader);
            //        }
            //    }
            //}
            //Projectile.NewProjectile(player.FromObjectGetParent(), player.Center, Vector2.Zero, ModContent.ProjectileType<TestProj>(), 0, 0, player.whoAmI);
            return true;
        }
    }
}
