using CalamityOverhaul.Common;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes.BossParts
{
    /// <summary>部件在群组里的角色</summary>
    internal enum BossPartRole : byte
    {
        None,
        /// <summary>虫节：realLife 指向本体，生命与本体同池</summary>
        Segment,
        /// <summary>肢体/炮组：类型表登记，围绕本体行动</summary>
        Limb,
    }

    /// <summary>一次部件判定的结果</summary>
    internal readonly struct BossPartInfo(BossPartRole role, int anchorIndex)
    {
        public readonly BossPartRole Role = role;
        public readonly int AnchorIndex = anchorIndex;

        public bool IsPart => Role != BossPartRole.None
            && AnchorIndex >= 0 && AnchorIndex < Main.maxNPCs;
    }

    /// <summary>
    /// F15：部件↔本体关系解析。<br/>
    /// 「这个 NPC 是谁的部件、本体在哪、同体还有谁」都从这里问，
    /// 三条 BossPart 协议与扫描面板共用同一套判据。<br/>
    /// 肢体表在 <c>SetupData</c>（PostSetupContent）建一次——CWRID 是懒查缓存，
    /// 那时 Calamity 内容已注册完；缺员只是表里少一行，判定静默退化为普通 NPC
    /// </summary>
    internal class BossPartResolver : ICWRLoader
    {
        /// <summary>肢体类型 → 本体类型；只登记「围着本体打」的攻击性部件</summary>
        private static readonly Dictionary<int, int> limbToAnchorType = [];
        /// <summary>Calamity 出身的肢体类型；原版肢体在灾厄在场时 AI 被其 PreAI 重写，伪装打不进去</summary>
        private static readonly HashSet<int> calamityLimbTypes = [];
        /// <summary>Exo Mechs 协同图成员类型（协同断链的窗口范围）</summary>
        private static readonly HashSet<int> exoGroupTypes = [];

        void ICWRLoader.SetupData() {
            limbToAnchorType.Clear();
            calamityLimbTypes.Clear();
            exoGroupTypes.Clear();

            //原版肢体恒登记：无灾厄的裸原版环境照样可扫可打
            RegisterLimb(NPCID.PrimeSaw, NPCID.SkeletronPrime);
            RegisterLimb(NPCID.PrimeVice, NPCID.SkeletronPrime);
            RegisterLimb(NPCID.PrimeCannon, NPCID.SkeletronPrime);
            RegisterLimb(NPCID.PrimeLaser, NPCID.SkeletronPrime);
            RegisterLimb(NPCID.SkeletronHand, NPCID.SkeletronHead);
            RegisterLimb(NPCID.GolemFistLeft, NPCID.Golem);
            RegisterLimb(NPCID.GolemFistRight, NPCID.Golem);
            RegisterLimb(NPCID.MoonLordHand, NPCID.MoonLordCore);
            RegisterLimb(NPCID.MoonLordHead, NPCID.MoonLordCore);

            if (!CWRRef.Has) {
                return;
            }

            //灾厄多体成员。腿柱（RavagerLeg*）刻意不登记：没有攻击行为，征收了也是白花
            RegisterCalamityLimb(CWRID.NPC_RavagerClawLeft, CWRID.NPC_RavagerBody);
            RegisterCalamityLimb(CWRID.NPC_RavagerClawRight, CWRID.NPC_RavagerBody);
            RegisterCalamityLimb(CWRID.NPC_RavagerHead, CWRID.NPC_RavagerBody);
            RegisterCalamityLimb(CWRID.NPC_AresLaserCannon, CWRID.NPC_AresBody);
            RegisterCalamityLimb(CWRID.NPC_AresPlasmaFlamethrower, CWRID.NPC_AresBody);
            RegisterCalamityLimb(CWRID.NPC_AresTeslaCannon, CWRID.NPC_AresBody);
            RegisterCalamityLimb(CWRID.NPC_AresGaussNuke, CWRID.NPC_AresBody);
            //守卫两卫星不在 CWRID，本地懒查一次；查不到就当没有这两行
            RegisterCalamityLimb(FindCalamityNpc("ProfanedGuardianDefender"),
                CWRID.NPC_ProfanedGuardianCommander);
            RegisterCalamityLimb(FindCalamityNpc("ProfanedGuardianHealer"),
                CWRID.NPC_ProfanedGuardianCommander);

            AddExoType(CWRID.NPC_AresBody);
            AddExoType(CWRID.NPC_AresLaserCannon);
            AddExoType(CWRID.NPC_AresPlasmaFlamethrower);
            AddExoType(CWRID.NPC_AresTeslaCannon);
            AddExoType(CWRID.NPC_AresGaussNuke);
            AddExoType(CWRID.NPC_Apollo);
            AddExoType(CWRID.NPC_Artemis);
            AddExoType(CWRID.NPC_ThanatosHead);
            AddExoType(CWRID.NPC_ThanatosBody1);
            AddExoType(CWRID.NPC_ThanatosBody2);
            AddExoType(CWRID.NPC_ThanatosTail);
        }

        void ICWRLoader.UnLoadData() {
            limbToAnchorType.Clear();
            calamityLimbTypes.Clear();
            exoGroupTypes.Clear();
        }

        private static void RegisterLimb(int limbType, int anchorType) {
            if (limbType <= NPCID.None || anchorType <= NPCID.None) {
                return;
            }
            limbToAnchorType[limbType] = anchorType;
        }

        private static void RegisterCalamityLimb(int limbType, int anchorType) {
            if (limbType <= NPCID.None || anchorType <= NPCID.None) {
                return;
            }
            limbToAnchorType[limbType] = anchorType;
            calamityLimbTypes.Add(limbType);
        }

        private static void AddExoType(int type) {
            if (type > NPCID.None) {
                exoGroupTypes.Add(type);
            }
        }

        private static int FindCalamityNpc(string name)
            => ModContent.TryFind("CalamityMod", name, out ModNPC npc) ? npc.Type : NPCID.None;

        /// <summary>
        /// 判定这个 NPC 是不是「已注册 Boss 群组的非本体部件」。<br/>
        /// 肢体表优先于 realLife——Ares 炮组两者都占，按肢体算
        /// </summary>
        public static bool TryGetPart(NPC npc, out BossPartInfo info) {
            info = default;
            if (npc == null || !npc.active || npc.friendly || npc.townNPC
                || npc.CountsAsACritter) {
                return false;
            }

            //肢体：类型表命中，再找活着的本体
            if (limbToAnchorType.TryGetValue(npc.type, out int anchorType)) {
                int anchorIndex = ResolveLimbAnchor(npc, anchorType);
                if (anchorIndex >= 0 && IsQualifiedAnchor(anchorIndex, npc.whoAmI)) {
                    info = new BossPartInfo(BossPartRole.Limb, anchorIndex);
                    return true;
                }
                return false;
            }

            //虫节：realLife 指向活着的本体
            int rl = npc.realLife;
            if (rl >= 0 && rl < Main.maxNPCs && rl != npc.whoAmI
                && Main.npc[rl].active && IsQualifiedAnchor(rl, npc.whoAmI)) {
                info = new BossPartInfo(BossPartRole.Segment, rl);
                return true;
            }
            return false;
        }

        //本体必须是 Boss 级，杂兵组合（如史莱姆塔）不进部件系统
        private static bool IsQualifiedAnchor(int anchorIndex, int selfIndex) {
            if (anchorIndex == selfIndex) {
                return false;
            }
            NPC anchor = Main.npc[anchorIndex];
            return anchor.active && anchor.life > 0 && NpcGroupHelper.IsBossTier(anchor);
        }

        //肢体找本体：realLife 最准（Ares 炮），其次 ai[1] 惯例（Prime 四肢），最后按类型就近扫
        private static int ResolveLimbAnchor(NPC limb, int anchorType) {
            int rl = limb.realLife;
            if (rl >= 0 && rl < Main.maxNPCs && Main.npc[rl].active
                && Main.npc[rl].type == anchorType) {
                return rl;
            }
            int aiIndex = (int)limb.ai[1];
            if (aiIndex >= 0 && aiIndex < Main.maxNPCs && Main.npc[aiIndex].active
                && Main.npc[aiIndex].type == anchorType) {
                return aiIndex;
            }
            int nearest = -1;
            float nearestSq = float.MaxValue;
            foreach (NPC candidate in Main.ActiveNPCs) {
                if (candidate.type != anchorType) {
                    continue;
                }
                float distSq = candidate.DistanceSQ(limb.Center);
                if (distSq < nearestSq) {
                    nearestSq = distSq;
                    nearest = candidate.whoAmI;
                }
            }
            return nearest;
        }

        /// <summary>这个部件能不能被「肢体征收」。原版肢体在灾厄在场时 AI 进了灾厄的 PreAI，位置伪装晚一步打不进去</summary>
        public static bool CanSeizeLimb(NPC npc) {
            if (npc == null || !limbToAnchorType.ContainsKey(npc.type)) {
                return false;
            }
            return calamityLimbTypes.Contains(npc.type) || !CWRRef.Has;
        }

        /// <summary>是否属于 Exo Mechs 协同图（协同断链的伪装窗口范围）</summary>
        public static bool IsExoGroupMember(NPC npc)
            => npc != null && exoGroupTypes.Contains(npc.type);

        /// <summary>
        /// 该节在同锚点体节里的序号与总数（1 起，按槽位序）。
        /// 只做面板展示，不参与结算，槽位序足够稳定
        /// </summary>
        public static void GetSegmentOrdinal(NPC npc, int anchorIndex,
            out int ordinal, out int total) {
            ordinal = 0;
            total = 0;
            if (npc == null) {
                return;
            }
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC other = Main.npc[i];
                if (!other.active || other.realLife != anchorIndex
                    || other.whoAmI == anchorIndex) {
                    continue;
                }
                total++;
                if (other.whoAmI <= npc.whoAmI) {
                    ordinal++;
                }
            }
        }
    }
}
