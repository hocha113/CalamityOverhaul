using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Spazmatism
{
    /// <summary>二阶段喷火追击，弧线贴近+持续扇形火舌</summary>
    [InnoVault.StateMachines.VaultState((int)TwinsStateIndex.SpazmatismFlameChase, typeof(TwinsStateContext))]
    internal class SpazmatismFlameChaseState : TwinsStateBase
    {
        public override string StateName => "SpazmatismFlameChase";
        public override TwinsStateIndex StateIndex => TwinsStateIndex.SpazmatismFlameChase;

        /// <summary>二阶段套路；合击见 ComboSignal/ComboSequence*</summary>
        private static readonly string[] ComboSequenceWithPartner =
        [
            "Phase2Dash",
            "TetherSweep",
            "ShadowDash",
            "Supernova",
            "FlameStorm",
            "Ultimate"
        ];

        private static readonly string[] ComboSequenceSolo =
        [
            "Phase2Dash",
            "ShadowDash",
            "FlameChase",
            "FlameStorm",
            "Phase2Dash"
        ];

        private float ChaseSpeed => Context.IsAsuraMode ? 11f : 9f;
        private float MaxTurnRad => Context.IsAsuraMode ? 0.052f : 0.042f;
        private int FlameDuration => Context.IsAsuraMode ? 100 : 130;
        private int FlameInterval => Context.IsAsuraMode ? 8 : 9;

        /// <summary>最小交战半径，切向盘旋不贴脸</summary>
        private float StandoffRadius => Context.IsAsuraMode ? 280f : 320f;

        /// <summary>近于此距离不点火，防糊脸</summary>
        private const float MinFireDistance = 200f;

        /// <summary>喷口需大致朝向玩家才点火</summary>
        private const float FireFacingDot = 0.35f;

        private TwinsStateContext Context;
        private int comboStep;

        /// <summary>盘旋方向，进态锁定避免逐帧翻转</summary>
        private float orbitSide = 1f;

        /// <param name="currentComboStep">二阶段固定招式循环的当前步骤索引</param>
        public SpazmatismFlameChaseState() : this(0) {
        }

        public SpazmatismFlameChaseState(int currentComboStep) {
            comboStep = currentComboStep;
        }

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            Context = context;

            //按进态时的绕行趋势定盘旋方向
            orbitSide = 1f;
            if (context.Target != null) {
                Vector2 toNpc = context.Npc.Center - context.Target.Center;
                float cross = (toNpc.X * context.Npc.velocity.Y) - (toNpc.Y * context.Npc.velocity.X);
                orbitSide = cross >= 0f ? 1f : -1f;
            }
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //检测独眼狂暴模式触发
            if (context.SoloRageJustTriggered) {
                return new SpazmatismSoloRageState();
            }

            //锚点跟合击信号，立即跟进合击
            ITwinsState comboFollow = TwinsComboCoordinator.TryFollowSignal(context);
            if (comboFollow != null) {
                return comboFollow;
            }

            //外圈盘旋切入，过近则被甩向切线外侧
            Vector2 toNpc = npc.Center - player.Center;
            float distToPlayer = toNpc.Length();
            Vector2 radial = toNpc.SafeNormalize(Vector2.UnitY);
            bool tooClose = distToPlayer < StandoffRadius;
            float swing = tooClose ? 0.9f : 0.45f;
            float ringScale = tooClose ? 1.15f : 1f;
            Vector2 chasePoint = player.Center + radial.RotatedBy(orbitSide * swing) * StandoffRadius * ringScale;

            TwinsMotion.CurveChase(npc, chasePoint, ChaseSpeed, MaxTurnRad);
            FaceVelocity(npc);
            context.PushDashVisuals(0.25f, 0.35f);

            Timer++;

            //扇形火舌，切入段喷、脱离段停火
            if (Timer % FlameInterval == 0) {
                Vector2 fireDir = npc.velocity.SafeNormalize(Vector2.UnitY);
                Vector2 toPlayerDir = (player.Center - npc.Center).SafeNormalize(Vector2.UnitY);
                bool canFire = distToPlayer >= MinFireDistance
                    && Vector2.Dot(fireDir, toPlayerDir) > FireFacingDot;

                if (canFire) {
                    if (!VaultUtils.isClient) {
                        for (int i = -1; i <= 1; i++) {
                            Vector2 fireVel = fireDir.RotatedBy(i * 0.16f + Main.rand.NextFloat(-0.05f, 0.05f))
                                * Main.rand.NextFloat(11f, 13.5f);
                            Projectile.NewProjectile(
                                npc.GetSource_FromAI(),
                                npc.Center + fireDir * 40f,
                                fireVel,
                                ModContent.ProjectileType<CursedFlameJet>(),
                                26,
                                0f,
                                Main.myPlayer
                            );
                        }
                    }
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.75f }, npc.Center);
                    }
                }
            }

            //喷口火光
            if (!VaultUtils.isServer && Timer % 3 == 0) {
                Vector2 fireDir = npc.velocity.SafeNormalize(Vector2.UnitY);
                PRTLoader.NewParticle<PRT_TwinsSpark>(npc.Center + fireDir * 42f,
                    fireDir * 3f + Main.rand.NextVector2Circular(1f, 1f),
                    Color.White, Main.rand.NextFloat(0.9f, 1.4f))?.Configure(12, 1);
            }

            //喷火结束，按固定套路切换到下一招式
            if (Timer >= FlameDuration) {
                if (context.IsSoloRageMode) {
                    return new SpazmatismSoloRageState();
                }

                return GetNextComboState();
            }

            return null;
        }

        /// <summary>按固定套路取下一状态</summary>
        private ITwinsState GetNextComboState() {
            bool hasPartner = HasPartner();
            string[] sequence = hasPartner ? ComboSequenceWithPartner : ComboSequenceSolo;
            string nextMove = sequence[comboStep % sequence.Length];
            int nextStep = comboStep + 1;

            return nextMove switch {
                "Phase2Dash" => new SpazmatismPhase2DashPrepareState(0, nextStep),
                "ShadowDash" => new SpazmatismShadowDashState(nextStep),
                "FlameChase" => new SpazmatismFlameChaseState(nextStep),
                "FlameStorm" => new SpazmatismFlameStormState(nextStep),
                "TetherSweep" => TwinsComboCoordinator.InitiateCombo(Context, TwinsStateIndex.TwinsTetherSweep, nextStep),
                "Supernova" => TwinsComboCoordinator.InitiateCombo(Context, TwinsStateIndex.TwinsCombinedAttack, nextStep),
                "Ultimate" => TwinsComboCoordinator.InitiateUltimateOrCrossDash(Context, nextStep),
                _ => new SpazmatismPhase2DashPrepareState(0, nextStep)
            };
        }

        /// <summary>搭档(激光眼)是否存活</summary>
        private bool HasPartner() {
            foreach (var n in Main.npc) {
                if (n.active && n.type == NPCID.Retinazer) {
                    return true;
                }
            }
            return false;
        }
    }
}
