using CalamityOverhaul.Common;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.Generator.WindGriven
{
    internal class WGGWilderness : ModItem
    {
        public override string Texture => CWRConstant.Asset + "Generator/WGGWilderness";
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
            Item.createTile = ModContent.TileType<WGGWildernessTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 200;
        }
    }

    internal class WGGWildernessTile : BaseGeneratorTile
    {
        public override string Texture => CWRConstant.Asset + "Generator/WGGWildernessTile";
        public override int GeneratorTP => TileProcessorLoader.GetModuleID<WGGWildernessTP>();
        public override int GeneratorUI => 0;
        public override int TargetItem => ModContent.ItemType<WGGWilderness>();
        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            Main.tileSolidTop[Type] = true;
            AddMapEntry(new Color(67, 72, 81), CWRUtils.SafeGetItemName<WGGWilderness>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.Width = 3;
            TileObjectData.newTile.Height = 9;
            TileObjectData.newTile.Origin = new Point16(1, 8);
            TileObjectData.newTile.CoordinateHeights = [16, 16, 16, 16, 16, 16, 16, 16, 16];
            TileObjectData.newTile.StyleWrapLimit = 36;
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.addTile(Type);
        }
        public override bool CanDrop(int i, int j) => false;
    }

    internal class WGGWildernessTP : BaseGeneratorTP, ICWRLoader
    {
        public override int TargetTileID => ModContent.TileType<WGGWildernessTile>();
        private float rotition;
        private float rotSpeed;
        private int soundCount;
        public override float MaxUEValue => 200;
        public override bool CanDrop => false;
        public override int TargetItem => ModContent.ItemType<WGGWilderness>();
        internal static Asset<Texture2D> BladeWilderness { get; private set; }
        void ICWRLoader.LoadAsset() => BladeWilderness = CWRUtils.GetT2DAsset(CWRConstant.Asset + "Generator/BladeWilderness");
        void ICWRLoader.UnLoadData() => BladeWilderness = null;
        public override void GeneratorUpdate() {
            rotSpeed = 0.02f;
            rotition += rotSpeed;
            if (GeneratorData.UEvalue < MaxUEValue) {
                GeneratorData.UEvalue += rotSpeed * 2;
            }

            if (++soundCount > 160 && Main.LocalPlayer.Distance(CenterInWorld) < 600) {
                SoundEngine.PlaySound(CWRSound.Windmill with { Volume = 0.35f, MaxInstances = 12 }, CenterInWorld);
                soundCount = 0;
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            Texture2D blade = BladeWilderness.Value;
            Vector2 drawPos = PosInWorld - Main.screenPosition + new Vector2(26, 20);
            Vector2 drawOrig = new Vector2(blade.Width / 2, blade.Height);
            for (int i = 0; i < 3; i++) {
                float drawRot = (MathHelper.TwoPi) / 3f * i + rotition;
                Color color = Lighting.GetColor(Position.ToPoint() + drawRot.ToRotationVector2().ToPoint());
                spriteBatch.Draw(blade, drawPos, null, color, drawRot, drawOrig, 1, SpriteEffects.None, 0);
            }
        }
    }
}
