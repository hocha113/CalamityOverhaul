using CalamityOverhaul.Common;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.MiningMachines
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
            if (CWRID.DubiousCircuitryAvailable) {
                CreateRecipe().
                AddRecipeGroup(RecipeGroupID.IronBar, 15).
                AddRecipeGroup(CWRCrafted.IronPickaxeGroup).
                AddIngredient(CWRID.Item_DubiousPlating, 5).
                AddIngredient(CWRID.Item_MysteriousCircuitry, 5).
                AddTile(TileID.Anvils).
                Register();
            }
            else {
                CreateRecipe().
                AddRecipeGroup(RecipeGroupID.IronBar, 15).
                AddRecipeGroup(CWRCrafted.IronPickaxeGroup).
                AddTile(TileID.Anvils).
                Register();
            }
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

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

        public override void MouseOver(int i, int j) {
            Main.LocalPlayer.SetMouseOverByTile(ModContent.ItemType<MiningMachine>());
        }

        public override bool RightClick(int i, int j) {
            //只在交互客户端执行:打开勘探终端
            if (!TileProcessorLoader.AutoPositionGetTP<MiningMachineTP>(i, j, out var tp)) {
                return false;
            }
            tp.RightEvent();
            SoundEngine.PlaySound(CWRSound.ButtonZero with { Pitch = 0.2f, Volume = 0.5f });
            return true;
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch)
            => BaseMiningMachineTP.DrawMachineTile<MiningMachineTP>(i, j, spriteBatch, Type, 3);
    }

    internal class MiningMachineTP : BaseMiningMachineTP
    {
        public override int TargetTileID => ModContent.TileType<MiningMachineTile>();
        public override int TargetItem => ModContent.ItemType<MiningMachine>();
        public override bool ReceivedEnergy => true;
        public override float MaxUEValue => 800;

        public override int MachineTier => 1;
        public override float BasePickPower => 59f;
        public override int WorkInterval => 20;
        public override float YieldChance => 0.10f;
        public override int WorkConsumeUE => 5;
        public override int ModuleSlotCount => 3;
        public override int ScanWidth => 40;
        public override int ScanDepth => 40;
        public override LocalizedText DontWorkText => MiningMachine.DontWork;
    }
}
