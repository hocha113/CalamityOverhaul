using CalamityMod.Items.Materials;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Items.OldDuchests
{
    internal class OldDuchest : ModItem
    {
        public override string Texture => CWRConstant.Item_Placeable + "OldDuchest";
        [VaultLoaden(CWRConstant.Item_Placeable + "OldDuchestGlow")]
        public static Asset<Texture2D> Glow = null;
        public static LocalizedText StorageText { get; private set; }

        public override void SetStaticDefaults() {
            StorageText = this.GetLocalization(nameof(StorageText), () => "大型储物箱");
        }

        public override void SetDefaults() {
            Item.width = 96;
            Item.height = 64;
            Item.maxStack = 9999;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.value = Item.buyPrice(0, 2, 50, 0);
            Item.rare = ItemRarityID.LightRed;
            Item.createTile = ModContent.TileType<OldDuchestTile>();
        }

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor
            , Color alphaColor, float rotation, float scale, int whoAmI) {
            if (Glow != null && !Glow.IsDisposed) {
                spriteBatch.Draw(Glow.Value, Item.Center - Main.screenPosition, null, Color.White
                    , rotation, Glow.Value.Size() / 2, scale, SpriteEffects.None, 0);
            }
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<DubiousPlating>(15)
                .AddIngredient<MysteriousCircuitry>(15)
                .AddIngredient(ItemID.IronBar, 10)
                .AddIngredient(ItemID.Wood, 50)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }

    internal class OldDuchestTile : ModTile
    {
        //宽6格，高4格，共两帧，第一帧为关闭状态，第二帧为打开状态
        public override string Texture => CWRConstant.Item_Placeable + "OldDuchestTile";

        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            AddMapEntry(new Color(139, 87, 42), VaultUtils.GetLocalizedItemName<OldDuchest>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.Width = 6;
            TileObjectData.newTile.Height = 4;
            TileObjectData.newTile.Origin = new Point16(3, 3);
            TileObjectData.newTile.CoordinateHeights = [16, 16, 16, 16];
            TileObjectData.newTile.StyleWrapLimit = 36;
            TileObjectData.newTile.AnchorBottom = new AnchorData(
                AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide,
                TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.addTile(Type);
        }

        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.BorealWood);
            return false;
        }

        public override void NumDust(int i, int j, bool fail, ref int num) => num = fail ? 1 : 3;

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            r = 0.4f;
            g = 0.2f;
            b = 0.1f;
        }

        public override bool CanDrop(int i, int j) => false;

        public override void MouseOver(int i, int j) {
            Player player = Main.LocalPlayer;
            player.noThrow = 2;
            player.mouseInterface = true;
            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = ModContent.ItemType<OldDuchest>();
        }
    }
}
