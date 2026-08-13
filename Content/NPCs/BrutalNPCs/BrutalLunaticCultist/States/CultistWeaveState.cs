using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 连接态：元素轮转（舞台换灯）+小瞬移换位+选招；破绽硬直期在此吊住不出招；
    /// npc.ai[3]=换位侧向种子
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.Weave, typeof(CultistStateContext))]
    internal class CultistWeaveState : CultistStateBase
    {
        public override string StateName => "Weave";
        public override CultistStateIndex StateIndex => CultistStateIndex.Weave;

        private const int BlinkMoment = 16;

        private int Duration(CultistStateContext ctx) {
            int baseTime = ctx.IsPhase2 ? 34 : 46;
            if (ctx.IsDeathMode) {
                baseTime -= 6;
            }
            return baseTime;
        }

        public override void OnEnter(CultistStateContext context) {
            base.OnEnter(context);
            if (!VaultUtils.isClient) {
                //元素轮转在连接态发生（舞台灯换色的可读节拍）
                context.AdvanceElement();
                context.Npc.ai[3] = Main.rand.Next(2);
                context.Npc.netUpdate = true;
            }
            else if (context.StaggerTimer > 50 && (int)context.Npc.ai[1] == 1) {
                //客户端：带着新鲜硬直+看破cue进入连接态 = 看破奖励刚发生
                CultistMirrorBlinkState.PlayRevealFx(context);
            }
        }

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;

            FaceTarget(context);
            context.ElementAura = 0.85f;
            context.CastGlow = 0.3f + 0.2f * (float)Math.Sin(Timer * 0.3f);

            bool staggered = context.StaggerTimer > 0;
            if (staggered) {
                //破绽硬直：跪伏喘息，不出招不移动
                context.CastPose = CultistPose.Stand;
                context.SkipDefaultHover = true;
                npc.velocity *= 0.9f;
                if (!VaultUtils.isServer && Main.rand.NextBool(5)) {
                    CultistRenderHelper.SpawnElementMote(npc.Center + Main.rand.NextVector2Circular(30f, 40f),
                        -Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.5f), context.Element, 0.6f, 20);
                }
                return null;
            }
            //硬直结束清除cue
            if (!VaultUtils.isClient && npc.ai[1] != 0f) {
                npc.ai[1] = 0f;
                npc.netUpdate = true;
            }

            //换灯期帷幕轻染新元素色
            CultistScreenFX.DeclareVeil(npc.Center, 0.1f, context.Element);

            //小瞬移换位
            int side = (int)npc.ai[3] % 2 == 0 ? 1 : -1;
            if ((int)Timer == BlinkMoment && player.Alives()) {
                Vector2 target = player.Center + new Vector2(side * 440f, -280f);
                if (!VaultUtils.isClient) {
                    CultistBossAI.BlinkTo(context, target);
                }
                else {
                    //客户端只放表现，位置交给同步
                    CultistRenderHelper.BlinkOut(npc.Center, context.Element);
                    CultistRenderHelper.BlinkIn(target, context.Element);
                }
            }

            if (player.Alives()) {
                SetHover(context, player.Center + new Vector2(side * 440f, -280f));
            }

            //选招（服务端）
            if (Timer >= Duration(context) && !VaultUtils.isClient) {
                return ChooseNextAttack(context);
            }
            return null;
        }

        private ICultistState ChooseNextAttack(CultistStateContext context) {
            //手写出招环：压迫与呼吸交替
            ICultistState[] cycle = context.IsPhase2
                ? new ICultistState[] {
                    new CultistElementBarrageState(),
                    new CultistMirrorBlinkState(),
                    new CultistAncientAssaultState(),
                    new CultistSigilVolleyState(),
                    new CultistElementWheelState(),
                    new CultistElementBarrageState(),
                    new CultistGrandRitualState(),
                    new CultistAncientAssaultState(),
                }
                : new ICultistState[] {
                    new CultistElementBarrageState(),
                    new CultistSigilVolleyState(),
                    new CultistMirrorBlinkState(),
                    new CultistElementBarrageState(),
                    new CultistElementWheelState(),
                    new CultistGrandRitualState(),
                };

            ICultistState next = cycle[context.AttackCycleIndex % cycle.Length];
            context.AttackCycleIndex++;

            //已有幻影龙/幻视在场时不再开坛，换成法阵齐射
            if (next is CultistGrandRitualState
                && (NPC.AnyNPCs(NPCID.CultistDragonHead) || NPC.AnyNPCs(NPCID.AncientCultistSquidhead))) {
                next = new CultistSigilVolleyState();
            }
            return next;
        }
    }
}
