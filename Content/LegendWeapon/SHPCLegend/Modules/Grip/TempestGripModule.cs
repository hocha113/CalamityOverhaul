using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Grip
{
    /// <summary>
    /// 风暴握把：每累计若干次命中触发一次"风暴爆发"，在最近一次命中处朝四周喷射多道电弧
    /// 电弧由 <see cref="CyberDataArcProj"/> 渲染，伤害与视觉同时承担节奏感打击
    /// </summary>
    internal sealed class TempestGripModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Grip;
        //风暴电黄
        public override Color TintColor => new(255, 230, 60);

        private const int HitsPerBurst = 7;
        private const int ArcsPerBurst = 5;
        private const float ArcLength = 220f;

        private int _hitCount;

        public override void Apply(ref ShootContext ctx) {
            ctx.DamageMul += -0.1f;
            ctx.ManaCostMul += 0.20f;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            TryBurst(beam.Projectile, target.Center, beam.Projectile.damage);
        }

        public override void OnLaserHitNPC(CyberPrismLaserProj laser, NPC target, NPC.HitInfo hit, int damageDone) {
            //激光命中频率高，每 2 次中一次记入风暴累计
            if (Main.rand.NextBool(2)) {
                TryBurst(laser.Projectile, target.Center, laser.Projectile.damage);
            }
        }

        private void TryBurst(Projectile source, Vector2 hitCenter, int sourceDamage) {
            if (source.owner != Main.myPlayer) return;
            _hitCount++;
            if (_hitCount < HitsPerBurst) return;
            _hitCount = 0;

            int dmg = Math.Max((int)(sourceDamage * 0.40f), 1);
            float baseAngle = Main.rand.NextFloat() * MathHelper.TwoPi;
            for (int i = 0; i < ArcsPerBurst; i++) {
                float angle = baseAngle + MathHelper.TwoPi * i / ArcsPerBurst;
                Vector2 delta = angle.ToRotationVector2() * ArcLength;
                int idx = Projectile.NewProjectile(source.GetSource_FromThis(),
                    hitCenter, Vector2.Zero,
                    ModContent.ProjectileType<CyberDataArcProj>(),
                    dmg, 0f, source.owner,
                    ai0: delta.X, ai1: delta.Y);
                if (idx >= 0 && idx < Main.maxProjectiles
                    && Main.projectile[idx].ModProjectile is CyberDataArcProj arc) {
                    arc.CoreColor = new Color(255, 245, 200).ToVector3();
                    arc.GlowColor = new Color(255, 200, 60).ToVector3();
                }
            }
        }
    }
}
