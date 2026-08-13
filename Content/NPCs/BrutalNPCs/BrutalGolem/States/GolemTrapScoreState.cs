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

        private int hopTimer;

        public override IGolemState OnUpdate(GolemStateContext context) {
            NPC npc = context.Npc;
            context.FrameMode = 0;
            RestoreTileCollide(context);
            context.VeinGlow = Math.Max(context.VeinGlow, 0.4f);

            //布谱（服务端一次性）
            if (Timer == 8 && !VaultUtils.isClient) {
                if (context.Sundered) {
                    PlantMixedScore(context);
                }
                else {
                    PlantSpikeRipple(context, context.Target.Center.X, false);
                }
            }
            //一阶段第二小节：反向涟漪，切分节奏
            if (Timer == Tempo(context, 120) && !context.Sundered && !VaultUtils.isClient) {
                PlantSpikeRipple(context, context.Target.Center.X, true);
            }
            //二阶段第二小节：追加一列追踪玩家的尖刺
            if (Timer == Tempo(context, 150) && context.Sundered && !VaultUtils.isClient) {
                PlantSpikeRipple(context, context.Target.Center.X, Main.rand.NextBool());
            }

            //躯干踏步压近（机关不是让Boss挂机的借口）
            if (OnGround(npc)) {
                GroundBrake(npc, 0.75f);
                if (++hopTimer >= Tempo(context, 54)) {
                    hopTimer = 0;
                    float dx = context.Target.Center.X - npc.Center.X;
                    if (Math.Abs(dx) > 240f) {
                        LaunchJump(npc, MathHelper.Clamp(dx / 70f, -9f, 9f), -8.5f);
                        if (!VaultUtils.isClient) {
                            npc.netUpdate = true;
                        }
                    }
                }
            }
            else {
                context.FrameMode = 2;
                npc.damage = npc.defDamage;
                AirSteer(context, 0.1f, 9f);
            }
            if (OnGround(npc)) {
                npc.damage = 0;
            }

            Timer++;
            int duration = Tempo(context, context.Sundered ? 330 : 290);
            if (Timer >= duration && !VaultUtils.isClient) {
                return new GolemConnectorState();
            }
            return null;
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
            PlantSpikeRipple(context, target.Center.X, false);

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
