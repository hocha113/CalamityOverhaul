using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>螺旋枪管，双束绕轴缠绕，whoAmI→相位，奇偶差π</summary>
    internal sealed class HelixBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        //青紫偏紫
        public override Color TintColor => new(160, 80, 255);

        private readonly Dictionary<int, float> _angles = new();

        public override void Apply(ref ShootContext ctx) {
            ctx.SpreadMul += -0.88f;
            ctx.DamageMul += -0.12f;
            ctx.BeamSpeedMul += -0.12f;
        }

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            if (beam.IsDerived) return;
            int id = beam.Projectile.whoAmI;
            if (!_angles.TryGetValue(id, out float angle)) {
                angle = id % 2 == 0 ? 0f : MathHelper.Pi;
            }
            angle += 0.1f;
            _angles[id] = angle;
            //偏移前记速，归一化沿用
            float baseSpeed = beam.Projectile.velocity.Length();
            Vector2 dir = beam.Projectile.velocity.SafeNormalize(Vector2.Zero);
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            float nudge = MathF.Sin(angle) * 0.5f;
            beam.Projectile.velocity += perp * nudge;
            //归一化回原速
            if (baseSpeed > 0.01f) {
                beam.Projectile.velocity = beam.Projectile.velocity.SafeNormalize(Vector2.Zero) * baseSpeed;
            }
        }

        public override void OnBeamKill(CyberTraceBeamProj beam, int timeLeft) {
            _angles.Remove(beam.Projectile.whoAmI);
        }
    }
}
