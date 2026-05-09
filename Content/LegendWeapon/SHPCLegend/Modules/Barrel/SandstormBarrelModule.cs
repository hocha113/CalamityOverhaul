using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>
    /// 砂暴枪管：连续光束在前方卷起砂幕，磨蚀敌人并削弱穿过的敌对弹幕。
    /// </summary>
    internal sealed class SandstormBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        public override Color TintColor => new(220, 180, 90);

        public override void Apply(ref ShootContext ctx) {
            ctx.AttackSpeedMul += 0.10f;
            ctx.DamageMul += -0.10f;
            ctx.BeamExtraPierce += 1;
            ctx.ManaCostMul += 0.22f;
        }

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            if (beam.IsDerived || beam.Projectile.owner != Main.myPlayer) return;
            if ((Main.GameUpdateCount + (uint)beam.Projectile.whoAmI) % 24 != 0) return;
            Vector2 pos = beam.Projectile.Center + beam.Projectile.velocity.SafeNormalize(Vector2.UnitX) * 42f;
            Projectile.NewProjectile(beam.Projectile.GetSource_FromThis(),
                pos, beam.Projectile.velocity.SafeNormalize(Vector2.Zero) * 1.5f,
                ModContent.ProjectileType<SHPCSandCurtainProj>(),
                System.Math.Max(beam.Projectile.damage / 2, 1), 0f, beam.Projectile.owner);
        }
    }

    internal sealed class SHPCSandCurtainProj : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        public override void SetDefaults() {
            Projectile.width = 180;
            Projectile.height = 180;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 90;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override void AI() {
            Projectile.velocity *= 0.92f;
            float radiusSq = 120f * 120f;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.boss) continue;
                if (Vector2.DistanceSquared(npc.Center, Projectile.Center) > radiusSq) continue;
                npc.velocity *= 0.92f;
                if (Main.GameUpdateCount % 20 == 0) {
                    npc.SimpleStrikeNPC(Math.Max(Projectile.damage / 8, 1), 0, false, 0f, DamageClass.Magic, false, 0f, true);
                }
            }
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile hostile = Main.projectile[i];
                if (!hostile.active || !hostile.hostile || hostile.friendly) continue;
                if (Vector2.DistanceSquared(hostile.Center, Projectile.Center) > radiusSq) continue;
                hostile.velocity *= 0.96f;
            }
            if (Main.netMode == NetmodeID.Server || Main.GameUpdateCount % 4 != 0) return;
            PRTLoader.AddParticle(new PRT_CyberSquare(
                Projectile.Center + Main.rand.NextVector2Circular(115f, 70f),
                Main.rand.NextVector2Circular(2.5f, 1.2f),
                new Color(225, 190, 110), new Color(130, 95, 45),
                Main.rand.NextFloat(0.35f, 0.9f), Main.rand.Next(15, 34)));
        }
    }
}
