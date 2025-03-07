using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
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
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = ThermalBatteryTP._maxUEValue;
        }
    }

    internal class ThermalBatteryTile : ModTile, ICWRLoader
    {
        public override string Texture => CWRConstant.Asset + "MaterialFlow/ThermalBatteryTile";
        public const int Width = 3;
        public const int Height = 4;
        public const int OriginOffsetX = 1;
        public const int OriginOffsetY = 1;
        public const int SheetSquare = 18;
        private static Asset<Texture2D> tileAsset;
        void ICWRLoader.LoadAsset() => tileAsset = ModContent.Request<Texture2D>(Texture);
        void ICWRLoader.UnLoadData() => tileAsset = null;
        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            Main.tileSolidTop[Type] = true;

            AnimationFrameHeight = 72;

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

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out ThermalBatteryTP thermal)) {
                return false;
            }

            Tile t = Main.tile[i, j];
            int frameXPos = t.TileFrameX;
            int frameYPos = t.TileFrameY;
            frameYPos += thermal.frame * (Height * SheetSquare);
            Texture2D tex = tileAsset.Value;
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 drawOffset = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y) + offset;
            Color drawColor = Lighting.GetColor(i, j);
            Texture2D glow = CWRUtils.GetT2DValue(CWRConstant.Asset + "MaterialFlow/ThermalBatteryFull");
            if (!t.IsHalfBlock && t.Slope == 0) {
                spriteBatch.Draw(tex, drawOffset, new Rectangle(frameXPos, thermal.fullLoad ? t.TileFrameY : frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
                spriteBatch.Draw(glow, drawOffset, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , thermal.drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
            else if (t.IsHalfBlock) {
                spriteBatch.Draw(tex, drawOffset + Vector2.UnitY * 8f, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
            return false;
        }
    }

    internal class ThermalBatteryTP : BaseBattery
    {
        public override int TargetTileID => ModContent.TileType<ThermalBatteryTile>();
        private bool hoverInTP;
        internal int frame;
        internal Color drawColor;
        internal float oldUEValue;
        internal int activeTime;
        internal const float _maxUEValue = 6000;
        public override float MaxUEValue => _maxUEValue;
        internal bool fullLoad;
        public override void Update() {
            fullLoad = MachineData.UEvalue >= MaxUEValue;
            hoverInTP = HitBox.Intersects(Main.MouseWorld.GetRectangle(1));
            if (--activeTime > 0 || fullLoad) {
                CWRUtils.ClockFrame(ref frame, 5, 5);
            }
            
            drawColor = Color.White * (MachineData.UEvalue / MaxUEValue);
            if (oldUEValue != MachineData.UEvalue) {
                activeTime = 60;
                oldUEValue = MachineData.UEvalue;
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (MachineData != null && hoverInTP) {
                Vector2 drawPos = CenterInWorld - Main.screenPosition + new Vector2(0, -6);
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, MachineData.UEvalue.ToString() + "UE"
                    , drawPos.X - 6, drawPos.Y, Color.White, Color.Black, new Vector2(0.1f), 0.5f);
            }
        }
    }
}
