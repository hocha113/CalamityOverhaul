using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>
    /// 黑曜石枪管：命中叠加裂纹，满层碎裂为自动寻敌的火山玻璃碎片。
    /// </summary>
    internal sealed class ObsidianBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        public override Color TintColor => new(95, 55, 135);

        public override void Apply(ref ShootContext ctx) {
            ctx.AttackSpeedMul += -0.12f;
            ctx.DamageMul += 0.08f;
            ctx.ManaCostMul += 0.18f;
            ctx.BeamExtraPierce += 1;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (target.TryGetGlobalNPC(out SHPCNPCEffects eff)) {
                eff.ApplyObsidianCrack(target, 300, beam.Projectile.owner, System.Math.Max(damageDone / 3, 1));
            }
        }

        public override void OnOrbDetonation(CyberChargeOrbProj orb) {
            if (orb.Projectile.owner != Main.myPlayer) return;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage) continue;
                if (Vector2.DistanceSquared(npc.Center, orb.Projectile.Center) > 520f * 520f) continue;
                if (!npc.TryGetGlobalNPC(out SHPCNPCEffects eff)) continue;
                if (eff.ObsidianCrackTime <= 0 || eff.ObsidianCrackOwner != orb.Projectile.owner) continue;
                SHPCNPCEffects.BurstObsidian(npc, orb.Projectile.owner, System.Math.Max(orb.Projectile.damage / 3, 1));
                eff.ObsidianCrackTime = 0;
                eff.ObsidianCrackStacks = 0;
            }
        }
    }

    internal sealed class SHPCObsidianShardProj : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 90;
            Projectile.extraUpdates = 1;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, new Vector3(0.6f, 0.18f, 0.08f));
            if (Main.netMode == NetmodeID.Server || Main.GameUpdateCount % 3 != 0) return;
            PRTLoader.AddParticle(new PRT_CyberSquare(
                Projectile.Center, -Projectile.velocity.SafeNormalize(Vector2.Zero) * 1.5f,
                new Color(80, 55, 120), new Color(255, 90, 45),
                Main.rand.NextFloat(0.35f, 0.8f), Main.rand.Next(8, 16)));
        }
    }
}
