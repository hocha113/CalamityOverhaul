using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Frame
{
    /// <summary>镜像机匣，新束派生 180° 对称镜像，HashSet 防重复</summary>
    internal sealed class MirrorFrameModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Frame;
        //镜像银白
        public override Color TintColor => new(200, 230, 255);

        private readonly HashSet<int> _mirrored = new();

        public override void Apply(ref ShootContext ctx) {
            ctx.DamageMul += -0.3f;
            ctx.ManaCostMul += 0.6f;
        }

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            if (beam.IsDerived) return;
            if (beam.Projectile.owner != Main.myPlayer) return;
            if (!_mirrored.Add(beam.Projectile.whoAmI)) return;
            int dmg = Math.Max((int)(beam.Projectile.damage * 0.45f), 1);
            Vector2 mirrorVel = -beam.Projectile.velocity;
            int idx = Projectile.NewProjectile(
                beam.Projectile.GetSource_FromThis(),
                beam.Projectile.Center, mirrorVel,
                ModContent.ProjectileType<CyberTraceBeamProj>(),
                dmg, 0f, beam.Projectile.owner, ai0: (int)beam.Projectile.ai[0]);
            if (idx >= 0 && idx < Main.maxProjectiles
                && Main.projectile[idx].ModProjectile is CyberTraceBeamProj mirror) {
                mirror.IsDerived = true;
                mirror.LifeMul = beam.LifeMul;
                mirror.SpeedMul = beam.SpeedMul;
                mirror.ExtraPierce = beam.ExtraPierce;
            }
        }

        public override void OnBeamKill(CyberTraceBeamProj beam, int timeLeft) {
            _mirrored.Remove(beam.Projectile.whoAmI);
        }
    }
}
