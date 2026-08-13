using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.States
{
    /// <summary>
    /// 半血变身：光向她体内坍缩→棱彩位移到玩家头顶→全屏棱彩爆发，
    /// 真形态自白光中展开；爆发后两环慢弹作为重启的第一句
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)EmpressStateIndex.PhaseTransition, typeof(EmpressStateContext))]
    internal class EmpressPhaseTransitionState : EmpressStateBase
    {
        public override string StateName => "EmpressPhaseTransition";
        public override EmpressStateIndex StateIndex => EmpressStateIndex.PhaseTransition;

        private const int BurstFrame = 90;
        private const int TotalTime = 200;

        public override void OnEnter(EmpressStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            //公平阀：清弹幕，变身舞台干净
            EmpressCast.ClearHostileProjectiles(npc);
            npc.velocity *= 0.4f;
            npc.damage = 0;
            PlayLocal(SoundID.Item161 with { Volume = 1f }, npc.Center);
        }

        public override IEmpressState OnUpdate(EmpressStateContext context) {
            NPC npc = context.Npc;
            Player target = context.Target;
            Timer++;

            npc.velocity *= 0.95f;
            npc.damage = 0;
            //原版窗口：30~170无敌
            npc.dontTakeDamage = Timer >= 30 && Timer <= 170;

            //变身姿态：原版绘制在此语义下自带白闪与8向幻影
            context.Pose = EmpressPose.Transform;
            context.PoseTimer = Timer;

            if (Timer < BurstFrame) {
                //坍缩：光向她体内收敛，越来越快，最后4帧静默
                float p = Timer / (float)BurstFrame;
                context.SetChargeState(3, p);
                if (!VaultUtils.isServer && Timer < BurstFrame - 4 && Main.rand.NextFloat() < 0.3f + p * 0.55f) {
                    float hue = Main.rand.NextFloat();
                    Vector2 spawn = npc.Center + Main.rand.NextVector2CircularEdge(300f, 320f) * (1f - p * 0.4f);
                    PRTLoader.NewParticle<PRT_EmpressSpark>(spawn, (npc.Center - spawn) * (0.045f + p * 0.05f),
                        EmpressMotion.Prism(hue, 0.7f), Main.rand.NextFloat(0.7f, 1.2f))?.Configure(18, hue);
                }
            }

            if (Timer == BurstFrame) {
                //棱彩位移到玩家头顶（原版变身瞬移点），全屏爆发
                if (target.Alives()) {
                    EmpressMotion.PrismStep(npc, target.Center + new Vector2(0f, -250f));
                    if (!VaultUtils.isClient) {
                        npc.netUpdate = true;
                    }
                }
                //服务端写入二阶段位（原版ai[3]语义，客户端经同步读出）
                if (!VaultUtils.isClient) {
                    npc.ai[3] = (int)npc.ai[3] | 1;
                    npc.netUpdate = true;
                    //二阶段循环表从头开始
                    context.AttackCounter = 0;
                }
                EmpressCast.Radiance(npc, npc.Center, 640f, 36, 0.7f);
                EmpressCast.Radiance(npc, npc.Center, 300f, 26, 0.2f);
                EmpressMotion.Shake(npc.Center, 8f, 24);
                if (!VaultUtils.isServer) {
                    EmpressScreenFX.PushPrismPulse(npc.Center, 1f, 38);
                    //真形态自白光展开：光蝶十二方绽出+径向光屑
                    for (int i = 0; i < 12; i++) {
                        float bh = i / 12f;
                        PRTLoader.NewParticle<PRT_EmpressButterfly>(npc.Center,
                            (MathHelper.TwoPi / 12f * i).ToRotationVector2() * Main.rand.NextFloat(3f, 6.5f),
                            EmpressMotion.Prism(bh, 0.7f), Main.rand.NextFloat(0.7f, 1.1f))?.Configure(80, bh);
                    }
                    for (int i = 0; i < 18; i++) {
                        float sh = Main.rand.NextFloat();
                        PRTLoader.NewParticle<PRT_EmpressSpark>(npc.Center, VaultUtils.RandVr(4f, 13f),
                            EmpressMotion.Prism(sh, 0.72f), Main.rand.NextFloat(0.8f, 1.4f))?.Configure(24, sh);
                    }
                }
                PlayLocal(SoundID.Item161 with { Volume = 1f, Pitch = -0.15f }, npc.Center);
                PlayLocal(SoundID.Item163 with { Volume = 1f, Pitch = 0.1f }, npc.Center);
            }

            //爆发后的第一句：两环慢弹，四缺口，庆典的余音
            if (Timer == BurstFrame + 24 || Timer == BurstFrame + 52) {
                PlayLocal(SoundID.Item164 with { Volume = 0.8f, Pitch = 0.15f }, npc.Center);
                if (!VaultUtils.isClient) {
                    int ringIdx = Timer == BurstFrame + 24 ? 0 : 1;
                    int bolts = 18;
                    for (int i = 0; i < bolts; i++) {
                        float angle = MathHelper.TwoPi / bolts * i + ringIdx * 0.17f;
                        //四个宽缺口（对角方位）：庆典不为杀
                        float rel = System.Math.Abs(MathHelper.WrapAngle(angle - MathHelper.PiOver4)) % MathHelper.PiOver2;
                        if (rel < 0.24f || rel > MathHelper.PiOver2 - 0.24f) {
                            continue;
                        }
                        Vector2 dir = angle.ToRotationVector2();
                        EmpressCast.Bolt(npc, npc.Center + dir * 60f, dir * (3.6f + ringIdx * 0.9f),
                            context.BoltDamage, 0, angle / MathHelper.TwoPi + ringIdx * 0.5f);
                    }
                }
            }

            if (Timer >= TotalTime) {
                npc.dontTakeDamage = false;
                return new EmpressConnectorState();
            }
            return null;
        }

        public override void OnExit(EmpressStateContext context) {
            base.OnExit(context);
            context.Npc.dontTakeDamage = false;
        }
    }
}
