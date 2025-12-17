using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers
{
    internal class MiningMachineMk2 : ModItem
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/MiningMachineMk2";
        public static LocalizedText DontWork { get; set; }
        public override void SetStaticDefaults() {
            DontWork = this.GetLocalization(nameof(DontWork),
                () => "需要放置在坚硬的表面上才能进行挖掘作业");
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
            Item.value = Item.buyPrice(0, 10, 50, 0);
            Item.rare = ItemRarityID.Pink;
            Item.createTile = ModContent.TileType<MiningMachineMk2Tile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 2400;
        }

        public override void AddRecipes() {
            if (!CWRRef.Has) {
                CreateRecipe().
                AddIngredient<MiningMachine>().
                AddRecipeGroup(CWRRecipes.AdamantiteBarGroup, 25).
                AddTile(TileID.MythrilAnvil).
                Register();
                return;
            }
            CreateRecipe().
                AddIngredient<MiningMachine>().
                AddRecipeGroup(CWRRecipes.AdamantiteBarGroup, 25).
                AddIngredient(CWRID.Item_DubiousPlating, 15).
                AddIngredient(CWRID.Item_MysteriousCircuitry, 15).
                AddTile(TileID.MythrilAnvil).
                Register();
        }
    }

    internal class MiningMachineMk2Tile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/MiningMachineMk2Tile";
        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;

            AddMapEntry(new Color(45, 50, 58), VaultUtils.GetLocalizedItemName<MiningMachineMk2>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);
            TileObjectData.newTile.Width = 9;
            TileObjectData.newTile.Height = 10;
            TileObjectData.newTile.Origin = new Point16(4, 9);
            TileObjectData.newTile.AnchorBottom = new AnchorData(
                AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide
                , TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateHeights = [16, 16, 16, 16, 16, 16, 16, 16, 16, 16];
            TileObjectData.newTile.LavaDeath = false;

            TileObjectData.addTile(Type);
        }

        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.Electric);
            return false;
        }

        public override bool CanDrop(int i, int j) => false;

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out MiningMachineMk2TP miningMachine)) {
                return false;
            }

            Tile t = Main.tile[i, j];
            int frameXPos = t.TileFrameX;
            int frameYPos = t.TileFrameY;
            frameYPos += miningMachine.frame * 18 * 10;
            Texture2D tex = TextureAssets.Tile[Type].Value;
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange) + miningMachine.offsetPos;
            Vector2 drawOffset = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y) + offset;
            Color drawColor = Lighting.GetColor(i, j);
            if (miningMachine.MachineData.UEvalue < MiningMachineMk2TP.consumeUE) {
                drawColor.R /= 2;
                drawColor.G /= 2;
                drawColor.B /= 2;
                drawColor.A = 255;
            }

            if (!t.IsHalfBlock && t.Slope == 0) {
                spriteBatch.Draw(tex, drawOffset, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
            else if (t.IsHalfBlock) {
                spriteBatch.Draw(tex, drawOffset + Vector2.UnitY * 8f, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
            return false;
        }
    }

    internal class MiningMachineMk2TP : BaseBattery, ICWRLoader
    {
        public override int TargetTileID => ModContent.TileType<MiningMachineMk2Tile>();
        public override int TargetItem => ModContent.ItemType<MiningMachineMk2>();
        public override bool ReceivedEnergy => true;
        public override float MaxUEValue => 2400;
        internal const int consumeUE = 8;
        internal int time;
        internal int time2;
        internal int frame;
        internal Vector2 offsetPos;
        internal static Dictionary<int, float> Ores { get; private set; }

        void ICWRLoader.SetupData() {
            Ores = new Dictionary<int, float>() {
                { ItemID.IronOre, 0.12f },
                { ItemID.CopperOre, 0.12f },
                { ItemID.GoldOre, 0.11f },
                { ItemID.SilverOre, 0.11f },
                { ItemID.TinOre, 0.12f },
                { ItemID.LeadOre, 0.12f },
                { ItemID.TungstenOre, 0.11f },
                { ItemID.PlatinumOre, 0.11f },
                { ItemID.DemoniteOre, 0.10f },
                { ItemID.CrimtaneOre, 0.10f },
                { ItemID.Meteorite, 0.09f },
                { ItemID.Obsidian, 0.08f },
                { ItemID.Hellstone, 0.07f },
                { ItemID.CobaltOre, 0.06f },
                { ItemID.PalladiumOre, 0.06f },
                { ItemID.MythrilOre, 0.05f },
                { ItemID.OrichalcumOre, 0.05f },
                { ItemID.AdamantiteOre, 0.04f },
                { ItemID.TitaniumOre, 0.04f },
                { ItemID.Coal, 0.13f },
            };
            if (CWRRef.Has) {
                Ores[CWRID.Item_DubiousPlating] = 0.08f;
                Ores[CWRID.Item_MysteriousCircuitry] = 0.062f;
            }
        }

        void ICWRLoader.UnLoadData() => Ores?.Clear();

        public override void SetBattery() {
            IdleDistance = 5000;
        }

        public override void UpdateMachine() {
            if (MachineData.UEvalue <= consumeUE) {
                offsetPos = Vector2.Zero;
                return;
            }

            VaultUtils.ClockFrame(ref frame, 4, 5);

            bool canDig = true;
            for (int i = 0; i < 9; i++) {
                for (int j = 0; j < 13; j++) {
                    Tile tile = Framing.GetTileSafely(Position + new Point16(i, j));
                    if (!tile.HasTile) {
                        canDig = false;
                    }
                }
            }

            if (canDig) {
                if (!Main.dedServ) {
                    if (++time > 3) {
                        offsetPos = new Vector2(Main.rand.Next(-1, 1), Main.rand.Next(0, 1));
                        time = 0;
                    }

                    Vector2 excavatePos = PosInWorld + new Vector2(92, 140);
                    if (Main.rand.NextBool(4)) {
                        Dust.NewDust(excavatePos, 1, 1, DustID.Stone);
                    }
                }

                if (++time2 > 24) {
                    if (!Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.Item22 with { Pitch = -0.6f, Volume = 0.7f }, CenterInWorld);
                        SoundEngine.PlaySound(SoundID.Dig with { Pitch = -0.6f, Volume = 0.7f }, CenterInWorld);
                    }

                    if (!VaultUtils.isClient && Main.rand.NextBool(4)) {
                        DropOre();
                    }

                    MachineData.UEvalue -= consumeUE;
                    time2 = 0;
                }
                return;
            }

            if (!Main.dedServ) {
                if (++time2 > 4) {
                    SoundEngine.PlaySound(SoundID.Item22 with { Pitch = -0.6f, Volume = 0.7f }, CenterInWorld);
                    time2 = 0;
                }

                if (++time > 180) {
                    int text = CombatText.NewText(HitBox, Color.DarkSeaGreen, MiningMachineMk2.DontWork.Value);
                    Main.combatText[text].lifeTime *= 2;
                    time = 0;
                }
            }
        }

        private void DropOre() {
            if (Ores == null || Ores.Count == 0) {
                return;
            }

            foreach (var ore in Ores) {
                if (Main.rand.NextFloat() < ore.Value) {
                    DropItem(ore.Key);
                    break;
                }
            }
        }

        public override void FrontDraw(SpriteBatch spriteBatch) => DrawChargeBar();
    }
}
