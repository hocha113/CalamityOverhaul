using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes.BossParts;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 节段离网：窗口期内打这一节不伤本体，落上去的伤害只记账，
    /// 到期把账面的六成一次性结算给本体（吃防御与 DR）。<br/>
    /// 设计稿原案要求悬空 <c>realLife</c> 并拦截 Calamity 的每帧回写（§5.2 全批最脆），
    /// 这里按其自己给出的处置降级：<b>不动 realLife</b>——命中照常经 realLife 落到本体，
    /// 由 <see cref="SegmentDelinkCombat"/> 在 <c>HitEffect</c>（扣血之后、checkDead 之前）
    /// 原额回填本体生命并记账。链条永远不断、体节永远不死、
    /// 「现在拿 100% 还是到期拿 60%」的投资决策完整保留
    /// </summary>
    internal class SegmentDelink : QuickHackDef
    {
        /// <summary>到期折算比例</summary>
        internal const float ConvertRatio = 0.6f;

        private static readonly Color Bone = new(232, 220, 186);
        private static readonly Color Amber = new(255, 186, 92);

        /// <summary>ActivationId → 账面伤害。协议实例是单例，per-effect 状态只能外挂</summary>
        private static readonly Dictionary<long, long> tallies = [];

        public override void SetDefaults() {
            UploadTime = 180;
            RamCost = 6;
            Category = QuickHackCategory.Lethal;
            SupportedTargets = HackTargetKind.BossPart;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => 360;

        public override bool CanApplyTo(IHackTarget target) {
            if (!base.CanApplyTo(target)) return false;
            if (!HackTargets.TryNpc(target, out NPC npc)) return false;
            SweepOrphaned();
            //只收 realLife 指向活体本体的虫节；分裂型蠕虫（星神游龙）明确拒收，
            //分裂会重排节段身份，账会记到不存在的节上
            if (!BossPartResolver.TryGetPart(npc, out BossPartInfo info)
                || info.Role != BossPartRole.Segment) {
                return false;
            }
            if (npc.immortal || IsSplittingWormSegment(npc.type)) return false;
            //同一节重复离网会开两本账互相盖
            return !HackEffectTracker.HasEffect<SegmentDelink>(npc.whoAmI);
        }

        private static bool IsSplittingWormSegment(int type) {
            return type == CWRID.NPC_AstrumDeusHead
                || type == CWRID.NPC_AstrumDeusBody
                || type == CWRID.NPC_AstrumDeusTail;
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (!HackTargets.TryNpc(target, out NPC npc)) return false;
            ActiveHackEffect effect
                = HackEffectTracker.GetEffect<SegmentDelink>(npc.whoAmI);
            if (effect == null) return false;
            tallies[effect.ActivationId] = 0L;
            if (Main.netMode != NetmodeID.Server) EmitDelink(npc);
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (HackTargets.TryNpc(target, out NPC npc)) EmitDelink(npc);
        }

        public override bool OnTick(IHackTarget target, int elapsed) {
            if (Main.netMode != NetmodeID.Server
                && HackTargets.TryNpc(target, out NPC npc)) {
                EmitPulse(npc, elapsed);
            }
            return true;
        }

        public override void OnReplicatedTick(IHackTarget target, int elapsed) {
            if (HackTargets.TryNpc(target, out NPC npc)) EmitPulse(npc, elapsed);
        }

        public override void OnRemove(IHackTarget target) {
            if (!HackTargets.TryNpc(target, out NPC npc)) return;

            ActiveHackEffect effect
                = HackEffectTracker.GetEffect<SegmentDelink>(npc.whoAmI);
            long tally = 0L;
            if (effect != null && tallies.Remove(effect.ActivationId, out long logged)) {
                tally = logged;
            }

            if (Main.netMode != NetmodeID.MultiplayerClient && tally > 0) {
                int anchorIndex = NpcGroupHelper.GetAnchorIndex(npc);
                if (anchorIndex >= 0 && anchorIndex != npc.whoAmI) {
                    NPC anchor = Main.npc[anchorIndex];
                    if (anchor.active && anchor.life > 0) {
                        int settle = (int)Math.Clamp((long)(tally * ConvertRatio),
                            1L, int.MaxValue / 2);
                        //吃防御与 DR 是设计写死的：SimpleStrikeNPC 会走完整命中计算
                        anchor.SimpleStrikeNPC(settle, 0, false, 0f, null, false, 0f, true);
                        if (Main.netMode != NetmodeID.Server) EmitSettle(anchor);
                    }
                }
            }
            if (Main.netMode != NetmodeID.Server) EmitRelink(npc);
        }

        public override void OnReplicatedRemove(IHackTarget target) {
            if (HackTargets.TryNpc(target, out NPC npc)) EmitRelink(npc);
        }

        public override void Unload() {
            base.Unload();
            tallies.Clear();
        }

        internal static void ResetLedgers() => tallies.Clear();

        #region 记账接口

        /// <summary>该节当前的账面伤害；仅权威端有账，远端客户端返回 false</summary>
        internal static bool TryGetTally(int npcIndex, out long tally) {
            tally = 0L;
            ActiveHackEffect effect
                = HackEffectTracker.GetEffect<SegmentDelink>(npcIndex);
            if (effect == null || effect.Replicated) return false;
            return tallies.TryGetValue(effect.ActivationId, out tally);
        }

        /// <summary>受击分派侧的记账入口，返回该效果是否在账上</summary>
        internal static bool TryLogHit(long activationId, int damage) {
            if (damage <= 0 || !tallies.TryGetValue(activationId, out long tally)) {
                return false;
            }
            tallies[activationId] = Math.Min(tally + damage, long.MaxValue / 2);
            return true;
        }

        /// <summary>
        /// 本体死亡等路径下追踪器直接丢效果、不走 OnRemove，账会变成无主。
        /// 下次施放前对齐一次，无主的直接销账（本体都没了，也没有结算对象）
        /// </summary>
        private static void SweepOrphaned() {
            if (tallies.Count == 0) return;
            List<long> orphaned = null;
            foreach (long activationId in tallies.Keys) {
                if (HackEffectTracker.FindEffect(activationId) != null) continue;
                (orphaned ??= []).Add(activationId);
            }
            if (orphaned == null) return;
            for (int i = 0; i < orphaned.Count; i++) {
                tallies.Remove(orphaned[i]);
            }
        }

        #endregion

        #region 表现

        //拔销：沿节段两端喷出骨白色火花
        private static void EmitDelink(NPC npc) {
            for (int i = 0; i < 12; i++) {
                float side = i < 6 ? -1f : 1f;
                Vector2 pos = npc.Center + new Vector2(side * npc.width * 0.45f,
                    Main.rand.NextFloat(-npc.height * 0.4f, npc.height * 0.4f));
                Vector2 vel = new(side * Main.rand.NextFloat(1.2f, 2.6f),
                    Main.rand.NextFloat(-1f, 1f));
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, Bone, 0.8f)
                    ?.Configure(false, 20);
            }
        }

        //离网心跳：账本还开着的提示
        private static void EmitPulse(NPC npc, int elapsed) {
            if (elapsed % 20 != 0) return;
            PRTLoader.NewParticle<PRT_Spark>(npc.Center, Vector2.Zero, Amber, 0.55f)
                ?.Configure(false, 14);
        }

        //到期结算：本体位置一圈折算冲击
        private static void EmitSettle(NPC anchor) {
            for (int i = 0; i < 16; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(5.5f, 5.5f)
                    * Main.rand.NextFloat(0.4f, 1f);
                PRTLoader.NewParticle<PRT_Spark>(anchor.Center, vel, Amber, 1.1f)
                    ?.Configure(false, 26);
            }
        }

        //回插：节段并回链条
        private static void EmitRelink(NPC npc) {
            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(1.8f, 1.8f);
                PRTLoader.NewParticle<PRT_Spark>(npc.Center, vel, Bone, 0.6f)
                    ?.Configure(false, 14);
            }
        }

        #endregion
    }

    /// <summary>
    /// 节段离网的受击分派。<br/>
    /// <c>HitEffect</c> 在 <c>StrikeNPC</c> 里排在本体扣血之后、<c>checkDead</c> 之前：
    /// 在这里把伤害原额还给本体，本体在窗口期内净扣为零、致命一击也到不了 checkDead；
    /// 账面额即玩家实际打出的（过了体节防御的）数字。<br/>
    /// 与 <see cref="HackEffectNPCCombat"/> 同款纪律：各端都会进 HitEffect，玩法只在权威端结算
    /// </summary>
    internal class SegmentDelinkCombat : GlobalNPC
    {
        //自己触发的结算再进 HitEffect 就是无限递归，同款闸
        private static bool resolving;

        public override void HitEffect(NPC npc, NPC.HitInfo hit) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            if (resolving || hit.Damage <= 0 || !npc.active) return;
            //InstantKill 的语义是绕过一切把它杀掉，不跟这种机制打架
            if (hit.InstantKill || npc.immortal) return;

            int rl = npc.realLife;
            if (rl < 0 || rl >= Main.maxNPCs) return;

            ActiveHackEffect effect
                = HackEffectTracker.GetEffect<SegmentDelink>(npc.whoAmI);
            if (effect == null || effect.Replicated) return;

            resolving = true;
            try {
                if (!SegmentDelink.TryLogHit(effect.ActivationId, hit.Damage)) return;
                NPC anchor = Main.npc[rl];
                if (!anchor.active) return;
                //扣血已经发生，这里原额补回；checkDead 在后面跑，本体不会因此死
                anchor.life = Math.Min(anchor.life + hit.Damage, anchor.lifeMax);
                //镜像段自己的显示血量，别让这节短一帧
                npc.life = anchor.life;
                if (Main.netMode == NetmodeID.Server) {
                    anchor.netUpdate = true;
                }
            } finally {
                resolving = false;
            }
        }
    }
}
