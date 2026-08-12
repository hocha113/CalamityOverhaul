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
            [NPCID.KingSlime] = KikasaKingSlime.KikasaKingSlimeServant.Summon,
            //世界吞噬怪：沉的可能是任意一节，头/体/尾都记同一条
            [NPCID.EaterofWorldsHead] = KikasaEater.KikasaEaterServant.Summon,
            [NPCID.EaterofWorldsBody] = KikasaEater.KikasaEaterServant.Summon,
            [NPCID.EaterofWorldsTail] = KikasaEater.KikasaEaterServant.Summon,
            //克苏鲁之脑：沉的可能是脑本体或任意血凝块，双双记同一条
            [NPCID.BrainofCthulhu] = KikasaBrain.KikasaBrainServant.Summon,
            [NPCID.Creeper] = KikasaBrain.KikasaBrainServant.Summon,
            [NPCID.QueenBee] = KikasaQueenBee.KikasaQueenBeeServant.Summon,
            //骷髅王：沉的可能是头或手，两键都记同一条
            [NPCID.SkeletronHead] = KikasaSkeletron.KikasaSkeletronServant.Summon,
            [NPCID.SkeletronHand] = KikasaSkeletron.KikasaSkeletronServant.Summon,
            [NPCID.Deerclops] = KikasaDeerclops.KikasaDeerclopsServant.Summon,
            //血肉墙：本体与墙眼都是可独立沉溺的部件，两条都记同一面墙
            [NPCID.WallofFlesh] = KikasaWallOfFlesh.KikasaWallOfFleshServant.Summon,
            [NPCID.WallofFleshEye] = KikasaWallOfFlesh.KikasaWallOfFleshServant.Summon,
            [NPCID.QueenSlimeBoss] = KikasaQueenSlime.KikasaQueenSlimeServant.Summon,
            //双子魔眼：同源同沉，沉任意一只都召出成对双瞳
            [NPCID.Retinazer] = KikasaTwins.KikasaTwinsServant.Summon,
            [NPCID.Spazmatism] = KikasaTwins.KikasaTwinsServant.Summon,
            //机械骷髅王：沉的可能是头或任意一条工具臂，五条都记同一门
            [NPCID.SkeletronPrime] = KikasaPrime.KikasaPrimeServant.Summon,
            [NPCID.PrimeCannon] = KikasaPrime.KikasaPrimeServant.Summon,
            [NPCID.PrimeSaw] = KikasaPrime.KikasaPrimeServant.Summon,
            [NPCID.PrimeVice] = KikasaPrime.KikasaPrimeServant.Summon,
            [NPCID.PrimeLaser] = KikasaPrime.KikasaPrimeServant.Summon,
            //世纪之花：沉的可能是花体或钩须/触手，三条都记同一条
            [NPCID.Plantera] = KikasaPlantera.KikasaPlanteraServant.Summon,
            [NPCID.PlanterasHook] = KikasaPlantera.KikasaPlanteraServant.Summon,
            [NPCID.PlanterasTentacle] = KikasaPlantera.KikasaPlanteraServant.Summon,
            //石巨人：身体/附着头/飞头/左右拳，沉哪个部件都记同一条
            [NPCID.Golem] = KikasaGolem.KikasaGolemServant.Summon,
            [NPCID.GolemHead] = KikasaGolem.KikasaGolemServant.Summon,
            [NPCID.GolemHeadFree] = KikasaGolem.KikasaGolemServant.Summon,
            [NPCID.GolemFistLeft] = KikasaGolem.KikasaGolemServant.Summon,
            [NPCID.GolemFistRight] = KikasaGolem.KikasaGolemServant.Summon,
            [NPCID.DukeFishron] = KikasaFishron.KikasaFishronServant.Summon,
            [NPCID.HallowBoss] = KikasaEmpress.KikasaEmpressServant.Summon,
            //拜月教邪教徒：本体与幻影分身都记同一条
            [NPCID.CultistBoss] = KikasaCultist.KikasaCultistServant.Summon,
            [NPCID.CultistBossClone] = KikasaCultist.KikasaCultistServant.Summon,
            //月球领主：核心/头/手/真眼沉哪个都记同一颗心——湖只认得那颗心脏
            [NPCID.MoonLordCore] = KikasaMoonLord.KikasaMoonLordServant.Summon,
            [NPCID.MoonLordHead] = KikasaMoonLord.KikasaMoonLordServant.Summon,
            [NPCID.MoonLordHand] = KikasaMoonLord.KikasaMoonLordServant.Summon,
            [NPCID.MoonLordFreeEye] = KikasaMoonLord.KikasaMoonLordServant.Summon,
        };

        /// <summary>该生物是否已有专门的鬼奴实现</summary>
        internal static bool TryGet(int npcType, out ServantSpawner spawner)
            => entries.TryGetValue(npcType, out spawner);
    }
}
