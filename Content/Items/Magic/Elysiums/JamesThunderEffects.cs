using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums
{
    /// <summary>
    /// 雅各布（James）门徒的雷霆审判效果
    /// 雷霆之子风格 - 狂暴闪电、连锁雷击、圣剑雷霆
    /// 与Jude的审判雷电区别：
    /// - Jude: 从天而降的神圣审判，单体高伤害，庄严肃穆
    /// - James: 横向连锁的狂暴雷霆，多目标连锁，狂野迅捷
    /// </summary>
    internal static class JamesThunderEffects
    {
        #region 颜色定义
        /// <summary>雷霆黄 - 主色调</summary>
        public static Color ThunderYellow => new Color(255, 255, 100);
        /// <summary>电弧白 - 核心</summary>
        public static Color ArcWhite => new Color(255, 255, 240);
        /// <summary>闪电蓝 - 边缘</summary>
        public static Color LightningBlue => new Color(180, 220, 255);
        /// <summary>雷暴紫 - 强化</summary>
        public static Color StormPurple => new Color(200, 150, 255);
        /// <summary>烈焰橙 - 灼烧</summary>
        public static Color BurnOrange => new Color(255, 180, 80);
        #endregion

        #region 主雷击特效
        /// <summary>
        /// 生成完整的雷霆审判特效
        /// </summary>
        public static void SpawnThunderJudgmentEffect(Vector2 source, NPC target, int chainCount) {
            if (VaultUtils.isServer) return;

            Vector2 targetCenter = target.Center;

            //播放雷霆音效
            PlayThunderSounds(targetCenter, chainCount);

            //阶段1：主雷击光束
            SpawnMainLightningBolt(source, targetCenter);

            //阶段2：目标位置爆发
            SpawnImpactExplosion(targetCenter, chainCount);

            //阶段3：电弧环绕
            SpawnElectricArcs(targetCenter);

            //阶段4：圣剑闪电印记
            SpawnSwordLightningMark(targetCenter);

            //阶段5：残留电流
            SpawnLingeringCurrents(targetCenter);
        }

        /// <summary>
        /// 生成连锁雷击特效
        /// </summary>
        public static void SpawnChainLightningEffect(Vector2 from, Vector2 to, int chainIndex) {
            if (VaultUtils.isServer) return;

            //连锁闪电颜色随深度变化
            Color chainColor = chainIndex switch {
                0 => ThunderYellow,
                1 => LightningBlue,
                2 => StormPurple,
                _ => Color.Lerp(StormPurple, LightningBlue, 0.5f)
            };

            //生成锯齿形闪电
            SpawnZigzagLightning(from, to, chainColor, 1f - chainIndex * 0.15f);

            //连接点爆发
            SpawnChainNodeBurst(to, chainColor);
        }
        #endregion

        #region 音效
        private static void PlayThunderSounds(Vector2 center, int intensity) {
            //主雷鸣
            SoundEngine.PlaySound(SoundID.Item122 with {
                Volume = 0.9f + intensity * 0.1f,
                Pitch = 0.2f,
                PitchVariance = 0.15f
            }, center);

            //电弧噼啪声
            SoundEngine.PlaySound(SoundID.Item93 with {
                Volume = 0.6f,
                Pitch = 0.5f
            }, center);
        }
        #endregion

        #region 主雷击
        /// <summary>
        /// 生成主雷击光束（锯齿形）
        /// </summary>
        private static void SpawnMainLightningBolt(Vector2 from, Vector2 to) {
            //生成多层闪电
            for (int layer = 0; layer < 3; layer++) {
                float offset = layer * 3f;
                float scale = 1f - layer * 0.25f;
                Color color = layer switch {
                    0 => ArcWhite,
                    1 => ThunderYellow,
                    _ => LightningBlue
                };

                SpawnZigzagLightning(from, to, color, scale, offset);
            }
        }

        /// <summary>
        /// 生成锯齿形闪电
        /// </summary>
        private static void SpawnZigzagLightning(Vector2 from, Vector2 to, Color color, float scale, float offsetSeed = 0f) {
            Vector2 direction = (to - from).SafeNormalize(Vector2.UnitX);
            float distance = Vector2.Distance(from, to);
            Vector2 perpendicular = direction.RotatedBy(MathHelper.PiOver2);

            //分段数量
            int segments = Math.Max(5, (int)(distance / 20f));
            List<Vector2> points = [from];

            //生成锯齿路径
            for (int i = 1; i < segments; i++) {
                float t = i / (float)segments;
                Vector2 basePos = Vector2.Lerp(from, to, t);

                //锯齿偏移（越靠近中间偏移越大）
                float zigzagIntensity = MathF.Sin(t * MathHelper.Pi) * 25f * scale;
                float zigzag = MathF.Sin((t * 8f + offsetSeed) * MathHelper.Pi) * zigzagIntensity;

                points.Add(basePos + perpendicular * zigzag);
            }
            points.Add(to);

            //沿路径生成粒子
            for (int i = 0; i < points.Count - 1; i++) {
                Vector2 segStart = points[i];
                Vector2 segEnd = points[i + 1];
                Vector2 segDir = (segEnd - segStart).SafeNormalize(Vector2.UnitX);
                float segLength = Vector2.Distance(segStart, segEnd);

                int particleCount = Math.Max(2, (int)(segLength / 8f));
                for (int j = 0; j <= particleCount; j++) {
                    float st = j / (float)particleCount;
                    Vector2 pos = Vector2.Lerp(segStart, segEnd, st);

                    BasePRT particle = new PRT_Light(
                        pos,
                        segDir * 2f + Main.rand.NextVector2Circular(1f, 1f),
                        0.15f * scale,
                        color,
                        Main.rand.Next(8, 15),
                        1.2f,
                        1.5f,
                        hueShift: 0f
                    );
                    PRTLoader.AddParticle(particle);
                }

                //闪电火花
                if (i % 2 == 0) {
                    Vector2 sparkPos = Vector2.Lerp(segStart, segEnd, 0.5f);
                    BasePRT spark = new PRT_Spark(
                        sparkPos,
                        perpendicular * Main.rand.NextFloat(-5f, 5f),
                        false,
                        Main.rand.Next(8, 15),
                        0.6f * scale,
                        color,
                        null
                    );
                    PRTLoader.AddParticle(spark);
                }
            }
        }
        #endregion

        #region 冲击爆发
        /// <summary>
        /// 生成冲击点爆发效果
        /// </summary>
        private static void SpawnImpactExplosion(Vector2 center, int intensity) {
            int particleCount = 20 + intensity * 5;

            //核心爆发
            for (int i = 0; i < particleCount; i++) {
                float angle = MathHelper.TwoPi * i / particleCount;
                float speed = Main.rand.NextFloat(6f, 14f);
                Vector2 velocity = angle.ToRotationVector2() * speed;

                Color color = Main.rand.Next(3) switch {
                    0 => ArcWhite,
                    1 => ThunderYellow,
                    _ => LightningBlue
                };

                BasePRT particle = new PRT_Light(
                    center + Main.rand.NextVector2Circular(10f, 10f),
                    velocity,
                    Main.rand.NextFloat(0.15f, 0.3f),
                    color,
                    Main.rand.Next(15, 25),
                    1f,
                    1.5f,
                    hueShift: Main.rand.NextFloat(-0.01f, 0.01f)
                );
                PRTLoader.AddParticle(particle);
            }

            //电击Dust
            for (int i = 0; i < 25; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(4f, 10f);
                Dust d = Dust.NewDustPerfect(center + Main.rand.NextVector2Circular(15f, 15f),
                    DustID.Electric, vel, 100, ThunderYellow, Main.rand.NextFloat(1.2f, 1.8f));
                d.noGravity = true;
            }

            //冲击波环
            for (int ring = 0; ring < 2; ring++) {
                float radius = 15f + ring * 20f;
                int count = 12 + ring * 6;

                for (int i = 0; i < count; i++) {
                    float angle = MathHelper.TwoPi * i / count + ring * 0.1f;
                    Vector2 pos = center + angle.ToRotationVector2() * radius;
                    Vector2 velocity = angle.ToRotationVector2() * (5f + ring * 3f);

                    BasePRT spark = new PRT_Spark(
                        pos,
                        velocity,
                        false,
                        Main.rand.Next(12, 20),
                        0.8f - ring * 0.2f,
                        Color.Lerp(ArcWhite, ThunderYellow, ring * 0.5f),
                        null
                    );
                    PRTLoader.AddParticle(spark);
                }
            }
        }
        #endregion

        #region 电弧环绕
        /// <summary>
        /// 生成电弧环绕效果
        /// </summary>
        private static void SpawnElectricArcs(Vector2 center) {
            //生成多条随机电弧
            int arcCount = Main.rand.Next(4, 7);

            for (int arc = 0; arc < arcCount; arc++) {
                float startAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                float arcLength = Main.rand.NextFloat(30f, 60f);
                float arcSpread = Main.rand.NextFloat(0.3f, 0.8f);

                Vector2 arcStart = center + startAngle.ToRotationVector2() * Main.rand.NextFloat(15f, 30f);
                Vector2 arcEnd = center + (startAngle + arcSpread).ToRotationVector2() * arcLength;

                //小型锯齿电弧
                SpawnMiniArc(arcStart, arcEnd, LightningBlue);
            }
        }

        /// <summary>
        /// 生成小型电弧
        /// </summary>
        private static void SpawnMiniArc(Vector2 from, Vector2 to, Color color) {
            Vector2 direction = (to - from).SafeNormalize(Vector2.UnitX);
            float distance = Vector2.Distance(from, to);
            Vector2 perpendicular = direction.RotatedBy(MathHelper.PiOver2);

            int points = Math.Max(3, (int)(distance / 10f));

            for (int i = 0; i <= points; i++) {
                float t = i / (float)points;
                Vector2 pos = Vector2.Lerp(from, to, t);

                //小幅锯齿
                float zigzag = MathF.Sin(t * MathHelper.Pi * 4f) * 8f * MathF.Sin(t * MathHelper.Pi);
                pos += perpendicular * zigzag;

                BasePRT particle = new PRT_Light(
                    pos,
                    direction * 1f,
                    0.1f,
                    color,
                    Main.rand.Next(8, 15),
                    0.8f,
                    1f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }
        }
        #endregion

        #region 圣剑闪电印记
        /// <summary>
        /// 生成圣剑形闪电印记（雅各布的象征物）
        /// </summary>
        private static void SpawnSwordLightningMark(Vector2 center) {
            //剑身（垂直）
            float bladeLength = 50f;
            float bladeWidth = 6f;

            //剑刃
            for (int i = 0; i < 15; i++) {
                float t = i / 14f;
                float y = -bladeLength * 0.7f + bladeLength * t;
                Vector2 pos = center + new Vector2(0, y);

                //剑身宽度变化（剑尖细）
                float width = t < 0.15f ? bladeWidth * (t / 0.15f) : bladeWidth;

                for (int side = -1; side <= 1; side += 2) {
                    Vector2 sidePos = pos + new Vector2(side * width * 0.4f, 0);

                    BasePRT particle = new PRT_Light(
                        sidePos,
                        new Vector2(0, -1.5f),
                        0.12f * (1f - t * 0.3f),
                        Color.Lerp(ArcWhite, ThunderYellow, t),
                        Main.rand.Next(15, 25),
                        1f,
                        1.2f,
                        hueShift: 0f
                    );
                    PRTLoader.AddParticle(particle);
                }
            }

            //剑柄（横向）
            float hiltLength = 25f;
            float hiltY = bladeLength * 0.2f;

            for (int i = 0; i < 8; i++) {
                float t = i / 7f;
                float x = -hiltLength + hiltLength * 2f * t;
                Vector2 pos = center + new Vector2(x, hiltY);

                BasePRT particle = new PRT_Light(
                    pos,
                    new Vector2(0, -1f),
                    0.1f,
                    ThunderYellow,
                    Main.rand.Next(12, 20),
                    0.9f,
                    1f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }

            //剑尖闪光
            Vector2 tipPos = center + new Vector2(0, -bladeLength * 0.7f);
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 6f);
                vel.Y -= 2f; //向上偏移

                BasePRT spark = new PRT_Spark(
                    tipPos,
                    vel,
                    false,
                    Main.rand.Next(10, 18),
                    0.7f,
                    ArcWhite,
                    null
                );
                PRTLoader.AddParticle(spark);
            }

            //剑身电流
            for (int i = 0; i < 5; i++) {
                float y = Main.rand.NextFloat(-bladeLength * 0.6f, bladeLength * 0.2f);
                Vector2 pos = center + new Vector2(Main.rand.NextFloat(-3f, 3f), y);

                Dust d = Dust.NewDustPerfect(pos, DustID.Electric,
                    new Vector2(Main.rand.NextFloat(-2f, 2f), -1f), 100, ThunderYellow, 0.8f);
                d.noGravity = true;
            }
        }
        #endregion

        #region 残留电流
        /// <summary>
        /// 生成残留电流效果
        /// </summary>
        private static void SpawnLingeringCurrents(Vector2 center) {
            //地面电流蔓延
            for (int i = 0; i < 15; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = Main.rand.NextFloat(20f, 50f);
                Vector2 pos = center + angle.ToRotationVector2() * dist;

                //电流粒子
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(1f, 3f);

                BasePRT particle = new PRT_Light(
                    pos,
                    vel,
                    Main.rand.NextFloat(0.08f, 0.12f),
                    Color.Lerp(ThunderYellow, LightningBlue, Main.rand.NextFloat()),
                    Main.rand.Next(20, 35),
                    0.7f,
                    0.9f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }

            //小型电火花
            for (int i = 0; i < 10; i++) {
                Vector2 pos = center + Main.rand.NextVector2Circular(40f, 40f);
                Dust d = Dust.NewDustPerfect(pos, DustID.Electric,
                    Main.rand.NextVector2Circular(2f, 2f), 100, LightningBlue, 0.6f);
                d.noGravity = true;
            }
        }
        #endregion

        #region 连锁节点
        /// <summary>
        /// 生成连锁节点爆发
        /// </summary>
        private static void SpawnChainNodeBurst(Vector2 center, Color color) {
            //小型爆发
            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(3f, 6f);

                BasePRT particle = new PRT_Light(
                    center,
                    vel,
                    0.12f,
                    color,
                    Main.rand.Next(10, 18),
                    0.9f,
                    1.2f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }

            //电击Dust
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f);
                Dust d = Dust.NewDustPerfect(center, DustID.Electric,
                    vel, 100, color, 0.8f);
                d.noGravity = true;
            }
        }
        #endregion

        #region 被动效果
        /// <summary>
        /// 被动雷霆光环
        /// </summary>
        public static void SpawnPassiveThunderAura(Vector2 discipleCenter, int timer) {
            if (VaultUtils.isServer) return;

            //每12帧生成一个小电弧
            if (timer % 12 == 0) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = Main.rand.NextFloat(15f, 30f);
                Vector2 pos = discipleCenter + angle.ToRotationVector2() * dist;

                //小型电弧粒子
                BasePRT particle = new PRT_Spark(
                    pos,
                    angle.ToRotationVector2() * Main.rand.NextFloat(1f, 3f),
                    false,
                    Main.rand.Next(8, 15),
                    0.5f,
                    ThunderYellow,
                    null
                );
                PRTLoader.AddParticle(particle);
            }

            //每30帧生成环绕电流
            if (timer % 30 == 0) {
                float angle = timer * 0.1f;
                Vector2 pos = discipleCenter + angle.ToRotationVector2() * 20f;

                Dust d = Dust.NewDustPerfect(pos, DustID.Electric,
                    angle.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 2f,
                    100, ThunderYellow, 0.6f);
                d.noGravity = true;
            }

            //每60帧生成小型闪电
            if (timer % 60 == 0) {
                float angle1 = Main.rand.NextFloat(MathHelper.TwoPi);
                float angle2 = angle1 + Main.rand.NextFloat(0.5f, 1.5f);
                Vector2 from = discipleCenter + angle1.ToRotationVector2() * 15f;
                Vector2 to = discipleCenter + angle2.ToRotationVector2() * 25f;

                SpawnMiniArc(from, to, LightningBlue);
            }
        }

        /// <summary>
        /// 雷霆蓄能效果
        /// </summary>
        public static void SpawnChargeEffect(Vector2 discipleCenter, float chargeRatio) {
            if (VaultUtils.isServer) return;
            if (chargeRatio < 0.3f) return;

            //蓄能光环
            float radius = 20f + chargeRatio * 15f;
            int segments = (int)(8 + chargeRatio * 8);

            for (int i = 0; i < segments; i++) {
                float angle = MathHelper.TwoPi * i / segments + Main.GameUpdateCount * 0.05f;
                Vector2 pos = discipleCenter + angle.ToRotationVector2() * radius;

                Color color = Color.Lerp(LightningBlue, ThunderYellow, chargeRatio);
                float scale = 0.08f + chargeRatio * 0.08f;

                BasePRT particle = new PRT_Light(
                    pos,
                    angle.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 1f,
                    scale,
                    color,
                    8,
                    0.8f,
                    1f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }

            //中心蓄能
            if (chargeRatio > 0.7f && Main.rand.NextBool(3)) {
                BasePRT core = new PRT_Light(
                    discipleCenter + Main.rand.NextVector2Circular(5f, 5f),
                    Vector2.Zero,
                    0.15f * chargeRatio,
                    ArcWhite,
                    10,
                    1f,
                    1.2f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(core);
            }
        }
        #endregion

        #region 门徒光芒
        /// <summary>
        /// 门徒释放技能时的光芒
        /// </summary>
        public static void SpawnDiscipleGlow(Vector2 center) {
            if (VaultUtils.isServer) return;

            //雷霆爆发
            for (int i = 0; i < 15; i++) {
                float angle = MathHelper.TwoPi * i / 15f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 8f);

                BasePRT particle = new PRT_Light(
                    center,
                    vel,
                    0.15f,
                    ThunderYellow,
                    Main.rand.Next(15, 25),
                    1f,
                    1.3f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }

            //电击效果
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 6f);
                Dust d = Dust.NewDustPerfect(center, DustID.Electric,
                    vel, 100, ThunderYellow, 1.2f);
                d.noGravity = true;
            }
        }
        #endregion
    }
}
