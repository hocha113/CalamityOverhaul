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
            if (beam.Projectile.owner == Main.myPlayer) {
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
            }

            //收束粒子是表现层,所有客户端可见,只挡服务端;旧版误随施力一起锁 owner,旁观者看不到引力场
            if (Main.netMode == NetmodeID.Server) return;
            if (Main.GameUpdateCount % 6 != 0) return;
            for (int i = 0; i < 2; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = Main.rand.NextFloat(46f, 96f);
                //锚向光束前路,粒子按到达时序收束并带切向卷入,读作螺旋坠入
                Vector2 sink = beam.Projectile.Center + beam.Projectile.velocity * 6f;
                Vector2 spawnPos = sink + angle.ToRotationVector2() * dist;
                int fallFrames = Main.rand.Next(14, 19);
                Vector2 inward = (sink - spawnPos) / fallFrames * 1.3f;
                Vector2 vel = inward.RotatedBy(0.42f * (Main.rand.NextBool() ? 1f : -1f));
                PRTLoader.NewParticle<PRT_CyberSquare>(spawnPos, vel, new Color(150, 110, 255),
                    Main.rand.NextFloat(0.5f, 1.0f)).Configure(new Color(80, 40, 200), fallFrames + 3);
            }
        }
    }
}
