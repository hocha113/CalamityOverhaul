using CalamityMod.Items;
using Microsoft.Xna.Framework.Graphics;
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
            return false;
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
            //bool news = player.PressKey();
            //if (news && !old) {
            //    if (Main.HoverItem.type < ItemID.Count) {
            //        Main.HoverItem.type.Domp();
            //    }
            //    else {
            //        ItemLoader.GetItem(Main.HoverItem.type).FullName.Domp();
            //    }
            //}
            //old = news;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI) {
            return false;
        }

        public override void HoldItem(Player player) {
        }

        public override bool? UseItem(Player player) {
            //Projectile.NewProjectile(player.FromObjectGetParent(), player.Center, Vector2.Zero, ModContent.ProjectileType<SetPosingStarm>(), 100, 0);
            //Framing.GetTileSafely((Main.MouseWorld / 16).ToPoint()).Domp();
            //bool copy = false;
            //if (copy) {
            //    RocketHut.InitializeData();
            //    Point startPoint = new Point(4187, 576);
            //    Point endPoint = new Point(4203, 587);
            //    int heiget = Math.Abs(startPoint.Y - endPoint.Y);
            //    int wid = Math.Abs(startPoint.X - endPoint.X);
            //    using (BinaryWriter writer = new BinaryWriter(File.Open("D:\\TileWorldData\\structure.dat", FileMode.Create))) {
            //        writer.Write(wid * heiget);
            //        for (int x = 0; x < wid; x++) {
            //            for (int y = 0; y < heiget; y++) {
            //                Point offsetPoint = new Point(x, y);
            //                RocketHut.WriteTile(writer, Main.tile[startPoint.X + x, startPoint.Y + y], offsetPoint);
            //            }
            //        }
            //    }
            //}
            //else {
            //    using (BinaryReader reader = new BinaryReader(File.Open("D:\\TileWorldData\\structure.dat", FileMode.Open))) {
            //        int count = reader.ReadInt32();
            //        for (int x = 0; x < count; x++) {
            //            RocketHut.ReadTile(reader, new Point((int)Main.MouseWorld.X / 16, (int)Main.MouseWorld.Y / 16));
            //        }
            //    }
            //}

            return true;
        }
    }
}
