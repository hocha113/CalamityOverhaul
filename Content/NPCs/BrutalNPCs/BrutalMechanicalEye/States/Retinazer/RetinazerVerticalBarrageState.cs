using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Common;
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

        private int Duration => Context.IsMachineRebellion ? 180 : (Context.IsDeathMode ? 120 : 150);
        private int RapidFireRate => Context.IsMachineRebellion ? 10 : (Context.IsDeathMode ? 12 : 15);
        private float LaserSpeed => Context.IsDeathMode ? 18f : 16f;

        private TwinsStateContext Context;

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
                    Vector2 shootVel = GetDirectionToTarget(context) * LaserSpeed;
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
                //独眼模式下切换到狂暴状态
                if (context.IsSoloRageMode) {
                    return new RetinazerSoloRageState();
                }

                int choice = Main.rand.Next(5);
                return choice switch {
                    0 => new RetinazerFocusedBeamState(),
                    1 => new RetinazerLaserMatrixState(),
                    2 => new RetinazerPrecisionSniperState(),
                    3 => HasPartner() ? new TwinsCombinedAttackState() : new RetinazerHorizontalBarrageState(),
                    _ => new RetinazerHorizontalBarrageState()
                };
            }

            return null;
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
