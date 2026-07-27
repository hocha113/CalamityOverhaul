using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Frame
{
    /// <summary>虚空机匣，轨迹每 20 帧留微型爆炸残影</summary>
    internal sealed class NullspaceFrameModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Frame;
        //虚空暗紫
        public override Color TintColor => new(100, 30, 160);

        private const int TearInterval = 20;
        private const float TearDamageRatio = 0.10f;
        private readonly Dictionary<int, int> _tearTimers = new();

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
            int idx = Projectile.NewProjectile(
                source.Projectile.GetSource_FromThis(),
                source.Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<CyberDetonationProj>(),
                dmg, 0f, source.Projectile.owner, ai0: 0.15f);
            if (idx >= 0 && idx < Main.maxProjectiles) {
                //爆炸半径 60px
                Main.projectile[idx].localAI[2] = 60f;
            }
        }
    }
}
