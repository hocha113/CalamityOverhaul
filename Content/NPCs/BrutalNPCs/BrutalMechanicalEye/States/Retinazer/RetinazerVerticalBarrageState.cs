using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Retinazer
{
    /// <summary>
    /// 激光眼二阶段垂直弹幕状态
    /// 在玩家侧面发射激光弹幕
    /// </summary>
    internal class RetinazerVerticalBarrageState : TwinsStateBase
    {
        public override string StateName => "RetinazerVerticalBarrage";

        private const int Duration = 240;
        private int RapidFireRate => Context.IsMachineRebellion ? 10 : 15;

        private TwinsStateContext Context;

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            Context = context;
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //计算目标位置，在玩家侧面
            Vector2 targetPos = player.Center + new Vector2(npc.Center.X < player.Center.X ? -400 : 400, 0);

            //保持Y轴对齐
            float yDiff = player.Center.Y - npc.Center.Y;
            npc.velocity.Y = MathHelper.Lerp(npc.velocity.Y, yDiff * 0.1f, 0.1f);
            npc.velocity.X = MathHelper.Lerp(npc.velocity.X, (targetPos.X - npc.Center.X) * 0.05f, 0.1f);

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

            //切换到水平弹幕
            if (Timer >= Duration) {
                return new RetinazerHorizontalBarrageState();
            }

            return null;
        }
    }
}
