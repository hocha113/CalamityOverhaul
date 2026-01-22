using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums
{
    /// <summary>
    /// 达泰门徒的奇迹效果辅助类
    /// 提供各种奇迹的专属视觉特效
    /// </summary>
    internal static class JudeMiracleEffects
    {
        #region 颜色定义
        /// <summary>治愈奇迹的颜色 - 翠绿色</summary>
        public static Color HealingColor => new Color(100, 255, 150);
        /// <summary>守护奇迹的颜色 - 金色</summary>
        public static Color GuardianColor => new Color(255, 215, 100);
        /// <summary>审判奇迹的颜色 - 白金色</summary>
        public static Color JudgmentColor => new Color(255, 240, 180);
        /// <summary>迅捷奇迹的颜色 - 天蓝色</summary>
        public static Color SwiftColor => new Color(100, 200, 255);
        /// <summary>魔力奇迹的颜色 - 靛蓝色</summary>
        public static Color ManaColor => new Color(120, 100, 255);
        #endregion

        #region 治愈奇迹特效
        /// <summary>
        /// 生成治愈奇迹的完整特效
        /// </summary>
        public static void SpawnHealingMiracleEffect(Player player) {
            if (VaultUtils.isServer) return;

            Vector2 center = player.Center;

            //播放治愈音效
            SoundEngine.PlaySound(SoundID.Item4 with {
                Volume = 0.8f,
                Pitch = 0.3f
            }, center);

            //生成上升的治愈光点
            for (int i = 0; i < 25; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = Main.rand.NextFloat(20f, 60f);
                Vector2 spawnPos = center + angle.ToRotationVector2() * dist;

                //向上飘动的光点
                Vector2 velocity = new Vector2(
                    Main.rand.NextFloat(-1f, 1f),
                    Main.rand.NextFloat(-4f, -2f)
                );

                BasePRT particle = new PRT_Light(
                    spawnPos,
                    velocity,
                    Main.rand.NextFloat(0.15f, 0.3f),
                    HealingColor,
                    Main.rand.Next(30, 50),
                    1f,
                    1.2f,
                    hueShift: Main.rand.NextFloat(-0.02f, 0.02f)
                );
                PRTLoader.AddParticle(particle);
            }

            //生成十字架形状的治愈光芒
            SpawnCrossPattern(center, HealingColor, 40f, 8);

            //生成环形扩散
            SpawnExpandingRing(center, HealingColor, 12, 8f);

            //生成心形Dust（治愈象征）
            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f);
                vel.Y -= 2f; //向上偏移
                Dust d = Dust.NewDustPerfect(center + Main.rand.NextVector2Circular(30, 30),
                    DustID.GreenFairy, vel, 100, HealingColor, 1.2f);
                d.noGravity = true;
            }
        }
        #endregion

        #region 守护奇迹特效
        /// <summary>
        /// 生成守护奇迹的完整特效
        /// </summary>
        public static void SpawnGuardianMiracleEffect(Player player) {
            if (VaultUtils.isServer) return;

            Vector2 center = player.Center;

            //播放护盾音效
            SoundEngine.PlaySound(SoundID.Item29 with {
                Volume = 0.9f,
                Pitch = 0.1f
            }, center);

            //生成金色护盾环
            int ringSegments = 24;
            for (int i = 0; i < ringSegments; i++) {
                float angle = MathHelper.TwoPi * i / ringSegments;
                float radius = 50f;
                Vector2 pos = center + angle.ToRotationVector2() * radius;

                //向外扩散的粒子
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(2f, 4f);

                BasePRT particle = new PRT_Light(
                    pos,
                    velocity,
                    0.25f,
                    GuardianColor,
                    Main.rand.Next(25, 40),
                    1f,
                    1.5f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }

            //生成旋转的十字架守护符文
            for (int cross = 0; cross < 4; cross++) {
                float baseAngle = MathHelper.PiOver2 * cross;
                Vector2 crossCenter = center + baseAngle.ToRotationVector2() * 45f;

                //每个十字架位置生成小十字
                SpawnCrossPattern(crossCenter, GuardianColor * 0.8f, 15f, 4);
            }

            //生成闪烁的保护Dust
            for (int i = 0; i < 30; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = Main.rand.NextFloat(30f, 60f);
                Vector2 pos = center + angle.ToRotationVector2() * dist;

                Dust d = Dust.NewDustPerfect(pos, DustID.GoldFlame,
                    angle.ToRotationVector2() * Main.rand.NextFloat(1f, 3f),
                    100, GuardianColor, Main.rand.NextFloat(1f, 1.5f));
                d.noGravity = true;
            }

            //生成护盾球体效果
            SpawnShieldSphere(center, GuardianColor, 50f);
        }

        /// <summary>
        /// 生成护盾球体效果
        /// </summary>
        private static void SpawnShieldSphere(Vector2 center, Color color, float radius) {
            //生成多层环形粒子模拟球体
            for (int layer = 0; layer < 3; layer++) {
                float layerRadius = radius * (0.6f + layer * 0.2f);
                int particleCount = 8 + layer * 4;

                for (int i = 0; i < particleCount; i++) {
                    float angle = MathHelper.TwoPi * i / particleCount + layer * 0.3f;
                    Vector2 pos = center + angle.ToRotationVector2() * layerRadius;

                    BasePRT particle = new PRT_Spark(
                        pos,
                        angle.ToRotationVector2() * 1f,
                        false,
                        Main.rand.Next(20, 35),
                        0.8f,
                        color * (0.6f + layer * 0.15f),
                        null
                    );
                    PRTLoader.AddParticle(particle);
                }
            }
        }
        #endregion

        #region 迅捷奇迹特效
        /// <summary>
        /// 生成迅捷奇迹的完整特效
        /// </summary>
        public static void SpawnSwiftMiracleEffect(Player player) {
            if (VaultUtils.isServer) return;

            Vector2 center = player.Center;
            Vector2 velocity = player.velocity;

            //播放疾风音效
            SoundEngine.PlaySound(SoundID.Item66 with {
                Volume = 0.7f,
                Pitch = 0.5f
            }, center);

            //生成速度线（沿移动反方向）
            Vector2 backDir = (-velocity).SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 20; i++) {
                float offset = Main.rand.NextFloat(-30f, 30f);
                Vector2 perpendicular = backDir.RotatedBy(MathHelper.PiOver2);
                Vector2 spawnPos = center + backDir * Main.rand.NextFloat(20f, 60f) + perpendicular * offset;

                //高速向后的粒子
                Vector2 particleVel = backDir * Main.rand.NextFloat(8f, 15f);

                BasePRT particle = new PRT_Spark(
                    spawnPos,
                    particleVel,
                    false,
                    Main.rand.Next(10, 20),
                    Main.rand.NextFloat(0.8f, 1.2f),
                    SwiftColor,
                    player
                );
                PRTLoader.AddParticle(particle);
            }

            //生成环绕的风元素
            for (int i = 0; i < 15; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = center + angle.ToRotationVector2() * Main.rand.NextFloat(25f, 50f);

                //螺旋状的风粒子
                Vector2 spiralVel = angle.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(3f, 6f);

                BasePRT particle = new PRT_Light(
                    pos,
                    spiralVel,
                    0.18f,
                    SwiftColor,
                    Main.rand.Next(15, 25),
                    0.8f,
                    1f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }

            //生成翅膀形状的Dust
            SpawnWingPattern(center, SwiftColor);
        }

        /// <summary>
        /// 生成翅膀形状的粒子
        /// </summary>
        private static void SpawnWingPattern(Vector2 center, Color color) {
            //左翼
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.ToRadians(-45 - i * 10);
                float dist = 20f + i * 5f;
                Vector2 pos = center + new Vector2(-20, 0) + angle.ToRotationVector2() * dist;

                Dust d = Dust.NewDustPerfect(pos, DustID.BlueFairy,
                    angle.ToRotationVector2() * 2f, 100, color, 1f);
                d.noGravity = true;
            }

            //右翼
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.ToRadians(-135 + i * 10);
                float dist = 20f + i * 5f;
                Vector2 pos = center + new Vector2(20, 0) + angle.ToRotationVector2() * dist;

                Dust d = Dust.NewDustPerfect(pos, DustID.BlueFairy,
                    angle.ToRotationVector2() * 2f, 100, color, 1f);
                d.noGravity = true;
            }
        }
        #endregion

        #region 魔力奇迹特效
        /// <summary>
        /// 生成魔力奇迹的完整特效
        /// </summary>
        public static void SpawnManaMiracleEffect(Player player) {
            if (VaultUtils.isServer) return;

            Vector2 center = player.Center;

            //播放魔力恢复音效
            SoundEngine.PlaySound(SoundID.Item35 with {
                Volume = 0.8f,
                Pitch = 0.2f
            }, center);

            //生成向内汇聚的魔力光点
            for (int i = 0; i < 30; i++) {
                float angle = MathHelper.TwoPi * i / 30f;
                float dist = Main.rand.NextFloat(80f, 120f);
                Vector2 spawnPos = center + angle.ToRotationVector2() * dist;

                //向玩家中心汇聚
                Vector2 velocity = (center - spawnPos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(4f, 8f);

                BasePRT particle = new PRT_Light(
                    spawnPos,
                    velocity,
                    Main.rand.NextFloat(0.2f, 0.35f),
                    ManaColor,
                    Main.rand.Next(20, 35),
                    1f,
                    1.3f,
                    hueShift: Main.rand.NextFloat(-0.03f, 0.03f),
                    _entity: player,
                    _followingRateRatio: 0.5f
                );
                PRTLoader.AddParticle(particle);
            }

            //生成魔法符文环
            SpawnRuneRing(center, ManaColor, 60f, 6);

            //生成螺旋上升的魔力
            for (int i = 0; i < 20; i++) {
                float spiralAngle = MathHelper.TwoPi * i / 20f;
                float height = i * 3f;
                float radius = 30f - i * 1f;

                Vector2 pos = center + spiralAngle.ToRotationVector2() * radius - new Vector2(0, height);
                Vector2 vel = spiralAngle.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 2f + new Vector2(0, -1f);

                Dust d = Dust.NewDustPerfect(pos, DustID.BlueTorch, vel, 100, ManaColor, 1.2f);
                d.noGravity = true;
            }

            //生成中心爆发
            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 7f);
                BasePRT particle = new PRT_Light(
                    center,
                    vel,
                    0.2f,
                    Color.Lerp(ManaColor, Color.White, 0.3f),
                    Main.rand.Next(15, 25),
                    0.9f,
                    1.2f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }
        }

        /// <summary>
        /// 生成魔法符文环
        /// </summary>
        private static void SpawnRuneRing(Vector2 center, Color color, float radius, int runeCount) {
            for (int i = 0; i < runeCount; i++) {
                float angle = MathHelper.TwoPi * i / runeCount;
                Vector2 runePos = center + angle.ToRotationVector2() * radius;

                //每个符文位置生成一个小星形
                for (int j = 0; j < 5; j++) {
                    float starAngle = MathHelper.TwoPi * j / 5f + angle;
                    Vector2 starPoint = runePos + starAngle.ToRotationVector2() * 8f;

                    Dust d = Dust.NewDustPerfect(starPoint, DustID.PurpleTorch,
                        (starPoint - runePos).SafeNormalize(Vector2.Zero) * 1f,
                        100, color, 0.8f);
                    d.noGravity = true;
                }

                //符文中心
                BasePRT particle = new PRT_Light(
                    runePos,
                    Vector2.Zero,
                    0.3f,
                    color,
                    25,
                    1f,
                    1f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }
        }
        #endregion

        #region 通用效果
        /// <summary>
        /// 生成十字架形状的粒子
        /// </summary>
        public static void SpawnCrossPattern(Vector2 center, Color color, float length, int particlesPerArm) {
            //四个方向（上下左右）
            for (int arm = 0; arm < 4; arm++) {
                float baseAngle = MathHelper.PiOver2 * arm;
                for (int i = 1; i <= particlesPerArm; i++) {
                    float dist = length * i / particlesPerArm;
                    Vector2 pos = center + baseAngle.ToRotationVector2() * dist;

                    float scale = 1f - (float)i / particlesPerArm * 0.5f;

                    Dust d = Dust.NewDustPerfect(pos, DustID.GoldFlame,
                        baseAngle.ToRotationVector2() * 1f, 100, color, scale);
                    d.noGravity = true;
                }
            }
        }

        /// <summary>
        /// 生成扩散环
        /// </summary>
        public static void SpawnExpandingRing(Vector2 center, Color color, int particleCount, float speed) {
            for (int i = 0; i < particleCount; i++) {
                float angle = MathHelper.TwoPi * i / particleCount;
                Vector2 velocity = angle.ToRotationVector2() * speed;

                BasePRT particle = new PRT_Spark(
                    center,
                    velocity,
                    false,
                    Main.rand.Next(15, 25),
                    1f,
                    color,
                    null
                );
                PRTLoader.AddParticle(particle);
            }
        }

        /// <summary>
        /// 生成奇迹光环（通用）
        /// </summary>
        public static void SpawnMiracleAura(Vector2 center, Color color, string miracleName) {
            if (VaultUtils.isServer) return;

            //显示奇迹名称
            CombatText.NewText(
                new Microsoft.Xna.Framework.Rectangle((int)center.X - 50, (int)center.Y - 30, 100, 30),
                color,
                miracleName,
                true
            );

            //生成光环
            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12f;
                Vector2 pos = center + angle.ToRotationVector2() * 40f;

                BasePRT particle = new PRT_Light(
                    pos,
                    angle.ToRotationVector2() * 2f,
                    0.2f,
                    color,
                    20,
                    0.8f,
                    1f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }
        }
        #endregion
    }
}
