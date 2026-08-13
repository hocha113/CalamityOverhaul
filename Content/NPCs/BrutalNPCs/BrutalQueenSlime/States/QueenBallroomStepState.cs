using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.States
{
    /// <summary>一阶段枢纽：芭蕾步跳跃，蹲身预备→足尖腾跃→轻盈落点，间隔选招</summary>
    [InnoVault.StateMachines.VaultState((int)QueenSlimeStateIndex.BallroomStep, typeof(QueenSlimeStateContext))]
    internal class QueenBallroomStepState : QueenSlimeStateBase
    {
        public override string StateName => "BallroomStep";
        public override QueenSlimeStateIndex StateIndex => QueenSlimeStateIndex.BallroomStep;

        private const int CrouchTime = 14;

        private int hopsPlanned;
        private int hopsDone;
        /// <summary>0落地待机 1蹲身预备 2腾空</summary>
        private int stage;
        private int stageTimer;

        public QueenBallroomStepState() : this(2) {
        }

        public QueenBallroomStepState(int hops) {
            hopsPlanned = hops;
        }

        public override void OnEnter(QueenSlimeStateContext context) {
            base.OnEnter(context);
            context.Npc.noGravity = false;
            context.Npc.noTileCollide = false;
            stage = 0;
            stageTimer = 0;
            if (context.IsDeathMode) {
                hopsPlanned = Math.Max(hopsPlanned, 3);
            }
        }

        public override IQueenSlimeState OnUpdate(QueenSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;
            stageTimer++;

            switch (stage) {
                case 0://落地待机，短暂呼吸拍
                    DisableContactDamage(npc);
                    if (npc.velocity.Y == 0f) {
                        npc.velocity.X *= 0.8f;
                        FaceTarget(npc, player.Center);
                        if (stageTimer >= 10) {
                            //跳够了→选招
                            if (hopsDone >= hopsPlanned) {
                                if (!VaultUtils.isClient) {
                                    return ChooseNextAttack(context);
                                }
                                return null;
                            }
                            stage = 1;
                            stageTimer = 0;
                        }
                    }
                    break;

                case 1://蹲身预备，可读前摇
                    DisableContactDamage(npc);
                    npc.velocity.X *= 0.7f;
                    context.PoseCommand = 3;
                    context.PushSquash(-0.38f * QueenMotion.LateSnap(stageTimer / (float)CrouchTime, 3));
                    if (!VaultUtils.isServer && stageTimer % 4 == 0) {
                        QueenMotion.GelSplashBurst(npc.Bottom, 0.4f, 2);
                    }
                    if (stageTimer >= CrouchTime) {
                        DoLaunch(context);
                        stage = 2;
                        stageTimer = 0;
                    }
                    break;

                case 2://腾空，末跳大跨越
                    EnableContactDamageIfFast(npc, 9f);
                    context.PushSquash(0.42f * MathHelper.Clamp(Math.Abs(npc.velocity.Y) / 14f, 0f, 1f));
                    context.AfterimageBoost = Math.Max(context.AfterimageBoost, 0.45f);

                    //空中微调向目标
                    float steer = hopsDone == hopsPlanned - 1 ? 0.22f : 0.13f;
                    if ((npc.direction == 1 && npc.velocity.X < 11f) || (npc.direction == -1 && npc.velocity.X > -11f)) {
                        npc.velocity.X += steer * npc.direction;
                    }

                    //足尖闪星
                    if (!VaultUtils.isServer && stageTimer % 3 == 0) {
                        PRTLoader.NewParticle<PRT_Sparkle>(npc.Bottom + Main.rand.NextVector2Circular(16f, 6f),
                            -npc.velocity * 0.08f, Color.White, Main.rand.NextFloat(0.5f, 0.85f))?
                            .Configure(QueenMotion.PrismHue(hopsDone * 0.27f), 16, 0.06f, 1.2f);
                    }

                    //上升结束转下落姿态由帧机自动处理；落地判定(碰撞标记兜底)
                    if (stageTimer > 12 && (npc.velocity.Y == 0f || npc.collideY)) {
                        DoLanding(context);
                        stage = 0;
                        stageTimer = 0;
                    }
                    //超时保险(卡在斜坡等)
                    if (stageTimer > 150) {
                        stage = 0;
                        stageTimer = 0;
                        hopsDone++;
                    }
                    break;
            }

            return null;
        }

        /// <summary>起跳：末跳为跨越大跳</summary>
        private void DoLaunch(QueenSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            bool grandJete = hopsDone == hopsPlanned - 1;

            float dx = player.Center.X - npc.Center.X;
            int dir = dx >= 0f ? 1 : -1;
            npc.direction = npc.spriteDirection = dir;

            float vx;
            float vy;
            if (grandJete) {
                //大跨越：越过玩家头顶
                vx = MathHelper.Clamp(dx * 0.026f, -13.5f, 13.5f) + dir * 4.6f;
                vy = context.IsDeathMode ? -15.5f : -14f;
            }
            else {
                //-10.6避开重力步长0.3的整数倍(顶点恰好归零误判落地)
                vx = MathHelper.Clamp(dx * 0.02f, -9f, 9f) + dir * 2.8f;
                vy = -10.6f;
            }
            QueenMotion.LaunchHop(npc, vx, vy);
            context.PushSquash(0.5f);
            context.PoseCommand = 1;

            SoundEngine.PlaySound(SoundID.Item154 with { Volume = 0.55f, Pitch = 0.4f + hopsDone * 0.12f, MaxInstances = 3 }, npc.Center);
            QueenMotion.GelSplashBurst(npc.Bottom, 0.8f, 5);
        }

        /// <summary>落点：轻环+凝胶珍珠圈(第二跳起)</summary>
        private void DoLanding(QueenSlimeStateContext context) {
            NPC npc = context.Npc;
            hopsDone++;

            context.PushSquash(-0.5f);
            QueenMotion.LandingRingFX(npc.Bottom, 1f, hopsDone * 0.3f);
            QueenMotion.Shake(npc.Center, 3f, 9, "QueenBallroomLand");
            SoundEngine.PlaySound(SoundID.Item167 with { Volume = 0.5f, Pitch = 0.55f, MaxInstances = 3 }, npc.Center);

            //第二跳起落点绽放珍珠(慢速弧线，可读)
            if (!VaultUtils.isClient && hopsDone >= 2) {
                int pearls = context.IsDeathMode ? 7 : 5;
                for (int i = 0; i < pearls; i++) {
                    float angle = MathHelper.Pi + MathHelper.Pi * (i + 0.5f) / pearls;
                    Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(6.5f, 8f);
                    vel.Y = -Math.Abs(vel.Y) * 0.9f - 2f;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Bottom - new Vector2(0f, 12f), vel,
                        ModContent.ProjectileType<QueenShardProj>(), QueenShardProj.PearlDamage, 0f, Main.myPlayer,
                        (int)QueenShardProj.Mode.Pearl, 0f, hopsDone * 0.2f);
                }
            }
        }

        /// <summary>选招(服务端)</summary>
        private IQueenSlimeState ChooseNextAttack(QueenSlimeStateContext context) {
            IQueenSlimeState[] cycle = [
                new QueenPrismVolleyState(),
                new QueenCrystalWaltzState(),
                new QueenGelMeteorRainState(),
            ];
            IQueenSlimeState next = cycle[context.AttackPhaseIndex % cycle.Length];
            context.AttackPhaseIndex++;
            return next;
        }

        public override void OnExit(QueenSlimeStateContext context) {
            base.OnExit(context);
            DisableContactDamage(context.Npc);
        }
    }
}
