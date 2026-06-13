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
    /// 激光眼一阶段悬停射击状态：
    /// 弹簧悬停带呼吸浮动，三连点射激光并伴随后坐力位移
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)TwinsStateIndex.RetinazerHoverShoot, typeof(TwinsStateContext))]
    internal class RetinazerHoverShootState : TwinsStateBase
    {
        public override string StateName => "RetinazerHoverShoot";
        public override TwinsStateIndex StateIndex => TwinsStateIndex.RetinazerHoverShoot;

        private int ShootRate => Context.IsDeathMode ? 52 : 64;
        private float LaserSpeed => Context.IsDeathMode ? 12f : 10f;
        private int MaxBurstCount => Context.IsDeathMode ? 2 : 3;
        private const int BurstShots = 3;
        private const int BurstInterval = 6;

        private TwinsStateContext Context;
        private int comboStep;
        private int burstRemaining;
        private int burstTimer;

        /// <summary>一阶段套路：悬停射击→扫射→悬停→ reposition；comboStep%4==3 交叉冲刺合击</summary>
        public RetinazerHoverShootState() : this(0) {
        }

        public RetinazerHoverShootState(int currentComboStep) {
            comboStep = currentComboStep;
        }

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            Context = context;
            burstRemaining = 0;
            burstTimer = 0;
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //锚点状态:响应搭档发出的合击信号
            ITwinsState comboFollow = TwinsComboCoordinator.TryFollowSignal(context);
            if (comboFollow != null) {
                return comboFollow;
            }

            //弹簧悬停在玩家上方，带呼吸浮动
            Vector2 hoverTarget = player.Center + new Vector2(0, -350) + TwinsMotion.BreathingOffset(seed: 4.2f);
            TwinsMotion.SpringHover(npc, hoverTarget, 0.012f, 0.078f);
            FaceTarget(npc, player.Center);

            Timer++;

            //触发一轮三连点射
            if (Timer >= ShootRate && burstRemaining <= 0) {
                burstRemaining = BurstShots;
                burstTimer = 0;
                Timer = 0;
                Counter++;
            }

            //执行点射:每数帧一发，每发带后坐力
            if (burstRemaining > 0 && ++burstTimer >= BurstInterval) {
                burstTimer = 0;
                burstRemaining--;
                FireLaser(npc, player);
            }

            //射击轮数后按固定套路切换状态
            if (Counter >= MaxBurstCount && burstRemaining <= 0) {
                //每轮套路末尾与魔焰眼同步交叉冲刺
                if (comboStep % 4 == 3 && TwinsStateContext.GetPartnerNpc(npc.type).Alives()) {
                    return TwinsComboCoordinator.InitiateCombo(context, TwinsStateIndex.TwinsCrossDash, comboStep + 1);
                }
                //固定交替: 激光扫射 → 调整位置 → 激光扫射 → 调整位置...
                if (comboStep % 2 == 0) {
                    return new RetinazerLaserSweepState(comboStep + 1);
                }
                else {
                    return new RetinazerRepositionState(comboStep + 1);
                }
            }

            return null;
        }

        /// <summary>
        /// 发射单发预判激光并产生后坐力
        /// </summary>
        private void FireLaser(NPC npc, Player player) {
            Vector2 predicted = TwinsMotion.PredictTarget(player, npc.Center, LaserSpeed * 3f, 0.45f);
            Vector2 shootDir = (predicted - npc.Center).SafeNormalize(Vector2.UnitY);

            if (!VaultUtils.isClient) {
                Projectile.NewProjectile(
                    npc.GetSource_FromAI(),
                    npc.Center + shootDir * 38f,
                    shootDir * LaserSpeed,
                    ModContent.ProjectileType<RetinazerLaser>(),
                    22,
                    0f,
                    Main.myPlayer
                );
            }

            //后坐力位移与机体反冲
            npc.velocity -= shootDir * 5.5f;
            Context.PushDashVisuals(0.2f, 0.25f);

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item33 with { Pitch = 0.2f, Volume = 0.8f }, npc.Center);
                for (int i = 0; i < 4; i++) {
                    PRTLoader.NewParticle<PRT_TwinsSpark>(npc.Center + shootDir * 42f,
                        shootDir.RotatedBy(Main.rand.NextFloat(-0.35f, 0.35f)) * Main.rand.NextFloat(3f, 6f),
                        Color.White, Main.rand.NextFloat(0.9f, 1.4f))?.Configure(13, 0);
                }
            }
        }
    }
}
