using System.IO;
using Terraria;
using Terraria.ModLoader.IO;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.Generator.Thermal
{
    internal class ThermalData : GeneratorData
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
            ItemIO.Send(FuelItem, data);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            ChargeCool = reader.ReadInt32();
            Temperature = reader.ReadSingle();
            FuelItem = ItemIO.Receive(reader);
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
