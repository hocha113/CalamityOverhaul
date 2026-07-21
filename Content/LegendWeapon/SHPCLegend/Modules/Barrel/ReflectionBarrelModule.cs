using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>折射枪管，激光命中两侧弹射追踪副束，OnLaserHitNPC+CD</summary>
    internal sealed class ReflectionBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        //青蓝
        public override Color TintColor => new(100, 180, 255);

        private int _reflectCooldown;

        public override void Apply(ref ShootContext ctx) {
            ctx.LaserMode = true;
            ctx.DamageMul += -0.12f;
            ctx.ManaCostMul += 1f;
        }

        public override void OnLaserHitNPC(CyberPrismLaserProj laser, NPC target, NPC.HitInfo hit, int damageDone) {
            if (laser.Projectile.owner != Main.myPlayer) return;
            if (_reflectCooldown > 0) {
                _reflectCooldown--;
                return;
            }
            _reflectCooldown = 30;
            int dmg = Math.Max(laser.Projectile.damage / 2, 1);
            Vector2 baseDir = laser.Projectile.rotation.ToRotationVector2();
            for (int i = 0; i < 2; i++) {
                float ang = baseDir.ToRotation() + (i == 0 ? MathHelper.PiOver4 : -MathHelper.PiOver4);
                Vector2 vel = ang.ToRotationVector2() * 14f;
                int idx = Projectile.NewProjectile(laser.Projectile.GetSource_FromThis(),
                    target.Center, vel,
                    ModContent.ProjectileType<CyberTraceBeamProj>(),
                    dmg, 0f, laser.Projectile.owner,
                    ai0: Main.rand.Next(3));
                if (idx >= 0 && idx < Main.maxProjectiles) {
                    Main.projectile[idx].ai[1] = 1.5f;
                    if (Main.projectile[idx].ModProjectile is CyberTraceBeamProj beam) {
                        beam.IsDerived = true;
                        beam.LifeMul = 0.5f;
                    }
                }
            }
        }
    }
}
