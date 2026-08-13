using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Frame
{
    /// <summary>复制机匣，消亡原位短寿低伤回响，不递归</summary>
    internal sealed class ReplicatorFrameModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Frame;
        //复制蓝绿青
        public override Color TintColor => new(60, 220, 230);

        private static readonly Color EchoCyan = new(120, 235, 240);
        private static readonly Color EchoDim = new(25, 120, 130);

        public override void Apply(ref ShootContext ctx) {
            ctx.BeamLifeMul += -0.5f;
            ctx.DamageMul += -0.16f;
            ctx.ManaCostMul += 0.42f;
        }

        public override void OnBeamKill(CyberTraceBeamProj beam, int timeLeft) {
            if (beam.IsDerived || beam.SuppressDeathEffects || beam.Projectile.owner != Main.myPlayer) return;
            int dmg = Math.Max((int)(beam.Projectile.damage * 0.55f), 1);
            //回响沿原向略减速
            Vector2 vel = beam.Projectile.velocity.SafeNormalize(Vector2.UnitX) * 12f;
            //追踪倍率走 ai1 生成参数入同步包，生成后补写远端收不到
            int idx = Projectile.NewProjectile(beam.Projectile.GetSource_FromThis(),
                beam.Projectile.Center, vel,
                ModContent.ProjectileType<CyberTraceBeamProj>(),
                dmg, 0f, beam.Projectile.owner,
                ai0: (int)beam.Projectile.ai[0], ai1: MathHelper.Max(beam.Projectile.ai[1], 1.5f));
            if (idx >= 0 && idx < Main.maxProjectiles
                && Main.projectile[idx].ModProjectile is CyberTraceBeamProj echo) {
                echo.IsDerived = true;
                echo.LifeMul = 0.30f;
                echo.SpeedMul = beam.SpeedMul;
                echo.ExtraPierce = beam.ExtraPierce;
                SpawnEchoFlash(beam.Projectile.Center, vel);
            }
        }

        /// <summary>回响出生拍，两侧错位重影向新束头聚拢，拥有者端</summary>
        private static void SpawnEchoFlash(Vector2 pos, Vector2 vel) {
            if (Main.netMode == NetmodeID.Server) return;
            Vector2 dir = vel.SafeNormalize(Vector2.UnitX);
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            SoundEngine.PlaySound(SoundID.Item114 with { Volume = 0.2f, Pitch = -0.15f, MaxInstances = 3 }, pos);
            for (int s = -1; s <= 1; s++) {
                Vector2 off = perp * s * Main.rand.NextFloat(9f, 15f);
                //向前+向轴心聚拢
                Vector2 ghostVel = (dir * 9f - off * 0.45f) * 0.55f;
                PRTLoader.NewParticle<PRT_Light>(pos + off, ghostVel, EchoCyan, Main.rand.NextFloat(0.28f, 0.4f))
                    .Configure(Main.rand.Next(9, 14), 0.8f, 3f);
            }
            for (int k = 0; k < 3; k++) {
                PRTLoader.NewParticle<PRT_CyberSquare>(pos,
                    dir * Main.rand.NextFloat(1f, 3.5f) + Main.rand.NextVector2Circular(1f, 1f),
                    EchoCyan, Main.rand.NextFloat(0.35f, 0.7f)).Configure(EchoDim, Main.rand.Next(8, 16));
            }
        }
    }
}
