using System;
using System.IO;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.Generator.Thermal
{
    internal class ThermalData : MachineData
    {
        internal int MaxChargeCool;
        internal float MaxTemperature;
        internal float MaxUEValue;
        internal int ChargeCool;
        internal float Temperature;

        /// <summary>
        /// 当前燃料剩余燃烧时间（tick）
        /// </summary>
        internal int BurnTimeRemaining;
        /// <summary>
        /// 当前燃料总燃烧时间（tick），计算进度条
        /// </summary>
        internal int BurnTimeMax;
        /// <summary>
        /// 每tick释放的热量，由燃料热值和持续时间决定
        /// </summary>
        internal float HeatPerTick;

        /// <summary>
        /// 百分比散热系数，每tick散热 = MinDissipation + Temperature * DissipationRate
        /// </summary>
        internal float DissipationRate = 0.0015f;
        /// <summary>
        /// 最小固定散热量（每tick）
        /// </summary>
        internal float MinDissipation = 0.03f;
        /// <summary>
        /// 每产生1UE消耗的温度（值越低，发电对温度的消耗越小）
        /// </summary>
        internal float HeatCostPerUE = 0.08f;
        /// <summary>
        /// 最优工作温度，效率曲线在此温度附近趋于饱和
        /// </summary>
        internal float OptimalTemperature = 420f;
        /// <summary>
        /// 最大发电功率（UE/tick），实际输出 = MaxPowerPerTick * 效率
        /// </summary>
        internal float MaxPowerPerTick = 1.5f;
        /// <summary>
        /// 最低运行温度，低于此温度不发电（需要预热）
        /// </summary>
        internal float MinOperatingTemperature = 50f;

        internal Item FuelItem = new Item();

        /// <summary>
        /// 当前是否正在燃烧燃料
        /// </summary>
        internal bool IsBurning => BurnTimeRemaining > 0;

        /// <summary>
        /// 燃烧进度 0~1（0=刚开始燃烧, 1=燃尽）
        /// </summary>
        internal float BurnProgress => BurnTimeMax > 0 ? 1f - (float)BurnTimeRemaining / BurnTimeMax : 0f;

        /// <summary>
        /// 当前温度效率，基于指数衰减逼近曲线:
        /// η(T) = 1 - e^(-2 * T / T_optimal)
        /// 低温时效率快速上升，接近最优温度时趋于饱和
        /// </summary>
        internal float CurrentEfficiency {
            get {
                if (OptimalTemperature <= 0 || Temperature < MinOperatingTemperature) return 0f;
                float ratio = Temperature / OptimalTemperature;
                return MathHelper.Clamp(1f - (float)Math.Exp(-2f * ratio), 0f, 1f);
            }
        }

        public override void SendData(ModPacket data) {
            base.SendData(data);
            data.Write(ChargeCool);
            data.Write(Temperature);
            data.Write(BurnTimeRemaining);
            data.Write(BurnTimeMax);
            data.Write(HeatPerTick);
            data.Write(FuelItem.type);
            data.Write(FuelItem.stack);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            ChargeCool = reader.ReadInt32();
            Temperature = reader.ReadSingle();
            BurnTimeRemaining = reader.ReadInt32();
            BurnTimeMax = reader.ReadInt32();
            HeatPerTick = reader.ReadSingle();
            int itemID = reader.ReadInt32();
            int stack = reader.ReadInt32();
            if (itemID >= 0 && itemID < ItemLoader.ItemCount) {
                FuelItem = new Item(itemID);
                FuelItem.stack = stack;
            }
        }

        public override void SaveData(TagCompound tag) {
            base.SaveData(tag);
            tag["ThermalData_ChargeCool"] = ChargeCool;
            tag["ThermalData_Temperature"] = Temperature;
            tag["ThermalData_BurnTimeRemaining"] = BurnTimeRemaining;
            tag["ThermalData_BurnTimeMax"] = BurnTimeMax;
            tag["ThermalData_HeatPerTick"] = HeatPerTick;
            tag["ThermalData_FuelItem"] = FuelItem;
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);
            ChargeCool = tag.TryGet("ThermalData_ChargeCool", out int cc) ? cc : 0;
            //兼容旧存档的tag名"ThermalData_FEvalue"
            Temperature = tag.TryGet("ThermalData_Temperature", out float temp) ? temp
                        : tag.TryGet("ThermalData_FEvalue", out float oldTemp) ? oldTemp : 0f;
            BurnTimeRemaining = tag.TryGet("ThermalData_BurnTimeRemaining", out int btr) ? btr : 0;
            BurnTimeMax = tag.TryGet("ThermalData_BurnTimeMax", out int btm) ? btm : 0;
            HeatPerTick = tag.TryGet("ThermalData_HeatPerTick", out float hpt) ? hpt : 0f;
            FuelItem = tag.TryGet("ThermalData_FuelItem", out Item fi) ? fi : new Item();
        }
    }
}
