using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms
{
    /// <summary>
    /// 械奴解析链：专门条目优先，其余交给 <see cref="KikasaArmsProfiler"/> 档案推断兜底——
    /// 推断为枪类走通用枪奴、刀剑走通用刀奴，演出个性化由档案字段承担。
    /// key = 被沉武器的物品类型。沉入可复制武器时湖会把它写进鬼奴记忆
    /// （见 KikasaVaultPlayer.TrySink），召唤数量由湖藏存量折算，复制体不消耗湖藏原件
    /// </summary>
    internal static class KikasaArmsIndex
    {
        /// <summary>召唤委托：owner 本机受理后调用，count = 湖藏存量（实现自行钳上限）</summary>
        internal delegate void ArmsSpawner(Player owner, Vector2 emergeAt, int count);

        /// <summary>
        /// 专门条目：想给某件武器完全定制的械奴实现时挂这里，命中即短路通用推断。
        /// 当前全部走推断（迷你鲨/巨兽鲨的手调数值收进推断器的覆写档），
        /// 未来 boss 级武器的专属实现是这张表存在的理由
        /// </summary>
        private static readonly Dictionary<int, ArmsSpawner> entries = new();

        /// <summary>该武器能否被湖驱使：专门条目 → 枪推断 → 刀剑推断</summary>
        internal static bool TryGet(int itemType, out ArmsSpawner spawner) {
            if (entries.TryGetValue(itemType, out spawner)) {
                return true;
            }
            switch (KikasaArmsProfiler.Classify(itemType)) {
                case KikasaArmsKind.Gun:
                    spawner = (owner, at, count)
                        => KikasaGuns.KikasaGunServant.Summon(owner, at, count, itemType);
                    return true;
                case KikasaArmsKind.Blade:
                    spawner = (owner, at, count)
                        => KikasaBlades.KikasaBladeServant.Summon(owner, at, count, itemType);
                    return true;
                case KikasaArmsKind.Whip:
                    spawner = (owner, at, count)
                        => KikasaWhips.KikasaWhipServant.Summon(owner, at, count, itemType);
                    return true;
            }
            spawner = null;
            return false;
        }
    }
}
