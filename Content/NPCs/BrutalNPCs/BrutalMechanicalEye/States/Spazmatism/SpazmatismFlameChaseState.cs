using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using Microsoft.Xna.Framework;
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

        private float ChaseSpeed => Context.IsMachineRebellion ? 10f : 6f;
        private float TurnSpeed => Context.IsMachineRebellion ? 0.2f : 0.12f;
        private int FlameDuration => Context.IsMachineRebellion ? 380 : 300;

        private TwinsStateContext Context;

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            Context = context;
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //追击玩家
            Vector2 targetDir = GetDirectionToTarget(context);
            npc.velocity = Vector2.Lerp(npc.velocity, targetDir * ChaseSpeed, TurnSpeed);
            FaceVelocity(npc);

            Timer++;

            //喷火
            if (Timer % 8 == 0) {
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

            //喷火结束，切换到冲刺
            if (Timer >= FlameDuration) {
                return new SpazmatismPhase2DashPrepareState();
            }

            return null;
        }
    }
}
