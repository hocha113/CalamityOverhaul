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
    /// <summary>二阶段水平弹幕，玩家上方连射激光</summary>
    [InnoVault.StateMachines.VaultState((int)TwinsStateIndex.RetinazerHorizontalBarrage, typeof(TwinsStateContext))]
    internal class RetinazerHorizontalBarrageState : TwinsStateBase
    {
        public override string StateName => "RetinazerHorizontalBarrage";
        public override TwinsStateIndex StateIndex => TwinsStateIndex.RetinazerHorizontalBarrage;

        /// <summary>入场瞄准预警，开火前给出可读起手</summary>
        private const int AimPhase = 20;

        private const int Duration = 160;
        private int RapidFireRate => 18;

        private TwinsStateContext Context;
        private int comboStep;

        public RetinazerHorizontalBarrageState() : this(0) {
        }

        public RetinazerHorizontalBarrageState(int currentComboStep) {
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
                return new RetinazerSoloRageState();
            }

            //弹簧悬停在玩家上方，保持X轴跟随
            Vector2 targetPos = player.Center + new Vector2(0, -400) + TwinsMotion.BreathingOffset(seed: 3.6f, 10f);
            TwinsMotion.SpringHover(npc, targetPos, 0.016f, 0.09f);

            FaceTarget(npc, player.Center);

            Timer++;

            //入场瞄准，蓄力光+瞄准线，不开火
            if (Timer <= AimPhase) {
                float aimProgress = Timer / (float)AimPhase;
                context.SetChargeState(4, aimProgress);

                if (Timer == 1 && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.4f, Volume = 0.7f }, npc.Center);
                }
                if (!VaultUtils.isServer && Timer % 2 == 0) {
                    TwinsMotion.ChargeGatherFX(npc.Center, false, aimProgress, 80f);
                }
                if (!VaultUtils.isServer && Timer % 6 == 0) {
                    Vector2 aimDir = (npc.rotation + MathHelper.PiOver2).ToRotationVector2();
                    for (int i = 0; i < 8; i++) {
                        PRTLoader.NewParticle<PRT_TwinsSpark>(npc.Center + aimDir * (55 + i * 42),
                            aimDir * 1.5f, Color.White, 0.85f)?.Configure(14, 0);
                    }
                }
                return null;
            }

            if (Timer == AimPhase + 1) {
                context.ResetChargeState();
            }

            //发射预判激光
            if ((Timer - AimPhase) % RapidFireRate == 0) {
                Vector2 predicted = TwinsMotion.PredictTarget(player, npc.Center, 48f, 0.45f);
                Vector2 shootDir = (predicted - npc.Center).SafeNormalize(Vector2.UnitY);
                if (!VaultUtils.isClient) {
                    Projectile.NewProjectile(
                        npc.GetSource_FromAI(),
                        npc.Center + shootDir * 38f,
                        shootDir * 16f,
                        ModContent.ProjectileType<RetinazerLaser>(),
                        24,
                        0f,
                        Main.myPlayer
                    );
                }
                //后坐力
                npc.velocity -= shootDir * 4f;
                SoundEngine.PlaySound(SoundID.Item12, npc.Center);
            }

            //弹幕结束，回到垂直弹幕继续套路循环
            if (Timer >= Duration) {
                if (context.IsSoloRageMode) {
                    return new RetinazerSoloRageState();
                }

                return new RetinazerVerticalBarrageState(comboStep);
            }

            return null;
        }
    }
}
