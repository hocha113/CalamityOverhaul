using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants;
using CalamityOverhaul.Content.Narrative.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDrowns
{
    /// <summary>
    /// 沉溺门槛谓词：BOSS 级生物必须在本世界被击败过，湖才收得下它。
    /// 击败口径按世界进度三层取或：注册 boss 认原版 downed 旗（世吞体节死一节
    /// 不算击败，只有原版旗知道整条虫死没死）→ 灾厄 boss 认其 downed 旗追认旧档
    /// → 其余模组 boss 查 <see cref="KikasaBossLedger"/> 世界击杀台账
    /// </summary>
    internal static class KikasaBossGate
    {
        //注册 boss 规范类型 → 原版 downed 旗；血肉墙的"击败"就是困难模式本身
        private static readonly Dictionary<int, Func<bool>> vanillaDowned = new() {
            [NPCID.KingSlime] = static () => NPC.downedSlimeKing,
            [NPCID.EyeofCthulhu] = static () => NPC.downedBoss1,
            [NPCID.EaterofWorldsHead] = static () => NPC.downedBoss2,
            [NPCID.BrainofCthulhu] = static () => NPC.downedBoss2,
            [NPCID.QueenBee] = static () => NPC.downedQueenBee,
            [NPCID.SkeletronHead] = static () => NPC.downedBoss3,
            [NPCID.Deerclops] = static () => NPC.downedDeerclops,
            [NPCID.WallofFlesh] = static () => Main.hardMode,
            [NPCID.QueenSlimeBoss] = static () => NPC.downedQueenSlime,
            [NPCID.Retinazer] = static () => NPC.downedMechBoss2,
            [NPCID.TheDestroyer] = static () => NPC.downedMechBoss1,
            [NPCID.SkeletronPrime] = static () => NPC.downedMechBoss3,
            [NPCID.Plantera] = static () => NPC.downedPlantBoss,
            [NPCID.Golem] = static () => NPC.downedGolemBoss,
            [NPCID.DukeFishron] = static () => NPC.downedFishron,
            [NPCID.HallowBoss] = static () => NPC.downedEmpressOfLight,
            [NPCID.CultistBoss] = static () => NPC.downedAncientCultist,
            [NPCID.MoonLordCore] = static () => NPC.downedMoonlord,
        };

        //灾厄 boss 头类型 → 灾厄 downed 委托；懒建，CWRID 取值在灾厄缺席时只会得 0
        private static Dictionary<int, Func<bool>> calamityDowned;

        private static Dictionary<int, Func<bool>> BuildCalamityMap() {
            Dictionary<int, Func<bool>> map = [];
            void Add(int npcType, Func<bool> downed) {
                if (npcType > NPCID.None) {
                    map[npcType] = downed;
                }
            }
            Add(CWRID.NPC_DesertScourgeHead, InWorldBossPhase.Downed0);
            Add(CWRID.NPC_GiantClam, InWorldBossPhase.Downed1);
            Add(CWRID.NPC_Crabulon, InWorldBossPhase.Downed2);
            Add(CWRID.NPC_HiveMind, InWorldBossPhase.Downed3);
            Add(CWRID.NPC_PerforatorHive, InWorldBossPhase.Downed4);
            Add(CWRID.NPC_SlimeGodCore, InWorldBossPhase.Downed5);
            Add(CWRID.NPC_EbonianPaladin, InWorldBossPhase.Downed5);
            Add(CWRID.NPC_CrimulanPaladin, InWorldBossPhase.Downed5);
            Add(CWRID.NPC_SplitEbonianPaladin, InWorldBossPhase.Downed5);
            Add(CWRID.NPC_SplitCrimulanPaladin, InWorldBossPhase.Downed5);
            Add(CWRID.NPC_Cryogen, InWorldBossPhase.Downed6);
            Add(CWRID.NPC_BrimstoneElemental, InWorldBossPhase.Downed7);
            Add(CWRID.NPC_AquaticScourgeHead, InWorldBossPhase.Downed8);
            Add(CWRID.NPC_CragmawMire, InWorldBossPhase.Downed9);
            Add(CWRID.NPC_CalamitasClone, InWorldBossPhase.Downed10);
            Add(CWRID.NPC_Cataclysm, InWorldBossPhase.Downed10);
            Add(CWRID.NPC_Catastrophe, InWorldBossPhase.Downed10);
            Add(CWRID.NPC_GreatSandShark, InWorldBossPhase.Downed11);
            Add(CWRID.NPC_Anahita, InWorldBossPhase.Downed12);
            Add(CWRID.NPC_Leviathan, InWorldBossPhase.Downed12);
            Add(CWRID.NPC_AstrumAureus, InWorldBossPhase.Downed13);
            Add(CWRID.NPC_PlaguebringerGoliath, InWorldBossPhase.Downed14);
            Add(CWRID.NPC_RavagerBody, InWorldBossPhase.Downed15);
            Add(CWRID.NPC_AstrumDeusHead, InWorldBossPhase.Downed16);
            Add(CWRID.NPC_ProfanedGuardianCommander, InWorldBossPhase.Downed17);
            Add(CWRID.NPC_Dragonfolly, InWorldBossPhase.Downed18);
            Add(CWRID.NPC_Providence, InWorldBossPhase.Downed19);
            Add(CWRID.NPC_CeaselessVoid, InWorldBossPhase.Downed20);
            Add(CWRID.NPC_StormWeaverHead, InWorldBossPhase.Downed21);
            Add(CWRID.NPC_Signus, InWorldBossPhase.Downed22);
            Add(CWRID.NPC_Polterghast, InWorldBossPhase.Downed23);
            Add(CWRID.NPC_Mauler, InWorldBossPhase.Downed24);
            Add(CWRID.NPC_NuclearTerror, InWorldBossPhase.Downed25);
            Add(CWRID.NPC_OldDuke, InWorldBossPhase.Downed26);
            Add(CWRID.NPC_DevourerofGodsHead, InWorldBossPhase.Downed27);
            Add(CWRID.NPC_Yharon, InWorldBossPhase.Downed28);
            Add(CWRID.NPC_ThanatosHead, InWorldBossPhase.Downed29);
            Add(CWRID.NPC_Apollo, InWorldBossPhase.Downed29);
            Add(CWRID.NPC_Artemis, InWorldBossPhase.Downed29);
            Add(CWRID.NPC_AresBody, InWorldBossPhase.Downed29);
            Add(CWRID.NPC_SupremeCalamitas, InWorldBossPhase.Downed30);
            Add(CWRID.NPC_PrimordialWyrmHead, InWorldBossPhase.Downed31);
            Add(CWRID.NPC_EidolonWyrmHead, InWorldBossPhase.Downed31);
            return map;
        }

        /// <summary>
        /// 目标的门槛身份类型：注册 boss 归并规范类型，其余归并组锚点（realLife 头）类型。
        /// 台账记录与击败查询都以它为键，指着蠕虫身子和指着头是同一回事
        /// </summary>
        internal static int IdentityTypeOf(NPC npc) {
            int canonical = KikasaServantIndex.CanonicalOf(npc.type);
            return canonical > 0 ? canonical : AnchorOf(npc).type;
        }

        private static NPC AnchorOf(NPC npc) {
            int anchorIndex = NpcGroupHelper.GetAnchorIndex(npc);
            return anchorIndex >= 0 && anchorIndex < Main.maxNPCs && Main.npc[anchorIndex].active
                ? Main.npc[anchorIndex] : npc;
        }

        /// <summary>BOSS 级：注册 boss 任意部位 / boss 旗 / 原版"视同 boss"表（世吞头、柱等）/ 组锚点是 boss</summary>
        internal static bool IsBossLevel(NPC npc) {
            if (npc == null) {
                return false;
            }
            if (KikasaServantIndex.CanonicalOf(npc.type) > 0) {
                return true;
            }
            if (npc.boss || NPCID.Sets.ShouldBeCountedAsBoss[npc.type]) {
                return true;
            }
            NPC anchor = AnchorOf(npc);
            return anchor.boss || NPCID.Sets.ShouldBeCountedAsBoss[anchor.type];
        }

        /// <summary>该 boss 在本世界是否被击败过（以门槛身份类型为准）</summary>
        internal static bool IsDefeated(NPC npc) => IsDefeatedType(IdentityTypeOf(npc));

        internal static bool IsDefeatedType(int identityType) {
            if (vanillaDowned.TryGetValue(identityType, out Func<bool> flag)) {
                return flag();
            }
            if (CWRRef.Has) {
                calamityDowned ??= BuildCalamityMap();
                if (calamityDowned.TryGetValue(identityType, out Func<bool> calFlag) && calFlag()) {
                    return true;
                }
            }
            return KikasaBossLedger.Contains(identityType);
        }

        /// <summary>沉溺被门槛拦下：BOSS 级且未击败，这一按该是鞭笞不是拖拽</summary>
        internal static bool DrownBlocked(NPC npc) => IsBossLevel(npc) && !IsDefeated(npc);
    }

    /// <summary>
    /// 世界击杀台账：记录本世界击杀过的未登记 boss 类型（注册 boss 走原版旗，不入账）。
    /// 各端都经 <see cref="KikasaBossLedgerNPC"/> 的死亡钩子本地记录，客户端副本只喂
    /// 悬停预测；服务器那份是门槛真相，随 WorldData（入世、boss 击败后自动重发）下发对齐
    /// </summary>
    internal class KikasaBossLedger : ModSystem
    {
        //原版类型号跨会话稳定直接存；模组 boss 存 FullName，会话内解析出类型号缓存
        private static readonly HashSet<int> vanillaKilled = [];
        private static readonly HashSet<string> moddedKilled = [];
        private static readonly HashSet<int> moddedResolved = [];

        internal static bool Contains(int npcType)
            => npcType < NPCID.Count ? vanillaKilled.Contains(npcType) : moddedResolved.Contains(npcType);

        /// <summary>本地入账（哪端死亡钩子跑到就记哪端，服务器份即权威份）</summary>
        internal static void Record(NPC npc) {
            if (npc.type < NPCID.Count) {
                vanillaKilled.Add(npc.type);
                return;
            }
            if (NPCLoader.GetNPC(npc.type) is ModNPC modNPC && moddedKilled.Add(modNPC.FullName)) {
                moddedResolved.Add(npc.type);
            }
        }

        private static void RebuildResolved() {
            moddedResolved.Clear();
            foreach (string fullName in moddedKilled) {
                //卸了模组的条目静默留存，等模组回来再解析
                if (ModContent.TryFind(fullName, out ModNPC modNPC)) {
                    moddedResolved.Add(modNPC.Type);
                }
            }
        }

        public override void ClearWorld() {
            vanillaKilled.Clear();
            moddedKilled.Clear();
            moddedResolved.Clear();
        }

        public override void SaveWorldData(TagCompound tag) {
            if (vanillaKilled.Count > 0) {
                tag["KikasaBossLedger"] = vanillaKilled.ToList();
            }
            if (moddedKilled.Count > 0) {
                tag["KikasaBossLedgerNames"] = moddedKilled.ToList();
            }
        }

        public override void LoadWorldData(TagCompound tag) {
            ClearWorld();
            if (tag.TryGet("KikasaBossLedger", out List<int> vanilla)) {
                foreach (int type in vanilla) {
                    if (type > NPCID.None && type < NPCID.Count) {
                        vanillaKilled.Add(type);
                    }
                }
            }
            if (tag.TryGet("KikasaBossLedgerNames", out List<string> names)) {
                foreach (string name in names) {
                    if (!string.IsNullOrEmpty(name)) {
                        moddedKilled.Add(name);
                    }
                }
            }
            RebuildResolved();
        }

        public override void NetSend(BinaryWriter writer) {
            writer.Write((ushort)vanillaKilled.Count);
            foreach (int type in vanillaKilled) {
                writer.Write(type);
            }
            writer.Write((ushort)moddedKilled.Count);
            foreach (string name in moddedKilled) {
                writer.Write(name);
            }
        }

        public override void NetReceive(BinaryReader reader) {
            //服务器份就是真相，整份覆盖本地副本
            vanillaKilled.Clear();
            moddedKilled.Clear();
            int vanillaCount = reader.ReadUInt16();
            for (int i = 0; i < vanillaCount; i++) {
                int type = reader.ReadInt32();
                if (type > NPCID.None && type < NPCID.Count) {
                    vanillaKilled.Add(type);
                }
            }
            int moddedCount = reader.ReadUInt16();
            for (int i = 0; i < moddedCount; i++) {
                string name = reader.ReadString();
                if (!string.IsNullOrEmpty(name)) {
                    moddedKilled.Add(name);
                }
            }
            RebuildResolved();
        }
    }

    /// <summary>
    /// 台账的死亡入口：未登记 boss 的头节点死亡时各端本地记一笔。
    /// 注册 boss 不入账，世吞体节死一节会在这里误记成"击败"，它们只认原版旗
    /// </summary>
    internal sealed class KikasaBossLedgerNPC : DeathTrackingNPC
    {
        public override void OnNPCDeath(NPC npc) {
            if (KikasaServantIndex.CanonicalOf(npc.type) > 0) {
                return;
            }
            if (!KikasaBossGate.IsBossLevel(npc)) {
                return;
            }
            KikasaBossLedger.Record(npc);
        }
    }
}
