using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.States
{
    /// <summary>
    /// 二阶段蜕变演出(60%)：蜂群双环蜂盾护体→蜕甲蓄力(末段静默)→蜂盾整环化镖炸散+女王怒辉觉醒<br/>
    /// 机制讲关系：亲卫用身体挡在蜕变的女王身前
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)QueenBeeStateIndex.PhaseTransition, typeof(QueenBeeStateContext))]
    internal class QBPhaseTransitionState : QueenBeeStateBase
    {
        public override string StateName => "PhaseTransition";
        public override QueenBeeStateIndex StateIndex => QueenBeeStateIndex.PhaseTransition;

        #region 节奏常量
        private const int GatherTime = 44;    //亲卫集结
        private const int MoltTime = 150;     //蜕甲蓄力
        private const int ReleaseFrame = GatherTime + MoltTime;   //194 炸散帧
        private const int TotalTime = ReleaseFrame + 52;
        #endregion

        public override void OnEnter(QueenBeeStateContext context) {
            base.OnEnter(context);
            QueenBeeMotion.RoarBurst(context.Npc.Center, 1.2f);
        }

        public override IQueenBeeState OnUpdate(QueenBeeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;

            //全程无害且短暂免伤(亲卫护体的机制表达)
            npc.dontTakeDamage = Timer < ReleaseFrame;

            //缓缓升到玩家上空正中
            Vector2 moltPos = player.Center + new Vector2(0f, -360f);
            QueenBeeMotion.SpringHover(npc, moltPos, 0.011f, 0.1f, 15f);
            FaceTarget(npc, player.Center);

            //亲卫集结成双环蜂盾
            if (Timer <= GatherTime) {
                context.Swarm.Declare(SwarmFormation.Shield, npc.Center, Vector2.UnitX);
                context.Swarm.PushSnap(2.4f);
                context.Swarm.PushRibbon(0.7f);
                //集结窗口急速补员
                if (!VaultUtils.isClient && Timer % 8 == 0) {
                    context.Swarm.ServerTopUp(24, 3);
                }
                return null;
            }

            //蜕甲蓄力
            if (Timer < ReleaseFrame) {
                float p = (Timer - GatherTime) / (float)MoltTime;
                context.Swarm.Declare(SwarmFormation.Shield, npc.Center, Vector2.UnitX,
                    //蜂盾随蓄力缓缓收紧
                    MathHelper.Lerp(1.1f, 0.82f, p));
                context.Swarm.PushRibbon(0.6f + p * 0.4f);
                context.SetChargeState(3, p);
                QueenBeeMotion.ChargeGatherFX(npc.Center, p, 130f);

                //升调蜂鸣，末28%静默(尖叫前的吸气)
                if (p < 0.72f && Timer % 24 == 0) {
                    QueenBeeMotion.WingHum(npc.Center, 0.35f + p * 0.3f, -0.4f + p * 0.8f);
                }
                //蜕甲蜡屑
                if (!VaultUtils.isServer && p > 0.3f && Main.rand.NextBool(5)) {
                    PRTLoader.NewParticle<PRT_WaxChip>(npc.Center + Main.rand.NextVector2Circular(30f, 26f),
                        Main.rand.NextVector2Circular(1.5f, 1f) + Vector2.UnitY * 1.2f,
                        QueenBeeMotion.WaxPale, Main.rand.NextFloat(0.7f, 1.1f));
                }
                return null;
            }

            //炸散帧：蜂盾整环化镖radial出射+怒吼
            if (Timer == ReleaseFrame) {
                context.Swarm.LaunchRadial(0, SwarmDirector.MaxBees - 1, npc.Center, 13f);
                QueenBeeMotion.RoarBurst(npc.Center, 1.4f);
                QueenBeeMotion.Shake(npc.Center, 8f, 16);
                if (!VaultUtils.isServer) {
                    //暖白爆点(≤2帧的过曝)+扩散环
                    PRTLoader.NewParticle<PRT_DWave>(npc.Center, Vector2.Zero,
                        new Color(255, 235, 180), 0.5f)?.Configure(Vector2.One, 0f, 2.4f, 18);
                    PRTLoader.NewParticle<PRT_DWave>(npc.Center, Vector2.Zero,
                        QueenBeeMotion.AmberDeep, 0.35f)?.Configure(Vector2.One, 0f, 1.7f, 14);
                    for (int i = 0; i < 20; i++) {
                        PRTLoader.NewParticle<PRT_BeeGlint>(npc.Center + Main.rand.NextVector2Circular(60f, 60f),
                            Main.rand.NextVector2Circular(4f, 4f), QueenBeeMotion.HoneyGold,
                            Main.rand.NextFloat(1f, 1.8f));
                    }
                }
                SoundEngine.PlaySound(SoundID.Zombie125 with { Volume = 1.1f, Pitch = 0.4f }, npc.Center);
                return null;
            }

            //觉醒余韵：怒辉常驻(控制器由IsPhase2驱动)，蜂群回巢
            context.Swarm.PushRibbon(0.35f);
            if (Timer >= TotalTime) {
                return new QBRepositionState();
            }
            return null;
        }

        public override void OnExit(QueenBeeStateContext context) {
            base.OnExit(context);
            context.Npc.dontTakeDamage = false;
        }
    }
}
