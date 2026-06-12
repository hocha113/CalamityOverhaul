using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.Projectiles.Boss.Destroyer;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.States
{
    /// <summary>
    /// 侧舷波次齐射：可见的充能波沿躯体传导，波峰经过的体节朝侧舷方向发射等离子弹。
    /// 弹幕方向 = 体节法线 ± 小幅瞄准修正，整套弹幕呈现"沿身体奔跑的火力波"而非全员直瞄。
    /// 激怒期波到头部后再从头部回扫一波
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)DestroyerStateIndex.LaserBarrage, typeof(DestroyerStateContext))]
    internal class DestroyerLaserBarrageState : DestroyerStateBase
    {
        public override string StateName => "LaserBarrage";
        public override DestroyerStateIndex StateIndex => DestroyerStateIndex.LaserBarrage;

        private const int ChargeTime = 50;
        private const int SweepGap = 24;
        private const int Outro = 30;

        private int lastFiredIndex = -1;
        private int currentSweep;

        public DestroyerLaserBarrageState() {
        }

        private int SweepTime(DestroyerStateContext ctx) => ctx.IsDeathMode ? 86 : 104;
        private float BoltSpeed(DestroyerStateContext ctx) => ctx.IsDeathMode ? 7.5f : 5.5f;

        public override void OnEnter(DestroyerStateContext context) {
            base.OnEnter(context);
            lastFiredIndex = -1;
            currentSweep = 0;
            context.RefreshBodySegments();
        }

        public override IDestroyerState OnUpdate(DestroyerStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //侧舷阵位：绕玩家缓慢拉弧线，让身体侧面朝向玩家，齐射姿态自然
            float orbitAngle = (npc.Center - player.Center).ToRotation() + 0.016f;
            Vector2 orbitTarget = player.Center + orbitAngle.ToRotationVector2() * 760f;
            SetMovement(context, orbitTarget, 13f, 0.55f);
            context.SlitherStrength = 0.4f;

            int sweepTime = SweepTime(context);
            Timer++;

            //阶段1：充能预热——一道波从尾部慢速推向头部，不开火
            if (Timer < ChargeTime) {
                float p = Timer / (float)ChargeTime;
                context.SetChargeState(2, p * 0.6f);
                DestroyerChargeWave.Push(npc.whoAmI, 1f - p, 0.3f, 0.35f + 0.4f * p);

                if (Timer == ChargeTime - 12) {
                    SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.55f, Volume = 0.9f }, npc.Center);
                }
                return null;
            }

            //阶段2：第一波齐射（尾→头）
            if (Timer < ChargeTime + sweepTime) {
                context.ResetChargeState();
                float sweepProgress = (Timer - ChargeTime) / (float)sweepTime;
                DoSweep(context, sweepProgress, reverse: false);
                return null;
            }

            //激怒期：间隙后回程齐射（头→尾）
            if (context.IsEnraged) {
                int phase2Start = ChargeTime + sweepTime + SweepGap;
                if (Timer < phase2Start) {
                    //间隙：波停在头部微光待发
                    if (currentSweep == 0) {
                        currentSweep = 1;
                        lastFiredIndex = -1;
                    }
                    DestroyerChargeWave.Push(npc.whoAmI, 0f, 0.2f, 0.5f);
                    return null;
                }
                if (Timer < phase2Start + sweepTime) {
                    float sweepProgress = (Timer - phase2Start) / (float)sweepTime;
                    DoSweep(context, sweepProgress, reverse: true);
                    return null;
                }
                if (Timer < phase2Start + sweepTime + Outro) {
                    return null;
                }
                return new DestroyerPatrolState();
            }

            //普通：收尾
            if (Timer < ChargeTime + sweepTime + Outro) {
                return null;
            }
            return new DestroyerPatrolState();
        }

        /// <summary>
        /// 推进一帧波次齐射：推送充能波视觉，并在服务端对波峰扫过的体节开火
        /// </summary>
        private void DoSweep(DestroyerStateContext context, float sweepProgress, bool reverse) {
            var segments = context.BodySegments;
            int count = segments.Count;
            if (count == 0) {
                return;
            }

            //normal: 波峰从尾(1)推向头(0)；reverse: 从头(0)推回尾(1)
            float phase = reverse ? sweepProgress : 1f - sweepProgress;
            DestroyerChargeWave.Push(context.Npc.whoAmI, phase, 0.14f, 1f);

            if (VaultUtils.isClient) {
                return;
            }

            int idx = Math.Clamp((int)(phase * (count - 1)), 0, count - 1);

            //公平阀：头部远在玩家视野外时只推进波形不开火，避免"屏幕外射来弹幕"
            if (context.Npc.Distance(context.Target.Center) > 1900f) {
                lastFiredIndex = idx;
                return;
            }

            if (lastFiredIndex < 0) {
                lastFiredIndex = idx;
                return;
            }
            if (idx == lastFiredIndex) {
                return;
            }

            int step = idx > lastFiredIndex ? 1 : -1;
            for (int i = lastFiredIndex + step; step > 0 ? i <= idx : i >= idx; i += step) {
                //隔节开火，留出弹幕间隙
                if (i % 2 != 0) {
                    continue;
                }
                NPC segment = segments[i];
                if (segment.active) {
                    FireBolt(context, segment);
                }
            }
            lastFiredIndex = idx;
        }

        private void FireBolt(DestroyerStateContext context, NPC segment) {
            //体节轴向：体节rotation = 朝向前节方向 + PiOver2
            Vector2 bodyAxis = (segment.rotation - MathHelper.PiOver2).ToRotationVector2();
            Vector2 normal = bodyAxis.RotatedBy(MathHelper.PiOver2);
            Vector2 toPlayer = (context.Target.Center - segment.Center).SafeNormalize(Vector2.UnitY);
            //法线取朝向玩家的一侧
            if (Vector2.Dot(normal, toPlayer) < 0f) {
                normal = -normal;
            }

            //侧舷方向 + 小幅瞄准修正：波形弹幕但仍具威胁
            Vector2 dir = (normal * 0.62f + toPlayer * 0.38f).SafeNormalize(Vector2.UnitY);
            Vector2 velocity = dir * BoltSpeed(context);

            int damage = (int)(HeadPrimeAI.SetMultiplier(CWRRef.GetProjectileDamage(context.Npc, ProjectileID.DeathLaser)) * 0.5f);
            Projectile.NewProjectile(segment.GetSource_FromAI(), segment.Center, velocity,
                ModContent.ProjectileType<DestroyerBolt>(), damage, 0f, Main.myPlayer, 0, 1);
        }
    }
}
