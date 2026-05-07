using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>
    /// 引力枪管：光束飞行时持续在自身周围生成微型引力场，将临近敌人朝光束牵引
    /// 通过 OnBeamAI 钩子直接对范围内 NPC 施加少量速度增量，去耦合且不依赖共享状态
    /// </summary>
    internal sealed class GraviticBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        //引力深紫蓝
        public override Color TintColor => new(110, 90, 220);

        public override void Apply(ref ShootContext ctx) {
            ctx.SpreadMul += -0.2f;
            ctx.BeamLifeMul += 0.25f;
            ctx.BeamSpeedMul += -0.1f;
            ctx.HomingMul += -0.2f;
        }

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            //仅本地玩家侧施力，避免多端冲突；派生光束也允许参与（轻微吸力是非破坏性的）
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

            //每 6 帧在光束周围生成轨道粒子，强化"引力场"视觉
            if (Main.netMode == NetmodeID.Server) return;
            if (Main.GameUpdateCount % 6 != 0) return;
            for (int i = 0; i < 2; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 offset = angle.ToRotationVector2() * Main.rand.NextFloat(40f, 90f);
                Vector2 spawnPos = beam.Projectile.Center + offset;
                Vector2 vel = (beam.Projectile.Center - spawnPos).SafeNormalize(Vector2.Zero) * 4f;
                PRTLoader.AddParticle(new PRT_CyberSquare(
                    spawnPos, vel,
                    new Color(150, 110, 255), new Color(80, 40, 200),
                    Main.rand.NextFloat(0.5f, 1.0f), Main.rand.Next(10, 22)));
            }
        }
    }
}
