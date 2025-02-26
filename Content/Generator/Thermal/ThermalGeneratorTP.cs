using InnoVault.UIHandles;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Generator.Thermal
{
    internal class ThermalGeneratorTP : BaseGeneratorTP
    {
        public override int TargetTileID => ModContent.TileType<ThermalGeneratorTile>();
        internal int frame;
        internal ThermalData ThermalData => GeneratorData as ThermalData;
        public override GeneratorData GetGeneratorDataInds() => new ThermalData();
        public override void GeneratorUpdate() {
            if (PosInWorld.Distance(Main.LocalPlayer.Center) > maxFindMode) {
                if (!VaultUtils.isServer && GeneratorUI?.GeneratorTP == this 
                    && UIHandleLoader.GetUIHandleOfType<ThermalGeneratorUI>().IsActive) {
                    UIHandleLoader.GetUIHandleOfType<ThermalGeneratorUI>().IsActive = false;
                    SoundEngine.PlaySound(SoundID.MenuClose);
                }
            }

            if (ThermalData.FuelItem != null && ThermalData.FuelItem.type != ItemID.None && ThermalData.Temperature <= 900) {
                if (++ThermalData.ChargeCool > 6) {
                    ThermalData.FuelItem.stack--;

                    if (FuelItems.FuelItemToCombustion.ContainsKey(ThermalData.FuelItem.type)) {
                        ThermalData.Temperature += FuelItems.FuelItemToCombustion[ThermalData.FuelItem.type];
                    }
                    
                    if (ThermalData.Temperature > 1000) {
                        ThermalData.Temperature = 1000;
                    }
                    if (ThermalData.FuelItem.stack <= 0) {
                        ThermalData.FuelItem.TurnToAir();
                    }
                    ThermalData.ChargeCool = 0;
                }
            }

            if (ThermalData.Temperature > 0 && GeneratorData.UEvalue <= 6000) {
                ThermalData.Temperature--;
                ThermalData.UEvalue++;
            }

            if (ThermalData.Temperature > 0) {
                CWRUtils.ClockFrame(ref frame, 5, 7, 1);
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

            if (item.IsAir) {
                return;
            }

            if (!ThermalData.FuelItem.IsAir && !VaultUtils.isClient) {
                int type = Item.NewItem(new EntitySource_WorldEvent(), HitBox, ThermalData.FuelItem.Clone());
                if (!VaultUtils.isSinglePlayer) {
                    NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type, 0f, 0f, 0f, 0, 0, 0);
                }
            }

            if (FuelItems.FuelItemToCombustion.TryGetValue(item.type, out _)) {
                ThermalData.FuelItem = item.Clone();
                item.TurnToAir();
                SoundEngine.PlaySound(SoundID.Grab);
            }
        }
    }
}
