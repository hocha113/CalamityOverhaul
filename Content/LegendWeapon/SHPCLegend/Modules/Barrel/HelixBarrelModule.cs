using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>
    /// 螺旋枪管：强制发射2束光束，两束绕共同前进轴做螺旋缠绕
    /// 用 whoAmI→angle 字典独立累积相位，奇偶 ID 初始相位差π形成对称螺旋
    /// </summary>
    internal sealed class HelixBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        //螺旋青紫双色偏紫
        public override Color TintColor => new(160, 80, 255);

        private readonly Dictionary<int, float> _angles = new();

        public override void Apply(ref ShootContext ctx) {
            ctx.SpreadMul += -1f;
            ctx.DamageMul += -0.1f;
            ctx.BeamSpeedMul += -0.1f;
        }

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            if (beam.IsDerived) return;
            int id = beam.Projectile.whoAmI;
            if (!_angles.TryGetValue(id, out float angle)) {
                angle = id % 2 == 0 ? 0f : MathHelper.Pi;
            }
            angle += 0.1f;
            _angles[id] = angle;
            //在施加偏移前记录当前速度大小，归一化时沿用，无需硬编码基础速度
            float baseSpeed = beam.Projectile.velocity.Length();
            Vector2 dir = beam.Projectile.velocity.SafeNormalize(Vector2.Zero);
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            float nudge = MathF.Sin(angle) * 0.5f;
            beam.Projectile.velocity += perp * nudge;
            //归一化回施加偏移前的速度大小
            if (baseSpeed > 0.01f) {
                beam.Projectile.velocity = beam.Projectile.velocity.SafeNormalize(Vector2.Zero) * baseSpeed;
            }
        }

        public override void OnBeamKill(CyberTraceBeamProj beam, int timeLeft) {
            _angles.Remove(beam.Projectile.whoAmI);
        }
    }
}
