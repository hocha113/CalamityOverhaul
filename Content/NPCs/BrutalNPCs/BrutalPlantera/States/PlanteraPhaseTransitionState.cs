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
    /// 蜕壳演出：清弹幕→痉挛聚能(孢子回吸)→花壳逐瓣崩裂→
    /// 壳爆蜕形，八根触手破体而出，二阶段开始
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PlanteraStateIndex.PhaseTransition, typeof(PlanteraStateContext))]
    internal class PlanteraPhaseTransitionState : PlanteraStateBase
    {
        public override string StateName => "PhaseTransition";
        public override PlanteraStateIndex StateIndex => PlanteraStateIndex.PhaseTransition;

        private const int ConvulseEnd = 60;
        private const int CrackEnd = 130;
        private const int BurstFrame = 130;
        private const int StateEnd = 185;

        private bool burstFired;

        public PlanteraPhaseTransitionState() {
        }

        public override void OnEnter(PlanteraStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
            burstFired = false;

            NPC npc = context.Npc;
            npc.dontTakeDamage = true;
            npc.damage = 0;

            if (!VaultUtils.isClient) {
                ClearMyProjectiles();
                //孢子雷全部回吸(服务端静默移除，客户端做汇流演出)
                foreach (var n in Main.ActiveNPCs) {
                    if (n.type == NPCID.Spore) {
                        n.life = 0;
                        n.active = false;
                        n.netUpdate = true;
                    }
                }
                //钩爪上提：把身体吊向高处蜕壳
                foreach (var hook in context.Hooks) {
                    Vector2 wish = npc.Center + new Vector2(Main.rand.NextFloat(-260f, 260f), -420f);
                    PlanteraHookAI.Command(hook, PlanteraHookAI.FindAnchorNear(wish, 8f, Vector2.Zero));
                }
            }

            SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.8f, Pitch = -0.5f }, npc.Center);
        }

        /// <summary>清掉本Boss的敌对弹幕(公平阀)</summary>
        private static void ClearMyProjectiles() {
            int seed = ModContent.ProjectileType<PlanteraSeed>();
            int poison = ModContent.ProjectileType<PlanteraPoisonSeed>();
            int thorn = ModContent.ProjectileType<PlanteraThornBall>();
            int cloud = ModContent.ProjectileType<PlanteraSporeCloud>();
            int beam = ModContent.ProjectileType<PlanteraVineLattice>();
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type == seed || proj.type == poison || proj.type == thorn || proj.type == cloud) {
                    proj.Kill();
                }
                else if (proj.type == beam && proj.ai[2] > -0.5f) {
                    proj.ai[2] = -1f;
                    proj.netUpdate = true;
                }
            }
        }

        public override IPlanteraState OnUpdate(PlanteraStateContext context) {
            NPC npc = context.Npc;

            context.SkipDefaultMovement = true;
            npc.damage = 0;
            Timer++;

            //被钩爪吊着缓缓上提
            Vector2 centroid = context.HookCentroid();
            npc.velocity = Vector2.Lerp(npc.velocity, (centroid - npc.Center) * 0.03f, 0.08f);

            if (Timer <= ConvulseEnd) {
                UpdateConvulse(context);
            }
            else if (Timer <= CrackEnd) {
                UpdateCrack(context);
            }
            else {
                UpdateAfterBurst(context);
            }

            if (Timer >= StateEnd && !VaultUtils.isClient) {
                return new PlanteraCanopyState();
            }
            return null;
        }

        /// <summary>幕一 痉挛聚能：孢子光尘回流身体</summary>
        private void UpdateConvulse(PlanteraStateContext context) {
            NPC npc = context.Npc;
            float t = Timer / (float)ConvulseEnd;

            npc.dontTakeDamage = true;
            context.GlowPulse = 0.3f + t * 0.6f;
            context.BodyScalePulse = (float)Math.Sin(Timer * 0.55f) * 0.04f * t;

            //痉挛位移抖动
            if (!VaultUtils.isServer) {
                npc.position += Main.rand.NextVector2Circular(t * 2.6f, t * 2.6f);

                //孢子尘从四面八方汇入
                if (Main.rand.NextBool(2)) {
                    Vector2 from = npc.Center + Main.rand.NextVector2CircularEdge(380f, 340f);
                    InnoVault.PRT.PRTLoader.NewParticle<PRT_PlanteraSporeMote>(from,
                        Vector2.Zero, PlanteraRenderHelper.SporeGreen, Main.rand.NextFloat(0.9f, 1.6f))
                        ?.Converge(npc.Center).SetLife(70);
                }
                if (Timer % 12 == 0) {
                    PlanteraScreenFX.CameraPunch(npc.Center, 1.5f + t * 2f, 10, "PlanteraMoltRumble");
                    SoundEngine.PlaySound(SoundID.NPCHit1 with {
                        Volume = 0.5f + t * 0.3f,
                        Pitch = -0.4f + t * 0.3f,
                        MaxInstances = 4
                    }, npc.Center);
                }
                PlanteraScreenFX.PushDusk(t * 0.4f);
            }
        }

        /// <summary>幕二 花壳逐瓣崩裂</summary>
        private void UpdateCrack(PlanteraStateContext context) {
            NPC npc = context.Npc;
            float t = (Timer - ConvulseEnd) / (float)(CrackEnd - ConvulseEnd);

            npc.dontTakeDamage = true;
            context.GlowPulse = 0.6f + t * 0.4f;
            context.BodyScalePulse = (float)Math.Sin(Timer * 0.8f) * 0.06f * t;

            if (!VaultUtils.isServer) {
                npc.position += Main.rand.NextVector2Circular(2f + t * 2f, 2f + t * 2f);
                PlanteraScreenFX.PushDusk(0.4f + t * 0.25f);

                //逐瓣剥落，越来越密
                int burstGap = (int)MathHelper.Lerp(14f, 6f, t);
                if (Timer % Math.Max(burstGap, 4) == 0) {
                    Vector2 edge = npc.Center + Main.rand.NextVector2CircularEdge(npc.width * 0.42f, npc.height * 0.42f);
                    PlanteraRenderHelper.SpawnPetalBurst(edge, 3 + (int)(t * 4f), 4f + t * 4f, false);
                    SoundEngine.PlaySound(SoundID.Grass with {
                        Volume = 0.6f,
                        Pitch = -0.2f + t * 0.4f,
                        MaxInstances = 5
                    }, edge);
                    PlanteraScreenFX.CameraPunch(npc.Center, 2f + t * 3f, 8, "PlanteraMoltCrack");
                }
            }
        }

        /// <summary>幕三 壳爆蜕形+触手破体</summary>
        private void UpdateAfterBurst(PlanteraStateContext context) {
            NPC npc = context.Npc;

            if (!burstFired) {
                burstFired = true;
                //阶段翻转：帧动画/配色/部件行为全部切换
                context.IsPhase2 = true;

                if (!VaultUtils.isClient) {
                    int count = context.IsAsuraMode ? 10 : 8;
                    for (int i = 0; i < count; i++) {
                        PlanteraTentacleAI.SpawnTentacle(npc, MathHelper.TwoPi * i / count);
                    }
                    npc.netUpdate = true;
                }

                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.DD2_OgreRoar with { Volume = 1.2f, Pitch = -0.35f }, npc.Center);
                    SoundEngine.PlaySound(SoundID.NPCDeath1 with { Volume = 1f, Pitch = -0.6f }, npc.Center);
                    PlanteraRenderHelper.SpawnPetalBurst(npc.Center, 46, 12f, false);
                    PlanteraRenderHelper.SpawnSporePuff(npc.Center, 2.2f);
                    PlanteraScreenFX.CameraPunch(npc.Center, 12f, 22, "PlanteraMoltBurst");
                    PlanteraScreenFX.PushFlash(npc.Center, 0.7f, 14);
                    PlanteraScreenFX.PushRing(npc.Center, 760f, true, 34);
                }
            }

            //蜕形后余韵：触手甩动尘埃落定，宽限期不追击
            float t = (Timer - BurstFrame) / (float)(StateEnd - BurstFrame);
            context.GlowPulse = MathHelper.Lerp(1f, 0.4f, t);
            npc.dontTakeDamage = Timer < BurstFrame + 26;
        }

        public override void OnExit(PlanteraStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
            NPC npc = context.Npc;
            npc.dontTakeDamage = false;
            npc.damage = npc.defDamage;
            //钩爪放回追猎
            if (!VaultUtils.isClient) {
                foreach (var hook in context.Hooks) {
                    PlanteraHookAI.Release(hook);
                }
                //蜕壳宽限：二阶段开场不许立刻上投技
                context.VineFeastCooldown = Math.Max(context.VineFeastCooldown,
                    PlanteraDirector.FeastPhaseEntryDelay);
            }
        }
    }
}
