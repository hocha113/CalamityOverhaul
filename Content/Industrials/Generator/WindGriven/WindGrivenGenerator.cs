using CalamityMod.Items.Materials;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.Generator.Thermal;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Pipelines;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.Generator.WindGriven
{
    internal class WindGrivenGenerator : ModItem
    {
        public override string Texture => CWRConstant.Asset + "Generator/WindGrivenGenerator";
        public static LocalizedText UnderstandWindGriven { get; private set; }
        public override void SetStaticDefaults() {
            UnderstandWindGriven = this.GetLocalization(nameof(UnderstandWindGriven), 
                () => "An initial understanding of how wind power works is required");
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
            Item.value = Item.buyPrice(0, 2, 0, 0);
            Item.rare = ItemRarityID.Quest;
            Item.createTile = ModContent.TileType<WindGrivenGeneratorTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 200;
        }

        public static LocalizedText WindGrivenRecipeCondition(out Func<bool> condition) {
            condition = new Func<bool>(() => Main.LocalPlayer.CWR().UnderstandWindGriven);
            return UnderstandWindGriven;
        }

        public override void AddRecipes() {
            CreateRecipe().
                AddIngredient<DubiousPlating>(8).
                AddIngredient<MysteriousCircuitry>(8).
                AddIngredient(ItemID.CopperBar, 15).
                AddIngredient(ItemID.IronBar, 5).
                AddCondition(WindGrivenRecipeCondition(out var condition), condition).
                AddTile(TileID.Anvils).
                Register();
        }
    }

    internal class WindGrivenGeneratorTile : BaseGeneratorTile
    {
        public override string Texture => CWRConstant.Asset + "Generator/WindGrivenGeneratorTile";
        public override int GeneratorTP => TileProcessorLoader.GetModuleID<WindGrivenGeneratorTP>();
        public override int GeneratorUI => 0;
        public override int TargetItem => ModContent.ItemType<WindGrivenGenerator>();
        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            Main.tileSolidTop[Type] = true;
            AddMapEntry(new Color(67, 72, 81), CWRUtils.SafeGetItemName<WindGrivenGenerator>());

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

    internal class WindGrivenGeneratorTP : BaseGeneratorTP, ICWRLoader
    {
        public override int TargetTileID => ModContent.TileType<WindGrivenGeneratorTile>();
        private float rotition;
        private float rotSpeed;
        private int soundCount;
        public override float MaxUEValue => 200f;
        public override int TargetItem => ModContent.ItemType<WindGrivenGenerator>();
        internal static Asset<Texture2D> Blade { get; private set; }
        void ICWRLoader.LoadAsset() => Blade = CWRUtils.GetT2DAsset(CWRConstant.Asset + "Generator/Blade");
        void ICWRLoader.UnLoadData() => Blade = null;
        public override void GeneratorUpdate() {
            rotSpeed = 0.02f;
            rotition += rotSpeed;
            if (MachineData.UEvalue < MaxUEValue) {
                MachineData.UEvalue += rotSpeed * 10;
            }

            if (++soundCount > 160 && Main.LocalPlayer.Distance(CenterInWorld) < 600) {
                SoundEngine.PlaySound(CWRSound.Windmill with { Volume = 0.35f, MaxInstances = 12 }, CenterInWorld);
                soundCount = 0;
            }
        }

        public override void FrontDraw(SpriteBatch spriteBatch) {
            Texture2D blade = Blade.Value;
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
