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
    /// 藤蔓格栅：三钩爪外扩围出三角，钩间架起可破坏藤墙重塑走位空间；
    /// 本体沿格栅周界滑轨压近，配合种子扇面逼位。打断一根梁=打开生路
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PlanteraStateIndex.VineLattice, typeof(PlanteraStateContext))]
    internal class PlanteraVineLatticeState : PlanteraStateBase
    {
        public override string StateName => "VineLattice";
        public override PlanteraStateIndex StateIndex => PlanteraStateIndex.VineLattice;

        private const int AnchorEnd = 70;     //钩爪外扩
        private const int WeaveEnd = 130;     //织梁
        private const int PressureEnd = 430;  //周界压迫
        private const int StateEnd = 462;

        /// <summary>周界滑轨参数 0~3</summary>
        private float railPos;
        private int railDir = 1;
        private bool reversed;
        /// <summary>三根梁的已架设标记(服务端)</summary>
        private readonly bool[] beamSpawned = new bool[3];

        public PlanteraVineLatticeState() {
        }

        public override void OnEnter(PlanteraStateContext context) {
            base.OnEnter(context);
            railPos = 0f;
            railDir = 1;
            reversed = false;
            beamSpawned[0] = beamSpawned[1] = beamSpawned[2] = false;

            NPC npc = context.Npc;
            Player player = context.Target;

            //权威端派锚：绕玩家三角
            if (!VaultUtils.isClient) {
                float baseAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                for (int i = 0; i < context.Hooks.Count && i < 3; i++) {
                    float angle = baseAngle + i * MathHelper.TwoPi / 3f;
                    Vector2 wish = player.Center + angle.ToRotationVector2() * PlanteraDirector.LatticeRadius;
                    Vector2 anchor = PlanteraHookAI.FindAnchorNear(wish, 9f, Vector2.Zero);
                    PlanteraHookAI.Command(context.Hooks[i], anchor);
                }
                npc.netUpdate = true;
            }

            SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.6f, Pitch = -0.35f }, npc.Center);
        }

        public override IPlanteraState OnUpdate(PlanteraStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;

            //------ 幕一 钩爪外扩，本体退后压制 ------
            if (Timer <= AnchorEnd) {
                Vector2 away = (npc.Center - player.Center).SafeNormalize(Vector2.UnitY);
                SetSuspension(context, away * 120f, PlanteraDirector.DriftSpeedP1, 0.05f);
                FireLightSeed(context, 26);
                return null;
            }

            //------ 幕二 织梁 ------
            if (Timer <= WeaveEnd) {
                context.SkipDefaultMovement = false;
                SetSuspension(context, Vector2.Zero, PlanteraDirector.DriftSpeedP1 * 0.7f, 0.04f);
                context.SetChargeState(4, (Timer - AnchorEnd) / (float)(WeaveEnd - AnchorEnd));

                //服务端逐根架梁：到拍后持续尝试，等两端锚定(慢钩不丢梁)
                TrySpawnBeams(context, Timer - AnchorEnd);

                //织梁时脉络全亮
                if (!VaultUtils.isServer) {
                    foreach (var hook in context.Hooks) {
                        PlanteraVineRenderer.PushPulse(hook.whoAmI, 0.5f);
                    }
                }
                return null;
            }

            //------ 幕三 周界滑轨压迫 ------
            if (Timer <= PressureEnd) {
                //迟到的梁在压迫期前80帧内补架
                if (Timer < WeaveEnd + 80) {
                    TrySpawnBeams(context, Timer - AnchorEnd);
                }
                UpdateRailPressure(context);
                return null;
            }

            //------ 收梁 ------
            if (Timer == PressureEnd + 1 && !VaultUtils.isClient) {
                WitherMyBeams();
                foreach (var hook in context.Hooks) {
                    PlanteraHookAI.Release(hook);
                }
            }
            context.SkipDefaultMovement = false;
            SetSuspension(context, Vector2.Zero, PlanteraDirector.DriftSpeedP1, 0.05f);

            if (Timer >= StateEnd && !VaultUtils.isClient) {
                return new PlanteraCanopyState();
            }
            return null;
        }

        /// <summary>到拍且两端已锚定的梁架起来，服务端</summary>
        private void TrySpawnBeams(PlanteraStateContext context, int weaveTimer) {
            if (VaultUtils.isClient || context.Hooks.Count < 3) {
                return;
            }
            NPC npc = context.Npc;
            for (int i = 0; i < 3; i++) {
                if (beamSpawned[i] || weaveTimer < 6 + i * 16) {
                    continue;
                }
                NPC a = context.Hooks[i];
                NPC b = context.Hooks[(i + 1) % 3];
                if (PlanteraHookAI.IsAnchored(a) && PlanteraHookAI.IsAnchored(b)) {
                    PlanteraVineLattice.Spawn(npc, a, b, Math.Max((int)(npc.defDamage * 0.33f), 14));
                    beamSpawned[i] = true;
                }
            }
        }

        /// <summary>本体沿三角周界滑行，中段变轨一次</summary>
        private void UpdateRailPressure(PlanteraStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            if (context.Hooks.Count < 3) {
                //钩爪异常，退化成普通悬吊
                context.SkipDefaultMovement = false;
                SetSuspension(context, Vector2.Zero, PlanteraDirector.DriftSpeedP1, 0.05f);
                return;
            }

            //中段变轨(预告：闪光+咔声)
            if (!reversed && Timer > (WeaveEnd + PressureEnd) / 2) {
                reversed = true;
                railDir = -railDir;
                context.GlowPulse = 1f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = 0.1f, Volume = 0.9f }, npc.Center);
                    PlanteraRenderHelper.SpawnPetalBurst(npc.Center, 8, 5f, false);
                }
            }

            //滑轨推进：靠近玩家所在边时加速(压迫)
            float speedBoost = MathHelper.Clamp(1f - npc.Distance(player.Center) / 900f, 0f, 1f);
            railPos += railDir * (0.0055f + speedBoost * 0.004f);
            railPos = (railPos % 3f + 3f) % 3f;

            int edgeA = (int)railPos;
            int edgeB = (edgeA + 1) % 3;
            Vector2 railPoint = Vector2.Lerp(context.Hooks[edgeA].Center, context.Hooks[edgeB].Center, railPos - edgeA);

            //直控速度贴轨
            context.SkipDefaultMovement = true;
            Vector2 toRail = railPoint - npc.Center;
            npc.velocity = Vector2.Lerp(npc.velocity, toRail.SafeNormalize(Vector2.Zero)
                * Math.Min(toRail.Length() * 0.09f, 14f), 0.1f);
            context.RotationMode = 0;
            context.GlowPulse = 0.45f;

            //滑轨中的种子扇面
            if (Timer % 40 == 20 && !VaultUtils.isClient
                && Collision.CanHitLine(npc.Center, 1, 1, player.Center, 1, 1)) {
                Vector2 aim = (player.Center - npc.Center).SafeNormalize(Vector2.UnitY);
                for (int i = -1; i <= 1; i++) {
                    Vector2 vel = aim.RotatedBy(i * 0.17f) * 20f;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + aim * 46f, vel,
                        ModContent.ProjectileType<PlanteraSeed>(), PlanteraSeed.GetDamage(npc), 0f, Main.myPlayer);
                }
            }
            if (Timer % 40 == 20 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.65f, Pitch = 0.05f, MaxInstances = 5 }, npc.Center);
            }
        }

        private void FireLightSeed(PlanteraStateContext context, int gap) {
            NPC npc = context.Npc;
            Player player = context.Target;
            if (Timer % gap != 0) {
                return;
            }
            if (!VaultUtils.isClient && Collision.CanHitLine(npc.Center, 1, 1, player.Center, 1, 1)) {
                Vector2 aim = (player.Center - npc.Center).SafeNormalize(Vector2.UnitY);
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + aim * 46f, aim * 17f,
                    ModContent.ProjectileType<PlanteraSeed>(), PlanteraSeed.GetDamage(npc), 0f, Main.myPlayer);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.5f, Pitch = 0.1f, MaxInstances = 5 }, npc.Center);
            }
        }

        /// <summary>凋萎自己的梁</summary>
        private static void WitherMyBeams() {
            int beamType = ModContent.ProjectileType<PlanteraVineLattice>();
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type == beamType && proj.ai[2] > -0.5f) {
                    proj.ai[2] = -1f;
                    proj.netUpdate = true;
                }
            }
        }

        public override void OnExit(PlanteraStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
            //提前被打断也要收梁放钩
            if (!VaultUtils.isClient) {
                WitherMyBeams();
                foreach (var hook in context.Hooks) {
                    PlanteraHookAI.Release(hook);
                }
            }
        }
    }
}
