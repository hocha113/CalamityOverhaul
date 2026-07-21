using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Banish
{
    /// <summary>放逐故障方块粒子</summary>
    internal static class CyberBanishParticles
    {
        /// <summary>放逐中每帧粒子</summary>
        public static void SpawnBanishParticles(NPC npc, float progress, float seed) {
            Vector2 center = npc.Center;
            float halfW = npc.width * 0.5f;
            float halfH = npc.height * 0.5f;

            int count;
            if (progress < 0.5f) {
                //阶段一稀疏
                count = Main.rand.Next(1, 3);
            }
            else if (progress < 0.85f) {
                //阶段二加密
                count = Main.rand.Next(3, 7);
            }
            else {
                //阶段三爆发
                count = Main.rand.Next(5, 10);
            }

            for (int i = 0; i < count; i++) {
                Vector2 spawnPos = center + new Vector2(
                    Main.rand.NextFloat(-halfW, halfW),
                    Main.rand.NextFloat(-halfH, halfH)
                );

                //外扩，后期加速
                float speed = MathHelper.Lerp(1.5f, 6f, progress * progress);
                Vector2 vel = (spawnPos - center).SafeNormalize(Vector2.UnitX)
                    * Main.rand.NextFloat(speed * 0.5f, speed);

                //轻微上浮
                vel.Y -= Main.rand.NextFloat(0.3f, 1.2f);

                float scale = Main.rand.NextFloat(0.6f, 1.8f) * MathHelper.Lerp(1.2f, 0.5f, progress);
                int lifeTime = Main.rand.Next(20, 45);

                PRTLoader.NewParticle<PRT_BanishGlitch>(spawnPos, vel, Color.White, scale).Configure(lifeTime);
            }
        }

        /// <summary>抹除瞬间爆发</summary>
        public static void SpawnFinalBurst(Vector2 center, float npcScale) {
            int count = (int)(24 * npcScale);
            count = Math.Clamp(count, 16, 60);

            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi * i / count + Main.rand.NextFloat(-0.2f, 0.2f);
                float speed = Main.rand.NextFloat(4f, 12f);
                Vector2 vel = angle.ToRotationVector2() * speed;

                PRTLoader.NewParticle<PRT_BanishGlitch>(center + vel * 2f, vel, Color.White, Main.rand.NextFloat(1f, 2.5f)).Configure(Main.rand.Next(25, 55));
            }

            //中心小碎片
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(3f, 3f);
                PRTLoader.NewParticle<PRT_BanishGlitch>(center, vel, Color.White, Main.rand.NextFloat(0.3f, 0.7f)).Configure(Main.rand.Next(15, 30));
            }
        }
    }
}
