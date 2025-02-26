using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys
{
    internal class ThermalBattery : ModItem
    {
        public override string Texture => CWRConstant.Asset + "MaterialFlow/ThermalBattery";
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
            Item.value = Item.buyPrice(0, 2, 0, 0);
            Item.rare = ItemRarityID.Quest;
            Item.createTile = ModContent.TileType<ThermalBatteryTile>();
        }
    }

    internal class ThermalBatteryTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "MaterialFlow/ThermalBatteryTile";
        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;

            AddMapEntry(new Color(67, 72, 81), CWRUtils.SafeGetItemName<ThermalBattery>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style3x4);
            TileObjectData.newTile.Width = 3;
            TileObjectData.newTile.Height = 4;
            TileObjectData.newTile.Origin = new Point16(1, 1);
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateHeights = [16, 16, 16, 16];
            TileObjectData.newTile.LavaDeath = false;

            TileObjectData.addTile(Type);
        }
    }

    internal class ThermalBatteryTP : BaseBattery
    {
        public override int TargetTileID => ModContent.TileType<ThermalBatteryTile>();
        public override void SetProperty() {
            GeneratorData = new GeneratorData();
        }

        public override void Update() {
            base.Update();
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (GeneratorData != null) {
                Vector2 drawPos = PosInWorld - Main.screenPosition + new Vector2(0, -6);
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.ItemStack.Value, GeneratorData.UEvalue.ToString()
                    , drawPos.X, drawPos.Y, Color.White, Color.Black, new Vector2(0.1f), 0.5f);
            }
        }
    }
}
