using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    /// <summary>雪炮跨Use状态，ModPlayer不入档</summary>
    internal class SnowCannonPlayer : ModPlayer
    {
        //冰河时代 CrystalDimming
        /// 左键炮击就绪时间戳
        public uint CrystalShellReadyTime;
        /// 右键冰河波就绪时间戳
        public uint CrystalWaveReadyTime;

        //雪蝰 SnowQuay
        /// 鼓风弹药节流计数，每3次吹雪耗1雪球
        public int SnowQuayStreamThrottle;

        //雪蝰MK2 SnowQuayMK2
        /// 左键点射就绪时间戳
        public uint MK2BurstReadyTime;
        /// 右键霰射就绪时间戳
        public uint MK2ScatterReadyTime;

        //万象霜天 UniversalFrost
        /// 霜穹蓄能 0~MaxCharge
        public float FrostAuroraCharge;
        /// 蓄满提示只播一次的标记
        public bool FrostChargeCueDone;
        /// 霜辉弹节流计数，每2发耗1雪球
        public int FrostGlimmerThrottle;

        //凛冬神性 DarkFrostSolstice
        /// 当前射击间隔(tick)，扫射中逐渐下降提速
        public int SolsticeFireRate = 20;
        /// 射速攀升的节拍计数
        public int SolsticeFireIndex;
        /// 超频积累计数，每20发触发一次轰鸣蓄势
        public int SolsticeFireIndex2;
    }
}
