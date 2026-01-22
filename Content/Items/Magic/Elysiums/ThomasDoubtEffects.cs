using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums
{
    /// <summary>
    /// 多马（Thomas）门徒的怀疑之触效果
    /// 怀疑者风格 - 洞察之眼、验证光芒、真理揭示
    /// </summary>
    internal static class ThomasDoubtEffects
    {
        #region 颜色定义
        /// <summary>怀疑橙 - 主色调</summary>
        public static Color DoubtOrange => new Color(255, 165, 50);
        /// <summary>验证金 - 确认时</summary>
        public static Color VerifyGold => new Color(255, 215, 100);
        /// <summary>洞察白 - 高光</summary>
        public static Color InsightWhite => new Color(255, 250, 240);
        /// <summary>质疑红 - 怀疑时</summary>
        public static Color QuestionRed => new Color(255, 120, 80);
        /// <summary>真理蓝 - 揭示时</summary>
        public static Color TruthBlue => new Color(150, 200, 255);
        #endregion

        #region 验证激活特效
        /// <summary>
        /// 生成完整的怀疑验证特效
        /// </summary>
        public static void SpawnVerificationEffect(Player player, Vector2 discipleCenter) {
            if (VaultUtils.isServer) return;

            Vector2 playerCenter = player.Center;

            //播放验证音效
            PlayVerificationSounds(playerCenter);

            //阶段1：从门徒发射洞察光束
            SpawnInsightBeam(discipleCenter, playerCenter);

            //阶段2：洞察之眼
            SpawnEyeOfInsight(playerCenter);

            //阶段3：问号转变为感叹号
            SpawnQuestionToExclamation(playerCenter);

            //阶段4：验证光环
            SpawnVerificationHalo(playerCenter);

            //阶段5：真理符文环
            SpawnTruthRunes(playerCenter);

            //阶段6：暴击能量爆发
            SpawnCriticalEnergyBurst(playerCenter);

            //阶段7：长枪形光芒（多马的象征）
            SpawnSpearOfTruth(playerCenter);

            //在门徒位置生成光芒
            SpawnDiscipleGlow(discipleCenter);
        }
        #endregion

        #region 音效
        private static void PlayVerificationSounds(Vector2 center) {
            //洞察音效
            SoundEngine.PlaySound(SoundID.Item29 with {
                Volume = 0.9f,
                Pitch = 0.5f
            }, center);

            //验证确认音效
            SoundEngine.PlaySound(SoundID.Item4 with {
                Volume = 0.6f,
                Pitch = 0.7f
            }, center);
        }
        #endregion

        #region 洞察光束
        /// <summary>
        /// 从门徒到玩家的洞察光束
        /// </summary>
        private static void SpawnInsightBeam(Vector2 from, Vector2 to) {
            Vector2 direction = (to - from).SafeNormalize(Vector2.UnitX);
            float distance = Vector2.Distance(from, to);
            int particleCount = (int)(distance / 6f);

            for (int i = 0; i <= particleCount; i++) {
                float t = i / (float)particleCount;
                Vector2 pos = Vector2.Lerp(from, to, t);

                //锯齿形偏移（怀疑的不确定性）
                float zigzag = MathF.Sin(t * MathHelper.Pi * 6f) * 6f * (1f - t);
                Vector2 perpendicular = direction.RotatedBy(MathHelper.PiOver2);
                pos += perpendicular * zigzag;

                //颜色从橙色渐变到金色（怀疑到验证）
                Color color = Color.Lerp(QuestionRed, VerifyGold, t);
                float scale = 0.12f + MathF.Sin(t * MathHelper.Pi) * 0.12f;

                BasePRT particle = new PRT_Light(
                    pos,
                    direction * 1.5f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                    scale,
                    color,
                    Main.rand.Next(15, 25),
                    1f,
                    1.2f,
                    hueShift: t * 0.02f
                );
                PRTLoader.AddParticle(particle);
            }
        }
        #endregion

        #region 洞察之眼
        /// <summary>
        /// 生成洞察之眼效果
        /// </summary>
        private static void SpawnEyeOfInsight(Vector2 center) {
            Vector2 eyeCenter = center - new Vector2(0, 60f);
            float eyeWidth = 35f;
            float eyeHeight = 20f;

            //眼睛轮廓（椭圆形）
            int outlinePoints = 24;
            for (int i = 0; i < outlinePoints; i++) {
                float angle = MathHelper.TwoPi * i / outlinePoints;
                float x = MathF.Cos(angle) * eyeWidth;
                float y = MathF.Sin(angle) * eyeHeight;
                Vector2 pos = eyeCenter + new Vector2(x, y);

                //轮廓向外扩散
                Vector2 velocity = new Vector2(x, y).SafeNormalize(Vector2.Zero) * 2f;

                BasePRT particle = new PRT_Light(
                    pos,
                    velocity,
                    0.15f,
                    DoubtOrange,
                    Main.rand.Next(25, 40),
                    1f,
                    1.2f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }

            //眼球（中心）
            BasePRT pupil = new PRT_Light(
                eyeCenter,
                Vector2.Zero,
                0.4f,
                InsightWhite,
                35,
                1.2f,
                1f,
                hueShift: 0f
            );
            PRTLoader.AddParticle(pupil);

            //瞳孔
            BasePRT iris = new PRT_Light(
                eyeCenter,
                Vector2.Zero,
                0.25f,
                TruthBlue,
                30,
                1.1f,
                0.8f,
                hueShift: 0.01f
            );
            PRTLoader.AddParticle(iris);

            //眼睛光芒射线
            for (int ray = 0; ray < 8; ray++) {
                float rayAngle = MathHelper.TwoPi * ray / 8f;
                for (int i = 1; i <= 5; i++) {
                    float dist = eyeWidth + i * 8f;
                    Vector2 pos = eyeCenter + rayAngle.ToRotationVector2() * dist;

                    BasePRT spark = new PRT_Spark(
                        pos,
                        rayAngle.ToRotationVector2() * 3f,
                        false,
                        Main.rand.Next(15, 25),
                        0.6f - i * 0.08f,
                        Color.Lerp(DoubtOrange, InsightWhite, i / 5f),
                        null
                    );
                    PRTLoader.AddParticle(spark);
                }
            }
        }
        #endregion

        #region 问号转感叹号
        /// <summary>
        /// 生成问号转变为感叹号的效果
        /// </summary>
        private static void SpawnQuestionToExclamation(Vector2 center) {
            //问号形状的点（怀疑）- 先生成，会消散
            List<Vector2> questionPoints = [
                new(0, -40), new(8, -45), new(12, -50), new(10, -58),
                new(4, -62), new(-4, -62), new(-10, -58), new(-8, -50),
                new(0, -42), new(0, -35), new(0, -28),
                // 问号的点
                new(0, -18)
            ];

            foreach (Vector2 point in questionPoints) {
                Vector2 pos = center + point;
                Vector2 vel = new Vector2(Main.rand.NextFloat(-1f, 1f), -2f);

                Dust d = Dust.NewDustPerfect(pos, DustID.Torch,
                    vel, 100, QuestionRed, 0.9f);
                d.noGravity = true;
            }

            //感叹号形状（验证）- 后生成，更明亮
            List<Vector2> exclamationPoints = [];
            //感叹号主体
            for (float y = -65f; y <= -25f; y += 4f) {
                exclamationPoints.Add(new Vector2(0, y));
            }
            //感叹号的点
            exclamationPoints.Add(new Vector2(0, -15f));

            foreach (Vector2 point in exclamationPoints) {
                Vector2 pos = center + point;

                BasePRT particle = new PRT_Light(
                    pos,
                    new Vector2(0, -1f),
                    0.2f,
                    VerifyGold,
                    Main.rand.Next(30, 45),
                    1.1f,
                    1.3f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }
        }
        #endregion

        #region 验证光环
        /// <summary>
        /// 生成验证光环
        /// </summary>
        private static void SpawnVerificationHalo(Vector2 center) {
            float haloRadius = 50f;

            //双层旋转光环
            for (int layer = 0; layer < 2; layer++) {
                int segments = 16 + layer * 8;
                float layerRadius = haloRadius * (0.8f + layer * 0.3f);
                float rotationOffset = layer * MathHelper.Pi / 8f;

                for (int i = 0; i < segments; i++) {
                    float angle = MathHelper.TwoPi * i / segments + rotationOffset;
                    Vector2 pos = center + angle.ToRotationVector2() * layerRadius;

                    //旋转方向的速度
                    float rotDir = layer == 0 ? 1f : -1f;
                    Vector2 velocity = angle.ToRotationVector2().RotatedBy(MathHelper.PiOver2 * rotDir) * 3f;

                    Color color = layer == 0 ? DoubtOrange : VerifyGold;

                    BasePRT particle = new PRT_Light(
                        pos,
                        velocity,
                        0.15f - layer * 0.03f,
                        color,
                        Main.rand.Next(25, 40),
                        1f,
                        1.2f,
                        hueShift: 0f
                    );
                    PRTLoader.AddParticle(particle);
                }
            }

            //光环连接线
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8f;
                Vector2 inner = center + angle.ToRotationVector2() * (haloRadius * 0.5f);
                Vector2 outer = center + angle.ToRotationVector2() * (haloRadius * 1.1f);

                for (int j = 0; j < 4; j++) {
                    float t = j / 3f;
                    Vector2 pos = Vector2.Lerp(inner, outer, t);

                    Dust d = Dust.NewDustPerfect(pos, DustID.Torch,
                        angle.ToRotationVector2() * 1f, 100, VerifyGold, 0.7f);
                    d.noGravity = true;
                }
            }
        }
        #endregion

        #region 真理符文
        /// <summary>
        /// 生成真理符文环
        /// </summary>
        private static void SpawnTruthRunes(Vector2 center) {
            int runeCount = 6;
            float runeRadius = 70f;

            for (int i = 0; i < runeCount; i++) {
                float angle = MathHelper.TwoPi * i / runeCount;
                Vector2 runeCenter = center + angle.ToRotationVector2() * runeRadius;

                //每个符文是一个小型几何图案
                SpawnSingleRune(runeCenter, angle, i);
            }
        }

        /// <summary>
        /// 生成单个符文
        /// </summary>
        private static void SpawnSingleRune(Vector2 center, float rotation, int index) {
            //交替不同的符文形状
            switch (index % 3) {
                case 0: //三角形
                    SpawnTriangleRune(center, rotation);
                    break;
                case 1: //菱形
                    SpawnDiamondRune(center, rotation);
                    break;
                case 2: //十字
                    SpawnCrossRune(center, rotation);
                    break;
            }
        }

        private static void SpawnTriangleRune(Vector2 center, float rotation) {
            float size = 12f;
            for (int i = 0; i < 3; i++) {
                float angle = rotation + MathHelper.TwoPi * i / 3f - MathHelper.PiOver2;
                Vector2 vertex = center + angle.ToRotationVector2() * size;

                float nextAngle = rotation + MathHelper.TwoPi * ((i + 1) % 3) / 3f - MathHelper.PiOver2;
                Vector2 nextVertex = center + nextAngle.ToRotationVector2() * size;

                //边上的粒子
                for (int j = 0; j < 3; j++) {
                    float t = j / 2f;
                    Vector2 pos = Vector2.Lerp(vertex, nextVertex, t);

                    Dust d = Dust.NewDustPerfect(pos, DustID.Torch,
                        new Vector2(0, -1.5f), 100, TruthBlue, 0.6f);
                    d.noGravity = true;
                }
            }
        }

        private static void SpawnDiamondRune(Vector2 center, float rotation) {
            float size = 10f;
            Vector2[] vertices = [
                center + new Vector2(0, -size).RotatedBy(rotation),
                center + new Vector2(size, 0).RotatedBy(rotation),
                center + new Vector2(0, size).RotatedBy(rotation),
                center + new Vector2(-size, 0).RotatedBy(rotation)
            ];

            for (int i = 0; i < 4; i++) {
                Vector2 start = vertices[i];
                Vector2 end = vertices[(i + 1) % 4];

                for (int j = 0; j < 3; j++) {
                    float t = j / 2f;
                    Vector2 pos = Vector2.Lerp(start, end, t);

                    Dust d = Dust.NewDustPerfect(pos, DustID.Torch,
                        new Vector2(0, -1.5f), 100, DoubtOrange, 0.6f);
                    d.noGravity = true;
                }
            }
        }

        private static void SpawnCrossRune(Vector2 center, float rotation) {
            float size = 8f;
            //垂直
            for (int i = -2; i <= 2; i++) {
                Vector2 pos = center + new Vector2(0, i * size / 2f).RotatedBy(rotation);
                Dust d = Dust.NewDustPerfect(pos, DustID.Torch,
                    new Vector2(0, -1.5f), 100, VerifyGold, 0.7f);
                d.noGravity = true;
            }
            //水平
            for (int i = -1; i <= 1; i++) {
                if (i == 0) continue;
                Vector2 pos = center + new Vector2(i * size / 2f, 0).RotatedBy(rotation);
                Dust d = Dust.NewDustPerfect(pos, DustID.Torch,
                    new Vector2(0, -1.5f), 100, VerifyGold, 0.7f);
                d.noGravity = true;
            }
        }
        #endregion

        #region 暴击能量爆发
        /// <summary>
        /// 生成暴击能量爆发
        /// </summary>
        private static void SpawnCriticalEnergyBurst(Vector2 center) {
            //中心爆发
            for (int i = 0; i < 30; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float speed = Main.rand.NextFloat(5f, 12f);
                Vector2 velocity = angle.ToRotationVector2() * speed;

                Color color = Main.rand.Next(3) switch {
                    0 => DoubtOrange,
                    1 => VerifyGold,
                    _ => InsightWhite
                };

                BasePRT particle = new PRT_Light(
                    center + Main.rand.NextVector2Circular(10f, 10f),
                    velocity,
                    Main.rand.NextFloat(0.15f, 0.3f),
                    color,
                    Main.rand.Next(25, 40),
                    1f,
                    1.5f,
                    hueShift: Main.rand.NextFloat(-0.02f, 0.02f)
                );
                PRTLoader.AddParticle(particle);
            }

            //暴击文字效果的光芒
            for (int i = 0; i < 20; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 7f);
                Dust d = Dust.NewDustPerfect(center + Main.rand.NextVector2Circular(20f, 20f),
                    DustID.Torch, vel, 100, VerifyGold, Main.rand.NextFloat(1.2f, 1.8f));
                d.noGravity = true;
            }

            //环形冲击波
            for (int ring = 0; ring < 3; ring++) {
                float radius = 20f + ring * 25f;
                int count = 12 + ring * 4;

                for (int i = 0; i < count; i++) {
                    float angle = MathHelper.TwoPi * i / count + ring * 0.15f;
                    Vector2 pos = center + angle.ToRotationVector2() * radius;
                    Vector2 velocity = angle.ToRotationVector2() * (4f + ring * 2f);

                    BasePRT spark = new PRT_Spark(
                        pos,
                        velocity,
                        false,
                        Main.rand.Next(15, 25),
                        0.8f - ring * 0.15f,
                        Color.Lerp(DoubtOrange, VerifyGold, ring / 2f),
                        null
                    );
                    PRTLoader.AddParticle(spark);
                }
            }
        }
        #endregion

        #region 真理之枪
        /// <summary>
        /// 生成长枪形光芒（多马的象征物）
        /// </summary>
        private static void SpawnSpearOfTruth(Vector2 center) {
            //枪尖向上
            float spearLength = 80f;
            float spearWidth = 8f;
            Vector2 spearTip = center - new Vector2(0, 30f);

            //枪身
            for (int i = 0; i < 20; i++) {
                float t = i / 19f;
                float y = spearLength * t;
                Vector2 pos = spearTip + new Vector2(0, y);

                //枪身宽度变化（枪尖细，枪柄粗）
                float width = t < 0.2f ? spearWidth * (t / 0.2f) : spearWidth * (0.8f + t * 0.2f);

                //两侧的粒子
                for (int side = -1; side <= 1; side += 2) {
                    Vector2 sidePos = pos + new Vector2(side * width * 0.5f, 0);

                    float scale = 0.12f + (1f - t) * 0.1f;
                    Color color = Color.Lerp(InsightWhite, DoubtOrange, t);

                    BasePRT particle = new PRT_Light(
                        sidePos,
                        new Vector2(0, -2f),
                        scale,
                        color,
                        Main.rand.Next(20, 35),
                        1f,
                        1.1f,
                        hueShift: 0f
                    );
                    PRTLoader.AddParticle(particle);
                }
            }

            //枪尖闪光
            for (int i = 0; i < 10; i++) {
                Vector2 vel = new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-5f, -2f));
                BasePRT tip = new PRT_Light(
                    spearTip + Main.rand.NextVector2Circular(3f, 3f),
                    vel,
                    0.2f,
                    InsightWhite,
                    Main.rand.Next(15, 25),
                    1.2f,
                    1.3f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(tip);
            }

            //枪尖的穿刺光线
            for (int ray = 0; ray < 4; ray++) {
                float angle = MathHelper.ToRadians(-45 + ray * 30);
                for (int i = 1; i <= 4; i++) {
                    Vector2 pos = spearTip + angle.ToRotationVector2() * (i * 8f);

                    Dust d = Dust.NewDustPerfect(pos, DustID.Torch,
                        angle.ToRotationVector2() * 2f, 100, VerifyGold, 0.7f - i * 0.1f);
                    d.noGravity = true;
                }
            }
        }
        #endregion

        #region 门徒光芒
        private static void SpawnDiscipleGlow(Vector2 center) {
            //怀疑之光
            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 6f);

                BasePRT particle = new PRT_Light(
                    center + Main.rand.NextVector2Circular(10f, 10f),
                    vel,
                    Main.rand.NextFloat(0.12f, 0.2f),
                    DoubtOrange,
                    Main.rand.Next(20, 35),
                    1f,
                    1.2f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }

            //问号形状
            Dust q1 = Dust.NewDustPerfect(center + new Vector2(0, -15f), DustID.Torch,
                new Vector2(0, -1f), 100, QuestionRed, 1f);
            q1.noGravity = true;
            Dust q2 = Dust.NewDustPerfect(center + new Vector2(0, -5f), DustID.Torch,
                new Vector2(0, -1f), 100, QuestionRed, 0.6f);
            q2.noGravity = true;
        }
        #endregion

        #region 暴击命中特效
        /// <summary>
        /// 暴击命中时的特效
        /// </summary>
        public static void SpawnCriticalHitEffect(Vector2 hitPosition) {
            if (VaultUtils.isServer) return;

            //验证确认爆发
            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 8f);

                BasePRT particle = new PRT_Light(
                    hitPosition,
                    vel,
                    0.2f,
                    VerifyGold,
                    Main.rand.Next(15, 25),
                    1f,
                    1.3f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }

            //感叹号
            for (int i = 0; i < 5; i++) {
                Vector2 pos = hitPosition + new Vector2(0, -10f - i * 6f);
                Dust d = Dust.NewDustPerfect(pos, DustID.Torch,
                    new Vector2(0, -2f), 100, InsightWhite, 0.8f);
                d.noGravity = true;
            }
            //点
            Dust dot = Dust.NewDustPerfect(hitPosition + new Vector2(0, 5f), DustID.Torch,
                new Vector2(0, -1f), 100, InsightWhite, 1f);
            dot.noGravity = true;
        }
        #endregion

        #region 被动效果
        /// <summary>
        /// 被动怀疑光环
        /// </summary>
        public static void SpawnPassiveDoubtAura(Vector2 discipleCenter, int timer) {
            if (VaultUtils.isServer) return;

            //每20帧生成一个问号粒子
            if (timer % 20 == 0) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = discipleCenter + angle.ToRotationVector2() * Main.rand.NextFloat(15f, 30f);

                BasePRT particle = new PRT_Light(
                    pos,
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-1.5f, -0.5f)),
                    0.1f,
                    Color.Lerp(DoubtOrange, QuestionRed, Main.rand.NextFloat()),
                    Main.rand.Next(25, 40),
                    0.7f,
                    0.9f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }

            //每45帧生成一个小眼睛
            if (timer % 45 == 0) {
                Vector2 eyePos = discipleCenter + Main.rand.NextVector2Circular(20f, 20f);

                //眼睛轮廓
                for (int i = 0; i < 6; i++) {
                    float angle = MathHelper.TwoPi * i / 6f;
                    Vector2 pos = eyePos + angle.ToRotationVector2() * 5f;

                    Dust d = Dust.NewDustPerfect(pos, DustID.Torch,
                        new Vector2(0, -0.5f), 100, DoubtOrange, 0.4f);
                    d.noGravity = true;
                }

                //瞳孔
                Dust pupil = Dust.NewDustPerfect(eyePos, DustID.BlueTorch,
                    new Vector2(0, -0.5f), 100, TruthBlue, 0.5f);
                pupil.noGravity = true;
            }
        }

        /// <summary>
        /// 验证状态激活时的持续效果
        /// </summary>
        public static void SpawnActiveVerificationAura(Player player, int timer) {
            if (VaultUtils.isServer) return;

            //玩家周围的验证光环
            if (timer % 5 == 0) {
                float angle = timer * 0.2f;
                Vector2 pos = player.Center + angle.ToRotationVector2() * 40f;

                BasePRT particle = new PRT_Spark(
                    pos,
                    angle.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 2f,
                    false,
                    Main.rand.Next(10, 18),
                    0.6f,
                    VerifyGold,
                    player
                );
                PRTLoader.AddParticle(particle);
            }

            //眼睛跟随
            if (timer % 15 == 0) {
                Vector2 eyePos = player.Center - new Vector2(0, 50f) + Main.rand.NextVector2Circular(10f, 5f);

                BasePRT eye = new PRT_Light(
                    eyePos,
                    new Vector2(0, -1f),
                    0.15f,
                    InsightWhite,
                    20,
                    1f,
                    1f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(eye);
            }
        }
        #endregion
    }

    /// <summary>
    /// 多马的怀疑验证玩家效果
    /// </summary>
    internal class ThomasVerificationPlayer : ModPlayer
    {
        /// <summary>是否处于验证状态</summary>
        public bool IsVerified { get; set; } = false;

        /// <summary>验证状态持续时间</summary>
        public int VerificationTime { get; set; } = 0;

        /// <summary>验证状态计时器</summary>
        public int VerificationTimer { get; set; } = 0;

        /// <summary>累计暴击次数</summary>
        public int CriticalHitCount { get; set; } = 0;

        public override void ResetEffects() {
            if (VerificationTime > 0) {
                VerificationTime--;
                VerificationTimer++;

                if (VerificationTime <= 0) {
                    IsVerified = false;
                    VerificationTimer = 0;
                }
            }
        }

        public override void PostUpdate() {
            if (IsVerified && !VaultUtils.isServer) {
                ThomasDoubtEffects.SpawnActiveVerificationAura(Player, VerificationTimer);
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (IsVerified) {
                //验证状态下必定暴击
                modifiers.SetCrit();
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (hit.Crit && IsVerified) {
                CriticalHitCount++;

                //暴击时生成特效
                if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                    ThomasDoubtEffects.SpawnCriticalHitEffect(target.Center);
                }

                //每5次暴击延长验证时间
                if (CriticalHitCount % 5 == 0) {
                    VerificationTime = Math.Min(VerificationTime + 30, 300); //最多延长到5秒
                    CombatText.NewText(Player.Hitbox, ThomasDoubtEffects.VerifyGold, "验证延长!", true);
                }
            }
        }

        /// <summary>
        /// 激活验证状态
        /// </summary>
        public void ActivateVerification(int duration) {
            IsVerified = true;
            VerificationTime = duration;
            VerificationTimer = 0;
            CriticalHitCount = 0;
        }
    }
}
