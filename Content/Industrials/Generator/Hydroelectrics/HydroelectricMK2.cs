using InnoVault.PRT;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
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
    internal class HydroelectricMK2 : ModItem
    {
        public override string Texture => CWRConstant.Asset + "Generator/HydroelectricMK2";
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
            Item.rare = ItemRarityID.Pink;
            Item.createTile = ModContent.TileType<HydroelectricMK2Tile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 2200;
        }

        public override void AddRecipes() {
            CreateRecipe().
                AddIngredient<Hydroelectric>().
                AddIngredient(ItemID.InletPump).
                AddIngredient(ItemID.OutletPump).
                AddIngredient(CWRID.Item_DubiousPlating, 20).
                AddIngredient(CWRID.Item_MysteriousCircuitry, 20).
                AddRecipeGroup(CWRRecipes.MythrilBarGroup, 5).
                AddRecipeGroup(CWRRecipes.TinBarGroup, 15).
                AddTile(TileID.MythrilAnvil).
                Register();
        }
    }

    internal class HydroelectricMK2Tile : BaseGeneratorTile
    {
        public override string Texture => CWRConstant.Asset + "Generator/HydroelectricMK2Tile";
        public override int GeneratorTP => TileProcessorLoader.GetModuleID<HydroelectricTP>();
        public override int GeneratorUI => 0;
        public override int TargetItem => ModContent.ItemType<HydroelectricMK2>();
        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            Main.tileSolidTop[Type] = true;
            AddMapEntry(new Color(67, 72, 81), VaultUtils.GetLocalizedItemName<HydroelectricMK2>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.Width = 8;
            TileObjectData.newTile.Height = 4;
            TileObjectData.newTile.Origin = new Point16(4, 3);
            TileObjectData.newTile.CoordinateHeights = [16, 16, 16, 16];
            TileObjectData.newTile.StyleWrapLimit = 36;
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.addTile(Type);
        }
    }

    internal class HydroelectricMK2TP : BaseGeneratorTP
    {
        public override int TargetTileID => ModContent.TileType<HydroelectricMK2Tile>();
        public override int TargetItem => ModContent.ItemType<HydroelectricMK2>();
        public override float MaxUEValue => 2200;
        private float hasElmdVlome;
        private SlotId hydroelectricSoundSlot;
        private SoundStyle hydroelectricSoundStyle = new SoundStyle(CWRConstant.Asset + "Sounds/RollingMERoer") {
            IsLooped = true,
            MaxInstances = 6,
        };

        private bool LoopingSoundUpdate(ActiveSound soundInstance) {
            soundInstance.Pitch = (-0.5f + hasElmdVlome) * 2.4f;
            soundInstance.Position = CenterInWorld;
            soundInstance.Volume = hasElmdVlome * 2.4f;
            return Active;
        }

        public override void GeneratorUpdate() {
            bool hasWater = true;
            for (int i = 0; i < Width / 16; i++) {
                for (int j = 0; j < Height / 16; j++) {
                    Tile tile = Framing.GetTileSafely(Position + new Point16(i, j));
                    if (tile.LiquidAmount == 0 || tile.LiquidType != LiquidID.Water) {
                        hasWater = false;
                        break;
                    }
                }
            }

            if (!hasWater) {
                hasElmdVlome = 0;
                return;
            }

            if (hasElmdVlome < 0.4f) {
                hasElmdVlome += 0.002f;
            }

            if (MachineData.UEvalue < MaxUEValue) {
                MachineData.UEvalue += hasElmdVlome * 1.6f;
            }

            for (int i = 0; i < 3; i++) {
                if (!InScreen || !Main.rand.NextBool(Math.Max(10 - (int)(hasElmdVlome * 10), 4))) {
                    continue;
                }

                PRTLoader.NewParticle<PRT_WaterBubble>(
                    CenterInWorld + new Vector2(62, -16) + VaultUtils.RandVr(6)
                    , new Vector2(2, -4), Color.White, Main.rand.NextFloat(0.4f, 0.8f));

                BasePRT prt;

                Vector2 targetPos = CenterInWorld + new Vector2(44, 18);
                Vector2 spawnPos = CenterInWorld + new Vector2(46, 120) + VaultUtils.RandVr(46);
                prt = PRTLoader.NewParticle<PRT_HomeBubble>(
                    spawnPos, new Vector2(0, -6), Color.White, Main.rand.NextFloat(0.2f, 0.6f));
                prt.ai[0] = targetPos.X;
                prt.ai[1] = targetPos.Y;

                targetPos = CenterInWorld + new Vector2(24, 18);
                spawnPos = CenterInWorld + new Vector2(12, 120) + VaultUtils.RandVr(46);
                prt = PRTLoader.NewParticle<PRT_HomeBubble>(
                    spawnPos, new Vector2(0, -6), Color.White, Main.rand.NextFloat(0.2f, 0.6f));
                prt.ai[0] = targetPos.X;
                prt.ai[1] = targetPos.Y;
            }

            if (!SoundEngine.TryGetActiveSound(hydroelectricSoundSlot, out var activeSound)) {
                hydroelectricSoundSlot = SoundEngine.PlaySound(hydroelectricSoundStyle, CenterInWorld, LoopingSoundUpdate);
            }
        }

        public override void FrontDraw(SpriteBatch spriteBatch) => DrawChargeBar();
    }

    internal class PRT_HomeBubble : BasePRT
    {
        public override string Texture => CWRConstant.Placeholder;
        private bool spawn;
        public override void SetProperty() {
            Lifetime = Main.rand.Next(180, 220);
            ai[2] = Main.rand.NextFloat(12);
            Opacity = 1f;
        }

        public override void AI() {
            if (!spawn) {
                while (true) {
                    Tile tile = Framing.GetTileSafely(Position);
                    if (tile.LiquidAmount == 0 || tile.LiquidType != LiquidID.Water) {
                        Position.Y -= 16;
                    }
                    else {
                        break;
                    }
                    Opacity -= 0.04f;
                    if (Opacity <= 0) {
                        Kill();
                        break;
                    }
                }
                spawn = true;
            }

            Vector2 targetPos = new Vector2(ai[0], ai[1]);
            ai[2] += Main.rand.NextFloat(2);
            Velocity = Vector2.Lerp(Velocity, Position.To(targetPos), 0.15f).UnitVector() * Velocity.Length();

            float langs = Position.Distance(targetPos);
            if (langs < 16) {
                Velocity *= 0.8f;
                if (langs < 8) {
                    Kill();
                }
            }
            if (ai[2] > 12) {
                Velocity = Velocity.RotatedByRandom(0.2f);
                ai[2] = 0;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (!spawn) {
                return false;
            }
            Main.instance.LoadProjectile(ProjectileID.Bubble);
            Texture2D value = TextureAssets.Projectile[ProjectileID.Bubble].Value;
            spriteBatch.Draw(value, Position - Main.screenPosition, null
                , Lighting.GetColor(Position.ToTileCoordinates()) * (1f - LifetimeCompletion) * Opacity
                , Rotation, value.Size() / 2, Scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
