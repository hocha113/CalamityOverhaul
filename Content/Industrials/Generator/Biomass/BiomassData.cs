using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using System.IO;

namespace CalamityOverhaul.Content.Industrials.Generator.Biomass
{
    /// <summary>
    /// 生物质发电机数据:平功率模型,烧着就按固定功率出电,没有温度曲线。
    /// 一份燃料的总出电 = 热值对应的燃烧时长 × 每tick功率
    /// </summary>
    internal class BiomassData : MachineData
    {
        internal float MaxUEValue;
        /// <summary>剩余燃烧时间(tick)</summary>
        internal int BurnTimeRemaining;
        /// <summary>总燃烧时间(tick),进度条用</summary>
        internal int BurnTimeMax;
        /// <summary>燃烧时的固定功率(UE/tick)</summary>
        internal float PowerPerTick = 0.6f;

        internal Item FuelItem = new Item();

        /// <summary>正在燃烧</summary>
        internal bool IsBurning => BurnTimeRemaining > 0;

        /// <summary>燃烧进度 0~1</summary>
        internal float BurnProgress => BurnTimeMax > 0 ? 1f - (float)BurnTimeRemaining / BurnTimeMax : 0f;

        public override void SendData(ModPacket data) {
            base.SendData(data);
            data.Write(BurnTimeRemaining);
            data.Write(BurnTimeMax);
            data.Write(FuelItem.type);
            data.Write(FuelItem.stack);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            BurnTimeRemaining = reader.ReadInt32();
            BurnTimeMax = reader.ReadInt32();
            int itemID = reader.ReadInt32();
            int stack = reader.ReadInt32();
            if (itemID >= 0 && itemID < ItemLoader.ItemCount) {
                FuelItem = new Item(itemID);
                FuelItem.stack = stack;
            }
        }

        public override void SaveData(TagCompound tag) {
            base.SaveData(tag);
            tag["BiomassData_BurnTimeRemaining"] = BurnTimeRemaining;
            tag["BiomassData_BurnTimeMax"] = BurnTimeMax;
            tag["BiomassData_FuelItem"] = FuelItem;
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);
            BurnTimeRemaining = tag.TryGet("BiomassData_BurnTimeRemaining", out int btr) ? btr : 0;
            BurnTimeMax = tag.TryGet("BiomassData_BurnTimeMax", out int btm) ? btm : 0;
            FuelItem = tag.TryGet("BiomassData_FuelItem", out Item fi) ? fi : new Item();
        }
    }
}
