using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.States
{
    /// <summary>
    /// 包围状态：加速旋转+半径收缩，体节激光密度递增
    /// </summary>
    internal class DestroyerEncircleState : DestroyerStateBase
    {
        public override string StateName => "Encircle";
        public override DestroyerStateIndex StateIndex => DestroyerStateIndex.Encircle;

        private static int EncircleDuration => 400;
        private static int TightenPauseDuration => 40;
        private static float MinRadius => 1050f;
        private static float MaxRadius => 1500f;

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

            //缓出曲线收缩，带最小半径限制
            float shrinkProgress = Math.Min(Timer / (float)EncircleDuration, 1f);
            float easeOut = 1f - (1f - shrinkProgress) * (1f - shrinkProgress);
            float targetRadius = MathHelper.Lerp(MaxRadius, MinRadius, easeOut);

            //以NPC当前相对玩家的实际角度为基础递增，轨道锚定玩家实时位置
            float currentAngle = (npc.Center - player.Center).ToRotation();
            float angularSpeed = MathHelper.Lerp(0.03f,
                context.IsEnraged ? 0.08f : 0.06f, Math.Min(Timer / 300f, 1f));
            float nextAngle = currentAngle + angularSpeed;

            Vector2 orbitTarget = player.Center + nextAngle.ToRotationVector2() * targetRadius;
            float speed = MathHelper.Lerp(28f, 40f, shrinkProgress);
            float turnSpeed = MathHelper.Lerp(0.8f, 1.5f, shrinkProgress);

            SetMovement(context, orbitTarget, speed, turnSpeed);
            context.SetChargeState(3, shrinkProgress);

            //体节激光，降低密度避免无法躲避
            int baseFireChance = CWRWorld.Death ? 130 : 180;
            int fireChance = (int)(baseFireChance * (1f - easeOut * 0.5f));
            fireChance = Math.Max(fireChance, 40);

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
            int damage = (int)(HeadPrimeAI.SetMultiplier(CWRRef.GetProjectileDamage(context.Npc, ProjectileID.DeathLaser)) * 0.4f);
            Projectile.NewProjectile(source.GetSource_FromAI(), source.Center, velocity,
                ProjectileID.DeathLaser, damage, 0f, Main.myPlayer, ai2: context.Npc.target);
        }
    }
}
