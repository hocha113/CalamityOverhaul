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
    /// <summary>一阶段悬停射击，弹簧悬停+预判火球+后坐力</summary>
    [InnoVault.StateMachines.VaultState((int)TwinsStateIndex.SpazmatismHoverShoot, typeof(TwinsStateContext))]
    internal class SpazmatismHoverShootState : TwinsStateBase
    {
        public override string StateName => "SpazmatismHoverShoot";
        public override TwinsStateIndex StateIndex => TwinsStateIndex.SpazmatismHoverShoot;

        private int ShootRate => Context.IsAsuraMode ? 60 : 80;
        private int MaxShootCount => Context.IsAsuraMode ? 2 : 3;

        private TwinsStateContext Context;
        private int comboStep;

        /// <summary>一阶段套路；comboStep%4==3 交叉合击</summary>
        public SpazmatismHoverShootState() : this(0) {
        }

        public SpazmatismHoverShootState(int currentComboStep) {
            comboStep = currentComboStep;
        }

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            Context = context;
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //锚点跟合击信号
            ITwinsState comboFollow = TwinsComboCoordinator.TryFollowSignal(context);
            if (comboFollow != null) {
                return comboFollow;
            }

            //弹簧悬停在玩家侧边，带呼吸浮动
            Vector2 hoverTarget = player.Center
                + new Vector2(npc.Center.X < player.Center.X ? -400 : 400, -200)
                + TwinsMotion.BreathingOffset(seed: 1.7f);
            TwinsMotion.SpringHover(npc, hoverTarget, 0.013f, 0.08f);
            FaceTarget(npc, player.Center);

            Timer++;
            if (Timer >= ShootRate) {
                //预判射击火球
                float shootSpeed = Context.IsAsuraMode ? 14f : 12f;
                Vector2 predicted = TwinsMotion.PredictTarget(player, npc.Center, shootSpeed * 2f, 0.5f);
                Vector2 shootDir = (predicted - npc.Center).SafeNormalize(Vector2.UnitY);

                if (!VaultUtils.isClient) {
                    Projectile.NewProjectile(
                        npc.GetSource_FromAI(),
                        npc.Center + shootDir * 36f,
                        shootDir * shootSpeed,
                        ModContent.ProjectileType<Fireball>(),
                        22,
                        0f,
                        Main.myPlayer
                    );
                }

                //开火后坐
                npc.velocity -= shootDir * 7f;
                context.PushDashVisuals(0.25f, 0.3f);

                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item34, npc.Center);
                    //喷口闪光
                    for (int i = 0; i < 5; i++) {
                        PRTLoader.NewParticle<PRT_TwinsSpark>(npc.Center + shootDir * 40f,
                            shootDir.RotatedBy(Main.rand.NextFloat(-0.4f, 0.4f)) * Main.rand.NextFloat(3f, 7f),
                            Color.White, Main.rand.NextFloat(1f, 1.5f))?.Configure(14, 1);
                    }
                }
                Timer = 0;
                Counter++;
            }

            //射击次数后按固定套路切换状态
            if (Counter >= MaxShootCount) {
                //每轮套路末尾与激光眼同步交叉冲刺
                if (comboStep % 4 == 3 && TwinsStateContext.GetPartnerNpc(npc.type).Alives()) {
                    return TwinsComboCoordinator.InitiateCombo(context, TwinsStateIndex.TwinsCrossDash, comboStep + 1);
                }
                //固定交替
                if (comboStep % 2 == 0) {
                    return new SpazmatismFireVortexState(comboStep + 1);
                }
                else {
                    return new SpazmatismDashPrepareState(0, comboStep + 1);
                }
            }

            return null;
        }
    }
}
