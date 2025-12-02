using CalamityMod.Items.Materials;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Collectors
{
    internal class Collector : ModItem
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/Collector";
        internal static LocalizedText Text1;
        internal static LocalizedText Text2;
        internal static LocalizedText Text3;
        public override void SetStaticDefaults() {
            Text1 = this.GetLocalization(nameof(Text1), () => "Excessive Quantity!");
            Text2 = this.GetLocalization(nameof(Text2), () => "There are no boxes around!");
            Text3 = this.GetLocalization(nameof(Text3), () => "Lack of Electricity!");
        }
        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.maxStack = 9999;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.value = Item.buyPrice(0, 2, 40, 0);
            Item.rare = ItemRarityID.LightRed;
            Item.createTile = ModContent.TileType<CollectorTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 800;
        }

        public override void AddRecipes() {
            CreateRecipe().
                AddIngredient<DubiousPlating>(15).
                AddIngredient<MysteriousCircuitry>(20).
                AddRecipeGroup(CWRRecipes.TungstenBarGroup, 8).
                AddIngredient(ItemID.Hook, 3).
                AddTile(TileID.Anvils).
                Register();
        }
    }

    internal class CollectorTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/CollectorTile";
        [VaultLoaden(CWRConstant.Asset + "ElectricPowers/CollectorStartTile")]
        public static Asset<Texture2D> startAsset = null;
        [VaultLoaden(CWRConstant.Asset + "ElectricPowers/CollectorStartTileGlow")]
        public static Asset<Texture2D> startGlowAsset = null;
        [VaultLoaden(CWRConstant.Asset + "ElectricPowers/CollectorTileGlow")]
        public static Asset<Texture2D> tileGlowAsset = null;
        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            AddMapEntry(new Color(67, 72, 81), VaultUtils.GetLocalizedItemName<Collector>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);
            TileObjectData.newTile.Width = 3;
            TileObjectData.newTile.Height = 5;
            TileObjectData.newTile.Origin = new Point16(1, 4);
            TileObjectData.newTile.CoordinateHeights = [16, 16, 16, 16, 16];
            TileObjectData.newTile.StyleWrapLimit = 36;
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile
                | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.addTile(Type);
        }

        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.Electric);
            return false;
        }

        public override bool CanDrop(int i, int j) => false;

        public override void MouseOver(int i, int j) => Main.LocalPlayer.SetMouseOverByTile(ModContent.ItemType<Collector>());

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out CollectorTP collector)) {
                return false;
            }

            Tile t = Main.tile[i, j];
            int frameXPos = t.TileFrameX;
            int frameYPos = t.TileFrameY;
            frameYPos += collector.frame * 18 * 5;
            Texture2D tex = collector.workState ? TextureAssets.Tile[Type].Value : startAsset.Value;
            Texture2D glow = collector.workState ? tileGlowAsset.Value : startGlowAsset.Value;
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 drawOffset = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y) + offset;
            Color drawColor = Lighting.GetColor(i, j);
            if (!t.IsHalfBlock && t.Slope == 0) {
                spriteBatch.Draw(tex, drawOffset, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
                spriteBatch.Draw(glow, drawOffset, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , Color.White, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
            else if (t.IsHalfBlock) {
                spriteBatch.Draw(tex, drawOffset + Vector2.UnitY * 8f, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
                spriteBatch.Draw(glow, drawOffset + Vector2.UnitY * 8f, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , Color.White, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
            return false;
        }
    }
}
