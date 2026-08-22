using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Rendering;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.States
{
    /// <summary>
    /// 凋零绽放(低血大招，一场一次)：三钩星锚锁体→吞光聚能→死寂一拍→
    /// 全屏花瓣波×3(双旋转安全门)+种子双螺旋+孢子环→力竭喘息窗
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PlanteraStateIndex.BloomNova, typeof(PlanteraStateContext))]
    internal class PlanteraBloomNovaState : PlanteraStateBase
    {
        public override string StateName => "BloomNova";
        public override PlanteraStateIndex StateIndex => PlanteraStateIndex.BloomNova;

        private const int ChargeEnd = 90;
        private const int SilenceEnd = 104;
        private const int BloomFrame = 105;
        private const int SpiralEnd = 255;
        private const int StateEnd = 380;

        private Vector2 lockPoint;
        private float spiralBase;
        private bool bloomFired;

        public PlanteraBloomNovaState() {
        }

        public override void OnEnter(PlanteraStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
            context.NovaUsed = true;
            bloomFired = false;
            lockPoint = context.Npc.Center;

            NPC npc = context.Npc;

            if (!VaultUtils.isClient) {
                //三钩最大张角星锚：把身体钉在场中央
                for (int i = 0; i < context.Hooks.Count && i < 3; i++) {
                    float angle = -MathHelper.PiOver2 + MathHelper.TwoPi * i / 3f;
                    Vector2 wish = npc.Center + angle.ToRotationVector2() * 700f;
                    PlanteraHookAI.Command(context.Hooks[i], PlanteraHookAI.FindAnchorNear(wish, 10f, Vector2.Zero));
                }
                //触手收拢
                foreach (var tent in context.Tentacles) {
                    PlanteraTentacleAI.CommandIdle(tent);
                }
                spiralBase = Main.rand.NextFloat(MathHelper.TwoPi);
                npc.ai[0] = spiralBase;
                npc.netUpdate = true;
            }

            SoundEngine.PlaySound(SoundID.Roar with { Volume = 1f, Pitch = -0.6f }, npc.Center);
        }

        public override IPlanteraState OnUpdate(PlanteraStateContext context) {
            NPC npc = context.Npc;

            context.SkipDefaultMovement = true;
            //客户端从同步槽恢复螺旋基角
            spiralBase = npc.ai[0];

            Timer++;

            //身体钉死场心(钩爪星锚的物理表达)
            npc.velocity = Vector2.Lerp(npc.velocity, (lockPoint - npc.Center) * 0.06f, 0.2f);

            if (Timer <= ChargeEnd) {
                UpdateCharge(context);
            }
            else if (Timer <= SilenceEnd) {
                UpdateSilence(context);
            }
            else {
                UpdateBloom(context);
            }

            if (Timer >= StateEnd && !VaultUtils.isClient) {
                return new PlanteraCanopyState();
            }
            return null;
        }

        /// <summary>幕一 吞光聚能：全场光尘倒灌，t²压暗</summary>
        private void UpdateCharge(PlanteraStateContext context) {
            NPC npc = context.Npc;
            float t = Timer / (float)ChargeEnd;

            npc.damage = 0;
            context.SetChargeState(3, t);
            context.GlowPulse = 0.3f + t * 0.6f;
            //蓄力收缩(临爆变小)
            context.BodyScalePulse = -t * 0.07f;

            if (!VaultUtils.isServer) {
                PlanteraScreenFX.PushDusk(t * t * 0.6f);
                //双族吸入：径向+切向卷旋(72%后剪断，尖啸前的吸气)
                PlanteraRenderHelper.SpawnChargeIntake(context, t);
                if (t < 0.72f && Main.rand.NextBool(2)) {
                    Vector2 from = npc.Center + Main.rand.NextVector2CircularEdge(520f, 480f);
                    InnoVault.PRT.PRTLoader.NewParticle<PRT_PlanteraSporeMote>(from,
                        Vector2.Zero, PlanteraRenderHelper.GlowMagenta, Main.rand.NextFloat(1f, 1.8f))
                        ?.Converge(npc.Center).SetLife(80);
                }
                //藤蔓行波全速倒灌
                foreach (var hook in context.Hooks) {
                    PlanteraVineRenderer.PushPulse(hook.whoAmI, 0.3f + t * 0.7f);
                }
                if (Timer % 10 == 0) {
                    PlanteraScreenFX.CameraPunch(npc.Center, t * t * 3f, 10, "PlanteraNovaRumble");
                }
            }
        }

        /// <summary>幕二 死寂：一切熄灭，收缩定格</summary>
        private void UpdateSilence(PlanteraStateContext context) {
            NPC npc = context.Npc;
            npc.damage = 0;
            context.GlowPulse = 0.08f;
            context.BodyScalePulse = -0.08f;
            context.ResetChargeState();

            if (Timer == ChargeEnd + 2 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.5f, Pitch = -0.9f }, npc.Center);
            }
        }

        /// <summary>幕三 绽放+双螺旋+孢子环→力竭</summary>
        private void UpdateBloom(PlanteraStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            if (!bloomFired) {
                bloomFired = true;
                DoBloomImpact(context);
            }

            //花瓣波×3 权威端按拍生成
            if (!VaultUtils.isClient) {
                if (Timer == BloomFrame || Timer == BloomFrame + 26 || Timer == BloomFrame + 52) {
                    int waveIndex = (Timer - BloomFrame) / 26;
                    float gapAngle = spiralBase + waveIndex * 1.3f;
                    float gapSpin = (waveIndex % 2 == 0 ? 1f : -1f) * 0.008f;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                        ModContent.ProjectileType<PlanteraPetalWave>(), PlanteraPetalWave.GetDamage(npc), 0f,
                        Main.myPlayer, 8.5f + waveIndex * 1.2f, gapAngle, gapSpin);
                }

                //种子双螺旋
                if (Timer > BloomFrame + 8 && Timer <= SpiralEnd && Timer % 5 == 0) {
                    float spiralAngle = spiralBase + (Timer - BloomFrame) * 0.19f;
                    for (int arm = 0; arm < 2; arm++) {
                        Vector2 dir = (spiralAngle + arm * MathHelper.Pi).ToRotationVector2();
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + dir * 50f, dir * 13.5f,
                            ModContent.ProjectileType<PlanteraSeed>(), PlanteraSeed.GetDamage(npc), 0f, Main.myPlayer);
                    }
                }

                //孢子环
                if (Timer == BloomFrame + 25) {
                    for (int i = 0; i < 10; i++) {
                        float angle = spiralBase + MathHelper.TwoPi * i / 10f;
                        PlanteraSporeAI.SpawnSpore(npc, npc.Center + angle.ToRotationVector2() * 200f,
                            angle.ToRotationVector2() * 3.2f);
                    }
                }
            }

            //演出与节奏包络
            if (Timer <= SpiralEnd) {
                npc.damage = 0;
                context.GlowPulse = 0.9f;
                context.RotationMode = 2;
                npc.rotation += 0.02f;

                if (!VaultUtils.isServer && Timer % 5 == 0) {
                    SoundEngine.PlaySound(SoundID.Item17 with {
                        Volume = 0.5f,
                        Pitch = 0.3f,
                        MaxInstances = 8
                    }, npc.Center);
                }
            }
            else {
                //力竭喘息：低垂暗淡，给玩家输出窗(奖励)
                float t = (Timer - SpiralEnd) / (float)(StateEnd - SpiralEnd);
                npc.damage = 0;
                context.GlowPulse = MathHelper.Lerp(0.1f, 0.3f, t);
                context.BodyScalePulse = (float)Math.Sin(Timer * 0.09f) * 0.02f;
                context.RotationMode = 0;

                if (Timer == SpiralEnd + 1 && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCDeath1 with { Volume = 0.7f, Pitch = -0.5f }, npc.Center);
                    PlanteraRenderHelper.SpawnSporePuff(npc.Center, 1.6f);
                }
            }
        }

        /// <summary>绽放冲击帧：本战唯一的满屏时刻</summary>
        private void DoBloomImpact(PlanteraStateContext context) {
            NPC npc = context.Npc;
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.DD2_OgreRoar with { Volume = 1.3f, Pitch = 0.1f }, npc.Center);
            SoundEngine.PlaySound(SoundID.Item74 with { Volume = 1f, Pitch = -0.4f }, npc.Center);
            PlanteraRenderHelper.SpawnPetalBurst(npc.Center, 60, 14f, true);
            PlanteraScreenFX.CameraPunch(npc.Center, 16f, 30, "PlanteraNovaBloom");
            PlanteraScreenFX.PushFlash(npc.Center, 1f, 20);
            PlanteraScreenFX.PushRing(npc.Center, 900f, true, 40);
            PlanteraScreenFX.PushRing(npc.Center, 560f, true, 30);
        }

        public override void OnExit(PlanteraStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
            context.Npc.damage = context.Npc.defDamage;
            if (!VaultUtils.isClient) {
                foreach (var hook in context.Hooks) {
                    PlanteraHookAI.Release(hook);
                }
            }
        }
    }
}
