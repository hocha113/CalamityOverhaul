using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Optic
{
    /// <summary>
    /// 联机瞄具：命中 NPC 时自动在被击目标与最近的另一只敌人之间生成一道数据电弧
    /// 电弧由 <see cref="CyberDataArcProj"/> 渲染，对沿线敌人造成一次性附带伤害
    /// </summary>
    internal sealed class CrosslinkOpticModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Optic;
        //数据链电青
        public override Color TintColor => new(80, 200, 255);

        private const float LinkRange = 360f;

        public override void Apply(ref ShootContext ctx) {
            ctx.BeamChainCount += 1;
            ctx.BeamChainRange = MathF.Max(ctx.BeamChainRange, 240f) + 40f;
            ctx.DamageMul += -0.1f;
            ctx.ManaCostMul += 0.2f;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (beam.Projectile.owner != Main.myPlayer) return;
            //找最近的另一只敌人作为电弧另一端
            NPC other = FindClosestOther(target.Center, target.whoAmI, LinkRange);
            if (other == null) return;
            SpawnArc(beam.Projectile, target.Center, other.Center, beam.Projectile.damage);
        }

        public override void OnLaserHitNPC(CyberPrismLaserProj laser, NPC target, NPC.HitInfo hit, int damageDone) {
            if (laser.Projectile.owner != Main.myPlayer) return;
            //激光命中频率更高，节流为 20% 概率触发
            if (Main.rand.NextFloat() > 0.2f) return;
            NPC other = FindClosestOther(target.Center, target.whoAmI, LinkRange);
            if (other == null) return;
            SpawnArc(laser.Projectile, target.Center, other.Center, laser.Projectile.damage);
        }

        private static NPC FindClosestOther(Vector2 from, int excludeWhoAmI, float range) {
            float bestSq = range * range;
            NPC best = null;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage) continue;
                if (npc.whoAmI == excludeWhoAmI) continue;
                float distSq = Vector2.DistanceSquared(npc.Center, from);
                if (distSq < bestSq) {
                    bestSq = distSq;
                    best = npc;
                }
            }
            return best;
        }

        private static void SpawnArc(Projectile source, Vector2 start, Vector2 end, int sourceDamage) {
            Vector2 delta = end - start;
            int dmg = Math.Max((int)(sourceDamage * 0.45f), 1);
            int idx = Projectile.NewProjectile(source.GetSource_FromThis(),
                start, Vector2.Zero,
                ModContent.ProjectileType<CyberDataArcProj>(),
                dmg, 0f, source.owner,
                ai0: delta.X, ai1: delta.Y);
            if (idx >= 0 && idx < Main.maxProjectiles
                && Main.projectile[idx].ModProjectile is CyberDataArcProj arc) {
                arc.CoreColor = new Color(220, 240, 255).ToVector3();
                arc.GlowColor = new Color(80, 200, 255).ToVector3();
            }
        }
    }
}
