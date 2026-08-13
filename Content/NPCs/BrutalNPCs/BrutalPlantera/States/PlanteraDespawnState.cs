using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Rendering;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.States
{
    /// <summary>脱战撤离：钩爪脱力，本体坠入地底消失</summary>
    [InnoVault.StateMachines.VaultState((int)PlanteraStateIndex.Despawn, typeof(PlanteraStateContext))]
    internal class PlanteraDespawnState : PlanteraStateBase
    {
        public override string StateName => "Despawn";
        public override PlanteraStateIndex StateIndex => PlanteraStateIndex.Despawn;

        public PlanteraDespawnState() {
        }

        public override void OnEnter(PlanteraStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
            context.Npc.dontTakeDamage = true;
            context.Npc.damage = 0;

            if (!VaultUtils.isClient) {
                foreach (var hook in context.Hooks) {
                    PlanteraHookAI.GoLimp(hook);
                }
                foreach (var tent in context.Tentacles) {
                    PlanteraTentacleAI.GoLimp(tent);
                }
            }
        }

        public override IPlanteraState OnUpdate(PlanteraStateContext context) {
            NPC npc = context.Npc;

            context.SkipDefaultMovement = true;
            npc.dontTakeDamage = true;
            npc.damage = 0;

            //坠向地底+渐隐
            npc.velocity.X *= 0.95f;
            npc.velocity.Y = Math.Min(npc.velocity.Y + 0.32f, 14f);
            npc.alpha = Math.Min(npc.alpha + 3, 255);
            context.GlowPulse = 0f;
            context.RotationMode = 2;

            Timer++;

            if (Timer > 130 && !VaultUtils.isClient) {
                PlanteraAI.DespawnParts();
                npc.active = false;
                npc.netUpdate = true;
            }

            return null;
        }
    }

    /// <summary>
    /// 凋亡演出：锁血→荧光乱闪枯萎→钩爪逐根崩断(身体随之坠荡)→
    /// 自由落体→触地花粉新星，孢光升腾散尽→真死放行
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PlanteraStateIndex.Death, typeof(PlanteraStateContext))]
    internal class PlanteraDeathState : PlanteraStateBase
    {
        public override string StateName => "Death";
        public override PlanteraStateIndex StateIndex => PlanteraStateIndex.Death;

        private const int ConvulseEnd = 45;
        private const int Snap1 = 60;
        private const int Snap2 = 95;
        private const int Snap3 = 130;
        private const int FallStart = 132;
        private const int ForceFinale = 250;
        private const int FinaleHold = 70;

        private bool landed;
        private int finaleFrame = -1;

        public PlanteraDeathState() {
        }

        public override void OnEnter(PlanteraStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
            context.DeathPerformanceFinished = false;
            landed = false;
            finaleFrame = -1;

            NPC npc = context.Npc;
            npc.dontTakeDamage = true;
            npc.damage = 0;
            if (npc.life < 1) {
                npc.life = 1;
            }

            if (!VaultUtils.isClient) {
                //触手脱力，孢子雷全数哑火
                foreach (var tent in context.Tentacles) {
                    PlanteraTentacleAI.GoLimp(tent);
                }
                foreach (var n in Main.ActiveNPCs) {
                    if (n.type == NPCID.Spore) {
                        n.life = 0;
                        n.HitEffect();
                        n.active = false;
                        n.netUpdate = true;
                    }
                }
            }

            SoundEngine.PlaySound(SoundID.NPCDeath1 with { Volume = 1f, Pitch = -0.7f }, npc.Center);
            SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.8f, Pitch = -0.85f }, npc.Center);
        }

        public override IPlanteraState OnUpdate(PlanteraStateContext context) {
            NPC npc = context.Npc;

            //全程锁血无害
            context.SkipDefaultMovement = true;
            npc.dontTakeDamage = true;
            npc.damage = 0;
            if (npc.life < 1) {
                npc.life = 1;
            }

            Timer++;

            //枯萎进度喂给主控绘制(颜色抽干)
            context.DeathWilt = MathHelper.Clamp(Timer / (float)ForceFinale, 0f, 1f);

            if (Timer <= ConvulseEnd) {
                UpdateConvulse(context);
            }
            else if (Timer < FallStart) {
                UpdateSnaps(context);
            }
            else if (finaleFrame < 0) {
                UpdateFall(context);
            }

            //终幕结算
            if (finaleFrame > 0 && Timer >= finaleFrame + FinaleHold && !VaultUtils.isClient) {
                context.DeathPerformanceFinished = true;
                PlanteraAI.DespawnParts();
                npc.dontTakeDamage = false;
                npc.life = 0;
                npc.HitEffect();
                npc.checkDead();
                npc.netUpdate = true;
            }

            return null;
        }

        /// <summary>幕一 痉挛：荧光乱闪衰减</summary>
        private void UpdateConvulse(PlanteraStateContext context) {
            NPC npc = context.Npc;
            float t = Timer / (float)ConvulseEnd;

            npc.velocity *= 0.9f;
            context.RotationMode = 0;
            //荧光不规则频闪，逐渐熄灭
            float strobe = (float)(Math.Sin(Timer * 1.7f) * Math.Sin(Timer * 0.83f + 1.3f));
            context.GlowPulse = Math.Max(0f, (0.7f - t * 0.4f) * (0.4f + 0.6f * Math.Abs(strobe)));

            if (!VaultUtils.isServer) {
                npc.position += Main.rand.NextVector2Circular(1.8f, 1.8f);
                if (Timer % 9 == 0) {
                    PlanteraRenderHelper.SpawnPetalBurst(
                        npc.Center + Main.rand.NextVector2Circular(30f, 30f), 2, 3f, context.IsPhase2);
                    SoundEngine.PlaySound(SoundID.NPCHit1 with {
                        Volume = 0.5f, Pitch = -0.5f, MaxInstances = 4
                    }, npc.Center);
                }
            }
        }

        /// <summary>幕二 钩爪逐根崩断，身体坠荡</summary>
        private void UpdateSnaps(PlanteraStateContext context) {
            NPC npc = context.Npc;

            //按拍崩断
            if (Timer == Snap1 || Timer == Snap2 || Timer == Snap3) {
                int snapIndex = Timer == Snap1 ? 0 : Timer == Snap2 ? 1 : 2;
                if (!VaultUtils.isClient && snapIndex < context.Hooks.Count) {
                    PlanteraHookAI.GoLimp(context.Hooks[snapIndex]);
                }
                //坠荡冲量
                npc.velocity.Y += 3.5f + snapIndex * 1.5f;
                npc.velocity.X += Main.rand.NextFloat(-2f, 2f);

                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 1f, Pitch = 0.3f }, npc.Center);
                    SoundEngine.PlaySound(SoundID.Grass with { Volume = 1f, Pitch = -0.6f }, npc.Center);
                    PlanteraScreenFX.CameraPunch(npc.Center, 5f + snapIndex * 2f, 12, "PlanteraSnap");
                    PlanteraRenderHelper.SpawnPetalBurst(npc.Center, 6, 5f, context.IsPhase2);
                }
            }

            //剩余悬吊松垮回拉
            Vector2 centroid = HookAliveCentroid(context);
            npc.velocity = Vector2.Lerp(npc.velocity, (centroid - npc.Center) * 0.02f, 0.05f);
            npc.velocity.Y += 0.12f;
            context.RotationMode = 2;
            npc.rotation += npc.velocity.X * 0.004f;
            context.GlowPulse = 0.12f;
        }

        /// <summary>仍在锚定的钩爪质心</summary>
        private static Vector2 HookAliveCentroid(PlanteraStateContext context) {
            Vector2 sum = Vector2.Zero;
            int count = 0;
            foreach (var hook in context.Hooks) {
                if ((int)hook.ai[2] != PlanteraHookAI.ModeLimp) {
                    sum += hook.Center;
                    count++;
                }
            }
            return count > 0 ? sum / count : context.Npc.Center - Vector2.UnitY * 60f;
        }

        /// <summary>幕三 自由落体到触地终幕</summary>
        private void UpdateFall(PlanteraStateContext context) {
            NPC npc = context.Npc;

            npc.velocity.X *= 0.99f;
            npc.velocity.Y = Math.Min(npc.velocity.Y + 0.34f, 13f);
            context.RotationMode = 2;
            npc.rotation += npc.velocity.X * 0.006f + 0.004f;
            context.GlowPulse = 0.06f;

            //坠落拖尾余光
            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                InnoVault.PRT.PRTLoader.NewParticle<PRT_PlanteraSporeMote>(
                    npc.Center + Main.rand.NextVector2Circular(30f, 30f),
                    -npc.velocity * 0.1f, PlanteraRenderHelper.GlowByPhase(context.IsPhase2) * 0.7f,
                    Main.rand.NextFloat(0.6f, 1.2f))?.SetLife(50);
            }

            //触地或超时→终幕
            if (!landed && (Collision.SolidCollision(npc.position + npc.velocity, npc.width, npc.height)
                || Timer >= ForceFinale)) {
                landed = true;
                finaleFrame = Timer;
                npc.velocity = Vector2.Zero;
                DoFinale(context);
            }
        }

        /// <summary>终幕 花粉新星："丛林呼出最后一口气"</summary>
        private void DoFinale(PlanteraStateContext context) {
            NPC npc = context.Npc;

            if (VaultUtils.isServer) {
                return;
            }

            SoundEngine.PlaySound(SoundID.NPCDeath1 with { Volume = 1.2f, Pitch = -0.4f }, npc.Center);
            SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.8f, Pitch = -0.6f }, npc.Center);
            SoundEngine.PlaySound(SoundID.Grass with { Volume = 1f, Pitch = -0.8f }, npc.Center);

            PlanteraScreenFX.CameraPunch(npc.Center, 13f, 26, "PlanteraDeathFinale");
            PlanteraScreenFX.PushFlash(npc.Center, 0.6f, 16);
            PlanteraScreenFX.PushRing(npc.Center, 820f, context.IsPhase2, 44);

            PlanteraRenderHelper.SpawnPetalBurst(npc.Center, 40, 10f, context.IsPhase2);
            PlanteraRenderHelper.SpawnSporePuff(npc.Center, 2.4f);

            //孢光升腾：亡后余晖比爆点活得久
            for (int i = 0; i < 34; i++) {
                Vector2 pos = npc.Center + Main.rand.NextVector2Circular(140f, 60f);
                InnoVault.PRT.PRTLoader.NewParticle<PRT_PlanteraSporeMote>(pos,
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.6f, 1.8f)),
                    PlanteraRenderHelper.GlowByPhase(context.IsPhase2), Main.rand.NextFloat(0.8f, 1.7f))
                    ?.SetLife(Main.rand.Next(90, 160));
            }
        }
    }
}
