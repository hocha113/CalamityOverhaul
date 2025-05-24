using CalamityMod.Items.Materials;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
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
        public static LocalizedText UnderstandWindGriven2 { get; private set; }
        public override void SetStaticDefaults() {
            UnderstandWindGriven = this.GetLocalization(nameof(UnderstandWindGriven),
                () => "An initial understanding of how wind power works is required");
            UnderstandWindGriven2 = this.GetLocalization(nameof(UnderstandWindGriven2),
                () => "Need to dig up old wind machines on asteroids to understand how they're made");
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
            Item.value = Item.buyPrice(0, 1, 0, 0);
            Item.rare = ItemRarityID.Orange;
            Item.createTile = ModContent.TileType<WindGrivenGeneratorTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 200;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            if (Main.LocalPlayer.CWR().UnderstandWindGriven) {
                return;
            }
            TooltipLine line = new TooltipLine(CWRMod.Instance, "UnderstandWindGriven", UnderstandWindGriven2.Value);
            line.OverrideColor = Color.Cyan;
            tooltips.Add(line);
        }

        public static LocalizedText WindGrivenRecipeCondition(out Func<bool> condition) {
            condition = new Func<bool>(() => Main.LocalPlayer.CWR().UnderstandWindGriven);
            return UnderstandWindGriven;
        }

        public override void AddRecipes() {
            CreateRecipe().
                AddIngredient<DubiousPlating>(8).
                AddIngredient<MysteriousCircuitry>(8).
                AddRecipeGroup(CWRRecipes.TinBarGroup, 15).
                AddRecipeGroup(RecipeGroupID.IronBar, 5).
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
            AddMapEntry(new Color(67, 72, 81), VaultUtils.GetLocalizedItemName<WindGrivenGenerator>());

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

    internal class WindGrivenGeneratorTP : BaseWindGrivenTP
    {
        public override int TargetTileID => ModContent.TileType<WindGrivenGeneratorTile>();
        public override float MaxUEValue => 200f;
        public override int TargetItem => ModContent.ItemType<WindGrivenGenerator>();
        [VaultLoaden(CWRConstant.Asset + "Generator/Blade")]
        internal static Asset<Texture2D> Blade { get; private set; }
        public override void SetWindGriven() {
            baseRotSpeed = 0.016f;
            energyConversion = 1.6f;
            baseSoundPith = 0.45f;
            baseVolume = 0.6f;
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

            DrawChargeBar();
        }
    }
}
