using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using InnoVault.PRT;
using Terraria;

namespace CalamityOverhaul.Content.Items.Magic.Everdeeps
{
    /// <summary>
    /// 永渊共用色板与水花派发。离散液体复用海洋洪流的池化 PRT
    /// (<see cref="PRT_OceanCurrentDroplet"/> 等),色板换成深渊系
    /// </summary>
    internal static class EverdeepVFX
    {
        /// <summary>深渊水体,近黑的暗蓝</summary>
        public static readonly Color AbyssDeep = new(18, 44, 82);
        /// <summary>中层水色</summary>
        public static readonly Color AbyssBlue = new(30, 88, 152);
        /// <summary>深渊生物光青辉</summary>
        public static readonly Color AbyssGlow = new(86, 214, 234);
        /// <summary>泡沫苍白</summary>
        public static readonly Color AbyssFoam = new(188, 232, 246);

        /// <summary>随机一档深渊水色:深水到生物光之间(液滴走加色绘制,基色偏亮些才立得住)</summary>
        public static Color RandomWater(float glowBias = 0f)
            => Color.Lerp(AbyssDeep, AbyssGlow, Main.rand.NextFloat(0.30f, 0.75f + glowBias));

        /// <summary>飞行途中甩出的水滴,速度拉伸由 PRT 自理</summary>
        public static void ShedDroplet(Vector2 pos, Vector2 vel, float scale = 1f) {
            if (Main.dedServ) {
                return;
            }
            PRTLoader.NewParticle<PRT_OceanCurrentDroplet>(pos, vel, RandomWater()
                , Main.rand.NextFloat(0.11f, 0.20f) * scale)
                ?.Configure(Main.rand.Next(26, 44), gravityPerFrame: 0.22f, dragMultiplier: 0.985f
                    , turbulence: Main.rand.NextFloat(0.015f, 0.04f), canSplit: false);
        }

        /// <summary>水花爆发:命中/折返/消散共用,能量随撞击速度走</summary>
        public static void SplashBurst(Vector2 pos, Vector2 impactVel, float scale = 1f) {
            if (Main.dedServ) {
                return;
            }
            float impactSpeed = MathHelper.Clamp(impactVel.Length(), 2f, 26f);
            float energy = MathHelper.Clamp(impactSpeed / 15f, 0.4f, 1.5f) * scale;
            Vector2 rebound = (-impactVel).SafeNormalize(-Vector2.UnitY);
            Vector2 tangent = rebound.RotatedBy(MathHelper.PiOver2);

            PRTLoader.NewParticle<PRT_OceanCurrentWake>(pos, Vector2.Zero, AbyssGlow, 0.06f * scale)
                ?.Configure(tangent, new Vector2(1f, 0.5f), 0.42f * energy, Main.rand.Next(11, 16));

            int dropletCount = (int)MathHelper.Clamp(7f + impactSpeed * 0.6f * scale, 7f, 22f);
            for (int i = 0; i < dropletCount; i++) {
                float lateral = Main.rand.NextFloat(-0.9f, 0.9f);
                Vector2 dir = (rebound * Main.rand.NextFloat(0.35f, 1f) + tangent * lateral
                    - Vector2.UnitY * Main.rand.NextFloat(0.05f, 0.35f)).SafeNormalize(rebound);
                float speed = Main.rand.NextFloat(2f, 5.5f + energy * 3.6f);
                PRTLoader.NewParticle<PRT_OceanCurrentDroplet>(pos + Main.rand.NextVector2Circular(6f, 6f)
                    , dir * speed, RandomWater(), Main.rand.NextFloat(0.12f, 0.24f) * MathHelper.Clamp(scale, 0.6f, 1.2f))
                    ?.Configure(Main.rand.Next(30, 55), gravityPerFrame: 0.26f, dragMultiplier: 0.984f
                        , turbulence: Main.rand.NextFloat(0.01f, 0.03f), canSplit: speed > 5f && Main.rand.NextBool(3));
            }

            int foamCount = (int)MathHelper.Clamp(3f + energy * 4f, 3f, 9f);
            for (int i = 0; i < foamCount; i++) {
                Vector2 vel = rebound.RotatedByRandom(1f) * Main.rand.NextFloat(0.7f, 3.2f) * energy
                    - Vector2.UnitY * Main.rand.NextFloat(0.2f, 1f);
                PRTLoader.NewParticle<PRT_OceanCurrentFoam>(pos + Main.rand.NextVector2Circular(8f, 7f)
                    , vel, AbyssFoam, Main.rand.NextFloat(0.05f, 0.11f))
                    ?.Configure(Main.rand.Next(26, 46), Main.rand.NextFloat(0.02f, 0.05f));
            }
        }
    }
}
