using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.States
{
    /// <summary>
    /// 凝胶分裂召唤：深蹲聚能→跃身→体表分裂甩出仆从(可见的出生)→列队亮相→首轮同步问候齐射。
    /// 一阶段补凝胶伴舞，二阶段补伴舞+翼卫；由枢纽在缺员且冷却好时插队触发。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)QueenSlimeStateIndex.GelSplitSummon, typeof(QueenSlimeStateContext))]
    internal class QueenGelSplitSummonState : QueenSlimeStateBase
    {
        public override string StateName => "GelSplitSummon";
        public override QueenSlimeStateIndex StateIndex => QueenSlimeStateIndex.GelSplitSummon;

        #region 节奏常量
        private const int GatherTime = 22;
        private const int SplitFrame = GatherTime + 12;   //34 跃至高点分裂
        private const int VolleyStart = 66;               //问候齐射起拍
        private const int VolleyStagger = 7;              //各仆从错拍
        private const int TotalTime = 118;
        /// <summary>触发后冷却</summary>
        internal const int CooldownAfter = 540;
        #endregion

        /// <summary>枢纽插队条件：冷却好+缺员</summary>
        internal static bool NeedSummon(QueenSlimeStateContext ctx) {
            if (ctx.SummonCooldown > 0) {
                return false;
            }
            int dancers = ctx.CountMinions(QueenMinionRole.GelDancer);
            if (!ctx.Phase2Unfolded) {
                return dancers < 2;
            }
            int escorts = ctx.CountMinions(QueenMinionRole.WingedEscort);
            return dancers < 2 || escorts < 2;
        }

        public QueenGelSplitSummonState() {
        }

        public override void OnEnter(QueenSlimeStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            DisableContactDamage(npc);
            if (context.Phase2Unfolded) {
                npc.noGravity = true;
                npc.noTileCollide = true;
            }
            if (!VaultUtils.isClient) {
                context.SummonCooldown = CooldownAfter;
            }
        }

        public override IQueenSlimeState OnUpdate(QueenSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;
            DisableContactDamage(npc);
            FaceTarget(npc, player.Center);

            //幕一 深蹲聚能：体表向心光尘
            if (Timer <= GatherTime) {
                float p = Timer / (float)GatherTime;
                if (context.Phase2Unfolded) {
                    npc.velocity *= 0.86f;
                    context.PoseCommand = 5;
                }
                else {
                    npc.velocity.X *= 0.7f;
                    context.PoseCommand = 3;
                }
                context.PushSquash(-0.55f * QueenMotion.LateSnap(p, 3));
                context.SetChargeState(3, p * 0.7f);
                context.PrismShimmer = p * 0.8f;
                if (!VaultUtils.isServer && Timer % 3 == 0) {
                    QueenMotion.ChargeGatherFX(npc.Center, p, 130f, p * 0.6f);
                    QueenMotion.GelSplashBurst(npc.Bottom, 0.4f, 2);
                }
                if (Timer == 4) {
                    SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.7f, Pitch = -0.1f }, npc.Center);
                }
                return null;
            }

            //跃身帧
            if (Timer == GatherTime + 1) {
                if (context.Phase2Unfolded) {
                    npc.velocity = new Vector2(0f, -7.5f);
                }
                else {
                    npc.velocity = new Vector2(npc.direction * 1.6f, -12.6f);
                }
                if (!VaultUtils.isClient) {
                    npc.netUpdate = true;
                }
                context.PushSquash(0.62f);
                context.PoseCommand = 1;
                SoundEngine.PlaySound(SoundID.Item154 with { Volume = 0.7f, Pitch = 0.5f }, npc.Center);
            }

            //分裂帧：体表甩出仆从(可见的出生弧线)
            if (Timer == SplitFrame) {
                DoSplit(context);
            }

            //幕二 列队亮相：仆从各自归位(随从AI弹簧收拢)，皇后回落/悬停
            if (Timer > SplitFrame && Timer < VolleyStart) {
                if (context.Phase2Unfolded) {
                    Vector2 anchor = player.Center + new Vector2(npc.Center.X < player.Center.X ? -320f : 320f, -300f);
                    QueenMotion.SpringHover(npc, anchor, 0.014f, 0.1f, 16f);
                    context.PoseCommand = 5;
                }
                context.PrismShimmer = Math.Max(context.PrismShimmer, 0.5f);
            }

            //幕三 问候齐射：仆从按槽位错拍开火，皇后收尾补扇(服务端)
            if (Timer >= VolleyStart && !VaultUtils.isClient) {
                FireGreetingVolley(context);
            }

            if (Timer >= TotalTime) {
                if (!VaultUtils.isClient) {
                    return context.Phase2Unfolded
                        ? new QueenAerialBalletState()
                        : new QueenBallroomStepState(1);
                }
                return null;
            }
            return null;
        }

        /// <summary>分裂：补齐缺员仆从并沿弧线甩出，配碎晶与凝胶爆点</summary>
        private void DoSplit(QueenSlimeStateContext context) {
            NPC npc = context.Npc;
            context.PushSquash(0.7f);
            QueenMotion.Shake(npc.Center, 5f, 12, "QueenSplit");
            SoundEngine.PlaySound(SoundID.Item167 with { Volume = 0.75f, Pitch = 0.3f }, npc.Center);
            SoundEngine.PlaySound(SoundID.NPCDeath1 with { Volume = 0.6f, Pitch = 0.4f }, npc.Center);

            if (!VaultUtils.isServer) {
                QueenMotion.GelSplashBurst(npc.Center, 1.5f, 12);
                QueenMotion.CrystalShatterBurst(npc.Center, 1f, 0.3f, playSound: false);
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_DWave>(npc.Center, Vector2.Zero,
                        QueenMotion.PrismHue(i * 0.33f) * 0.75f, 0.25f + i * 0.1f)?
                        .Configure(new Vector2(1f, 1f), 0f, 1.2f + i * 0.4f, 18);
                }
            }

            if (VaultUtils.isClient) {
                return;
            }

            //补齐伴舞(两阶段都要)
            int dancers = context.CountMinions(QueenMinionRole.GelDancer);
            for (int i = dancers; i < 2; i++) {
                Vector2 fling = new Vector2((i == 0 ? -1 : 1) * 7.5f, -5.5f);
                QueenMotion.SpawnMinion(npc, NPCID.QueenSlimeMinionPink, QueenMinionRole.GelDancer,
                    i, npc.Center + fling.SafeNormalize(Vector2.UnitX) * 26f, QueenSlimeMinionAI.DancerLife(), fling);
            }
            //二阶段补翼卫
            if (context.Phase2Unfolded) {
                int escorts = context.CountMinions(QueenMinionRole.WingedEscort);
                for (int i = escorts; i < 2; i++) {
                    Vector2 fling = new Vector2((i == 0 ? -1 : 1) * 5.5f, -8f);
                    QueenMotion.SpawnMinion(npc, NPCID.QueenSlimeMinionPurple, QueenMinionRole.WingedEscort,
                        i, npc.Center + fling.SafeNormalize(Vector2.UnitX) * 30f, QueenSlimeMinionAI.EscortLife(), fling);
                }
            }
            npc.netUpdate = true;
        }

        /// <summary>问候齐射(服务端逐帧检拍)：伴舞槽位 0/1 → 翼卫槽位 0/1 → 皇后扇</summary>
        private void FireGreetingVolley(QueenSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            int beat = (int)Timer - VolleyStart;

            foreach (var n in Main.ActiveNPCs) {
                int slotBeat = -1;
                if (context.IsMyMinion(n, QueenMinionRole.GelDancer)) {
                    slotBeat = (int)n.ai[1] * VolleyStagger;
                }
                else if (context.IsMyMinion(n, QueenMinionRole.WingedEscort)) {
                    slotBeat = (2 + (int)n.ai[1]) * VolleyStagger;
                }
                if (slotBeat >= 0 && beat == slotBeat) {
                    QueenMotion.SpawnSpikeFan(n, n.Center, player.Center, 1, 0f, 8.6f,
                        QueenCrystalSpikeProj.SpikeDamage, n.whoAmI * 0.17f % 1f);
                }
            }

            //皇后收尾三刺扇
            if (beat == 4 * VolleyStagger) {
                QueenMotion.SpawnSpikeFan(npc, npc.Center, player.Center, 3, 0.22f, 8.8f,
                    QueenCrystalSpikeProj.SpikeDamage, 0.55f);
            }
        }

        public override void OnExit(QueenSlimeStateContext context) {
            base.OnExit(context);
            NPC npc = context.Npc;
            DisableContactDamage(npc);
            if (!context.Phase2Unfolded) {
                npc.noGravity = false;
                npc.noTileCollide = false;
            }
        }
    }
}
