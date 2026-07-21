using CalamityOverhaul.Common;
using System.IO;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Incinerators
{
    /// <summary>焚烧炉槽位与进度</summary>
    internal class IncineratorData : MachineData
    {
        /// <summary>材料槽</summary>
        internal Item InputItem = new Item();
        /// <summary>成品槽</summary>
        internal Item OutputItem = new Item();
        /// <summary>进度0..Max</summary>
        internal int SmeltingProgress;
        /// <summary>完成所需进度</summary>
        internal int MaxSmeltingProgress = 120;
        /// <summary>单次耗电</summary>
        internal float UEPerTick = 0.5f;
        /// <summary>电量上限</summary>
        internal float MaxUE = 500;
        /// <summary>温度(视觉)</summary>
        internal float Temperature;
        /// <summary>温度上限</summary>
        internal float MaxTemperature = 100;
        internal bool IsWorking => SmeltingProgress > 0 && UEvalue >= UEPerTick;

        public override void SendData(ModPacket data) {
            base.SendData(data);
            data.Write(SmeltingProgress);
            data.Write(Temperature);
            ItemIO.Send(InputItem ?? new Item(), data, true, true);
            ItemIO.Send(OutputItem ?? new Item(), data, true, true);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            SmeltingProgress = reader.ReadInt32();
            Temperature = reader.ReadSingle();
            InputItem = ItemIO.Receive(reader, true, true);
            OutputItem = ItemIO.Receive(reader, true, true);
        }

        public override void SaveData(TagCompound tag) {
            base.SaveData(tag);
            tag["Incinerator_SmeltingProgress"] = SmeltingProgress;
            tag["Incinerator_Temperature"] = Temperature;
            if (InputItem != null && !InputItem.IsAir) {
                tag["Incinerator_InputItem"] = ItemIO.Save(InputItem);
            }
            if (OutputItem != null && !OutputItem.IsAir) {
                tag["Incinerator_OutputItem"] = ItemIO.Save(OutputItem);
            }
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);
            if (!tag.TryGet("Incinerator_SmeltingProgress", out SmeltingProgress)) {
                SmeltingProgress = 0;
            }
            if (!tag.TryGet("Incinerator_Temperature", out Temperature)) {
                Temperature = 0;
            }
            InputItem = CWRSaveData.LoadItemFromTag(tag, "Incinerator_InputItem", nameof(IncineratorData));
            OutputItem = CWRSaveData.LoadItemFromTag(tag, "Incinerator_OutputItem", nameof(IncineratorData));
        }
    }
}
