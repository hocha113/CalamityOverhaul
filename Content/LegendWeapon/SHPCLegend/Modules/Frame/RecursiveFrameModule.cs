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
    /// <summary>递归机匣，消亡自玩家沿原向重发低伤副本，IsDerived 防无限</summary>
    internal sealed class RecursiveFrameModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Frame;
        //递归暗金
        public override Color TintColor => new(200, 160, 40);

        private static readonly Color RecastGold = new(235, 195, 80);
        private static readonly Color RecastDim = new(150, 110, 30);

        public override void Apply(ref ShootContext ctx) {
            ctx.BeamLifeMul += -0.42f;
            ctx.DamageMul += -0.18f;
            ctx.ManaCostMul += 0.9f;
        }

        public override void OnBeamKill(CyberTraceBeamProj beam, int timeLeft) {
            if (beam.IsDerived || beam.SuppressDeathEffects || beam.Projectile.owner != Main.myPlayer) return;
            Player owner = Main.player[beam.Projectile.owner];
            if (owner == null || !owner.active) return;
            int dmg = Math.Max((int)(beam.Projectile.damage * 0.6f), 1);
            Vector2 dir = beam.Projectile.velocity.SafeNormalize(Vector2.UnitX);
            //追踪倍率走 ai1 生成参数入同步包，生成后补写远端收不到
            int idx = Projectile.NewProjectile(beam.Projectile.GetSource_FromThis(),
                owner.Center, dir * 14f,
                ModContent.ProjectileType<CyberTraceBeamProj>(),
                dmg, 0f, beam.Projectile.owner,
                ai0: Main.rand.Next(3), ai1: beam.Projectile.ai[1]);
            if (idx >= 0 && idx < Main.maxProjectiles
                && Main.projectile[idx].ModProjectile is CyberTraceBeamProj child) {
                child.IsDerived = true;
                child.LifeMul = 0.8f;
                child.ExtraPierce = beam.ExtraPierce;
                child.ChainCount = beam.ChainCount;
                child.ChainRange = beam.ChainRange;
                child.ExplodeOnHit = beam.ExplodeOnHit;
                child.ExplodeRadius = beam.ExplodeRadius;
                SpawnRecastFlash(owner, dir);
            }
        }

        /// <summary>递归重发拍，胸前沿射向拉伸光斑+方粒喷流，拥有者端</summary>
        private static void SpawnRecastFlash(Player owner, Vector2 dir) {
            if (Main.netMode == NetmodeID.Server) return;
            Vector2 muzzle = owner.Center + dir * 14f;
            SoundEngine.PlaySound(SoundID.Item12 with { Volume = 0.22f, Pitch = 0.45f, MaxInstances = 3 }, muzzle);
            for (int k = 0; k < 3; k++) {
                PRTLoader.NewParticle<PRT_Light>(muzzle,
                    dir * Main.rand.NextFloat(4f, 7f) + Main.rand.NextVector2Circular(0.6f, 0.6f),
                    RecastGold, Main.rand.NextFloat(0.3f, 0.45f)).Configure(Main.rand.Next(8, 14), 0.85f, 3f);
            }
            for (int k = 0; k < 4; k++) {
                PRTLoader.NewParticle<PRT_CyberSquare>(muzzle,
                    dir * Main.rand.NextFloat(1.5f, 5f) + Main.rand.NextVector2Circular(1.2f, 1.2f),
                    RecastGold, Main.rand.NextFloat(0.4f, 0.8f)).Configure(RecastDim, Main.rand.Next(10, 18));
            }
        }
    }
}
