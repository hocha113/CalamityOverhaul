using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>棱光枪管，两侧 RGB 色散尾，命中光屑+少量暴击/额外束</summary>
    internal sealed class PrismaticBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        //彩虹偏品红
        public override Color TintColor => new(255, 90, 200);

        public override void Apply(ref ShootContext ctx) {
            ctx.BeamCountAdd += 1;
            ctx.SpreadMul += 0.18f;
            ctx.DamageMul += -0.18f;
            ctx.CritAdd += 5;
        }

        private static readonly Color RChannel = new(255, 60, 80);
        private static readonly Color GChannel = new(60, 255, 130);
        private static readonly Color BChannel = new(80, 130, 255);

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            if (beam.IsDerived) return;
            if (Main.netMode == NetmodeID.Server) return;
            //每3帧 RGB 色散尾
            if (Main.GameUpdateCount % 3 != 0) return;

            Vector2 forward = beam.Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 perp = forward.RotatedBy(MathHelper.PiOver2);
            Color[] channels = { RChannel, GChannel, BChannel };
            //三色横向拉开+纵向错位，读作三条平行光谱缕而非白噪点
            for (int i = 0; i < 3; i++) {
                float side = i - 1f; //-1, 0, 1
                Vector2 offset = perp * side * 10f - forward * (i * 7f);
                Vector2 vel = -forward * 1.2f + perp * side * 1.1f;
                PRTLoader.NewParticle<PRT_CyberSquare>(beam.Projectile.Center + offset, vel, channels[i], Main.rand.NextFloat(0.42f, 0.75f)).Configure(Color.White, Main.rand.Next(12, 20));
            }
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.netMode == NetmodeID.Server) return;
            //白光进三色出，沿入射向分光成扇
            Vector2 inDir = beam.Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Color[] channels = { RChannel, GChannel, BChannel };
            for (int c = 0; c < 3; c++) {
                float fanAngle = (c - 1f) * 0.42f;
                for (int k = 0; k < 3; k++) {
                    Vector2 vel = inDir.RotatedBy(fanAngle + Main.rand.NextFloat(-0.12f, 0.12f))
                        * Main.rand.NextFloat(4.2f, 7.5f);
                    PRTLoader.NewParticle<PRT_CyberSquare>(target.Center, vel, channels[c], Main.rand.NextFloat(0.7f, 1.3f)).Configure(Color.White, Main.rand.Next(15, 28));
                }
            }
            PRTLoader.NewParticle<PRT_StarPulseRing>(target.Center, Vector2.Zero, new Color(245, 245, 255), 0.04f).Configure(0.04f, 0.26f, 12);
        }
    }
}
