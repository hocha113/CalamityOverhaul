using InnoVault.TileProcessors;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.Generator.Thermal
{
    internal class ThermalGenerator : ModItem
    {
        public override string Texture => CWRConstant.Asset + "Generator/ThermalGenerator";
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
            Item.value = Item.buyPrice(0, 0, 60, 20);
            Item.rare = ItemRarityID.LightRed;
            Item.createTile = ModContent.TileType<ThermalGeneratorTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 1000;
        }

        public override void AddRecipes() {
            CreateRecipe().
                AddIngredient(ItemID.Furnace).
                AddRecipeGroup(RecipeGroupID.IronBar, 5).
                AddRecipeGroup(CWRRecipes.TinBarGroup, 5).
                AddIngredient(CWRID.Item_DubiousPlating, 10).
                AddIngredient(CWRID.Item_MysteriousCircuitry, 10).
                AddTile(TileID.Anvils).
                Register();
        }
    }

    internal class ThermalGeneratorTile : BaseGeneratorTile
    {
        public override string Texture => CWRConstant.Asset + "Generator/ThermalGeneratorTile";
        public override int GeneratorTP => TileProcessorLoader.GetModuleID<ThermalGeneratorTP>();
        public override int GeneratorUI => UIHandleLoader.GetUIHandleID<ThermalGeneratorUI>();
        public override int TargetItem => ModContent.ItemType<ThermalGenerator>();
        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            Main.tileSolidTop[Type] = true;
            AddMapEntry(new Color(67, 72, 81), VaultUtils.GetLocalizedItemName<ThermalGenerator>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.Width = 2;
            TileObjectData.newTile.Height = 2;
            TileObjectData.newTile.Origin = new Point16(1, 1);
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile
                | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateHeights = [16, 16];
            TileObjectData.newTile.LavaDeath = false;

            TileObjectData.addTile(Type);
        }
        public override void MouseOver(int i, int j) {
            Item item = Main.LocalPlayer.GetItem();
            int type = TargetItem;
            if (FuelItems.FuelItemToCombustion.ContainsKey(item.type)) {
                type = item.type;
            }
            Main.LocalPlayer.SetMouseOverByTile(type);
        }
        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out ThermalGeneratorTP thermal)) {
                return false;
            }

            Tile t = Main.tile[i, j];
            int frameXPos = t.TileFrameX;
            int frameYPos = t.TileFrameY;
            frameYPos += thermal.frame * 2 * 18;
            Texture2D tex = TextureAssets.Tile[Type].Value;
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 drawOffset = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y) + offset;
            Color drawColor = Lighting.GetColor(i, j);

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

    internal class ThermalGeneratorTP : BaseGeneratorTP
    {
        public override int TargetTileID => ModContent.TileType<ThermalGeneratorTile>();
        internal int frame;
        internal ThermalData ThermalData => MachineData as ThermalData;
        public override float MaxUEValue => 1000;
        public int GeneratingSpeed = 1;
        public int MaxFrame = 4;
        public override int TargetItem => ModContent.ItemType<ThermalGenerator>();
        public override MachineData GetGeneratorDataInds() {
            var inds = new ThermalData();
            inds.MaxChargeCool = 6;
            inds.MaxTemperature = 600;
            inds.MaxUEValue = MaxUEValue;
            return inds;
        }

        internal void HandlerItem() {
            SoundEngine.PlaySound(SoundID.Grab);
            if (ThermalData.FuelItem.type == ItemID.None) {
                ThermalData.FuelItem = Main.mouseItem.Clone();
                Main.mouseItem.TurnToAir();
            }
            else if (Main.mouseItem.IsAir) {
                Main.mouseItem = ThermalData.FuelItem.Clone();
                ThermalData.FuelItem.TurnToAir();
            }
            else if (Main.mouseItem.type == ThermalData.FuelItem.type) {
                ThermalData.FuelItem.stack += Main.mouseItem.stack;
                Main.mouseItem.TurnToAir();
            }
            else if (Main.mouseItem.type != ItemID.None) {
                ThermalData.FuelItem = Main.mouseItem.Clone();
                Main.mouseItem = ThermalData.FuelItem.Clone();
            }
            SendData();
        }

        public bool CanUseFuel(out int value) {
            value = 0;
            bool reset = false;
            if (ThermalData.FuelItem == null || ThermalData.FuelItem.type == ItemID.None) {
                return false;
            }
            if (FuelItems.FuelItemToCombustion.TryGetValue(ThermalData.FuelItem.type, out value)) {
                reset = true;
            }
            if (ThermalData.Temperature > ThermalData.MaxTemperature - value) {
                reset = false;
            }
            if (ThermalData.Temperature <= 0) {
                reset = true;
            }
            if (++ThermalData.ChargeCool < ThermalData.MaxChargeCool) {
                reset = false;
            }
            return reset;
        }

        public sealed override void GeneratorUpdate() {
            if (PosInWorld.Distance(Main.LocalPlayer.Center) > MaxFindMode) {
                if (!VaultUtils.isServer && GeneratorUI?.GeneratorTP == this
                    && UIHandleLoader.GetUIHandleOfType<ThermalGeneratorUI>().IsActive) {
                    UIHandleLoader.GetUIHandleOfType<ThermalGeneratorUI>().IsActive = false;
                    SoundEngine.PlaySound(SoundID.MenuTick);
                }
            }
            else {
                Main.LocalPlayer.CWR().ThermalGenerationActiveTime = 2;
            }

            if (CanUseFuel(out int value)) {
                ThermalData.FuelItem.stack--;
                ThermalData.TemperatureTransfer += value;
                ThermalData.MaxTemperatureTransfer = ThermalData.TemperatureTransfer;
                FuelItems.OnAfterFlaming(ThermalData.FuelItem.type, this);
                if (ThermalData.Temperature > ThermalData.MaxTemperature) {
                    ThermalData.Temperature = ThermalData.MaxTemperature;
                }
                if (ThermalData.FuelItem.stack <= 0) {
                    ThermalData.FuelItem.TurnToAir();
                }

                ThermalData.ChargeCool = 0;
            }

            for (int i = 0; i < 6; i++) {
                if (ThermalData.TemperatureTransfer > 0 && ThermalData.Temperature < ThermalData.MaxTemperature) {
                    ThermalData.Temperature++;
                    ThermalData.TemperatureTransfer--;
                }
            }

            UpdateThermal();
        }

        public virtual void UpdateThermal() {
            if (ThermalData.Temperature > 0) {
                if (ThermalData.UEvalue < ThermalData.MaxUEValue - GeneratingSpeed) {
                    ThermalData.Temperature--;
                    ThermalData.UEvalue += GeneratingSpeed;
                }
                VaultUtils.ClockFrame(ref frame, 5, MaxFrame, 1);
            }
            else {
                frame = 0;
            }
            ThermalData.Temperature = MathHelper.Clamp(ThermalData.Temperature, 0, ThermalData.MaxTemperature);
        }

        public override void GeneratorKill() {
            if (!VaultUtils.isClient) {
                int type = Item.NewItem(new EntitySource_WorldEvent(), HitBox, ThermalData.FuelItem.Clone());
                if (!VaultUtils.isSinglePlayer) {
                    NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type, 0f, 0f, 0f, 0, 0, 0);
                }
            }

            ThermalData.FuelItem.TurnToAir();

            if (!VaultUtils.isServer && GeneratorUI?.GeneratorTP == this
                    && UIHandleLoader.GetUIHandleOfType<ThermalGeneratorUI>().IsActive) {
                UIHandleLoader.GetUIHandleOfType<ThermalGeneratorUI>().IsActive = false;
            }
        }

        public override void RightClickByTile(bool newTP) {
            Item item = Main.LocalPlayer.GetItem();

            if (Main.keyState.PressingShift()) {
                if (!ThermalData.FuelItem.IsAir) {//这里代码不会在服务端运行
                    Main.LocalPlayer.QuickSpawnItem(new EntitySource_WorldEvent(), ThermalData.FuelItem, ThermalData.FuelItem.stack);
                    ThermalData.FuelItem.TurnToAir();
                }
                SendData();
                SoundEngine.PlaySound(SoundID.Grab);
                return;
            }

            if (item.IsAir || !FuelItems.FuelItemToCombustion.ContainsKey(item.type)) {
                return;
            }

            if (!ThermalData.FuelItem.IsAir) {
                Main.LocalPlayer.QuickSpawnItem(new EntitySource_WorldEvent(), ThermalData.FuelItem, ThermalData.FuelItem.stack);
                ThermalData.FuelItem.TurnToAir();
            }

            if (FuelItems.FuelItemToCombustion.TryGetValue(item.type, out _)) {
                ThermalData.FuelItem = item.Clone();
                item.TurnToAir();
                SoundEngine.PlaySound(SoundID.Grab);
            }

            SendData();
        }

        public override void FrontDraw(SpriteBatch spriteBatch) => DrawChargeBar();
    }
}
