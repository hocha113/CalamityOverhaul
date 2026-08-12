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
            if (CWRID.DubiousCircuitryAvailable) {
                CreateRecipe().
                AddIngredient<MiningMachine>().
                AddRecipeGroup(CWRCrafted.AdamantiteBarGroup, 25).
                AddIngredient(CWRID.Item_DubiousPlating, 15).
                AddIngredient(CWRID.Item_MysteriousCircuitry, 15).
                AddTile(TileID.MythrilAnvil).
                Register();
            }
            else {
                CreateRecipe().
                AddIngredient<MiningMachine>().
                AddRecipeGroup(CWRCrafted.AdamantiteBarGroup, 25).
                AddTile(TileID.MythrilAnvil).
                Register();
            }
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

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

        public override void MouseOver(int i, int j) {
            Main.LocalPlayer.SetMouseOverByTile(ModContent.ItemType<MiningMachineMk2>());
        }

        public override bool RightClick(int i, int j) {
            //只在交互客户端执行:打开勘探终端
            if (!TileProcessorLoader.AutoPositionGetTP<MiningMachineMk2TP>(i, j, out var tp)) {
                return false;
            }
            tp.RightEvent();
            SoundEngine.PlaySound(CWRSound.ButtonZero with { Pitch = 0.2f, Volume = 0.5f });
            return true;
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch)
            => BaseMiningMachineTP.DrawMachineTile<MiningMachineMk2TP>(i, j, spriteBatch, Type, 10);
    }

    internal class MiningMachineMk2TP : BaseMiningMachineTP
    {
        public override int TargetTileID => ModContent.TileType<MiningMachineMk2Tile>();
        public override int TargetItem => ModContent.ItemType<MiningMachineMk2>();
        public override bool ReceivedEnergy => true;
        public override float MaxUEValue => 2400 * ModuleRack.StorageMult;

        public override int MachineTier => 2;
        public override float BasePickPower => 180f;
        public override int WorkInterval => 24;
        public override float YieldChance => 0.16f;
        public override int WorkConsumeUE => 8;
        public override int ModuleSlotCount => 6;
        public override int ScanWidth => 80;
        public override int ScanDepth => 64;
        public override int FrameInterval => 4;
        public override int FrameMax => 5;
        public override int ShakeAmp => 1;
        public override int DustDenominator => 4;
        public override Vector2 ExcavateOffset => new(92, 140);
        public override float WorkPitch => -0.6f;
        public override float WorkVolume => 0.7f;
        public override LocalizedText DontWorkText => MiningMachineMk2.DontWork;
    }
}
