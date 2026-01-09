using System.IO;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Incinerators
{
    /// <summary>
    /// 焚烧炉数据，用于管理输入物和焚烧后的输出物
    /// </summary>
    internal class IncineratorData : MachineData
    {
        /// <summary>
        /// 待焚烧的输入物品
        /// </summary>
        internal Item InputItem = new Item();
        /// <summary>
        /// 焚烧后的输出物品
        /// </summary>
        internal Item OutputItem = new Item();
        /// <summary>
        /// 当前焚烧进度(0到MaxSmeltingProgress)
        /// </summary>
        internal int SmeltingProgress;
        /// <summary>
        /// 完成焚烧所需的最大进度
        /// </summary>
        internal int MaxSmeltingProgress = 120;
        /// <summary>
        /// 每次焚烧消耗的电量
        /// </summary>
        internal float UEPerTick = 0.5f;
        /// <summary>
        /// 最大电量存储
        /// </summary>
        internal float MaxUE = 500;
        /// <summary>
        /// 当前温度(用于视觉效果)
        /// </summary>
        internal float Temperature;
        /// <summary>
        /// 最大温度
        /// </summary>
        internal float MaxTemperature = 100;
        /// <summary>
        /// 是否正在工作
        /// </summary>
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
            if (tag.ContainsKey("Incinerator_InputItem")) {
                InputItem = ItemIO.Load(tag.GetCompound("Incinerator_InputItem"));
            }
            else {
                InputItem = new Item();
            }
            if (tag.ContainsKey("Incinerator_OutputItem")) {
                OutputItem = ItemIO.Load(tag.GetCompound("Incinerator_OutputItem"));
            }
            else {
                OutputItem = new Item();
            }
        }
    }
}
