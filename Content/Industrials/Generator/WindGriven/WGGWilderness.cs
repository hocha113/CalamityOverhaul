using CalamityMod.Items.Materials;
using CalamityOverhaul.Common;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
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
            Item.value = Item.buyPrice(0, 1, 0, 0);
            Item.rare = ItemRarityID.Green;
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
            AddMapEntry(new Color(67, 72, 81), VaultUtils.GetLocalizedItemName<WGGWilderness>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.Width = 3;
            TileObjectData.newTile.Height = 8;
            TileObjectData.newTile.Origin = new Point16(1, 7);
            TileObjectData.newTile.CoordinateHeights = [16, 16, 16, 16, 16, 16, 16, 16];
            TileObjectData.newTile.StyleWrapLimit = 36;
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.addTile(Type);
        }
        public override bool CanDrop(int i, int j) => false;
    }

    internal class WGGWildernessTP : BaseWindGrivenTP
    {
        public override int TargetTileID => ModContent.TileType<WGGWildernessTile>();
        public override float MaxUEValue => 200;
        public override bool CanDrop => false;
        public override int TargetItem => ModContent.ItemType<WGGWilderness>();
        [VaultLoaden(CWRConstant.Asset + "Generator/BladeWilderness")]
        internal static Asset<Texture2D> BladeWilderness { get; private set; }
        public override void SetWindGriven() {
            baseRotSpeed = 0.016f;
            energyConversion = 0.02f;
            baseSoundPith = 0.45f;
            baseVolume = 0.6f;
        }

        public override void GeneratorKill() {
            if (!VaultUtils.isServer) {
                Player player = VaultUtils.FindClosestPlayer(CenterInWorld, 1200);
                if (player != null && (!player.CWR().UnderstandWindGriven || Main.rand.NextBool(3))) {
                    player.CWR().UnderstandWindGriven = true;
                    int text = CombatText.NewText(HitBox, new Color(111, 247, 200), CWRLocText.Instance.WindGriven_Text1.Value, false);
                    Main.combatText[text].lifeTime = 300;
                }
            }

            if (VaultUtils.isClient) {
                return;
            }
            int dropNum = Main.rand.Next(2, 5);
            for (int i = 0; i < dropNum; i++) {
                DropItem(ModContent.ItemType<DubiousPlating>());
            }
            dropNum = Main.rand.Next(1, 3);
            for (int i = 0; i < dropNum; i++) {
                DropItem(ModContent.ItemType<MysteriousCircuitry>());
            }
            if (Main.rand.NextBool(50)) {
                DropItem(ModContent.ItemType<SuspiciousScrap>());
            }
        }

        public override void FrontDraw(SpriteBatch spriteBatch) {
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
