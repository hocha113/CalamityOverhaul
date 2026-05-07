using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Frame
{
    /// <summary>
    /// 镜像机匣：每束新光束生成时立即派生一束180°对称镜像
    /// 用 HashSet 记录已处理的 whoAmI 确保只生成一次，OnBeamKill 清理
    /// </summary>
    internal sealed class MirrorFrameModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Frame;
        //镜像银白
        public override Color TintColor => new(200, 230, 255);

        private readonly HashSet<int> _mirrored = new();

        public override void Apply(ref ShootContext ctx) {
            ctx.DamageMul += -0.2f;
            ctx.ManaCostMul += 0.5f;
        }

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            if (beam.IsDerived) return;
            if (beam.Projectile.owner != Main.myPlayer) return;
            if (!_mirrored.Add(beam.Projectile.whoAmI)) return;
            int dmg = Math.Max((int)(beam.Projectile.damage * 0.7f), 1);
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
