using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Common;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Spazmatism
{
    /// <summary>
    /// 魔焰眼二阶段喷火追击状态
    /// </summary>
    internal class SpazmatismFlameChaseState : TwinsStateBase
    {
        public override string StateName => "SpazmatismFlameChase";

        private float ChaseSpeed => Context.IsMachineRebellion ? 10f : (Context.IsDeathMode ? 8f : 6f);
        private float TurnSpeed => Context.IsMachineRebellion ? 0.2f : (Context.IsDeathMode ? 0.16f : 0.12f);
        private int FlameDuration => Context.IsMachineRebellion ? 200 : (Context.IsDeathMode ? 120 : 150);
        private int FlameInterval => Context.IsDeathMode ? 6 : 8;

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
                return new SpazmatismSoloRageState();
            }

            //追击玩家
            Vector2 targetDir = GetDirectionToTarget(context);
            npc.velocity = Vector2.Lerp(npc.velocity, targetDir * ChaseSpeed, TurnSpeed);
            FaceVelocity(npc);

            Timer++;

            //喷火
            if (Timer % FlameInterval == 0) {
                if (!VaultUtils.isClient) {
                    Vector2 fireVel = npc.velocity.SafeNormalize(Vector2.UnitY) * 12f;
                    Projectile.NewProjectile(
                        npc.GetSource_FromAI(),
                        npc.Center,
                        fireVel,
                        ProjectileID.EyeFire,
                        32,
                        0f,
                        Main.myPlayer
                    );
                }
                SoundEngine.PlaySound(SoundID.Item34, npc.Center);
            }

            //喷火结束，随机切换到特殊招式
            if (Timer >= FlameDuration) {
                //独眼模式下切换到狂暴状态
                if (context.IsSoloRageMode) {
                    return new SpazmatismSoloRageState();
                }

                int choice = Main.rand.Next(5);
                return choice switch {
                    0 => new SpazmatismShadowDashState(),
                    1 => new SpazmatismFlameStormState(),
                    2 => HasPartner() ? new TwinsCombinedAttackState() : new SpazmatismPhase2DashPrepareState(),
                    _ => new SpazmatismPhase2DashPrepareState()
                };
            }

            return null;
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
