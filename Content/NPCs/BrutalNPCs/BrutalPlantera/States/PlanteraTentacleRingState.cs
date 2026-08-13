using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Rendering;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.States
{
    /// <summary>
    /// 触手绽放处刑圈：触手外扩成旋转硬环，本体贴身追猎——
    /// 玩家要么卡进环缝里贴身周旋，要么退到环外吃追击；
    /// 打死相邻触手能撕开永久缺口
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PlanteraStateIndex.TentacleRing, typeof(PlanteraStateContext))]
    internal class PlanteraTentacleRingState : PlanteraStateBase
    {
        public override string StateName => "TentacleRing";
        public override PlanteraStateIndex StateIndex => PlanteraStateIndex.TentacleRing;

        private const int StateEnd = 380;
        private const int ReverseAt = 190;

        private bool reversed;

        public PlanteraTentacleRingState() {
        }

        public override void OnEnter(PlanteraStateContext context) {
            base.OnEnter(context);
            reversed = false;

            if (!VaultUtils.isClient) {
                int dir = Main.rand.NextBool() ? 1 : -1;
                foreach (var tent in context.Tentacles) {
                    PlanteraTentacleAI.CommandRing(tent, dir);
                }
                //钩爪放自由追锚，配合本体贴身
                foreach (var hook in context.Hooks) {
                    PlanteraHookAI.Release(hook);
                }
            }

            SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.9f, Pitch = 0.1f }, context.Npc.Center);
        }

        public override IPlanteraState OnUpdate(PlanteraStateContext context) {
            NPC npc = context.Npc;

            Timer++;

            //本体贴身狂追(处刑圈的移动压迫)
            SetSuspension(context, Vector2.Zero, PlanteraDirector.DriftSpeedP2 * 1.15f, 0.075f);
            context.GlowPulse = 0.5f;

            //中段反转：预告帧闪+咔声，再下反转命令
            if (!reversed && Timer >= ReverseAt) {
                reversed = true;
                if (!VaultUtils.isClient) {
                    foreach (var tent in context.Tentacles) {
                        PlanteraTentacleAI.CommandRing(tent, tent.ai[1] >= 0f ? -1 : 1);
                    }
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = 0.2f, Volume = 1f }, npc.Center);
                    PlanteraRenderHelper.SpawnPetalBurst(npc.Center, 10, 6f, true);
                }
                context.GlowPulse = 1f;
            }

            //触手全灭提前收招(奖励火力集中的玩家)
            bool allDead = context.Tentacles.Count == 0 && Timer > 40;

            if ((Timer >= StateEnd || allDead) && !VaultUtils.isClient) {
                return new PlanteraCanopyState();
            }
            return null;
        }

        public override void OnExit(PlanteraStateContext context) {
            base.OnExit(context);
            if (!VaultUtils.isClient) {
                foreach (var tent in context.Tentacles) {
                    PlanteraTentacleAI.CommandIdle(tent);
                }
            }
        }
    }
}
