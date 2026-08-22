using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaDestroyer;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEye;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants
{
    /// <summary>
    /// 鬼奴穷举注册表：每种可复制生物一条专门实现，不做通用代码
    /// 演出与机制个性化优先，后续逐个补条目。多部位 boss 归并到规范类型
    /// （毁灭者沉哪一节都记同一条），沉影盘的收集册与影位都以规范类型为键；
    /// 每条记忆标一系灵异亲和，驻湖后由 <see cref="KikasaEffigyBoard"/> 点数
    /// </summary>
    internal static class KikasaServantIndex
    {
        /// <summary>召唤委托：owner 本机受理后调用，负责生成对应鬼奴弹幕</summary>
        internal delegate void ServantSpawner(Player owner, Vector2 emergeAt);

        /// <summary>一条鬼奴记忆的全部登记项</summary>
        internal readonly struct ServantEntry
        {
            /// <summary>规范 NPC 类型（主部位）：收集册/影位/展示名的唯一键</summary>
            internal readonly int CanonicalType;

            /// <summary>灵异亲和</summary>
            internal readonly KikasaAffinity Affinity;

            /// <summary>召唤委托</summary>
            internal readonly ServantSpawner Spawner;

            /// <summary>鬼奴控制器弹幕类型（驻影在场判定用）；延迟取值避开静态构造次序</summary>
            internal readonly Func<int> ProjType;

            internal ServantEntry(int canonicalType, KikasaAffinity affinity,
                ServantSpawner spawner, Func<int> projType) {
                CanonicalType = canonicalType;
                Affinity = affinity;
                Spawner = spawner;
                ProjType = projType;
            }
        }

        //规范条目按进度序排列（收集册展示序）；partToIndex 把任意部位映射到规范条目
        private static readonly List<ServantEntry> canon = [];
        private static readonly Dictionary<int, int> partToIndex = [];

        /// <summary>登记一条：partTypes[0] 即规范类型，其余部位归并到它</summary>
        private static void Register(KikasaAffinity affinity, ServantSpawner spawner,
            Func<int> projType, params int[] partTypes) {
            int index = canon.Count;
            canon.Add(new ServantEntry(partTypes[0], affinity, spawner, projType));
            foreach (int part in partTypes) {
                partToIndex[part] = index;
            }
        }

        static KikasaServantIndex() {
            Register(KikasaAffinity.Rain, KikasaKingSlime.KikasaKingSlimeServant.Summon,
                static () => ModContent.ProjectileType<KikasaKingSlime.KikasaKingSlimeServant>(),
                NPCID.KingSlime);
            Register(KikasaAffinity.Nightmare, KikasaEyeServant.Summon,
                static () => ModContent.ProjectileType<KikasaEyeServant>(),
                NPCID.EyeofCthulhu);
            //世界吞噬怪：沉的可能是任意一节，头/体/尾都记同一条
            Register(KikasaAffinity.Rain, KikasaEater.KikasaEaterServant.Summon,
                static () => ModContent.ProjectileType<KikasaEater.KikasaEaterServant>(),
                NPCID.EaterofWorldsHead, NPCID.EaterofWorldsBody, NPCID.EaterofWorldsTail);
            //克苏鲁之脑：脑本体或任意血凝块，双双记同一条
            Register(KikasaAffinity.Nightmare, KikasaBrain.KikasaBrainServant.Summon,
                static () => ModContent.ProjectileType<KikasaBrain.KikasaBrainServant>(),
                NPCID.BrainofCthulhu, NPCID.Creeper);
            Register(KikasaAffinity.Flame, KikasaQueenBee.KikasaQueenBeeServant.Summon,
                static () => ModContent.ProjectileType<KikasaQueenBee.KikasaQueenBeeServant>(),
                NPCID.QueenBee);
            //骷髅王：头或手都记同一条
            Register(KikasaAffinity.Nightmare, KikasaSkeletron.KikasaSkeletronServant.Summon,
                static () => ModContent.ProjectileType<KikasaSkeletron.KikasaSkeletronServant>(),
                NPCID.SkeletronHead, NPCID.SkeletronHand);
            Register(KikasaAffinity.Nightmare, KikasaDeerclops.KikasaDeerclopsServant.Summon,
                static () => ModContent.ProjectileType<KikasaDeerclops.KikasaDeerclopsServant>(),
                NPCID.Deerclops);
            //血肉墙：本体与墙眼都是可独立沉溺的部件，两条都记同一面墙
            Register(KikasaAffinity.Flame, KikasaWallOfFlesh.KikasaWallOfFleshServant.Summon,
                static () => ModContent.ProjectileType<KikasaWallOfFlesh.KikasaWallOfFleshServant>(),
                NPCID.WallofFlesh, NPCID.WallofFleshEye);
            Register(KikasaAffinity.Rain, KikasaQueenSlime.KikasaQueenSlimeServant.Summon,
                static () => ModContent.ProjectileType<KikasaQueenSlime.KikasaQueenSlimeServant>(),
                NPCID.QueenSlimeBoss);
            //双子魔眼：同源同沉，沉任意一只都召出成对双瞳
            Register(KikasaAffinity.Nightmare, KikasaTwins.KikasaTwinsServant.Summon,
                static () => ModContent.ProjectileType<KikasaTwins.KikasaTwinsServant>(),
                NPCID.Retinazer, NPCID.Spazmatism);
            //毁灭者：沉的可能是任意一节，头/体/尾都记同一条
            Register(KikasaAffinity.Flame, KikasaDestroyerServant.Summon,
                static () => ModContent.ProjectileType<KikasaDestroyerServant>(),
                NPCID.TheDestroyer, NPCID.TheDestroyerBody, NPCID.TheDestroyerTail);
            //机械骷髅王：头或任意一条工具臂，五条都记同一门
            Register(KikasaAffinity.Flame, KikasaPrime.KikasaPrimeServant.Summon,
                static () => ModContent.ProjectileType<KikasaPrime.KikasaPrimeServant>(),
                NPCID.SkeletronPrime, NPCID.PrimeCannon, NPCID.PrimeSaw,
                NPCID.PrimeVice, NPCID.PrimeLaser);
            //世纪之花：花体或钩须/触手，三条都记同一条
            Register(KikasaAffinity.Rain, KikasaPlantera.KikasaPlanteraServant.Summon,
                static () => ModContent.ProjectileType<KikasaPlantera.KikasaPlanteraServant>(),
                NPCID.Plantera, NPCID.PlanterasHook, NPCID.PlanterasTentacle);
            //石巨人：身体/附着头/飞头/左右拳，沉哪个部件都记同一条
            Register(KikasaAffinity.Flame, KikasaGolem.KikasaGolemServant.Summon,
                static () => ModContent.ProjectileType<KikasaGolem.KikasaGolemServant>(),
                NPCID.Golem, NPCID.GolemHead, NPCID.GolemHeadFree,
                NPCID.GolemFistLeft, NPCID.GolemFistRight);
            Register(KikasaAffinity.Rain, KikasaFishron.KikasaFishronServant.Summon,
                static () => ModContent.ProjectileType<KikasaFishron.KikasaFishronServant>(),
                NPCID.DukeFishron);
            Register(KikasaAffinity.Flame, KikasaEmpress.KikasaEmpressServant.Summon,
                static () => ModContent.ProjectileType<KikasaEmpress.KikasaEmpressServant>(),
                NPCID.HallowBoss);
            //拜月教邪教徒：本体与幻影分身都记同一条；三珠杂耍=百搭亲和
            Register(KikasaAffinity.Wild, KikasaCultist.KikasaCultistServant.Summon,
                static () => ModContent.ProjectileType<KikasaCultist.KikasaCultistServant>(),
                NPCID.CultistBoss, NPCID.CultistBossClone);
            //月球领主：核心/头/手/真眼沉哪个都记同一颗心，湖只认得那颗心脏
            Register(KikasaAffinity.Nightmare, KikasaMoonLord.KikasaMoonLordServant.Summon,
                static () => ModContent.ProjectileType<KikasaMoonLord.KikasaMoonLordServant>(),
                NPCID.MoonLordCore, NPCID.MoonLordHead, NPCID.MoonLordHand,
                NPCID.MoonLordFreeEye);
        }

        /// <summary>规范条目总表（进度序），沉影盘收集册按它排位</summary>
        internal static IReadOnlyList<ServantEntry> AllEntries => canon;

        /// <summary>该生物是否已有专门的鬼奴实现</summary>
        internal static bool TryGet(int npcType, out ServantSpawner spawner) {
            if (partToIndex.TryGetValue(npcType, out int index)) {
                spawner = canon[index].Spawner;
                return true;
            }
            spawner = null;
            return false;
        }

        /// <summary>取完整登记项（部位或规范类型皆可查）</summary>
        internal static bool TryGetEntry(int npcType, out ServantEntry entry) {
            if (partToIndex.TryGetValue(npcType, out int index)) {
                entry = canon[index];
                return true;
            }
            entry = default;
            return false;
        }

        /// <summary>任意部位归并到规范类型；未登记返回 0</summary>
        internal static int CanonicalOf(int npcType)
            => partToIndex.TryGetValue(npcType, out int index) ? canon[index].CanonicalType : 0;

        /// <summary>规范类型的灵异亲和；未登记返回 None</summary>
        internal static KikasaAffinity AffinityOf(int npcType)
            => partToIndex.TryGetValue(npcType, out int index) ? canon[index].Affinity : KikasaAffinity.None;
    }
}
