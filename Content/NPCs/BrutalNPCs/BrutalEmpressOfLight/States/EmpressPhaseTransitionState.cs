using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.States
{
    /// <summary>
    /// 二阶段变身（60%）：侧上方就位并收光坍缩 90f（震屏 ∝ t²）→ 位移到玩家头顶全屏爆发 → 45f 定格让爆炸落地
    /// → 两环慢光球（四缺口）作为重启的第一句
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)EmpressStateIndex.PhaseTransition, typeof(EmpressStateContext))]
    internal class EmpressPhaseTransitionState : EmpressStateBase
    {
        public override string StateName => "EmpressPhaseTransition";
        public override EmpressStateIndex StateIndex => EmpressStateIndex.PhaseTransition;

        private const int BurstFrame = 90;
        private const int HoldEnd = BurstFrame + 45;
        private const int TotalTime = 230;

        public override void OnEnter(EmpressStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            //公平阀：退潮清弹，变身舞台干净
            EmpressCast.ClearHostileProjectiles(npc);
            npc.velocity *= 0.4f;
            npc.damage = 0;
            PlayLocal(SoundID.Item161 with { Volume = 1f }, npc.Center);
        }

        public override IEmpressState OnUpdate(EmpressStateContext context) {
            NPC npc = context.Npc;
            Player target = context.Target;
            Timer++;

            npc.damage = 0;
            //原版窗口：30~170 无敌
            npc.dontTakeDamage = Timer >= 30 && Timer <= 170;

            context.Pose = EmpressPose.Transform;
            context.PoseTimer = Timer;

            if (Timer < BurstFrame) {
                //侧上方就位 (∓375,-200)，光向体内收敛，越来越快，最后 4 帧静默
                float p = Timer / (float)BurstFrame;
                if (target.Alives() && Timer > 5) {
                    Vector2 dest = target.Center + new Vector2(target.Center.X > npc.Center.X ? -375f : 375f, -200f);
                    npc.velocity = npc.velocity * 0.7f + (dest - npc.Center) * 0.04f;
                }
                context.SetChargeState(3, p);
                EmpressMotion.Shake(npc.Center, p * p * 2.2f, 4);
                if (!VaultUtils.isServer && Timer < BurstFrame - 4 && Main.rand.NextFloat() < 0.3f + p * 0.55f) {
                    float hue = Main.rand.NextFloat();
                    Vector2 spawn = npc.Center + Main.rand.NextVector2CircularEdge(300f, 320f) * (1f - p * 0.4f);
                    PRTLoader.NewParticle<PRT_EmpressSpark>(spawn, (npc.Center - spawn) * (0.045f + p * 0.05f),
                        EmpressMotion.FormColor(hue, context.DayFormBlend, 0.7f), Main.rand.NextFloat(0.7f, 1.2f))?.Configure(18, hue, context.DayFormBlend);
                }
            }
            else if (Timer < HoldEnd) {
                //定格：爆发后一动不动，让爆炸落地
                npc.velocity *= 0.8f;
            }
            else {
                npc.velocity *= 0.95f;
            }

            if (Timer == BurstFrame) {
                if (target.Alives()) {
                    EmpressMotion.PrismStep(npc, target.Center + new Vector2(0f, -250f), context.DayFormBlend);
                    if (!VaultUtils.isClient) {
                        npc.netUpdate = true;
                    }
                }
                if (!VaultUtils.isClient) {
                    npc.ai[3] = (int)npc.ai[3] | 1;
                    npc.netUpdate = true;
                    context.AttackCounter = 0;
                }
                EmpressCast.Bloom(npc, npc.Center, 640f, 36, 0.7f);
                EmpressCast.Bloom(npc, npc.Center, 300f, 26, 0.2f);
                EmpressMotion.Shake(npc.Center, 12f, 24);
                if (!VaultUtils.isServer) {
                    EmpressScreenFX.PushPrismPulse(npc.Center, 1f, 38);
                    EmpressScreenFX.PushImpactDim(0.3f);
                    for (int i = 0; i < 12; i++) {
                        float bh = i / 12f;
                        PRTLoader.NewParticle<PRT_EmpressButterfly>(npc.Center,
                            (MathHelper.TwoPi / 12f * i).ToRotationVector2() * Main.rand.NextFloat(3f, 6.5f),
                            EmpressMotion.FormColor(bh, context.DayFormBlend, 0.7f), Main.rand.NextFloat(0.7f, 1.1f))?.Configure(80, bh, context.DayFormBlend);
                    }
                    EmpressMotion.SparkBurst(npc.Center, Vector2.UnitY, 18, 4f, 13f, context.DayFormBlend, MathHelper.Pi);
                }
                PlayLocal(SoundID.Item161 with { Volume = 1f, Pitch = -0.15f }, npc.Center);
                PlayLocal(SoundID.Item163 with { Volume = 1f, Pitch = 0.1f }, npc.Center);
            }

            //定格后的第一句：两环慢光球，四个宽缺口，庆典不为杀
            if (Timer == HoldEnd + 10 || Timer == HoldEnd + 38) {
                PlayLocal(SoundID.Item164 with { Volume = 0.8f, Pitch = 0.15f }, npc.Center);
                if (!VaultUtils.isClient) {
                    int ringIdx = Timer == HoldEnd + 10 ? 0 : 1;
                    int bolts = 18;
                    for (int i = 0; i < bolts; i++) {
                        float angle = MathHelper.TwoPi / bolts * i + ringIdx * 0.17f;
                        float rel = System.Math.Abs(MathHelper.WrapAngle(angle - MathHelper.PiOver4)) % MathHelper.PiOver2;
                        if (rel < 0.24f || rel > MathHelper.PiOver2 - 0.24f) {
                            continue;
                        }
                        Vector2 dir = angle.ToRotationVector2();
                        EmpressCast.Bolt(npc, npc.Center + dir * 60f, dir * (3.6f + ringIdx * 0.9f), context.BoltDamage, EmpressBoltMode.Straight);
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
