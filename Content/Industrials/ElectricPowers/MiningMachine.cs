using CalamityMod.Items.Materials;
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
    internal class MiningMachine : ModItem
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/MiningMachine";
        public static LocalizedText DontWork { get; set; }
        public override void SetStaticDefaults() {
            DontWork = this.GetLocalization(nameof(DontWork),
                () => "It needs to be placed on a hard surface in order to carry out mining operations.");
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
            Item.value = Item.buyPrice(0, 1, 10, 0);
            Item.rare = ItemRarityID.Orange;
            Item.createTile = ModContent.TileType<MiningMachineTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 800;
        }

        public override void AddRecipes() {
            CreateRecipe().
                AddRecipeGroup(RecipeGroupID.IronBar, 15).
                AddRecipeGroup(CWRRecipes.IronPickaxeGroup).
                AddIngredient<DubiousPlating>(5).
                AddIngredient<MysteriousCircuitry>(5).
                AddTile(TileID.Anvils).
                Register();
        }
    }

    internal class MiningMachineTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/MiningMachineTile";
        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;

            AddMapEntry(new Color(67, 72, 81), VaultUtils.GetLocalizedItemName<MiningMachine>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);
            TileObjectData.newTile.Width = 3;
            TileObjectData.newTile.Height = 3;
            TileObjectData.newTile.Origin = new Point16(1, 2);
            TileObjectData.newTile.AnchorBottom = new AnchorData(
                AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide
                , TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateHeights = [16, 16, 16];
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
            if (!TileProcessorLoader.ByPositionGetTP(point, out MiningMachineTP miningMachine)) {
                return false;
            }

            Tile t = Main.tile[i, j];
            int frameXPos = t.TileFrameX;
            int frameYPos = t.TileFrameY;
            frameYPos += miningMachine.frame * 18 * 3;
            Texture2D tex = TextureAssets.Tile[Type].Value;
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange) + miningMachine.offsetPos;
            Vector2 drawOffset = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y) + offset;
            Color drawColor = Lighting.GetColor(i, j);
            if (miningMachine.MachineData.UEvalue < MiningMachineTP.consumeUE) {
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

    internal class MiningMachineTP : BaseBattery, ICWRLoader
    {
        public override int TargetTileID => ModContent.TileType<MiningMachineTile>();
        public override int TargetItem => ModContent.ItemType<MiningMachine>();
        public override bool ReceivedEnergy => true;
        public override float MaxUEValue => 800;
        internal const int consumeUE = 5;
        internal int time;
        internal int time2;
        internal int frame;
        internal Vector2 offsetPos;
        internal static Dictionary<int, float> Ores { get; private set; }

        void ICWRLoader.SetupData() {
            Ores = new Dictionary<int, float>() {
                { ItemID.IronOre, 0.1f },
                { ItemID.CopperOre, 0.1f },
                { ItemID.GoldOre, 0.1f },
                { ItemID.SilverOre, 0.1f },
                { ItemID.TinOre, 0.1f },
                { ItemID.LeadOre, 0.1f },
                { ItemID.TungstenOre, 0.1f },
                { ItemID.PlatinumOre, 0.1f },
                { ItemID.DemoniteOre, 0.1f },
                { ItemID.Coal, 0.1f },
                { ModContent.ItemType<DubiousPlating>(), 0.04f },
                { ModContent.ItemType<MysteriousCircuitry>(), 0.012f },
            };
        }

        void ICWRLoader.UnLoadData() => Ores?.Clear();

        public override void SetBattery() {
            IdleDistance = 4000;//玩家远离后停止作业
        }

        public override void UpdateMachine() {
            if (MachineData.UEvalue <= consumeUE) {
                offsetPos = Vector2.Zero;
                return; // 如果没有能量，无法运行
            }

            VaultUtils.ClockFrame(ref frame, 5, 3);

            bool canDig = true;
            for (int i = 0; i < 3; i++) {
                for (int j = 0; j < 6; j++) {
                    Tile tile = Framing.GetTileSafely(Position + new Point16(i, j));
                    if (!tile.HasTile) {
                        canDig = false;
                    }
                }
            }

            if (canDig) {
                if (!Main.dedServ) {
                    if (++time > 4) {
                        offsetPos = new Vector2(Main.rand.Next(-2, 2), Main.rand.Next(0, 2));
                        time = 0;
                    }

                    Vector2 excavatePos = PosInWorld + new Vector2(10, 40);
                    if (Main.rand.NextBool(6)) {
                        Dust.NewDust(excavatePos, 1, 1, DustID.Stone);
                    }
                }

                if (++time2 > 20) {
                    if (!Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.Item22 with { Pitch = -0.2f, Volume = 0.6f }, CenterInWorld);
                        SoundEngine.PlaySound(SoundID.Dig with { Pitch = -0.2f, Volume = 0.6f }, CenterInWorld);
                    }

                    if (!VaultUtils.isClient && Main.rand.NextBool(6)) {
                        DropOre(); // 生成矿物掉落
                    }

                    MachineData.UEvalue -= consumeUE; // 挖掘消耗能量
                    time2 = 0;
                }
                return;
            }

            if (!Main.dedServ) {
                if (++time2 > 4) {
                    SoundEngine.PlaySound(SoundID.Item22 with { Pitch = -0.2f, Volume = 0.6f }, CenterInWorld);
                    time2 = 0;
                }

                if (++time > 180) {
                    int text = CombatText.NewText(HitBox, Color.DarkSeaGreen, MiningMachine.DontWork.Value);
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