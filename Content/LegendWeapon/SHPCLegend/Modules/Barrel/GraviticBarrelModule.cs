using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>引力枪管，OnBeamAI 近距牵引 NPC</summary>
    internal sealed class GraviticBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        //深紫蓝
        public override Color TintColor => new(110, 90, 220);

        public override void Apply(ref ShootContext ctx) {
            ctx.SpreadMul += -0.16f;
            ctx.BeamLifeMul += 0.2f;
            ctx.BeamSpeedMul += -0.12f;
            ctx.HomingMul += -0.24f;
        }

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            //仅 owner 施力，派生束可参与
            if (beam.Projectile.owner != Main.myPlayer) return;
            const float pullRange = 180f;
            const float pullStrength = 0.18f;
            float rangeSq = pullRange * pullRange;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.boss) continue;
                Vector2 toBeam = beam.Projectile.Center - npc.Center;
                if (toBeam.LengthSquared() > rangeSq) continue;
                if (toBeam.LengthSquared() < 16f) continue;
                npc.velocity += toBeam.SafeNormalize(Vector2.Zero) * pullStrength;
            }

            //每6帧轨道粒子
            if (Main.netMode == NetmodeID.Server) return;
            if (Main.GameUpdateCount % 6 != 0) return;
            for (int i = 0; i < 2; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 offset = angle.ToRotationVector2() * Main.rand.NextFloat(40f, 90f);
                Vector2 spawnPos = beam.Projectile.Center + offset;
                Vector2 vel = (beam.Projectile.Center - spawnPos).SafeNormalize(Vector2.Zero) * 4f;
                PRTLoader.NewParticle<PRT_CyberSquare>(spawnPos, vel, new Color(150, 110, 255), Main.rand.NextFloat(0.5f, 1.0f)).Configure(new Color(80, 40, 200), Main.rand.Next(10, 22));
            }
        }
    }
}
