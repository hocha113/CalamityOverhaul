using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Spazmatism
{
    /// <summary>残影连冲，蓄力→多段变向 dash+甩头火弹扇</summary>
    [InnoVault.StateMachines.VaultState((int)TwinsStateIndex.SpazmatismShadowDash, typeof(TwinsStateContext))]
    internal class SpazmatismShadowDashState : TwinsStateBase
    {
        public override string StateName => "SpazmatismShadowDash";
        public override TwinsStateIndex StateIndex => TwinsStateIndex.SpazmatismShadowDash;

        /// <summary>蓄力聚集阶段时长</summary>
        private int GatherPhase => Context.IsDeathMode ? 38 : 46;

        /// <summary>每段全速冲刺时长</summary>
        private int SegmentDashTime => Context.IsDeathMode ? 14 : 16;

        /// <summary>每段急停甩头时长</summary>
        private int SegmentWhipTime => Context.IsDeathMode ? 9 : 11;

        /// <summary>每段复位喘息时长，无伤，蓄力预警回涨作下段起手预告</summary>
        private int SegmentSettleTime => Context.IsDeathMode ? 11 : 16;

        /// <summary>冲刺段数</summary>
        private int SegmentCount => Context.IsDeathMode ? 4 : 3;

        /// <summary>恢复阶段时长</summary>
        private int RecoveryPhase => Context.IsDeathMode ? 20 : 26;

        private int SegmentTime => SegmentDashTime + SegmentWhipTime + SegmentSettleTime;
        private int TotalDuration => GatherPhase + SegmentCount * SegmentTime + RecoveryPhase;

        private float DashSpeed => Context.IsDeathMode ? 36f : 32f;

        private TwinsStateContext Context;
        private int comboStep;
        private int lastSegment = -1;

        public SpazmatismShadowDashState() : this(0) {
        }

        public SpazmatismShadowDashState(int currentComboStep) {
            comboStep = currentComboStep;
        }

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            Context = context;
            lastSegment = -1;
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //检测独眼狂暴模式触发
            if (context.SoloRageJustTriggered) {
                return new SpazmatismSoloRageState();
            }

            Timer++;

            if (Timer <= GatherPhase) {
                ExecuteGatherPhase(npc, player);
            }
            else if (Timer <= GatherPhase + SegmentCount * SegmentTime) {
                ExecuteDashSegments(npc, player);
            }
            else {
                ExecuteRecoveryPhase(npc, player);
            }

            if (Timer >= TotalDuration) {
                if (context.IsSoloRageMode) {
                    return new SpazmatismSoloRageState();
                }
                return new SpazmatismFlameChaseState(comboStep);
            }

            return null;
        }

        /// <summary>蓄力聚集，斜上方悬停+能量内聚</summary>
        private void ExecuteGatherPhase(NPC npc, Player player) {
            float progress = Timer / (float)GatherPhase;

            Vector2 targetPos = player.Center + new Vector2(npc.Center.X < player.Center.X ? -380 : 380, -260);
            TwinsMotion.SpringHover(npc, targetPos, 0.018f, 0.09f);
            FaceTarget(npc, player.Center);

            //设置蓄力状态(影分身预警)
            Context.SetChargeState(8, progress);

            //能量内聚
            if (Timer % 2 == 0) {
                TwinsMotion.ChargeGatherFX(npc.Center, true, progress, 110f);
            }

            //聚集音效
            if (Timer == 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.3f, Volume = 0.8f }, npc.Center);
            }

            if (Timer == GatherPhase - 2 && !VaultUtils.isServer) {
                PRTLoader.NewParticle<PRT_DWave>(npc.Center, Vector2.Zero, TwinsMotion.SpazColor, 0.2f)?
                    .Configure(Vector2.One, 0f, 1.2f, 14);
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.3f }, npc.Center);
            }
        }

        /// <summary>多段变向 dash，段首爆发，段内微弧，衔接甩头+火弹扇</summary>
        private void ExecuteDashSegments(NPC npc, Player player) {
            int phaseTimer = Timer - GatherPhase;
            int segment = (phaseTimer - 1) / SegmentTime;
            int inSegment = (phaseTimer - 1) % SegmentTime;

            //段首变向爆发
            if (segment != lastSegment) {
                lastSegment = segment;
                Context.ResetChargeState();

                Vector2 predicted = TwinsMotion.PredictTarget(player, npc.Center, DashSpeed, 0.55f);
                Vector2 dir = (predicted - npc.Center).SafeNormalize(Vector2.UnitY);
                TwinsMotion.DashLaunch(npc, dir, DashSpeed, spazTheme: true, boomStrength: 1.1f);
                EnableContactDamageIfFast(npc);

                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item74 with { Pitch = 0.2f + segment * 0.08f, Volume = 1.1f }, npc.Center);
                    //每段残影冲刺起步天空闪雷
                    MachineEffect.TriggerSkyFlash(npc.Center, 0.6f);
                }
            }

            if (inSegment < SegmentDashTime) {
                //全速弧线
                float speed = npc.velocity.Length();
                TwinsMotion.CurveChase(npc, player.Center, speed, 0.02f);
                EnableContactDamageIfFast(npc);
                FaceVelocity(npc);
                Context.PushDashVisuals(1f, 1f);

                //炽热残影拖尾
                if (!VaultUtils.isServer) {
                    PRTLoader.NewParticle<PRT_TwinsSpark>(
                        npc.Center - npc.velocity.SafeNormalize(Vector2.Zero) * 32f + Main.rand.NextVector2Circular(13, 13),
                        -npc.velocity * 0.16f, Color.White, Main.rand.NextFloat(1.1f, 1.8f))?.Configure(16, 1);
                }
            }
            else if (inSegment < SegmentDashTime + SegmentWhipTime) {
                //甩头段
                DisableContactDamage(npc);
                TwinsMotion.BrakeAndWhip(npc, player.Center, 0.74f, 0.36f);
                Context.PushDashVisuals(0.4f, 0.8f);

                //甩头残影+火扇
                if (inSegment == SegmentDashTime) {
                    if (!VaultUtils.isServer) {
                        //残影爆发环
                        PRTLoader.NewParticle<PRT_DWave>(npc.Center, Vector2.Zero, TwinsMotion.SpazColor, 0.18f)?
                            .Configure(Vector2.One, 0f, 0.9f, 12);
                        for (int i = 0; i < 12; i++) {
                            PRTLoader.NewParticle<PRT_TwinsSpark>(npc.Center,
                                VaultUtils.RandVr(5, 12), Color.White, Main.rand.NextFloat(1.2f, 2f))?.Configure(18, 1);
                        }
                        TwinsMotion.Shake(npc.Center, 3.5f, 7);
                    }

                    //向身后扇形抛出火球，封锁折返路线
                    if (!VaultUtils.isClient) {
                        Vector2 backDir = -npc.velocity.SafeNormalize(Vector2.UnitY);
                        for (int i = -1; i <= 1; i++) {
                            Vector2 vel = backDir.RotatedBy(i * 0.42f) * 7.5f;
                            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, vel,
                                ModContent.ProjectileType<Fireball>(), 22, 0f, Main.myPlayer);
                        }
                    }
                }
            }
            else {
                //复位喘息，飘回起手位并让蓄力预警重新涨起，预告下一段
                DisableContactDamage(npc);

                int inSettle = inSegment - SegmentDashTime - SegmentWhipTime;
                float settleProgress = inSettle / (float)SegmentSettleTime;

                Vector2 resetPos = player.Center
                    + new Vector2(npc.Center.X < player.Center.X ? -380 : 380, -260);
                TwinsMotion.SpringHover(npc, resetPos, 0.016f, 0.095f, 22f);
                FaceTarget(npc, player.Center);
                Context.SetChargeState(8, settleProgress);

                if (inSettle % 3 == 0) {
                    TwinsMotion.ChargeGatherFX(npc.Center, true, settleProgress, 95f);
                }
            }
        }

        /// <summary>恢复阶段，减速面向玩家，残余火星</summary>
        private void ExecuteRecoveryPhase(NPC npc, Player player) {
            DisableContactDamage(npc);
            Context.ResetChargeState();
            npc.velocity *= 0.92f;
            FaceTarget(npc, player.Center);

            if (!VaultUtils.isServer && Timer % 4 == 0) {
                PRTLoader.NewParticle<PRT_TwinsSpark>(npc.Center + Main.rand.NextVector2Circular(20, 20),
                    new Vector2(0, -2f), Color.White, 0.9f)?.Configure(14, 1);
            }
        }

        public override void OnExit(TwinsStateContext context) {
            base.OnExit(context);
            //OnExit 禁用接触伤害
            DisableContactDamage(context.Npc);
        }
    }
}
