using InnoVault.PRT;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using ReLogic.Utilities;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.Generator.Hydroelectrics
{
    internal class Hydroelectric : ModItem
    {
        public override string Texture => CWRConstant.Asset + "Generator/Hydroelectric";
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
            Item.value = Item.buyPrice(0, 0, 60, 30);
            Item.rare = ItemRarityID.Pink;
            Item.createTile = ModContent.TileType<HydroelectricTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 1000;
        }

        public static float GetEnvironmentEfficiency() {
            float multiplier = 1f;
            if (Main.raining) {
                multiplier += 0.2f;
            }
            if (Main.moonPhase == 0 || Main.moonPhase == 4) {
                multiplier += 0.1f;
            }
            return multiplier;
        }

        public override void AddRecipes() {
            if (!CWRRef.Has) {
                CreateRecipe().
                AddIngredient(ItemID.EmptyBucket, 4).
                AddRecipeGroup(RecipeGroupID.IronBar, 5).
                AddTile(TileID.Anvils).
                Register();
                return;
            }
            CreateRecipe().
                AddIngredient(ItemID.EmptyBucket, 4).
                AddRecipeGroup(RecipeGroupID.IronBar, 5).
                AddRecipeGroup(CWRRecipes.GoldBarGroup, 5).
                AddIngredient(CWRID.Item_DubiousPlating, 10).
                AddIngredient(CWRID.Item_MysteriousCircuitry, 10).
                AddTile(TileID.Anvils).
                Register();
        }
    }

    internal class HydroelectricTile : BaseGeneratorTile
    {
        public override string Texture => CWRConstant.Asset + "Generator/HydroelectricTile";
        public override int GeneratorTP => TileProcessorLoader.GetModuleID<HydroelectricTP>();
        public override int GeneratorUI => 0;
        public override int TargetItem => ModContent.ItemType<Hydroelectric>();
        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            Main.tileSolidTop[Type] = true;
            AddMapEntry(new Color(67, 72, 81), VaultUtils.GetLocalizedItemName<Hydroelectric>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.Width = 5;
            TileObjectData.newTile.Height = 3;
            TileObjectData.newTile.Origin = new Point16(3, 2);
            TileObjectData.newTile.CoordinateHeights = [16, 16, 16];
            TileObjectData.newTile.StyleWrapLimit = 36;
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.addTile(Type);
        }
    }

    internal class HydroelectricTP : BaseGeneratorTP
    {
        public override int TargetTileID => ModContent.TileType<HydroelectricTile>();
        public override int TargetItem => ModContent.ItemType<Hydroelectric>();
        public override float MaxUEValue => 1000;
        [VaultLoaden(CWRConstant.Asset + "Generator/")]
        private static Asset<Texture2D> HydroelectricFlabellum { get; set; }
        private Vector2 FlabellumPos => CenterInWorld + new Vector2(22, -12);
        private float flabellumRot;
        private float flabellumRotVlome;
        private SlotId hydroelectricSoundSlot;
        private SoundStyle hydroelectricSoundStyle = new SoundStyle(CWRConstant.Asset + "Sounds/RollingMERoer") {
            IsLooped = true,
            MaxInstances = 6,
        };

        private bool LoopingSoundUpdate(ActiveSound soundInstance) {
            soundInstance.Pitch = (-0.4f + flabellumRotVlome) * 2.5f;
            soundInstance.Position = FlabellumPos;
            soundInstance.Volume = flabellumRotVlome * 2f;
            return Active;
        }

        public override void GeneratorUpdate() {
            Tile tile = Framing.GetTileSafely(FlabellumPos);
            if (tile.LiquidAmount > 0 && tile.LiquidType == LiquidID.Water) {
                float efficiency = Hydroelectric.GetEnvironmentEfficiency();
                float maxRot = 0.4f * efficiency;
                if (flabellumRotVlome < maxRot) {
                    flabellumRotVlome += 0.002f;
                }
                flabellumRot += flabellumRotVlome;

                if (MachineData.UEvalue < MaxUEValue) {
                    MachineData.UEvalue += flabellumRotVlome * 0.24f;
                }

                if (InScreen && Main.rand.NextBool(Math.Max(10 - (int)(flabellumRotVlome * 10), 4))) {
                    PRTLoader.NewParticle<PRT_WaterBubble>(FlabellumPos + VaultUtils.RandVr(32), new Vector2(0, -4)
                        , Color.White, Main.rand.NextFloat(0.4f, 0.8f));
                }

                if (!SoundEngine.TryGetActiveSound(hydroelectricSoundSlot, out var activeSound)) {
                    hydroelectricSoundSlot = SoundEngine.PlaySound(hydroelectricSoundStyle, FlabellumPos, LoopingSoundUpdate);
                }
            }
            else {
                flabellumRotVlome = 0;
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            Texture2D value = HydroelectricFlabellum.Value;
            for (int i = 0; i < 4; i++) {
                spriteBatch.Draw(value, FlabellumPos - Main.screenPosition, null, Lighting.GetColor(FlabellumPos.ToTileCoordinates())
                , -flabellumRot + MathHelper.TwoPi / 4 * i, new Vector2(value.Width / 2, value.Height - 2), 1, SpriteEffects.None, 0);
            }
        }

        public override void FrontDraw(SpriteBatch spriteBatch) => DrawChargeBar();
    }

    internal class PRT_WaterBubble : BasePRT
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetProperty() {
            Lifetime = Main.rand.Next(180, 220);
            ai[1] = Main.rand.NextFloat(12);
            Opacity = 1f;
        }

        public override void AI() {
            if (ai[0] == 0) {
                Tile tile = Framing.GetTileSafely(Position + Velocity * 2);
                if (tile.LiquidAmount == 0 || tile.LiquidType != LiquidID.Water) {
                    ai[0] = 1;
                    ai[2] = Main.rand.Next(14);
                    Velocity /= 6;
                }
            }
            else {
                Opacity *= 0.9f;
                if (--ai[2] <= 0) {
                    for (int i = 0; i < 6; i++) {
                        Vector2 dustVer = VaultUtils.RandVr(6);
                        Dust.NewDust(Position - new Vector2(6, 6), 12, 12, DustID.Water_Snow, dustVer.X, dustVer.Y);
                    }
                    Kill();
                }
            }

            ai[1] += Main.rand.NextFloat(2);
            if (ai[1] > 12) {
                Velocity = Velocity.RotatedByRandom(0.2f);
                Velocity = Vector2.Lerp(Velocity, new Vector2(0, -1), 0.15f).UnitVector() * Velocity.Length();
                ai[1] = 0;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Main.instance.LoadProjectile(ProjectileID.Bubble);
            Texture2D value = TextureAssets.Projectile[ProjectileID.Bubble].Value;
            spriteBatch.Draw(value, Position - Main.screenPosition, null
                , Lighting.GetColor(Position.ToTileCoordinates()) * (1f - LifetimeCompletion) * Opacity
                , Rotation, value.Size() / 2, Scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
