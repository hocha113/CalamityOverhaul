using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.DomainFreeze
{
    /// <summary>冻结粒子生成</summary>
    internal static class CyberDomainFreezeParticles
    {
        /// <summary>每帧冻结 NPC 粒子</summary>
        public static void SpawnFreezeParticles(NPC npc, float progress, float seed) {
            Vector2 center = npc.Center;
            float halfW = npc.width * 0.5f;
            float halfH = npc.height * 0.5f;

            // 粒子密度：冻结期间稳定低密度，解冻前增多
            int count;
            if (progress < 0.85f) {
                // 持续冻结期间：稀疏六角碎片缓慢飘散
                if (Main.rand.NextBool(3)) return;
                count = Main.rand.Next(1, 3);
            }
            else {
                // 解冻阶段：密度急增
                count = Main.rand.Next(3, 7);
            }

            for (int i = 0; i < count; i++) {
                Vector2 spawnPos = center + new Vector2(
                    Main.rand.NextFloat(-halfW * 1.2f, halfW * 1.2f),
                    Main.rand.NextFloat(-halfH * 1.2f, halfH * 1.2f)
                );

                // 速度：缓慢向外飘散，解冻前加速
                float speed = MathHelper.Lerp(0.8f, 3.5f, progress * progress);
                Vector2 vel = (spawnPos - center).SafeNormalize(Vector2.UnitY)
                    * Main.rand.NextFloat(speed * 0.3f, speed);
                // 轻微上浮
                vel.Y -= Main.rand.NextFloat(0.2f, 0.8f);

                float scale = Main.rand.NextFloat(0.4f, 1.2f);
                int lifeTime = Main.rand.Next(25, 50);

                // 使用暗红晶系的赛博方块粒子
                Color core = new Color(0.85f, 0.06f, 0.2f);
                Color edge = new Color(1.0f, 0.35f, 0.4f);
                PRTLoader.NewParticle<PRT_CyberSquare>(spawnPos, vel, core, scale).Configure(edge, lifeTime);
            }
        }

        /// <summary>解冻爆发粒子</summary>
        public static void SpawnThawBurst(Vector2 center) {
            int count = 20;
            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi * i / count + Main.rand.NextFloat(-0.15f, 0.15f);
                float speed = Main.rand.NextFloat(3f, 8f);
                Vector2 vel = angle.ToRotationVector2() * speed;

                Color core = new Color(0.9f, 0.08f, 0.2f);
                Color edge = new Color(1.0f, 0.4f, 0.45f);

                PRTLoader.NewParticle<PRT_CyberSquare>(center + vel * 1.5f, vel, core, Main.rand.NextFloat(0.6f, 1.8f)).Configure(edge, Main.rand.Next(20, 40));
            }

            // 中心小碎片
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(2f, 2f);
                Color core = new Color(1.0f, 0.25f, 0.35f);
                Color edge = new Color(1.0f, 0.55f, 0.55f);

                PRTLoader.NewParticle<PRT_CyberSquare>(center, vel, core, Main.rand.NextFloat(0.2f, 0.5f)).Configure(edge, Main.rand.Next(15, 25));
            }
        }
    }
}
