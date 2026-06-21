using CalamityOverhaul.Content.Narrative.Data;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Shepel
{
    /// <summary>
    /// 响应式事件 bit 枚举，新成员保持 2 的幂
    /// </summary>
    [Flags]
    internal enum ShepelReactiveEvent
    {
        None = 0,
        BossDefeated = 1 << 0,
        CyberLevelUp = 1 << 1,
        RAMOverload = 1 << 2,
        BloodMoon = 1 << 3,
        SolarEclipse = 1 << 4,
        PlayerRespawned = 1 << 5,
        LowHealth = 1 << 6,
        RainStarted = 1 << 7,
        //后续新增事件在此追加，保持2的幂次
    }

    /// <summary>
    /// 响应式事件 bit 队列读写
    /// </summary>
    internal static class ShepelReactiveEvents
    {
        private static ShepelStoryData GetData(Player player)
            => player.GetModPlayer<StoryPlayer>().Get<ShepelStoryData>();

        /// <summary>
        /// 写入事件 bit
        /// </summary>
        public static void Enqueue(Player player, ShepelReactiveEvent evt) {
            GetData(player).ReactiveEventFlags |= (int)evt;
        }

        /// <summary>
        /// Boss 击败：写入 bit 并记录 NPC 类型
        /// </summary>
        public static void EnqueueBossDefeated(Player player, int npcType) {
            ShepelStoryData data = GetData(player);
            data.LastDefeatedBossNpcType = npcType;
            data.ReactiveEventFlags |= (int)ShepelReactiveEvent.BossDefeated;
        }

        /// <summary>
        /// 事件 bit 是否待播
        /// </summary>
        public static bool HasFlag(ShepelStoryData data, ShepelReactiveEvent evt)
            => (data.ReactiveEventFlags & (int)evt) != 0;

        /// <summary>
        /// 清除事件 bit，Build 时调用
        /// </summary>
        public static void ClearFlag(ShepelStoryData data, ShepelReactiveEvent evt)
            => data.ReactiveEventFlags &= ~(int)evt;

        /// <summary>
        /// 是否有任意待播事件
        /// </summary>
        public static bool HasPending(ShepelStoryData data) => data.ReactiveEventFlags != 0;
    }
}
