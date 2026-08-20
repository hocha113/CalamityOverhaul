using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms
{
    /// <summary>
    /// 械奴穷举注册表：每种可复制武器一条专门实现，不做通用代码——
    /// 演出与机制个性化优先，后续逐个补条目。key = 被沉武器的物品类型。
    /// 沉入已注册武器时湖会把它写进鬼奴记忆（见 KikasaVaultPlayer.TrySink），
    /// 召唤数量由湖藏存量折算，复制体不消耗湖藏原件
    /// </summary>
    internal static class KikasaArmsIndex
    {
        /// <summary>召唤委托：owner 本机受理后调用，count = 湖藏存量（实现自行钳上限）</summary>
        internal delegate void ArmsSpawner(Player owner, Vector2 emergeAt, int count);

        private static readonly Dictionary<int, ArmsSpawner> entries = new() {
            //鲨系连发枪共用一套鲨群骨架，按沉入武器换皮（贴图/口径/伤害档）
            [ItemID.Minishark] = (owner, at, count)
                => KikasaMinishark.KikasaMinisharkServant.Summon(owner, at, count, ItemID.Minishark),
            [ItemID.Megashark] = (owner, at, count)
                => KikasaMinishark.KikasaMinisharkServant.Summon(owner, at, count, ItemID.Megashark),
        };

        /// <summary>该武器是否已有专门的械奴实现</summary>
        internal static bool TryGet(int itemType, out ArmsSpawner spawner)
            => entries.TryGetValue(itemType, out spawner);
    }
}
