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

        /// <summary>剩余燃烧时间(tick)</summary>
        internal int BurnTimeRemaining;
        /// <summary>总燃烧时间(tick)，进度条用</summary>
        internal int BurnTimeMax;
        /// <summary>每tick热量</summary>
        internal float HeatPerTick;

        /// <summary>比例散热，每tick = MinDissipation + Temperature * DissipationRate</summary>
        internal float DissipationRate = 0.0015f;
        /// <summary>固定散热量/tick</summary>
        internal float MinDissipation = 0.03f;
        /// <summary>每1UE耗温</summary>
        internal float HeatCostPerUE = 0.08f;
        /// <summary>最优工作温度，效率曲线饱和点</summary>
        internal float OptimalTemperature = 420f;
        /// <summary>最大发电功率 UE/tick</summary>
        internal float MaxPowerPerTick = 1.5f;
        /// <summary>低于此温度不发电</summary>
        internal float MinOperatingTemperature = 50f;

        internal Item FuelItem = new Item();

        /// <summary>正在燃烧</summary>
        internal bool IsBurning => BurnTimeRemaining > 0;

        /// <summary>燃烧进度 0~1</summary>
        internal float BurnProgress => BurnTimeMax > 0 ? 1f - (float)BurnTimeRemaining / BurnTimeMax : 0f;

        /// <summary>温度效率 η(T)=1-e^(-2·T/T_opt)</summary>
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
            //旧档 tag 名 ThermalData_FEvalue
            Temperature = tag.TryGet("ThermalData_Temperature", out float temp) ? temp
                        : tag.TryGet("ThermalData_FEvalue", out float oldTemp) ? oldTemp : 0f;
            BurnTimeRemaining = tag.TryGet("ThermalData_BurnTimeRemaining", out int btr) ? btr : 0;
            BurnTimeMax = tag.TryGet("ThermalData_BurnTimeMax", out int btm) ? btm : 0;
            HeatPerTick = tag.TryGet("ThermalData_HeatPerTick", out float hpt) ? hpt : 0f;
            FuelItem = tag.TryGet("ThermalData_FuelItem", out Item fi) ? fi : new Item();
        }
    }
}
