using CalamityOverhaul.Content.Narrative.Data;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Shepel
{
    /// <summary>响应式事件bit，新成员保持2的幂</summary>
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
        //后续追加保持2的幂
    }

    /// <summary>响应式事件bit队列</summary>
    internal static class ShepelReactiveEvents
    {
        private static ShepelStoryData GetData(Player player)
            => player.GetModPlayer<StoryPlayer>().Get<ShepelStoryData>();

        public static void Enqueue(Player player, ShepelReactiveEvent evt) {
            GetData(player).ReactiveEventFlags |= (int)evt;
        }

        //写bit+LastDefeatedBossNpcType
        public static void EnqueueBossDefeated(Player player, int npcType) {
            ShepelStoryData data = GetData(player);
            data.LastDefeatedBossNpcType = npcType;
            data.ReactiveEventFlags |= (int)ShepelReactiveEvent.BossDefeated;
        }

        public static bool HasFlag(ShepelStoryData data, ShepelReactiveEvent evt)
            => (data.ReactiveEventFlags & (int)evt) != 0;

        public static void ClearFlag(ShepelStoryData data, ShepelReactiveEvent evt)
            => data.ReactiveEventFlags &= ~(int)evt;

        public static bool HasPending(ShepelStoryData data) => data.ReactiveEventFlags != 0;
    }
}
