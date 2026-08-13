using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>折射枪管，激光命中两侧弹射追踪副束，OnLaserHitNPC+CD</summary>
    internal sealed class ReflectionBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        //青蓝
        public override Color TintColor => new(100, 180, 255);

        //棱面闪配色，冰白芯+青蓝缘
        private static readonly Color PrismCore = new(190, 235, 255);
        private static readonly Color PrismEdge = new(100, 180, 255);

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
            //折射轻响，与霰射碎裂音错开音高
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.26f, Pitch = 0.85f, MaxInstances = 3 }, target.Center);
            for (int i = 0; i < 2; i++) {
                float ang = baseDir.ToRotation() + (i == 0 ? MathHelper.PiOver4 : -MathHelper.PiOver4);
                Vector2 vel = ang.ToRotationVector2() * 14f;
                //ai1 追踪倍率进生成参数随生成包同步，远端轨迹一致
                int idx = Projectile.NewProjectile(laser.Projectile.GetSource_FromThis(),
                    target.Center, vel,
                    ModContent.ProjectileType<CyberTraceBeamProj>(),
                    dmg, 0f, laser.Projectile.owner,
                    ai0: Main.rand.Next(3), ai1: 1.5f);
                if (idx >= 0 && idx < Main.maxProjectiles) {
                    if (Main.projectile[idx].ModProjectile is CyberTraceBeamProj beam) {
                        beam.IsDerived = true;
                        beam.LifeMul = 0.5f;
                    }
                }
                //弹射向棱面闪，标记折射事件
                for (int k = 0; k < 3; k++) {
                    PRTLoader.NewParticle<PRT_Sparkle>(target.Center + Main.rand.NextVector2Circular(6f, 6f),
                        vel * Main.rand.NextFloat(0.12f, 0.3f),
                        PrismCore, Main.rand.NextFloat(0.4f, 0.7f))
                        ?.Configure(PrismEdge, Main.rand.Next(10, 18), Main.rand.NextFloat(-0.2f, 0.2f), 0.8f);
                }
            }
        }
    }
}
