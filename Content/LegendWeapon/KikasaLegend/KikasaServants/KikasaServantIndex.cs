using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaDestroyer;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEye;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants
{
    /// <summary>
    /// 鬼奴穷举注册表：每种可复制生物一条专门实现，不做通用代码——
    /// 演出与机制个性化优先，后续逐个补条目。key = 被沉 NPC 类型
    /// </summary>
    internal static class KikasaServantIndex
    {
        /// <summary>召唤委托：owner 本机受理后调用，负责生成对应鬼奴弹幕</summary>
        internal delegate void ServantSpawner(Player owner, Vector2 emergeAt);

        private static readonly Dictionary<int, ServantSpawner> entries = new() {
            [NPCID.EyeofCthulhu] = KikasaEyeServant.Summon,
            //毁灭者：沉的可能是任意一节，头/体/尾都记同一条
            [NPCID.TheDestroyer] = KikasaDestroyerServant.Summon,
            [NPCID.TheDestroyerBody] = KikasaDestroyerServant.Summon,
            [NPCID.TheDestroyerTail] = KikasaDestroyerServant.Summon,
        };

        /// <summary>该生物是否已有专门的鬼奴实现</summary>
        internal static bool TryGet(int npcType, out ServantSpawner spawner)
            => entries.TryGetValue(npcType, out spawner);
    }
}
