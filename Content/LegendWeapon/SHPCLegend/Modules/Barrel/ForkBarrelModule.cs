using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>叉形枪管，追踪束每50帧分叉，whoAmI→timer+IsDerived</summary>
    internal sealed class ForkBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        //蓝绿
        public override Color TintColor => new(0, 220, 180);

        private const int ForkInterval = 50;
        private const float ForkDamageRatio = 0.5f;
        private const float ForkAngle = 0.44f;
        private readonly Dictionary<int, int> _timers = new();

        public override void Apply(ref ShootContext ctx) {
            ctx.DamageMul += -0.18f;
            ctx.ManaCostMul += 0.3f;
        }

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            if (beam.IsDerived) return;
            //IsDerived不入生成包，远端认不出子束，timer与爆点一并锁owner防幽灵爆点
            if (beam.Projectile.owner != Main.myPlayer || beam.Projectile.numUpdates != -1) return;
            int id = beam.Projectile.whoAmI;
            if (!_timers.TryGetValue(id, out int t)) t = 0;
            t++;
            if (t >= ForkInterval) {
                t = 0;
                SplitFlash(beam);
                SpawnFork(beam, -ForkAngle);
                SpawnFork(beam, ForkAngle);
            }
            _timers[id] = t;
        }

        /// <summary>分叉瞬间棱析爆点，纯表现</summary>
        private static void SplitFlash(CyberTraceBeamProj source) {
            Projectile proj = source.Projectile;
            if (!VaultUtils.IsPointOnScreen(proj.Center - Main.screenPosition, 150)) return;
            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.22f, Pitch = 0.8f }, proj.Center);
            Vector2 dir = proj.velocity.SafeNormalize(Vector2.UnitX);
            //沿两条子束方向锥形喷散，读作束流被劈开
            for (int side = -1; side <= 1; side += 2) {
                Vector2 forkDir = dir.RotatedBy(ForkAngle * side);
                for (int i = 0; i < 4; i++) {
                    Vector2 vel = forkDir.RotatedBy(Main.rand.NextFloat(-0.18f, 0.18f))
                        * Main.rand.NextFloat(3f, 7f);
                    PRTLoader.NewParticle<PRT_CyberSquare>(proj.Center, vel,
                        new Color(90, 255, 215), Main.rand.NextFloat(0.7f, 1.3f))
                        ?.Configure(new Color(10, 140, 110), Main.rand.Next(14, 24));
                }
            }
            //薄锐小冲环，PRT加色批需A=255
            PRTLoader.NewParticle<PRT_StarPulseRing>(proj.Center, Vector2.Zero,
                new Color(0, 220, 180), 0.05f)?.Configure(0.05f, 0.3f, 12);
        }

        public override void OnBeamKill(CyberTraceBeamProj beam, int timeLeft) {
            _timers.Remove(beam.Projectile.whoAmI);
        }

        private static void SpawnFork(CyberTraceBeamProj source, float angleOffset) {
            int dmg = Math.Max((int)(source.Projectile.damage * ForkDamageRatio), 1);
            Vector2 vel = source.Projectile.velocity.RotatedBy(angleOffset);
            int idx = Projectile.NewProjectile(
                source.Projectile.GetSource_FromThis(),
                source.Projectile.Center, vel,
                ModContent.ProjectileType<CyberTraceBeamProj>(),
                dmg, 0f, source.Projectile.owner, ai0: Main.rand.Next(3));
            if (idx >= 0 && idx < Main.maxProjectiles
                && Main.projectile[idx].ModProjectile is CyberTraceBeamProj fork) {
                fork.IsDerived = true;
                fork.LifeMul = 0.45f;
                fork.SpeedMul = source.SpeedMul;
            }
        }
    }
}
