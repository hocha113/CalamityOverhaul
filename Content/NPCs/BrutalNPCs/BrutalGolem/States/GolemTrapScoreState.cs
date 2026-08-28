using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Projectiles;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.States
{
    /// <summary>机关乐谱：布设机关单元按时序起爆，躯干踏步压近维持双线威胁</summary>
    [InnoVault.StateMachines.VaultState((int)GolemStateIndex.TrapScore, typeof(GolemStateContext))]
    internal class GolemTrapScoreState : GolemStateBase
    {
        public override string StateName => "TrapScore";
        public override GolemStateIndex StateIndex => GolemStateIndex.TrapScore;

        /// <summary>追猎布刺间隔拍</summary>
        private const int ChaseInterval = 17;
        /// <summary>追猎小节起拍</summary>
        private const int ChaseStart = 110;
        /// <summary>追猎预读系数：站桩与直线跑都会被咬中，急转向可甩脱</summary>
        private const float ChaseLead = 0.6f;

        private int hopTimer;
        private bool airborne;
        private int chasePlanted;

        public override IGolemState OnUpdate(GolemStateContext context) {
            NPC npc = context.Npc;
            context.FrameMode = 0;
            RestoreTileCollide(context);
            context.VeinGlow = Math.Max(context.VeinGlow, 0.4f);

            //第一小节：开场布谱（服务端一次性），涟漪中心带预读提前量
            if (Timer == 8 && !VaultUtils.isClient) {
                if (context.Sundered) {
                    PlantMixedScore(context);
                }
                else {
                    PlantSpikeRipple(context, RippleCenter(context), false);
                }
            }
            //第二小节：追猎布设——逐拍在玩家预读位置种刺（预告即承诺：种下即锁位；
            //每刺恒定 TrapTelegraph 预警；单点危害，缺口=其余全场）
            int chaseCount = (context.Sundered ? 11 : 9) + (context.DeathMode ? 2 : 0);
            int chaseStart = Tempo(context, ChaseStart);
            int chaseStep = Math.Max(Tempo(context, ChaseInterval), 4);
            if (!VaultUtils.isClient && Timer >= chaseStart && chasePlanted < chaseCount
                && (Timer - chaseStart) % chaseStep == 0) {
                chasePlanted++;
                PlantChaseSpike(context);
            }

            //躯干踏步节拍：每拍必跳，近身即原地起跳压顶（机关不是让Boss挂机的借口）
            if (OnGround(npc)) {
                GroundBrake(npc, 0.75f);
                npc.damage = 0;
                if (++hopTimer >= Tempo(context, 54)) {
                    hopTimer = 0;
                    float dx = context.Target.Center.X - npc.Center.X;
                    LaunchJump(context, MathHelper.Clamp(dx / 70f, -9f, 9f), -8.5f);
                    if (!VaultUtils.isClient) {
                        npc.netUpdate = true;
                    }
                }
            }
            else {
                context.FrameMode = 2;
                npc.damage = npc.defDamage;
                AirSteer(context, 0.1f, 9f);
            }
            if (LandedThisFrame(npc, ref airborne)) {
                LandingImpact(context, context.Sundered ? 3 : 2);
            }

            Timer++;
            int duration = Tempo(context, context.Sundered ? 330 : 290);
            if (Timer >= duration && !VaultUtils.isClient) {
                return new GolemConnectorState();
            }
            return null;
        }

        /// <summary>涟漪中心：带30帧速度预读，抓住直线跑动的目标</summary>
        private static float RippleCenter(GolemStateContext context) {
            return context.Target.Center.X + context.Target.velocity.X * 30f;
        }

        /// <summary>追猎刺：种在玩家预读位置，种下后不再改位</summary>
        private static void PlantChaseSpike(GolemStateContext context) {
            Player target = context.Target;
            float x = target.Center.X + target.velocity.X * (GolemDirector.TrapTelegraph * ChaseLead);
            int damage = GolemDirector.ScaleDamage(GolemDirector.SpikeDamage, context.DeathMode, context.Enraged);
            GolemTrapUnit.PlantOnGround(context.Npc, x, target.Center.Y,
                GolemTrapUnit.TrapKind.Spike, GolemDirector.TrapTelegraph, damage);
        }

        /// <summary>尖刺涟漪：一排尖刺按序起爆，reverse 反向</summary>
        private void PlantSpikeRipple(GolemStateContext context, float centerX, bool reverse) {
            NPC npc = context.Npc;
            int count = context.DeathMode ? 9 : 7;
            float spacing = 116f;
            int damage = ScaleDamage(context, GolemDirector.SpikeDamage);

            for (int i = 0; i < count; i++) {
                float x = centerX + (i - (count - 1) * 0.5f) * spacing;
                int order = reverse ? count - 1 - i : i;
                int delay = GolemDirector.TrapTelegraph + order * 9;
                GolemTrapUnit.PlantOnGround(npc, x, context.Target.Center.Y,
                    GolemTrapUnit.TrapKind.Spike, delay, damage);
            }
        }

        /// <summary>二阶段混合谱：尖刺涟漪 + 侧翼火焰喷口 + 顶部射线口</summary>
        private void PlantMixedScore(GolemStateContext context) {
            NPC npc = context.Npc;
            Player target = context.Target;
            PlantSpikeRipple(context, RippleCenter(context), false);

            int flameDamage = ScaleDamage(context, GolemDirector.FlameJetDamage);
            //左右喷口相位错开：左先右后，形成推挤走位
            GolemTrapUnit.PlantOnSide(npc, target, -1, GolemTrapUnit.TrapKind.FlameVent,
                GolemDirector.TrapTelegraph + 30, flameDamage);
            GolemTrapUnit.PlantOnSide(npc, target, 1, GolemTrapUnit.TrapKind.FlameVent,
                GolemDirector.TrapTelegraph + 96, flameDamage);

            //顶部射线口：周期横扫射线（短促可读）
            int rayDamage = ScaleDamage(context, GolemDirector.EyeRayDamage);
            GolemTrapUnit.PlantOnCeiling(npc, target, GolemTrapUnit.TrapKind.RayPort,
                GolemDirector.TrapTelegraph + 60, rayDamage);
        }
    }
}
