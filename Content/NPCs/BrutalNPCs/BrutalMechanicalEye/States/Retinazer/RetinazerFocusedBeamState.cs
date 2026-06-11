using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Projectiles.Boss.MechanicalEye;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Retinazer
{
    /// <summary>
    /// 激光眼二阶段死亡射线扫射状态：
    /// 就位→锁定蓄力(准心预警)→释放持续性宽死亡射线并以受限角速度追踪玩家→过热硬直。
    /// 射线方向由npc.rotation驱动，玩家须持续走位摆脱切割
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)TwinsStateIndex.RetinazerFocusedBeam, typeof(TwinsStateContext))]
    internal class RetinazerFocusedBeamState : TwinsStateBase
    {
        public override string StateName => "RetinazerFocusedBeam";
        public override TwinsStateIndex StateIndex => TwinsStateIndex.RetinazerFocusedBeam;

        private int ApproachPhase => Context.IsDeathMode ? 26 : 32;
        private int ChargePhase => Context.IsDeathMode ? 48 : 58;
        private int BeamPhase => Context.IsDeathMode ? 105 : 95;
        private const int RecoveryPhase = 38;

        private int TotalDuration => ApproachPhase + ChargePhase + BeamPhase + RecoveryPhase;

        /// <summary>
        /// 射线追踪角速度(弧度/帧)——刻意限制，确保可以被跑动摆脱
        /// </summary>
        private float TrackTurnRate => Context.IsDeathMode ? 0.019f : 0.014f;

        private TwinsStateContext Context;
        private int comboStep;
        private Vector2 anchorPos;
        private bool anchorLocked;
        private bool beamFired;

        public RetinazerFocusedBeamState() : this(0) {
        }

        public RetinazerFocusedBeamState(int currentComboStep) {
            comboStep = currentComboStep;
        }

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            Context = context;
            anchorLocked = false;
            beamFired = false;
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;

            if (Timer <= ApproachPhase) {
                ExecuteApproachPhase(npc, player);
            }
            else if (Timer <= ApproachPhase + ChargePhase) {
                ExecuteChargePhase(npc, player);
            }
            else if (Timer <= ApproachPhase + ChargePhase + BeamPhase) {
                ExecuteBeamPhase(npc, player);
            }
            else {
                ExecuteRecoveryPhase(npc, player);
            }

            //状态结束
            if (Timer >= TotalDuration) {
                //独眼模式下切换到狂暴状态
                if (context.IsSoloRageMode) {
                    return new RetinazerSoloRageState();
                }
                return new RetinazerVerticalBarrageState(comboStep);
            }

            return null;
        }

        /// <summary>
        /// 就位阶段：弹簧飞抵玩家斜上方射击位
        /// </summary>
        private void ExecuteApproachPhase(NPC npc, Player player) {
            float progress = Timer / (float)ApproachPhase;

            float side = npc.Center.X < player.Center.X ? -1f : 1f;
            Vector2 targetPos = player.Center + new Vector2(side * 400f, -260f);
            TwinsMotion.SpringHover(npc, targetPos, 0.022f, 0.1f);
            FaceTarget(npc, player.Center);

            Context.SetChargeState(6, progress * 0.25f);
        }

        /// <summary>
        /// 锁定蓄力阶段：准心预警，机体绷紧颤抖，能量向瞳孔汇聚
        /// </summary>
        private void ExecuteChargePhase(NPC npc, Player player) {
            int phaseTimer = Timer - ApproachPhase;
            float progress = phaseTimer / (float)ChargePhase;

            //锁定锚点
            if (!anchorLocked) {
                anchorLocked = true;
                anchorPos = npc.Center;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.35f, Volume = 0.9f }, npc.Center);
                }
            }

            TwinsMotion.SpringHover(npc, anchorPos, 0.045f, 0.2f);
            if (progress > 0.55f && !VaultUtils.isServer) {
                npc.position += Main.rand.NextVector2Circular(2f, 2f) * progress;
            }

            //持续瞄准玩家
            FaceTarget(npc, player.Center);

            Context.SetChargeState(6, 0.25f + progress * 0.75f);

            //能量向瞳孔汇聚
            if (phaseTimer % 2 == 0) {
                Vector2 muzzle = npc.Center + (npc.rotation + MathHelper.PiOver2).ToRotationVector2() * 46f;
                TwinsMotion.ChargeGatherFX(muzzle, false, progress, 90f);
            }

            //蓄力完成预告:收束闪光
            if (phaseTimer == ChargePhase - 4 && !VaultUtils.isServer) {
                PRTLoader.NewParticle<PRT_DWave>(npc.Center, Vector2.Zero, TwinsMotion.RetinColor, 0.18f)?
                    .Configure(Vector2.One, 0f, 0.9f, 12);
                SoundEngine.PlaySound(SoundID.Item92 with { Pitch = 0.3f, Volume = 1f }, npc.Center);
            }
        }

        /// <summary>
        /// 射线阶段：释放死亡射线，受限角速度追踪玩家，机体承受持续后坐
        /// </summary>
        private void ExecuteBeamPhase(NPC npc, Player player) {
            int phaseTimer = Timer - ApproachPhase - ChargePhase;

            Context.ResetChargeState();

            //发射死亡射线
            if (!beamFired) {
                beamFired = true;
                if (!VaultUtils.isClient) {
                    int damage = Context.IsDeathMode ? 50 : 44;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                        ModContent.ProjectileType<RetinazerDeathRay>(), damage, 0f, Main.myPlayer,
                        npc.whoAmI, BeamPhase + 4, 0f);
                    //开火瞬间的屏幕扭曲冲击波
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                        ModContent.ProjectileType<TwinsSupernovaBlast>(), 0, 0f, Main.myPlayer, 1f, 0f);
                }
                if (!VaultUtils.isServer) {
                    TwinsMotion.Shake(npc.Center, 8f, 16);
                }
            }

            //受限角速度追踪玩家(rotation驱动射线方向)
            float targetDirRot = (player.Center - npc.Center).ToRotation();
            TwinsMotion.RotateToward(npc, targetDirRot, TrackTurnRate);

            //射线后坐:机体被缓缓推离射线方向
            Vector2 beamDir = (npc.rotation + MathHelper.PiOver2).ToRotationVector2();
            npc.velocity = npc.velocity * 0.9f - beamDir * 0.55f;
            if (npc.velocity.Length() > 6f) {
                npc.velocity = npc.velocity.SafeNormalize(Vector2.Zero) * 6f;
            }
            Context.PushDashVisuals(0.3f, 0.4f);

            //持续震感
            if (phaseTimer % 14 == 0 && !VaultUtils.isServer) {
                TwinsMotion.Shake(npc.Center, 1.8f, 6);
            }
        }

        /// <summary>
        /// 过热硬直阶段：射线收束后排气下沉，给予输出窗口
        /// </summary>
        private void ExecuteRecoveryPhase(NPC npc, Player player) {
            int phaseTimer = Timer - ApproachPhase - ChargePhase - BeamPhase;

            npc.velocity *= 0.9f;
            npc.velocity.Y += 0.1f;
            FaceTarget(npc, player.Center);

            //过热排气
            if (!VaultUtils.isServer && phaseTimer % 5 == 0) {
                PRTLoader.NewParticle<PRT_Smoke>(npc.Center + Main.rand.NextVector2Circular(22, 22),
                    new Vector2(0, -1.4f) + Main.rand.NextVector2Circular(0.7f, 0.7f),
                    TwinsMotion.RetinColor * 0.45f, Main.rand.NextFloat(0.6f, 1f))?.Configure(32, 0.45f, 0.02f, false, 0f);
            }
        }
    }
}
