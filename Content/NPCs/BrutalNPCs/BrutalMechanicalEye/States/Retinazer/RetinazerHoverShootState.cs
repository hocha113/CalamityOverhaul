using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Retinazer
{
    /// <summary>
    /// 激光眼一阶段悬停射击状态
    /// </summary>
    internal class RetinazerHoverShootState : TwinsStateBase
    {
        public override string StateName => "RetinazerHoverShoot";

        private int ShootRate => Context.IsMachineRebellion ? 45 : (Context.IsDeathMode ? 50 : 60);
        private float MoveSpeed => Context.IsMachineRebellion ? 14f : (Context.IsDeathMode ? 12f : 10f);
        private float LaserSpeed => Context.IsDeathMode ? 11f : 9f;
        private int MaxShootCount => Context.IsDeathMode ? 2 : 3;

        private TwinsStateContext Context;

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            Context = context;
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //计算悬停位置，在玩家上方
            Vector2 hoverTarget = player.Center + new Vector2(0, -350);
            MoveTo(npc, hoverTarget, MoveSpeed, 0.06f);
            FaceTarget(npc, player.Center);

            Timer++;
            if (Timer >= ShootRate) {
                //发射激光
                if (!VaultUtils.isClient) {
                    Vector2 shootVel = GetDirectionToTarget(context) * LaserSpeed;
                    Projectile.NewProjectile(
                        npc.GetSource_FromAI(),
                        npc.Center,
                        shootVel,
                        ProjectileID.DeathLaser,
                        22,
                        0f,
                        Main.myPlayer
                    );
                }
                SoundEngine.PlaySound(SoundID.Item33, npc.Center);
                Timer = 0;
                Counter++;
            }

            //射击次数后切换状态
            if (Counter >= MaxShootCount) {
                //60%概率使用激光扫射，40%概率调整位置
                if (Main.rand.Next(5) < 3) {
                    return new RetinazerLaserSweepState();
                }
                else {
                    return new RetinazerRepositionState();
                }
            }

            return null;
        }
    }
}
