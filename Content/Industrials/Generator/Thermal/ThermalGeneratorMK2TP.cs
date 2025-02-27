using InnoVault.UIHandles;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.Industrials.Generator.Thermal
{
    internal class ThermalGeneratorMK2TP : BaseGeneratorTP
    {
        public override int TargetTileID => ModContent.TileType<ThermalGeneratorMK2Tile>();
        internal ThermalData ThermalData => GeneratorData as ThermalData;
        public override GeneratorData GetGeneratorDataInds() {
            var inds = new ThermalData();
            inds.MaxChargeCool = 4;
            inds.MaxTemperature = 2000;
            inds.MaxUEValue = 10000;
            return inds;
        }
        public override void GeneratorUpdate() {
            if (PosInWorld.Distance(Main.LocalPlayer.Center) > maxFindMode) {
                if (!VaultUtils.isServer && GeneratorUI?.GeneratorTP == this
                    && UIHandleLoader.GetUIHandleOfType<ThermalGeneratorUI>().IsActive) {
                    UIHandleLoader.GetUIHandleOfType<ThermalGeneratorUI>().IsActive = false;
                    SoundEngine.PlaySound(SoundID.MenuClose);
                }
            }

            if (ThermalData.FuelItem != null && ThermalData.FuelItem.type != ItemID.None && ThermalData.Temperature <= ThermalData.MaxTemperature - 250) {
                if (++ThermalData.ChargeCool > ThermalData.MaxChargeCool) {
                    ThermalData.FuelItem.stack--;

                    if (FuelItems.FuelItemToCombustion.ContainsKey(ThermalData.FuelItem.type)) {
                        ThermalData.Temperature += FuelItems.FuelItemToCombustion[ThermalData.FuelItem.type];
                    }

                    if (ThermalData.Temperature > ThermalData.MaxTemperature) {
                        ThermalData.Temperature = ThermalData.MaxTemperature;
                    }
                    if (ThermalData.FuelItem.stack <= 0) {
                        ThermalData.FuelItem.TurnToAir();
                    }
                    ThermalData.ChargeCool = 0;
                }
            }

            if (ThermalData.Temperature > 0 && ThermalData.UEvalue <= ThermalData.MaxUEValue) {
                ThermalData.Temperature--;
                ThermalData.UEvalue += 2;
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
                if (!ThermalData.FuelItem.IsAir && !VaultUtils.isClient) {
                    int type = Item.NewItem(new EntitySource_WorldEvent(), HitBox, ThermalData.FuelItem.Clone());
                    if (!VaultUtils.isSinglePlayer) {
                        NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type, 0f, 0f, 0f, 0, 0, 0);
                    }
                }
                ThermalData.FuelItem.TurnToAir();
                SoundEngine.PlaySound(SoundID.Grab);
                return;
            }

            if (item.IsAir || !FuelItems.FuelItemToCombustion.ContainsKey(item.type)) {
                return;
            }

            if (!ThermalData.FuelItem.IsAir && !VaultUtils.isClient) {
                int type = Item.NewItem(new EntitySource_WorldEvent(), HitBox, ThermalData.FuelItem.Clone());
                if (!VaultUtils.isSinglePlayer) {
                    NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type, 0f, 0f, 0f, 0, 0, 0);
                }
                ThermalData.FuelItem.TurnToAir();
            }

            if (FuelItems.FuelItemToCombustion.TryGetValue(item.type, out _)) {
                ThermalData.FuelItem = item.Clone();
                item.TurnToAir();
                SoundEngine.PlaySound(SoundID.Grab);
            }
        }
    }
}
