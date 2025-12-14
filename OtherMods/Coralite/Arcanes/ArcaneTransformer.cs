using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.OtherMods.Coralite.Arcanes
{
    internal class ArcaneTransformer : ModItem, ILocalizedModType
    {
        public override string Texture => CWRConstant.Arcanes + "ArcaneTransformer";
        public new string LocalizationCategory => "Arcanes.Items";
        public override bool IsLoadingEnabled(Mod mod) => MagikeCrossed.HasMod;
        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.maxStack = 1;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = Item.useTime = 15;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.createTile = ModContent.TileType<ArcaneTransformerTile>();
            Item.rare = CWRID.Rarity_DarkOrange;
            Item.value = Item.buyPrice(gold: 6);
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 1200;
        }
    }

    internal class ArcaneTransformerTile : ModTile, ILocalizedModType
    {
        public override string Texture => CWRConstant.Arcanes + "ArcaneTransformer";
        public new string LocalizationCategory => "Arcanes.Tiles";
        public override bool IsLoadingEnabled(Mod mod) => MagikeCrossed.HasMod;
        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            AddMapEntry(new Color(167, 72, 81), VaultUtils.GetLocalizedItemName<ArcaneTransformer>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.Width = 2;
            TileObjectData.newTile.Height = 2;
            TileObjectData.newTile.Origin = new Point16(1, 1);
            TileObjectData.newTile.CoordinateHeights = [16, 16];
            TileObjectData.newTile.StyleWrapLimit = 36;
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile
                | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.addTile(Type);
        }
    }

    internal class ArcaneTransformerTP : BaseBattery
    {
        public override int TargetTileID => ModContent.TileType<ArcaneTransformerTile>();
        public override bool ReceivedEnergy => true;
        public override float MaxUEValue => 1200;
        internal const int consumeUE = 1;
        public override bool IsLoadingEnabled(Mod mod) => MagikeCrossed.HasMod;
        public override void SetBattery() {
            base.SetBattery();
        }

        public override void UpdateMachine() {
            base.UpdateMachine();
        }

        public override void ExtraConductive(Point16 point, TileProcessor tp) {
            if (tp != null) {
                return;
            }

            if (MachineData.UEvalue > consumeUE && MagikeCrossed.AddMagike(point, consumeUE)) {
                MachineData.UEvalue -= consumeUE;
            }
        }

        public override void PostUpdateConductive() {
            base.PostUpdateConductive();
        }

        public override void PreTileDraw(SpriteBatch spriteBatch) {
            base.PreTileDraw(spriteBatch);
        }

        public override void Draw(SpriteBatch spriteBatch) {
            base.Draw(spriteBatch);
        }

        public override void FrontDraw(SpriteBatch spriteBatch) {
            DrawChargeBar();
        }
    }
}
