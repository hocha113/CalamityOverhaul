using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Frame
{
    /// <summary>镜像机匣，新束派生 180° 对称镜像，HashSet 防重复</summary>
    internal sealed class MirrorFrameModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Frame;
        //镜像银白
        public override Color TintColor => new(200, 230, 255);

        private static readonly Color MirrorSilver = new(200, 230, 255);
        private static readonly Color MirrorDim = new(90, 130, 190);

        private readonly HashSet<int> _mirrored = new();

        public override void Apply(ref ShootContext ctx) {
            ctx.DamageMul += -0.42f;
            ctx.ManaCostMul += 0.9f;
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
                SpawnMirrorFlash(beam.Projectile.Center, mirrorVel);
            }
        }

        /// <summary>镜像出生拍，分裂点细环+对称双向拉伸光斑，拥有者端</summary>
        private static void SpawnMirrorFlash(Vector2 pos, Vector2 mirrorVel) {
            if (Main.netMode == NetmodeID.Server) return;
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.22f, Pitch = 0.6f, MaxInstances = 2 }, pos);
            PRTLoader.NewParticle<PRT_StarPulseRing>(pos, Vector2.Zero, MirrorSilver, 0.05f)
                .Configure(0.05f, 0.26f, 12);
            Vector2 dir = mirrorVel.SafeNormalize(Vector2.UnitX);
            //正反两向对称喷散，读作镜面分裂
            for (int s = -1; s <= 1; s += 2) {
                for (int k = 0; k < 3; k++) {
                    Vector2 vel = dir * s * Main.rand.NextFloat(3f, 7f) + Main.rand.NextVector2Circular(0.7f, 0.7f);
                    PRTLoader.NewParticle<PRT_Light>(pos, vel, MirrorSilver, Main.rand.NextFloat(0.3f, 0.45f))
                        .Configure(Main.rand.Next(10, 16), 0.85f, 3f);
                }
                PRTLoader.NewParticle<PRT_CyberSquare>(pos, dir * s * Main.rand.NextFloat(1.5f, 4f),
                    MirrorSilver, Main.rand.NextFloat(0.35f, 0.7f)).Configure(MirrorDim, Main.rand.Next(8, 16));
            }
        }

        public override void OnBeamKill(CyberTraceBeamProj beam, int timeLeft) {
            _mirrored.Remove(beam.Projectile.whoAmI);
        }
    }
}
