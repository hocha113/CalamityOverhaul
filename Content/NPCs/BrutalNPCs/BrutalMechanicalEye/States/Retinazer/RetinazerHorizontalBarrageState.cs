using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using Microsoft.Xna.Framework;
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

        private const int Duration = 150;
        private int RapidFireRate => Context.IsMachineRebellion ? 10 : 15;

        private TwinsStateContext Context;

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            Context = context;
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

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

            //随机切换到不同的特殊招式
            if (Timer >= Duration) {
                int choice = Main.rand.Next(4);
                return choice switch {
                    0 => new RetinazerFocusedBeamState(),
                    1 => new RetinazerLaserMatrixState(),
                    2 => new RetinazerPrecisionSniperState(),
                    _ => new RetinazerVerticalBarrageState()
                };
            }

            return null;
        }
    }
}
