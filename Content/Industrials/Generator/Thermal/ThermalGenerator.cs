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
            if (CWRID.DubiousCircuitryAvailable) {
                CreateRecipe().
                AddIngredient(ItemID.Furnace).
                AddRecipeGroup(RecipeGroupID.IronBar, 5).
                AddRecipeGroup(CWRCrafted.TinBarGroup, 5).
                AddIngredient(CWRID.Item_DubiousPlating, 10).
                AddIngredient(CWRID.Item_MysteriousCircuitry, 10).
                AddTile(TileID.Anvils).
                Register();
            }
            else {
                CreateRecipe().
                AddIngredient(ItemID.Furnace).
                AddRecipeGroup(RecipeGroupID.IronBar, 5).
                AddRecipeGroup(CWRCrafted.TinBarGroup, 5).
                AddTile(TileID.Anvils).
                Register();
            }
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
        public override int TargetItem => ModContent.ItemType<ThermalGenerator>();
        public override float MaxUEValue => 1000;
        internal int frame;
        internal ThermalData ThermalData => MachineData as ThermalData;
        public int MaxFrame = 4;

        public override MachineData GetGeneratorDataInds() {
            var data = new ThermalData();
            data.MaxChargeCool = 6;
            data.MaxTemperature = 600;
            data.MaxUEValue = MaxUEValue;
            data.OptimalTemperature = 420f;
            data.MaxPowerPerTick = 1.5f;
            data.DissipationRate = 0.0015f;
            data.MinDissipation = 0.03f;
            data.HeatCostPerUE = 0.08f;
            data.MinOperatingTemperature = 50f;
            return data;
        }

        /// <summary>UI燃料槽放入/取出/交换，含类型校验</summary>
        internal void HandlerItem() {
            Item mouseItem = Main.mouseItem;
            bool mouseHasFuel = !mouseItem.IsAir && FuelItems.FuelItemToCombustion.ContainsKey(mouseItem.type);

            if (ThermalData.FuelItem.IsAir) {
                //空槽只收合法燃料
                if (mouseHasFuel) {
                    ThermalData.FuelItem = mouseItem.Clone();
                    mouseItem.TurnToAir();
                    SoundEngine.PlaySound(SoundID.Grab);
                }
            }
            else if (mouseItem.IsAir) {
                //取出
                Main.mouseItem = ThermalData.FuelItem.Clone();
                ThermalData.FuelItem.TurnToAir();
                SoundEngine.PlaySound(SoundID.Grab);
            }
            else if (mouseItem.type == ThermalData.FuelItem.type) {
                //同种堆叠
                int canAdd = ThermalData.FuelItem.maxStack - ThermalData.FuelItem.stack;
                int toAdd = canAdd < mouseItem.stack ? canAdd : mouseItem.stack;
                if (toAdd > 0) {
                    ThermalData.FuelItem.stack += toAdd;
                    mouseItem.stack -= toAdd;
                    if (mouseItem.stack <= 0) mouseItem.TurnToAir();
                }
                SoundEngine.PlaySound(SoundID.Grab);
            }
            else if (mouseHasFuel) {
                //异种交换
                Item temp = ThermalData.FuelItem.Clone();
                ThermalData.FuelItem = mouseItem.Clone();
                Main.mouseItem = temp;
                SoundEngine.PlaySound(SoundID.Grab);
            }

            SendData();
        }

        /// <summary>条件满足时消耗一份燃料开烧</summary>
        private void TryConsumeFuel() {
            if (ThermalData.FuelItem == null || ThermalData.FuelItem.IsAir) return;
            if (!FuelItems.FuelItemToCombustion.TryGetValue(ThermalData.FuelItem.type, out int combustion)) return;
            if (ThermalData.Temperature >= ThermalData.MaxTemperature * 0.95f) return;
            //电满不烧
            if (ThermalData.UEvalue >= ThermalData.MaxUEValue * 0.99f) return;

            int burnDuration = FuelItems.GetBurnDuration(combustion);
            float heatPerTick = FuelItems.GetHeatPerTick(combustion);

            ThermalData.BurnTimeRemaining = burnDuration;
            ThermalData.BurnTimeMax = burnDuration;
            ThermalData.HeatPerTick = heatPerTick;
            ThermalData.ChargeCool = 0;

            ThermalData.FuelItem.stack--;
            FuelItems.OnAfterFlaming(ThermalData.FuelItem.type, this);
            if (ThermalData.FuelItem.stack <= 0) {
                ThermalData.FuelItem.TurnToAir();
            }
        }

        public sealed override void GeneratorUpdate() {
            //UI与近距标记只对本地端有意义，专用服务器上LocalPlayer是占位实例
            if (!VaultUtils.isServer) {
                //超距关 UI
                if (PosInWorld.Distance(Main.LocalPlayer.Center) > MaxFindMode) {
                    if (GeneratorUI?.GeneratorTP == this
                        && UIHandleLoader.GetUIHandleOfType<ThermalGeneratorUI>().IsActive) {
                        UIHandleLoader.GetUIHandleOfType<ThermalGeneratorUI>().IsActive = false;
                        //并行阶段延后到主线程
                        Defer(() => SoundEngine.PlaySound(SoundID.MenuTick));
                    }
                }
                else {
                    //并行阶段延后到主线程
                    Defer(() => Main.LocalPlayer.CWR().ThermalGenerationActiveTime = 2);
                }
            }

            //燃烧产热
            if (ThermalData.IsBurning) {
                ThermalData.Temperature += ThermalData.HeatPerTick;
                ThermalData.BurnTimeRemaining--;
                if (ThermalData.Temperature > ThermalData.MaxTemperature) {
                    ThermalData.Temperature = ThermalData.MaxTemperature;
                }
            }

            //冷却满且未燃则续烧
            ThermalData.ChargeCool++;
            if (!ThermalData.IsBurning && ThermalData.ChargeCool >= ThermalData.MaxChargeCool) {
                TryConsumeFuel();
            }

            UpdateThermal();
        }

        /// <summary>按效率曲线发电 + 自然散热</summary>
        public virtual void UpdateThermal() {
            if (ThermalData.Temperature > 0) {
                float efficiency = ThermalData.CurrentEfficiency;
                float maxPower = ThermalData.MaxPowerPerTick * efficiency;

                if (ThermalData.UEvalue < ThermalData.MaxUEValue) {
                    float availableCapacity = ThermalData.MaxUEValue - ThermalData.UEvalue;
                    float actualPower = maxPower < availableCapacity ? maxPower : availableCapacity;
                    ThermalData.UEvalue += actualPower;
                    ThermalData.Temperature -= actualPower * ThermalData.HeatCostPerUE;
                }

                //固定 + 比例散热
                float dissipation = ThermalData.MinDissipation + ThermalData.Temperature * ThermalData.DissipationRate;
                ThermalData.Temperature -= dissipation;
                ThermalData.Temperature = MathHelper.Clamp(ThermalData.Temperature, 0, ThermalData.MaxTemperature);

                VaultUtils.ClockFrame(ref frame, 5, MaxFrame, 1);
            }
            else {
                frame = 0;
            }
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
                if (!ThermalData.FuelItem.IsAir) {
                    //直接入背包，MP下QuickSpawnItem是地面掉落会被队友截走
                    Main.LocalPlayer.GiveItem(new EntitySource_WorldEvent(), ThermalData.FuelItem.Clone());
                    ThermalData.FuelItem.TurnToAir();
                }
                SendData();
                SoundEngine.PlaySound(SoundID.Grab);
                return;
            }

            if (item.IsAir || !FuelItems.FuelItemToCombustion.ContainsKey(item.type)) {
                return;
            }

            //同种堆叠
            if (!ThermalData.FuelItem.IsAir && ThermalData.FuelItem.type == item.type) {
                int canAdd = ThermalData.FuelItem.maxStack - ThermalData.FuelItem.stack;
                int toAdd = canAdd < item.stack ? canAdd : item.stack;
                if (toAdd > 0) {
                    ThermalData.FuelItem.stack += toAdd;
                    item.stack -= toAdd;
                    if (item.stack <= 0) item.TurnToAir();
                }
            }
            //异种先吐再放(旧燃料直接回背包)
            else if (!ThermalData.FuelItem.IsAir) {
                Main.LocalPlayer.GiveItem(new EntitySource_WorldEvent(), ThermalData.FuelItem.Clone());
                ThermalData.FuelItem = item.Clone();
                item.TurnToAir();
            }
            else {
                ThermalData.FuelItem = item.Clone();
                item.TurnToAir();
            }

            SendData();
            SoundEngine.PlaySound(SoundID.Grab);
        }

        public override void FrontDraw(SpriteBatch spriteBatch) => DrawChargeBar();
    }
}
