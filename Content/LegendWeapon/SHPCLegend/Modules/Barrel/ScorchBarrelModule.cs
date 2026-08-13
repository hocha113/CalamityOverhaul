using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>灼烧激光枪管，命中灼烧 DoT；烙点干烧焦痕，与岩浆的流体喷涌分野</summary>
    internal sealed class ScorchBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        //橙红
        public override Color TintColor => new(255, 80, 20);

        public override void Apply(ref ShootContext ctx) {
            ctx.LaserMode = true;
            ctx.LaserScorchOnHit = true;
            ctx.LaserScorchDuration = 40;
            ctx.DamageMul += -0.36f;
            ctx.ManaCostMul += 0.78f;
        }

        public override void OnLaserAI(CyberPrismLaserProj laser) {
            //主题换灼烧橙红
            laser.ThemeCore = new Color(255, 160, 30);
            laser.ThemeGlow = new Color(220, 80, 5);
            laser.ThemeAura = new Color(140, 30, 0);
            laser.ThemeParticleMain = new Color(255, 140, 20);
            laser.ThemeParticleEdge = new Color(200, 50, 5);
        }

        public override void OnLaserHitNPC(CyberPrismLaserProj laser, NPC target, NPC.HitInfo hit, int damageDone) {
            //干烧焦痕,烙点微闪+干火星坠落+焦屑;激光5帧一跳,节流防刷屏
            if (Main.netMode == NetmodeID.Server || !Main.rand.NextBool(3)) return;
            Vector2 burnPos = target.Center + Main.rand.NextVector2Circular(target.width * 0.3f, target.height * 0.3f);
            //烙点过曝一瞬
            PRTLoader.NewParticle<PRT_Sparkle>(burnPos, Vector2.Zero, new Color(255, 235, 190), 0.42f)
                .Configure(new Color(255, 120, 20), 7, 0f, 0.8f);
            //干火星,重力坠落速冷,无烟无液
            for (int i = 0; i < 3; i++) {
                Vector2 vel = new(Main.rand.NextFloat(-2.4f, 2.4f), Main.rand.NextFloat(-3.6f, -1f));
                PRTLoader.NewParticle<PRT_Spark>(burnPos, vel,
                    Color.Lerp(new Color(255, 210, 90), new Color(225, 70, 10), Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.4f, 0.8f)).Configure(true, Main.rand.Next(14, 26));
            }
            //焦屑,近黑碎片带烬红断缘
            for (int i = 0; i < 2; i++) {
                Vector2 vel = new(Main.rand.NextFloat(-1.6f, 1.6f), Main.rand.NextFloat(-2.2f, -0.4f));
                PRTLoader.NewParticle<PRT_SHPCObsidianChip>(burnPos, vel, new Color(32, 20, 15),
                    Main.rand.NextFloat(0.4f, 0.7f)).Configure(new Color(255, 105, 25), Main.rand.Next(18, 30), 0.85f);
            }
        }
    }
}
