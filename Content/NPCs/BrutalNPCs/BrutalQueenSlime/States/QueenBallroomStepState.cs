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
    /// 一阶段枢纽·迅捷芭蕾步：短促蹲身→矮弧飞扑(过顶急坠)→重踏落点碎晶飞溅，间隔选招。
    /// 缺员且冷却好时插队分裂召唤。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)QueenSlimeStateIndex.BallroomStep, typeof(QueenSlimeStateContext))]
    internal class QueenBallroomStepState : QueenSlimeStateBase
    {
        public override string StateName => "BallroomStep";
        public override QueenSlimeStateIndex StateIndex => QueenSlimeStateIndex.BallroomStep;

        private const int BreathTime = 6;
        private const int CrouchTime = 9;

        private int hopsPlanned;
        private int hopsDone;
        /// <summary>0落地待机 1蹲身预备 2腾空</summary>
        private int stage;
        private int stageTimer;
        private bool apexPassed;

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
            apexPassed = false;
            if (context.IsAsuraMode) {
                hopsPlanned = Math.Max(hopsPlanned, 3);
            }
        }

        public override IQueenSlimeState OnUpdate(QueenSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;
            stageTimer++;

            switch (stage) {
                case 0://落地待机，短呼吸拍
                    DisableContactDamage(npc);
                    if (npc.velocity.Y == 0f) {
                        npc.velocity.X *= 0.78f;
                        FaceTarget(npc, player.Center);
                        if (stageTimer >= BreathTime) {
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
                    npc.velocity.X *= 0.68f;
                    context.PoseCommand = 3;
                    context.PushSquash(-0.42f * QueenMotion.LateSnap(stageTimer / (float)CrouchTime, 3));
                    if (!VaultUtils.isServer && stageTimer % 3 == 0) {
                        QueenMotion.GelSplashBurst(npc.Bottom, 0.4f, 2);
                    }
                    if (stageTimer >= CrouchTime) {
                        DoLaunch(context);
                        stage = 2;
                        stageTimer = 0;
                        apexPassed = false;
                    }
                    break;

                case 2://腾空：矮弧飞扑，过顶急坠
                    EnableContactDamageIfFast(npc, 9f);
                    context.PushSquash(0.42f * MathHelper.Clamp(Math.Abs(npc.velocity.Y) / 13f, 0f, 1f));
                    context.AfterimageBoost = Math.Max(context.AfterimageBoost, 0.5f);

                    //空中微调向目标(末跳更强)
                    float steer = hopsDone == hopsPlanned - 1 ? 0.26f : 0.16f;
                    if ((npc.direction == 1 && npc.velocity.X < 12.5f) || (npc.direction == -1 && npc.velocity.X > -12.5f)) {
                        npc.velocity.X += steer * npc.direction;
                    }

                    //过顶急坠(灵动的收势，落点更早更狠)
                    if (npc.velocity.Y > -0.5f) {
                        apexPassed = true;
                    }
                    if (apexPassed && npc.velocity.Y > 0f) {
                        npc.velocity.Y += 0.3f;
                        if (npc.velocity.Y > 14.5f) {
                            npc.velocity.Y = 14.5f;
                        }
                    }

                    //足尖闪星
                    if (!VaultUtils.isServer && stageTimer % 3 == 0) {
                        PRTLoader.NewParticle<PRT_Sparkle>(npc.Bottom + Main.rand.NextVector2Circular(16f, 6f),
                            -npc.velocity * 0.08f, Color.White, Main.rand.NextFloat(0.5f, 0.85f))?
                            .Configure(QueenMotion.PrismHue(hopsDone * 0.27f), 16, 0.06f, 1.2f);
                    }

                    //落地判定(碰撞标记兜底)
                    if (stageTimer > 10 && (npc.velocity.Y == 0f || npc.collideY)) {
                        DoLanding(context);
                        stage = 0;
                        stageTimer = 0;
                    }
                    //超时保险(卡在斜坡等)
                    if (stageTimer > 140) {
                        stage = 0;
                        stageTimer = 0;
                        hopsDone++;
                    }
                    break;
            }

            return null;
        }

        /// <summary>起跳：矮弧快扑，末跳为跨越大跳</summary>
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
                vx = MathHelper.Clamp(dx * 0.028f, -14f, 14f) + dir * 4.8f;
                vy = context.IsAsuraMode ? -14.6f : -13.4f;
            }
            else {
                //矮弧快扑(-9.7避开重力步长整数倍误判落地)
                vx = MathHelper.Clamp(dx * 0.024f, -10f, 10f) + dir * 3.6f;
                vy = -9.7f;
            }
            QueenMotion.LaunchHop(npc, vx, vy);
            context.PushSquash(0.55f);
            context.PoseCommand = 1;

            SoundEngine.PlaySound(SoundID.Item154 with { Volume = 0.55f, Pitch = 0.4f + hopsDone * 0.12f, MaxInstances = 3 }, npc.Center);
            QueenMotion.GelSplashBurst(npc.Bottom, 0.8f, 5);
        }

        /// <summary>落点：重踏环纹+碎晶飞溅(第二跳起，向两肩上方溅出直刺)</summary>
        private void DoLanding(QueenSlimeStateContext context) {
            NPC npc = context.Npc;
            hopsDone++;

            context.PushSquash(-0.55f);
            QueenMotion.LandingRingFX(npc.Bottom, 1f, hopsDone * 0.3f);
            QueenMotion.Shake(npc.Center, 3.2f, 9, "QueenBallroomLand");
            SoundEngine.PlaySound(SoundID.Item167 with { Volume = 0.5f, Pitch = 0.55f, MaxInstances = 3 }, npc.Center);

            //第二跳起落点碎晶飞溅(上-外斜向直刺，材质化出生自带前摇)
            if (!VaultUtils.isClient && hopsDone >= 2) {
                int spikes = context.IsAsuraMode ? 5 : 3;
                for (int i = 0; i < spikes; i++) {
                    //以竖直向上为基准向两侧展开
                    float lean = MathHelper.Lerp(-0.85f, 0.85f, spikes == 1 ? 0.5f : i / (float)(spikes - 1));
                    Vector2 vel = (-Vector2.UnitY).RotatedBy(lean) * 8.2f;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Bottom - new Vector2(0f, 14f), vel,
                        Terraria.ModLoader.ModContent.ProjectileType<QueenCrystalSpikeProj>(),
                        QueenCrystalSpikeProj.SpikeDamage, 0f, Main.myPlayer,
                        (int)QueenCrystalSpikeProj.Mode.Aimed, 0f, (hopsDone * 0.2f + i * 0.14f) % 1f);
                }
            }
        }

        /// <summary>选招(服务端)：缺员先分裂召唤，否则手排环</summary>
        private IQueenSlimeState ChooseNextAttack(QueenSlimeStateContext context) {
            if (QueenGelSplitSummonState.NeedSummon(context)) {
                return new QueenGelSplitSummonState();
            }
            IQueenSlimeState[] cycle = [
                new QueenSpikeRondoState(),
                new QueenCrystalWaltzState(),
                new QueenPrismVolleyState(),
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
