using System.IO;
using Terraria;
using Terraria.ModLoader.IO;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.Generator.Thermal
{
    internal class ThermalData : MachineData
    {
        internal int MaxChargeCool;
        internal float MaxTemperature;
        internal float MaxUEValue;
        internal int ChargeCool;
        internal float Temperature;
        internal Item FuelItem = new Item();
        public override void SendData(ModPacket data) {
            base.SendData(data);
            data.Write(ChargeCool);
            data.Write(Temperature);
            data.Write(FuelItem.type);
            data.Write(FuelItem.stack);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            ChargeCool = reader.ReadInt32();
            Temperature = reader.ReadSingle();
            int itemID = reader.ReadInt32();
            int stact = reader.ReadInt32();
            if (itemID > 0 && itemID < ItemLoader.ItemCount) {
                FuelItem = new Item(itemID);
                FuelItem.stack = stact;
            }
        }

        public override void SaveData(TagCompound tag) {
            base.SaveData(tag);
            tag["ThermalData_ChargeCool"] = ChargeCool;
            tag["ThermalData_FEvalue"] = Temperature;
            tag["ThermalData_FuelItem"] = FuelItem;
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);
            if (!tag.TryGet("ThermalData_ChargeCool", out ChargeCool)) {
                ChargeCool = 0;
            }
            if (!tag.TryGet("ThermalData_FEvalue", out Temperature)) {
                Temperature = 0;
            }
            if (!tag.TryGet("ThermalData_FuelItem", out FuelItem)) {
                FuelItem = new Item();
            }
        }
    }
}
