using Terraria;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.QueenBee
{
    /// <summary>
    /// 蜂涡信标：蜂后残酷遗物。<br/>
    /// 命中叠蜂标，叠满把目标卷进蜂涡(高频蜂噬+减速，续时/转移)；
    /// 静立时蜂群回巢结蜜蜡甲(吸伤护盾，移动后保留数秒)。<br/>
    /// 状态全在 <see cref="SwarmVortexPlayer"/>，此处只点亮装备旗
    /// </summary>
    internal class SwarmVortexBeacon : BaseBrutalRelic
    {
        //==================== 调参表(报告数值表同源) ====================
        /// <summary>蜂标叠满层数</summary>
        internal const int MarkMax = 8;
        /// <summary>无命中后蜂标保留帧数</summary>
        internal const int MarkFadeTicks = 240;
        /// <summary>蜂涡基础持续(5s)</summary>
        internal const int VortexBaseTicks = 300;
        /// <summary>蜂涡续时上限(10s)</summary>
        internal const int VortexMaxTicks = 600;
        /// <summary>期间每次命中续时</summary>
        internal const int VortexExtendPerHit = 30;
        /// <summary>蜂噬单跳基础伤害(吃全伤害加成)</summary>
        internal const int VortexHitDamage = 16;
        /// <summary>蜂噬间隔(帧)，约每秒12跳</summary>
        internal const int VortexHitInterval = 5;
        /// <summary>蜂巢背包(strongBees)协同倍率</summary>
        internal const float HivePackMult = 1.25f;
        /// <summary>蜜蜡甲吸收池上限</summary>
        internal const float WaxMax = 60f;
        /// <summary>静立每帧充蜡(75帧充满)</summary>
        internal const float WaxChargePerTick = 0.8f;
        /// <summary>移动后蜡甲保留帧数(5s)</summary>
        internal const int WaxRetainTicks = 300;
        /// <summary>保留期过后每帧融蜡</summary>
        internal const float WaxMeltPerTick = 2f;
        /// <summary>碎甲后重结晶锁(3s)</summary>
        internal const int WaxRebuildLock = 180;

        public override void SetDefaults() {
            base.SetDefaults();
            //同期参照：蜂后掉落物卖价约0.6~1金，此处按3~5倍档
            Item.value = Item.buyPrice(0, 18, 0, 0);
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            //蜡甲是功能读数，不受时装隐藏开关影响
            player.GetModPlayer<SwarmVortexPlayer>().Equipped = true;
        }
    }
}
