using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Rendering;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.States
{
    /// <summary>棱镜齐射：王冠光束打上棱晶节点，折射成瞄准碎晶弹</summary>
    [InnoVault.StateMachines.VaultState((int)QueenSlimeStateIndex.PrismVolley, typeof(QueenSlimeStateContext))]
    internal class QueenPrismVolleyState : QueenSlimeStateBase
    {
        public override string StateName => "PrismVolley";
        public override QueenSlimeStateIndex StateIndex => QueenSlimeStateIndex.PrismVolley;

        private const int ChargeTime = 48;
        private const int VolleyInterval = 52;

        private int VolleyCount(QueenSlimeStateContext ctx) => ctx.IsPhase2 ? 4 : 3;

        public QueenPrismVolleyState() {
        }

        public override void OnEnter(QueenSlimeStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            DisableContactDamage(npc);
            if (context.IsPhase2) {
                npc.noGravity = true;
                npc.noTileCollide = true;
            }
        }

        public override IQueenSlimeState OnUpdate(QueenSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;
            DisableContactDamage(npc);
            FaceTarget(npc, player.Center);

            //占位运动：一阶段站定呼吸，二阶段悬停
            if (context.IsPhase2) {
                Vector2 anchor = player.Center + new Vector2(npc.Center.X < player.Center.X ? -360f : 360f, -300f);
                QueenMotion.SpringHover(npc, anchor, 0.011f, 0.09f, 17f);
                QueenMotion.FlightLean(npc);
                context.PoseCommand = 5;
            }
            else if (npc.velocity.Y == 0f) {
                npc.velocity.X *= 0.82f;
            }

            //蓄力段：布晶+聚能
            if (Timer <= ChargeTime) {
                float p = Timer / (float)ChargeTime;
                context.SetChargeState(1, p);
                context.PrismShimmer = p * 0.8f;

                if (Timer == 6 && !VaultUtils.isClient) {
                    EnsureNodes(context);
                }
                if (Timer == 10) {
                    SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.8f, Pitch = 0.1f }, npc.Center);
                }
                if (!VaultUtils.isServer && Timer % 3 == 0) {
                    QueenMotion.ChargeGatherFX(QueenSlimeRenderHelper.CrownAnchor(npc), p, 130f, p * 0.8f);
                }
                return null;
            }

            //齐射段：逐节点点亮折射
            int volleyTimer = Timer - ChargeTime;
            int volleyIndex = volleyTimer / VolleyInterval;
            context.SetChargeState(1, 0.65f + 0.35f * QueenMotion.Bump(volleyTimer % VolleyInterval / (float)VolleyInterval));

            if (volleyIndex >= VolleyCount(context)) {
                //收势
                if (volleyTimer >= VolleyCount(context) * VolleyInterval + 26) {
                    if (!VaultUtils.isClient) {
                        return context.IsPhase2 ? new QueenAerialBalletState() : new QueenBallroomStepState(1);
                    }
                }
                context.ResetChargeState();
                return null;
            }

            //每轮开火帧(服务端)
            if (volleyTimer % VolleyInterval == 0 && !VaultUtils.isClient) {
                FireFeederBeam(context, volleyIndex);
            }

            //一阶段间隙小步腾挪
            if (!context.IsPhase2 && volleyTimer % VolleyInterval == 30 && npc.velocity.Y == 0f) {
                int dir = player.Center.X > npc.Center.X ? -1 : 1;
                QueenMotion.LaunchHop(npc, dir * 4.2f, -6.4f);
                context.PushSquash(0.35f);
            }

            return null;
        }

        /// <summary>补齐棱晶节点(服务端)，绕玩家三角布位</summary>
        private static void EnsureNodes(QueenSlimeStateContext context) {
            List<NPC> nodes = context.CollectPrismNodes();
            int want = 3;
            if (nodes.Count >= want) {
                return;
            }
            Vector2[] slots = [
                context.Target.Center + new Vector2(-380f, -280f),
                context.Target.Center + new Vector2(380f, -280f),
                context.Target.Center + new Vector2(0f, -460f),
            ];
            bool[] slotUsed = new bool[slots.Length];
            foreach (var n in nodes) {
                int s = (int)n.ai[1];
                if (s >= 0 && s < slotUsed.Length) {
                    slotUsed[s] = true;
                }
            }
            int alive = nodes.Count;
            for (int i = 0; i < slots.Length && alive < want; i++) {
                if (slotUsed[i]) {
                    continue;
                }
                NPC node = QueenMotion.SpawnMinion(context.Npc, NPCID.QueenSlimeMinionBlue,
                    QueenMinionRole.PrismNode, i, slots[i], QueenSlimeMinionAI.PrismNodeLife());
                if (node != null) {
                    node.ai[3] = 600f;//自毁寿命
                    node.netUpdate = true;
                    alive++;
                }
            }
        }

        /// <summary>发射王冠→节点馈送光束(服务端)</summary>
        private void FireFeederBeam(QueenSlimeStateContext context, int volleyIndex) {
            List<NPC> nodes = context.CollectPrismNodes();
            if (nodes.Count == 0) {
                return;
            }
            NPC node = nodes[volleyIndex % nodes.Count];
            Projectile.NewProjectile(context.Npc.GetSource_FromAI(), QueenSlimeRenderHelper.CrownAnchor(context.Npc),
                Vector2.Zero, ModContent.ProjectileType<QueenPrismBeamProj>(), QueenPrismBeamProj.BeamDamage, 0f, Main.myPlayer,
                context.Npc.whoAmI, node.whoAmI, (int)QueenPrismBeamProj.BeamMode.FeederVolley);
        }

        public override void OnExit(QueenSlimeStateContext context) {
            base.OnExit(context);
            NPC npc = context.Npc;
            if (!context.IsPhase2 || !context.Phase2Unfolded) {
                npc.noGravity = false;
                npc.noTileCollide = false;
            }
        }
    }
}
