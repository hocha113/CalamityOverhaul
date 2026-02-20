using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.States
{
    /// <summary>
    /// 包围状态：加速旋转+半径收缩，体节激光密度递增
    /// 改进：包围圈有最小半径限制，玩家不会被完全困死；
    /// 收缩到最紧时停顿一拍后转入冲刺，给予逃脱窗口
    /// </summary>
    internal class DestroyerEncircleState : DestroyerStateBase
    {
        public override string StateName => "Encircle";
        public override DestroyerStateIndex StateIndex => DestroyerStateIndex.Encircle;

        private const int EncircleDuration = 400;
        private const int TightenPauseDuration = 40;
        private const float MinRadius = 650f;
        private const float MaxRadius = 1500f;

        private bool tightenPause;

        public override void OnEnter(DestroyerStateContext context) {
            base.OnEnter(context);
            tightenPause = false;
            context.SetChargeState(3, 0f);
        }

        public override IDestroyerState OnUpdate(DestroyerStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;

            //收缩到最紧后短暂停顿
            if (tightenPause) {
                npc.velocity *= 0.96f;
                Counter++;
                context.SetChargeState(3, 1f);

                if (Counter >= TightenPauseDuration) {
                    return new DestroyerDashPrepareState();
                }
                return null;
            }

            //角速度递增
            float angularSpeed = MathHelper.Lerp(0.03f,
                context.IsEnraged ? 0.08f : 0.06f, Math.Min(Timer / 300f, 1f));
            float angle = Timer * angularSpeed;

            //缓出曲线收缩，带最小半径限制
            float shrinkProgress = Math.Min(Timer / (float)EncircleDuration, 1f);
            float easeOut = 1f - (1f - shrinkProgress) * (1f - shrinkProgress);
            float radius = MathHelper.Lerp(MaxRadius, MinRadius, easeOut);

            Vector2 offset = angle.ToRotationVector2() * radius;
            float speed = MathHelper.Lerp(28f, 40f, shrinkProgress);
            float turnSpeed = MathHelper.Lerp(0.8f, 1.5f, shrinkProgress);

            SetMovement(context, player.Center + offset, speed, turnSpeed);
            context.SetChargeState(3, shrinkProgress);

            //体节激光
            int baseFireChance = CWRWorld.Death ? 100 : 140;
            int fireChance = (int)(baseFireChance * (1f - easeOut * 0.6f));
            fireChance = Math.Max(fireChance, 25);

            if (Timer > 60 && Timer % 8 == 0 && context.BodySegments.Count > 0) {
                foreach (var segment in context.BodySegments) {
                    if (segment.active && Main.rand.NextBool(fireChance)) {
                        FireEncircleLaser(context, segment);
                    }
                }
            }

            //包围完成，进入停顿
            if (Timer >= EncircleDuration) {
                tightenPause = true;
                Counter = 0;
            }

            return null;
        }

        private static void FireEncircleLaser(DestroyerStateContext context, NPC source) {
            if (VaultUtils.isClient) return;
            float speed = CWRWorld.Death ? 6f : 4f;
            Vector2 velocity = (context.Target.Center - source.Center).SafeNormalize(Vector2.Zero) * speed;
            int damage = HeadPrimeAI.SetMultiplier(CWRRef.GetProjectileDamage(context.Npc, ProjectileID.DeathLaser));
            Projectile.NewProjectile(source.GetSource_FromAI(), source.Center, velocity,
                ProjectileID.DeathLaser, damage, 0f, Main.myPlayer, ai2: context.Npc.target);
        }
    }
}
