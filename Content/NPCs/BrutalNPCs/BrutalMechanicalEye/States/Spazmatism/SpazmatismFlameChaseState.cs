using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using CalamityOverhaul.Content.Projectiles.Boss.MechanicalEye;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Spazmatism
{
    /// <summary>
    /// 魔焰眼二阶段喷火追击状态：
    /// 弧线贴近压制并持续喷吐火舌(扇形火焰流)，二阶段套路锚点
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)TwinsStateIndex.SpazmatismFlameChase, typeof(TwinsStateContext))]
    internal class SpazmatismFlameChaseState : TwinsStateBase
    {
        public override string StateName => "SpazmatismFlameChase";
        public override TwinsStateIndex StateIndex => TwinsStateIndex.SpazmatismFlameChase;

        /// <summary>
        /// 二阶段固定招式套路(有搭档时)：
        /// 喷火追击→二阶冲刺→磁暴链锁→残影连冲→超新星对撞→火焰风暴→大招/交叉冲刺→(循环)
        /// 合击节点由合击信号同步双眼
        /// 
        /// 二阶段固定招式套路(独眼时)：
        /// 喷火追击→二阶冲刺→残影连冲→喷火追击→火焰风暴→二阶冲刺→(循环)
        /// </summary>
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

        private float ChaseSpeed => Context.IsDeathMode ? 11f : 9f;
        private float MaxTurnRad => Context.IsDeathMode ? 0.052f : 0.042f;
        private int FlameDuration => Context.IsDeathMode ? 100 : 130;
        private int FlameInterval => Context.IsDeathMode ? 7 : 9;

        private TwinsStateContext Context;
        private int comboStep;

        /// <param name="currentComboStep">二阶段固定招式循环的当前步骤索引</param>
        public SpazmatismFlameChaseState() : this(0) {
        }

        public SpazmatismFlameChaseState(int currentComboStep) {
            comboStep = currentComboStep;
        }

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            Context = context;
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //检测独眼狂暴模式触发
            if (context.SoloRageJustTriggered) {
                return new SpazmatismSoloRageState();
            }

            //锚点状态:响应搭档发出的合击信号，立即跟进合击
            ITwinsState comboFollow = TwinsComboCoordinator.TryFollowSignal(context);
            if (comboFollow != null) {
                return comboFollow;
            }

            //弧线追击玩家:速度恒定+限转速，产生贴身缠斗的弧线轨迹
            TwinsMotion.CurveChase(npc, player.Center, ChaseSpeed, MaxTurnRad);
            FaceVelocity(npc);
            context.PushDashVisuals(0.25f, 0.35f);

            Timer++;

            //喷吐火舌:沿运动方向的扇形火焰流
            if (Timer % FlameInterval == 0) {
                if (!VaultUtils.isClient) {
                    Vector2 fireDir = npc.velocity.SafeNormalize(Vector2.UnitY);
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

            //喷口火光
            if (!VaultUtils.isServer && Timer % 3 == 0) {
                Vector2 fireDir = npc.velocity.SafeNormalize(Vector2.UnitY);
                PRTLoader.NewParticle<PRT_TwinsSpark>(npc.Center + fireDir * 42f,
                    fireDir * 3f + Main.rand.NextVector2Circular(1f, 1f),
                    Color.White, Main.rand.NextFloat(0.9f, 1.4f))?.Configure(12, 1);
            }

            //喷火结束，按固定套路切换到下一招式
            if (Timer >= FlameDuration) {
                //独眼模式下切换到狂暴状态
                if (context.IsSoloRageMode) {
                    return new SpazmatismSoloRageState();
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

        /// <summary>
        /// 检查是否有另一只眼睛存活
        /// </summary>
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
