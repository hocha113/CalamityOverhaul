using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using CalamityOverhaul.Content.Projectiles.Boss.MechanicalEye;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Retinazer
{
    /// <summary>
    /// 激光眼二阶段游走点射状态：
    /// 弹簧侧翼游走，三连点射预判激光，二阶段套路锚点
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)TwinsStateIndex.RetinazerVerticalBarrage, typeof(TwinsStateContext))]
    internal class RetinazerVerticalBarrageState : TwinsStateBase
    {
        public override string StateName => "RetinazerVerticalBarrage";
        public override TwinsStateIndex StateIndex => TwinsStateIndex.RetinazerVerticalBarrage;

        /// <summary>
        /// 二阶段固定招式套路(有搭档时)：
        /// 游走点射→精准狙击→磁暴链锁→死亡射线扫射→超新星对撞→激光矩阵→大招/交叉冲刺→(循环)
        /// 
        /// 与魔焰眼的配合(combo索引对齐，合击节点1/3/5双眼同步)：
        /// 激光眼:精准狙击(爆发输出)   ←→ 魔焰眼:二阶冲刺(高速突袭)
        /// 激光眼:磁暴链锁(合击)       ←→ 魔焰眼:磁暴链锁(合击)
        /// 激光眼:死亡射线扫射(区域切割)←→ 魔焰眼:残影连冲(多段突进)
        /// 激光眼:超新星对撞(合击)     ←→ 魔焰眼:超新星对撞(合击)
        /// 激光眼:激光矩阵(区域封锁)   ←→ 魔焰眼:火焰风暴(区域控制)
        /// 激光眼:大招/交叉冲刺(合击)  ←→ 魔焰眼:大招/交叉冲刺(合击)
        /// 
        /// 二阶段固定招式套路(独眼时)：
        /// 游走点射→精准狙击→水平弹幕→死亡射线扫射→激光矩阵→精准狙击→(循环)
        /// </summary>
        private static readonly string[] ComboSequenceWithPartner =
        [
            "PrecisionSniper",
            "TetherSweep",
            "FocusedBeam",
            "Supernova",
            "LaserMatrix",
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

        private int Duration => Context.IsDeathMode ? 110 : 140;
        private int BurstRate => Context.IsDeathMode ? 34 : 42;
        private float LaserSpeed => Context.IsDeathMode ? 17f : 15f;
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

            //锚点状态:响应搭档发出的合击信号，立即跟进合击
            ITwinsState comboFollow = TwinsComboCoordinator.TryFollowSignal(context);
            if (comboFollow != null) {
                return comboFollow;
            }

            //弹簧侧翼游走:占位玩家侧面并带纵向呼吸
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
                //独眼模式下切换到狂暴状态
                if (context.IsSoloRageMode) {
                    return new RetinazerSoloRageState();
                }

                return GetNextComboState();
            }

            return null;
        }

        /// <summary>
        /// 根据固定套路获取下一个状态
        /// </summary>
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

        /// <summary>
        /// 检查是否有另一只眼睛存活
        /// </summary>
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
