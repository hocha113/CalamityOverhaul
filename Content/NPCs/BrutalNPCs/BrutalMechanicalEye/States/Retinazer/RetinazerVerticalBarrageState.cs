using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Retinazer
{
    /// <summary>激光眼二阶段游走点射状态，弹簧侧翼游走，三连点射预判激光，二阶段套路锚点</summary>
    [InnoVault.StateMachines.VaultState((int)TwinsStateIndex.RetinazerVerticalBarrage, typeof(TwinsStateContext))]
    internal class RetinazerVerticalBarrageState : TwinsStateBase
    {
        public override string StateName => "RetinazerVerticalBarrage";
        public override TwinsStateIndex StateIndex => TwinsStateIndex.RetinazerVerticalBarrage;

        /// <summary>二阶段套路；合击节点 1/3/5，见 ComboSequence*</summary>
        /// <remarks>第 2 步走定点矩阵、第 4 步才放追踪死光，避开魔焰残影连冲的双 striker 撞车</remarks>
        private static readonly string[] ComboSequenceWithPartner =
        [
            "PrecisionSniper",
            "TetherSweep",
            "LaserMatrix",
            "Supernova",
            "FocusedBeam",
            "Ultimate"
        ];

        private static readonly string[] ComboSequenceSolo =
        [
            "PrecisionSniper",
            "HorizontalBarrage",
            "FocusedBeam",
            "LaserMatrix",
            "PrecisionSniper"
        ];

        private int Duration => Context.IsAsuraMode ? 110 : 140;
        private int BurstRate => Context.IsAsuraMode ? 34 : 42;
        private float LaserSpeed => Context.IsAsuraMode ? 17f : 15f;
        private const int BurstShots = 3;
        private const int BurstInterval = 5;

        private TwinsStateContext Context;
        private int comboStep;
        private int burstRemaining;
        private int burstTimer;
        private int shootCooldown;

        /// <param name="currentComboStep">二阶段固定招式循环的当前步骤索引</param>
        public RetinazerVerticalBarrageState() : this(0) {
        }

        public RetinazerVerticalBarrageState(int currentComboStep) {
            comboStep = currentComboStep;
        }

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            Context = context;
            burstRemaining = 0;
            burstTimer = 0;
            shootCooldown = 0;
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //检测独眼狂暴模式触发
            if (context.SoloRageJustTriggered) {
                return new RetinazerSoloRageState();
            }

            //锚点跟合击信号，立即跟进合击
            ITwinsState comboFollow = TwinsComboCoordinator.TryFollowSignal(context);
            if (comboFollow != null) {
                return comboFollow;
            }

            //侧翼弹簧游走
            Vector2 targetPos = player.Center
                + new Vector2(npc.Center.X < player.Center.X ? -420 : 420, 0)
                + TwinsMotion.BreathingOffset(seed: 2.9f, 18f);
            TwinsMotion.SpringHover(npc, targetPos, 0.015f, 0.085f);
            FaceTarget(npc, player.Center);

            Timer++;

            //触发三连点射
            if (++shootCooldown >= BurstRate && burstRemaining <= 0) {
                burstRemaining = BurstShots;
                burstTimer = 0;
                shootCooldown = 0;
            }

            if (burstRemaining > 0 && ++burstTimer >= BurstInterval) {
                burstTimer = 0;
                burstRemaining--;

                Vector2 predicted = TwinsMotion.PredictTarget(player, npc.Center, LaserSpeed * 3f, 0.5f);
                Vector2 shootDir = (predicted - npc.Center).SafeNormalize(Vector2.UnitY);

                if (!VaultUtils.isClient) {
                    Projectile.NewProjectile(
                        npc.GetSource_FromAI(),
                        npc.Center + shootDir * 38f,
                        shootDir * LaserSpeed,
                        ModContent.ProjectileType<RetinazerLaser>(),
                        24,
                        0f,
                        Main.myPlayer
                    );
                }

                //后坐力与喷口闪光
                npc.velocity -= shootDir * 4.5f;
                Context.PushDashVisuals(0.2f, 0.25f);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item12 with { Pitch = 0.25f, Volume = 0.8f }, npc.Center);
                    for (int i = 0; i < 3; i++) {
                        PRTLoader.NewParticle<PRT_TwinsSpark>(npc.Center + shootDir * 42f,
                            shootDir.RotatedBy(Main.rand.NextFloat(-0.3f, 0.3f)) * Main.rand.NextFloat(3f, 6f),
                            Color.White, Main.rand.NextFloat(0.8f, 1.3f))?.Configure(12, 0);
                    }
                }
            }

            //按固定套路切换到下一招式
            if (Timer >= Duration && burstRemaining <= 0) {
                if (context.IsSoloRageMode) {
                    return new RetinazerSoloRageState();
                }

                return GetNextComboState();
            }

            return null;
        }

        /// <summary>根据固定套路获取下一个状态</summary>
        private ITwinsState GetNextComboState() {
            bool hasPartner = HasPartner();
            string[] sequence = hasPartner ? ComboSequenceWithPartner : ComboSequenceSolo;
            string nextMove = sequence[comboStep % sequence.Length];
            int nextStep = comboStep + 1;

            return nextMove switch {
                "PrecisionSniper" => new RetinazerPrecisionSniperState(0, nextStep),
                "HorizontalBarrage" => new RetinazerHorizontalBarrageState(nextStep),
                "FocusedBeam" => new RetinazerFocusedBeamState(nextStep),
                "LaserMatrix" => new RetinazerLaserMatrixState(nextStep),
                "TetherSweep" => TwinsComboCoordinator.InitiateCombo(Context, TwinsStateIndex.TwinsTetherSweep, nextStep),
                "Supernova" => TwinsComboCoordinator.InitiateCombo(Context, TwinsStateIndex.TwinsCombinedAttack, nextStep),
                "Ultimate" => TwinsComboCoordinator.InitiateUltimateOrCrossDash(Context, nextStep),
                _ => new RetinazerPrecisionSniperState(0, nextStep)
            };
        }

        /// <summary>检查是否有另一只眼睛存活</summary>
        private bool HasPartner() {
            foreach (var n in Main.npc) {
                if (n.active && n.type == NPCID.Spazmatism) {
                    return true;
                }
            }
            return false;
        }
    }
}
