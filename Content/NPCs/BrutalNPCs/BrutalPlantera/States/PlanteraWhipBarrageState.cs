using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Projectiles;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.States
{
    /// <summary>
    /// 触手鞭刑连段：触手逐根缩卷→朝玩家方位甩出长鞭，
    /// 波浪式轮转出鞭，伤害窗口只在鞭出与定格
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PlanteraStateIndex.WhipBarrage, typeof(PlanteraStateContext))]
    internal class PlanteraWhipBarrageState : PlanteraStateBase
    {
        public override string StateName => "WhipBarrage";
        public override PlanteraStateIndex StateIndex => PlanteraStateIndex.WhipBarrage;

        private int LashGap(PlanteraStateContext ctx) => (int)(26 * PlanteraDirector.TimeScale(ctx));
        private int TotalLashes(PlanteraStateContext ctx) => ctx.IsDeathMode ? 12 : 10;

        /// <summary>服务端出鞭轮转序</summary>
        private readonly List<int> lashOrder = [];

        public PlanteraWhipBarrageState() {
        }

        public override void OnEnter(PlanteraStateContext context) {
            base.OnEnter(context);
            lashOrder.Clear();

            if (!VaultUtils.isClient) {
                foreach (var tent in context.Tentacles) {
                    PlanteraTentacleAI.CommandIdle(tent);
                }
            }
        }

        public override IPlanteraState OnUpdate(PlanteraStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;

            //本体缓慢侧移施压，保持中距
            float side = (float)System.Math.Sin(Timer * 0.014f);
            SetSuspension(context, new Vector2(side * 160f, -30f), PlanteraDirector.DriftSpeedP2 * 0.85f, 0.06f);
            context.GlowPulse = 0.45f;

            //触手全灭直接收招
            if (context.Tentacles.Count == 0 && Timer > 30) {
                if (!VaultUtils.isClient) {
                    return new PlanteraCanopyState();
                }
                return null;
            }

            //逐根下鞭令(服务端裁决)
            int gap = LashGap(context);
            if (Timer % gap == 0 && Counter < TotalLashes(context) && !VaultUtils.isClient
                && context.Tentacles.Count > 0) {
                NPC tent = PickNextTentacle(context);
                if (tent != null) {
                    float angle = (player.Center + player.velocity * 10f - npc.Center).ToRotation()
                        + Main.rand.NextFloat(-0.14f, 0.14f);
                    PlanteraTentacleAI.CommandWhip(tent, angle);
                    //鞭线预警
                    PlanteraTelegraphLine.Spawn(npc, npc.Center, angle,
                        PlanteraDirector.WhipTelegraphFrames, 420f);
                    Counter++;
                }
            }
            if (Timer % gap == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.5f, Pitch = 0.1f, MaxInstances = 5 }, npc.Center);
            }

            //全部鞭完+收尾间隔→退场
            int endTime = (TotalLashes(context) + 2) * gap + 40;
            if (Timer >= endTime && !VaultUtils.isClient) {
                return new PlanteraCanopyState();
            }
            return null;
        }

        /// <summary>洗牌轮转选触手，避免连点同一根</summary>
        private NPC PickNextTentacle(PlanteraStateContext context) {
            if (lashOrder.Count == 0) {
                for (int i = 0; i < context.Tentacles.Count; i++) {
                    lashOrder.Add(context.Tentacles[i].whoAmI);
                }
                for (int i = lashOrder.Count - 1; i > 0; i--) {
                    int j = Main.rand.Next(i + 1);
                    (lashOrder[i], lashOrder[j]) = (lashOrder[j], lashOrder[i]);
                }
            }

            while (lashOrder.Count > 0) {
                int who = lashOrder[0];
                lashOrder.RemoveAt(0);
                NPC tent = Main.npc[who];
                //跳过已死/正在鞭的
                if (tent.active && tent.type == NPCID.PlanterasTentacle
                    && (int)tent.ai[2] == PlanteraTentacleAI.ModeIdle) {
                    return tent;
                }
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
