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
            //[鬼奴占位·史莱姆王] KikasaKingSlime 支线落地时用正式条目替换本行
            //[鬼奴占位·世界吞噬怪] KikasaEater 支线落地时用正式条目替换本行
            //[鬼奴占位·克苏鲁之脑] KikasaBrain 支线落地时用正式条目替换本行
            //[鬼奴占位·蜂后] KikasaQueenBee 支线落地时用正式条目替换本行
            //[鬼奴占位·骷髅王] KikasaSkeletron 支线落地时用正式条目替换本行
            //[鬼奴占位·鹿角怪] KikasaDeerclops 支线落地时用正式条目替换本行
            //[鬼奴占位·血肉墙] KikasaWallOfFlesh 支线落地时用正式条目替换本行
            //[鬼奴占位·史莱姆皇后] KikasaQueenSlime 支线落地时用正式条目替换本行
            //[鬼奴占位·双子魔眼] KikasaTwins 支线落地时用正式条目替换本行
            //[鬼奴占位·机械骷髅王] KikasaPrime 支线落地时用正式条目替换本行
            //[鬼奴占位·世纪之花] KikasaPlantera 支线落地时用正式条目替换本行
            //[鬼奴占位·石巨人] KikasaGolem 支线落地时用正式条目替换本行
            //[鬼奴占位·猪龙鱼公爵] KikasaFishron 支线落地时用正式条目替换本行
            //[鬼奴占位·光之女皇] KikasaEmpress 支线落地时用正式条目替换本行
            //[鬼奴占位·拜月教邪教徒] KikasaCultist 支线落地时用正式条目替换本行
            //[鬼奴占位·月球领主] KikasaMoonLord 支线落地时用正式条目替换本行
        };

        /// <summary>该生物是否已有专门的鬼奴实现</summary>
        internal static bool TryGet(int npcType, out ServantSpawner spawner)
            => entries.TryGetValue(npcType, out spawner);
    }
}
