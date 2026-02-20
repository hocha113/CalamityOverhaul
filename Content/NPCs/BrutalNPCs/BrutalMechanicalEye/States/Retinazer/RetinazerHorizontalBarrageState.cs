using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Retinazer
{
    /// <summary>
    /// 激光眼二阶段水平弹幕状态
    /// 在玩家上方发射激光弹幕
    /// </summary>
    internal class RetinazerHorizontalBarrageState : TwinsStateBase
    {
        public override string StateName => "RetinazerHorizontalBarrage";
        public override TwinsStateIndex StateIndex => TwinsStateIndex.RetinazerHorizontalBarrage;

        private const int Duration = 150;
        private int RapidFireRate => Context.IsMachineRebellion ? 10 : 15;

        private TwinsStateContext Context;
        private int comboStep;

        public RetinazerHorizontalBarrageState(int currentComboStep = 0) {
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

            //计算目标位置，在玩家上方
            Vector2 targetPos = player.Center + new Vector2(0, -400);

            //保持X轴对齐
            float xDiff = player.Center.X - npc.Center.X;
            npc.velocity.X = MathHelper.Lerp(npc.velocity.X, xDiff * 0.1f, 0.1f);
            npc.velocity.Y = MathHelper.Lerp(npc.velocity.Y, (targetPos.Y - npc.Center.Y) * 0.05f, 0.1f);

            FaceTarget(npc, player.Center);

            Timer++;

            //发射激光
            if (Timer % RapidFireRate == 0) {
                if (!VaultUtils.isClient) {
                    Vector2 shootVel = GetDirectionToTarget(context) * 16f;
                    Projectile.NewProjectile(
                        npc.GetSource_FromAI(),
                        npc.Center,
                        shootVel,
                        ProjectileID.DeathLaser,
                        30,
                        0f,
                        Main.myPlayer
                    );
                }
                SoundEngine.PlaySound(SoundID.Item12, npc.Center);
            }

            //弹幕结束，回到垂直弹幕继续套路循环
            if (Timer >= Duration) {
                //独眼模式下切换到狂暴状态
                if (context.IsSoloRageMode) {
                    return new RetinazerSoloRageState();
                }

                return new RetinazerVerticalBarrageState(comboStep);
            }

            return null;
        }
    }
}
