using CalamityOverhaul.Common;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.Generator.WindGriven
{
    internal class WGGMK2Wilderness : ModItem
    {
        public override string Texture => CWRConstant.Asset + "Generator/WGGMK2Wilderness";
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
            Item.createTile = ModContent.TileType<WGGMK2WildernessTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 800;
        }
    }

    internal class WGGMK2WildernessTile : BaseGeneratorTile
    {
        public override string Texture => CWRConstant.Asset + "Generator/WGGMK2WildernessTile";
        public override int GeneratorTP => TileProcessorLoader.GetModuleID<WGGMK2WildernessTP>();
        public override int GeneratorUI => 0;
        public override int TargetItem => ModContent.ItemType<WGGMK2Wilderness>();
        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            Main.tileSolidTop[Type] = true;
            AddMapEntry(new Color(67, 72, 81), CWRUtils.SafeGetItemName<WGGMK2Wilderness>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.Width = 3;
            TileObjectData.newTile.Height = 18;
            TileObjectData.newTile.Origin = new Point16(1, 16);
            TileObjectData.newTile.CoordinateHeights = [
                16, 16, 16, 16, 16, 16, 16, 16, 16
                , 16, 16, 16, 16, 16, 16, 16, 16, 16];
            TileObjectData.newTile.StyleWrapLimit = 36;
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.addTile(Type);
        }
        public override bool CanDrop(int i, int j) => false;
    }

    internal class WGGMK2WildernessTP : BaseGeneratorTP
    {
        public override int TargetTileID => ModContent.TileType<WGGMK2WildernessTile>();
        private float rotition;
        private float rotSpeed;
        private int soundCount;
        public override float MaxUEValue => 800f;
        public override bool CanDrop => false;
        public override int TargetItem => ModContent.ItemType<WGGMK2Wilderness>();
        //public override void SetGenerator() => LoadenWorldSendData = false;
        public override void GeneratorUpdate() {
            rotSpeed = 0.012f;
            rotition += rotSpeed;
            if (GeneratorData.UEvalue < MaxUEValue) {
                GeneratorData.UEvalue += rotSpeed * 6;
            }

            if (++soundCount > 160 && Main.LocalPlayer.Distance(CenterInWorld) < 600) {
                SoundEngine.PlaySound(CWRSound.Windmill with { Volume = 0.65f, MaxInstances = 12 }, CenterInWorld);
                soundCount = 0;
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            Texture2D blade = CWRUtils.GetT2DValue(CWRConstant.Asset + "Generator/MK2BladeWilderness");
            Vector2 drawPos = PosInWorld - Main.screenPosition + new Vector2(24, 28);
            Vector2 drawOrig = new Vector2(blade.Width / 2, blade.Height);
            for (int i = 0; i < 3; i++) {
                float drawRot = (MathHelper.TwoPi) / 3f * i + rotition;
                Color color = Lighting.GetColor(Position.ToPoint() + drawRot.ToRotationVector2().ToPoint());
                spriteBatch.Draw(blade, drawPos, null, color, drawRot, drawOrig, 1, SpriteEffects.None, 0);
            }
        }
    }
}
