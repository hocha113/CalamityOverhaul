using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Frame
{
    /// <summary>虚空机匣，轨迹每 20 帧撕开微型虚空裂点：吸入→爆闪→暗痕缝合</summary>
    internal sealed class NullspaceFrameModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Frame;
        //虚空暗紫
        public override Color TintColor => new(100, 30, 160);

        private const int TearInterval = 20;
        private const float TearDamageRatio = 0.25f;
        private readonly Dictionary<int, int> _tearTimers = new();

        //虚空色板，暗紫吸光+亮紫强调
        private static readonly Color VoidDark = new(38, 12, 66);
        private static readonly Color VoidMain = new(120, 50, 200);
        private static readonly Color VoidAccent = new(185, 110, 255);

        public override void Apply(ref ShootContext ctx) {
            ctx.BeamLifeMul += 0.3f;
            ctx.DamageMul += -0.1f;
        }

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            if (beam.IsDerived || beam.Projectile.owner != Main.myPlayer
                || beam.Projectile.numUpdates != -1) return;
            int id = beam.Projectile.whoAmI;
            if (!_tearTimers.TryGetValue(id, out int t)) t = 0;
            t++;
            if (t >= TearInterval) {
                t = 0;
                if (beam.Projectile.owner == Main.myPlayer) {
                    SpawnTear(beam);
                }
            }
            _tearTimers[id] = t;
        }

        public override void OnBeamKill(CyberTraceBeamProj beam, int timeLeft) {
            _tearTimers.Remove(beam.Projectile.whoAmI);
        }

        private static void SpawnTear(CyberTraceBeamProj source) {
            int dmg = Math.Max((int)(source.Projectile.damage * TearDamageRatio), 1);
            //爆炸半径 60px，ai2 走生成包同步；ai0 仅调色（半径被 ai2 覆写），取 1 走冷青色板配虚空紫
            Projectile.NewProjectile(
                source.Projectile.GetSource_FromThis(),
                source.Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<CyberDetonationProj>(),
                dmg, 0f, source.Projectile.owner, ai0: 1f, ai1: 0f, ai2: 60f);
            SpawnTearVisuals(source);
        }

        /// <summary>裂点虚空加饰，拥有者端；吸入碎光+吸光暗痕（活过爆闪）+两粒紫方</summary>
        private static void SpawnTearVisuals(CyberTraceBeamProj source) {
            if (Main.netMode == NetmodeID.Server) return;
            Vector2 center = source.Projectile.Center;
            if (!VaultUtils.IsPointOnScreen(center - Main.screenPosition, 200)) return;

            //吸入，周围碎光被拽向裂点
            for (int i = 0; i < 4; i++) {
                Vector2 spawnPos = center + Main.rand.NextVector2CircularEdge(60f, 60f) * Main.rand.NextFloat(0.7f, 1f);
                PRTLoader.NewParticle<PRT_CyberConverge>(spawnPos, Vector2.Zero,
                    VoidMain, Main.rand.NextFloat(0.5f, 0.9f))
                    .Configure(center, VoidAccent, Main.rand.Next(12, 20));
            }

            //暗痕，爆闪(40f)熄灭后显形再捏拢缝合
            Vector2 drift = source.FlightDirection * 0.3f;
            PRTLoader.NewParticle<PRT_SHPCNullspaceScar>(center, drift,
                VoidDark, Main.rand.NextFloat(0.5f, 0.65f))
                .Configure(VoidAccent, Main.rand.Next(52, 64));

            //两粒紫方外散
            for (int i = 0; i < 2; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(3f, 3f);
                PRTLoader.NewParticle<PRT_CyberSquare>(center, vel,
                    VoidMain, Main.rand.NextFloat(0.7f, 1.2f)).Configure(VoidAccent, Main.rand.Next(16, 26));
            }
        }
    }
}
