using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums
{
    /// <summary>
    /// 小雅各（Lesser）门徒的神圣治愈效果
    /// 奉献者风格 - 金色十字架、圣光环、天使羽翼
    /// </summary>
    internal static class LesserHealingEffects
    {
        #region 颜色定义
        /// <summary>神圣金色 - 主色调</summary>
        public static Color HolyGold => new Color(255, 215, 100);
        /// <summary>治愈绿色 - 辅助色</summary>
        public static Color HealingGreen => new Color(150, 255, 150);
        /// <summary>纯净白色 - 高光</summary>
        public static Color PureWhite => new Color(255, 255, 240);
        /// <summary>柔和黄色 - 光晕</summary>
        public static Color SoftYellow => new Color(255, 240, 180);
        #endregion

        /// <summary>
        /// 生成完整的神圣治愈效果
        /// </summary>
        /// <param name="player">目标玩家</param>
        /// <param name="healAmount">治愈量（用于调整特效强度）</param>
        /// <param name="discipleCenter">门徒位置</param>
        public static void SpawnFullHealingEffect(Player player, int healAmount, Vector2 discipleCenter) {
            if (VaultUtils.isServer) return;

            Vector2 playerCenter = player.Center;

            //播放神圣治愈音效序列
            PlayHealingSounds(playerCenter);

            //阶段1：从门徒位置发射神圣光束到玩家
            SpawnHolyBeam(discipleCenter, playerCenter);

            //阶段2：在玩家身上生成大型金色十字架
            SpawnGoldenCross(playerCenter, healAmount);

            //阶段3：生成环绕的圣光环
            SpawnHolyHalo(playerCenter);

            //阶段4：生成上升的治愈符文
            SpawnAscendingRunes(playerCenter);

            //阶段5：生成天使羽翼效果
            SpawnAngelWings(playerCenter);

            //阶段6：生成向外扩散的治愈波纹
            SpawnHealingRipples(playerCenter);

            //阶段7：生成漂浮的治愈光点
            SpawnFloatingLights(playerCenter, healAmount);

            //在门徒位置也生成光芒
            SpawnDiscipleGlow(discipleCenter);
        }

        #region 音效
        /// <summary>
        /// 播放治愈音效序列
        /// </summary>
        private static void PlayHealingSounds(Vector2 center) {
            //主治愈音效 - 柔和的铃声
            SoundEngine.PlaySound(SoundID.Item4 with {
                Volume = 0.9f,
                Pitch = 0.4f,
                PitchVariance = 0.1f
            }, center);

            //辅助音效 - 神圣回响
            SoundEngine.PlaySound(SoundID.Item29 with {
                Volume = 0.5f,
                Pitch = 0.6f
            }, center);
        }
        #endregion

        #region 神圣光束
        /// <summary>
        /// 从门徒到玩家的神圣光束
        /// </summary>
        private static void SpawnHolyBeam(Vector2 from, Vector2 to) {
            Vector2 direction = (to - from).SafeNormalize(Vector2.UnitX);
            float distance = Vector2.Distance(from, to);
            int particleCount = (int)(distance / 8f);

            for (int i = 0; i <= particleCount; i++) {
                float t = i / (float)particleCount;
                Vector2 pos = Vector2.Lerp(from, to, t);

                //添加波浪形偏移
                float wave = MathF.Sin(t * MathHelper.Pi * 3f) * 8f;
                Vector2 perpendicular = direction.RotatedBy(MathHelper.PiOver2);
                pos += perpendicular * wave;

                //沿路径的光点
                float scale = 0.15f + MathF.Sin(t * MathHelper.Pi) * 0.15f;
                Color color = Color.Lerp(HolyGold, PureWhite, t * 0.5f);

                BasePRT particle = new PRT_Light(
                    pos,
                    direction * 2f + Main.rand.NextVector2Circular(1f, 1f),
                    scale,
                    color,
                    Main.rand.Next(15, 25),
                    1f,
                    1.2f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);

                //每隔几个点生成火花
                if (i % 3 == 0) {
                    BasePRT spark = new PRT_Spark(
                        pos,
                        perpendicular * Main.rand.NextFloat(-3f, 3f),
                        false,
                        Main.rand.Next(10, 18),
                        0.6f,
                        HolyGold,
                        null
                    );
                    PRTLoader.AddParticle(spark);
                }
            }
        }
        #endregion

        #region 金色十字架
        /// <summary>
        /// 生成大型金色十字架效果
        /// </summary>
        private static void SpawnGoldenCross(Vector2 center, int healAmount) {
            //十字架大小根据治愈量调整
            float crossScale = 1f + Math.Min(healAmount / 100f, 0.5f);
            float verticalLength = 80f * crossScale;
            float horizontalLength = 50f * crossScale;
            float horizontalOffset = -15f; //横臂位置偏上

            //垂直臂（从上到下）
            SpawnCrossArm(center, Vector2.UnitY * -1, verticalLength * 0.4f, true); //上
            SpawnCrossArm(center, Vector2.UnitY, verticalLength * 0.6f, true);      //下

            //水平臂（从中心偏上位置）
            Vector2 armCenter = center + new Vector2(0, horizontalOffset);
            SpawnCrossArm(armCenter, Vector2.UnitX * -1, horizontalLength, false); //左
            SpawnCrossArm(armCenter, Vector2.UnitX, horizontalLength, false);      //右

            //十字架中心的强光
            SpawnCrossCenter(center + new Vector2(0, horizontalOffset));

            //十字架轮廓光芒
            SpawnCrossOutline(center, verticalLength, horizontalLength, horizontalOffset);
        }

        /// <summary>
        /// 生成十字架的一个臂
        /// </summary>
        private static void SpawnCrossArm(Vector2 start, Vector2 direction, float length, bool isVertical) {
            int particleCount = (int)(length / 5f);
            float thickness = isVertical ? 12f : 10f;

            for (int i = 0; i <= particleCount; i++) {
                float t = i / (float)particleCount;
                float dist = length * t;
                Vector2 pos = start + direction * dist;

                //中心线粒子
                float scale = 0.25f * (1f - t * 0.3f);
                Color color = Color.Lerp(PureWhite, HolyGold, t * 0.7f);

                BasePRT particle = new PRT_Light(
                    pos,
                    direction * 1f,
                    scale,
                    color,
                    Main.rand.Next(25, 40),
                    1.1f,
                    1.3f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);

                //两侧的辉光粒子
                Vector2 perpendicular = direction.RotatedBy(MathHelper.PiOver2);
                for (int side = -1; side <= 1; side += 2) {
                    float sideOffset = Main.rand.NextFloat(2f, thickness * 0.5f);
                    Vector2 sidePos = pos + perpendicular * side * sideOffset;

                    Dust d = Dust.NewDustPerfect(sidePos, DustID.GoldFlame,
                        direction * 0.5f + perpendicular * side * 0.3f,
                        100, HolyGold, Main.rand.NextFloat(0.8f, 1.2f));
                    d.noGravity = true;
                }
            }
        }

        /// <summary>
        /// 生成十字架中心的强光
        /// </summary>
        private static void SpawnCrossCenter(Vector2 center) {
            //中心爆发
            for (int i = 0; i < 20; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 6f);

                BasePRT particle = new PRT_Light(
                    center + Main.rand.NextVector2Circular(5f, 5f),
                    vel,
                    Main.rand.NextFloat(0.2f, 0.4f),
                    PureWhite,
                    Main.rand.Next(20, 35),
                    1.2f,
                    1.5f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }

            //四方向光芒射线
            for (int dir = 0; dir < 4; dir++) {
                float angle = MathHelper.PiOver2 * dir;
                for (int i = 0; i < 8; i++) {
                    float dist = 5f + i * 4f;
                    Vector2 pos = center + angle.ToRotationVector2() * dist;

                    BasePRT spark = new PRT_Spark(
                        pos,
                        angle.ToRotationVector2() * (3f + i * 0.5f),
                        false,
                        Main.rand.Next(12, 20),
                        0.8f - i * 0.08f,
                        Color.Lerp(PureWhite, HolyGold, i / 8f),
                        null
                    );
                    PRTLoader.AddParticle(spark);
                }
            }
        }

        /// <summary>
        /// 生成十字架轮廓的光芒
        /// </summary>
        private static void SpawnCrossOutline(Vector2 center, float vLength, float hLength, float hOffset) {
            //生成闪烁的轮廓点
            List<Vector2> outlinePoints = [];

            //垂直线轮廓
            for (float y = -vLength * 0.4f; y <= vLength * 0.6f; y += 8f) {
                outlinePoints.Add(center + new Vector2(-6f, y));
                outlinePoints.Add(center + new Vector2(6f, y));
            }

            //水平线轮廓
            Vector2 hCenter = center + new Vector2(0, hOffset);
            for (float x = -hLength; x <= hLength; x += 8f) {
                outlinePoints.Add(hCenter + new Vector2(x, -5f));
                outlinePoints.Add(hCenter + new Vector2(x, 5f));
            }

            foreach (Vector2 point in outlinePoints) {
                if (Main.rand.NextBool(3)) {
                    Dust d = Dust.NewDustPerfect(point, DustID.GoldFlame,
                        Main.rand.NextVector2Circular(1f, 1f),
                        100, SoftYellow, 0.7f);
                    d.noGravity = true;
                }
            }
        }
        #endregion

        #region 圣光环
        /// <summary>
        /// 生成环绕的圣光环（头顶光环效果）
        /// </summary>
        private static void SpawnHolyHalo(Vector2 center) {
            Vector2 haloCenter = center - new Vector2(0, 50f); //头顶位置
            float haloRadius = 25f;

            //主光环
            int segments = 24;
            for (int i = 0; i < segments; i++) {
                float angle = MathHelper.TwoPi * i / segments;
                Vector2 pos = haloCenter + angle.ToRotationVector2() * haloRadius;

                //光环粒子
                BasePRT particle = new PRT_Light(
                    pos,
                    angle.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 2f,
                    0.18f,
                    HolyGold,
                    Main.rand.Next(30, 45),
                    1f,
                    1.2f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }

            //内层光环
            for (int i = 0; i < segments / 2; i++) {
                float angle = MathHelper.TwoPi * i / (segments / 2);
                Vector2 pos = haloCenter + angle.ToRotationVector2() * (haloRadius * 0.6f);

                Dust d = Dust.NewDustPerfect(pos, DustID.GoldFlame,
                    angle.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 1.5f,
                    100, PureWhite, 0.9f);
                d.noGravity = true;
            }

            //光环中心光芒
            BasePRT centerGlow = new PRT_Light(
                haloCenter,
                Vector2.Zero,
                0.4f,
                PureWhite,
                40,
                1.2f,
                1f,
                hueShift: 0f
            );
            PRTLoader.AddParticle(centerGlow);
        }
        #endregion

        #region 上升符文
        /// <summary>
        /// 生成上升的治愈符文
        /// </summary>
        private static void SpawnAscendingRunes(Vector2 center) {
            //生成多个上升的符文圈
            for (int ring = 0; ring < 3; ring++) {
                float ringDelay = ring * 0.3f;
                float ringRadius = 30f + ring * 15f;
                int runeCount = 4 + ring * 2;

                for (int i = 0; i < runeCount; i++) {
                    float angle = MathHelper.TwoPi * i / runeCount + ring * 0.2f;
                    Vector2 runePos = center + angle.ToRotationVector2() * ringRadius;

                    //符文核心
                    Vector2 velocity = new Vector2(0, -2f - ring * 0.5f) + angle.ToRotationVector2() * 0.5f;

                    BasePRT rune = new PRT_Light(
                        runePos,
                        velocity,
                        0.22f - ring * 0.03f,
                        Color.Lerp(HolyGold, HealingGreen, ring * 0.2f),
                        Main.rand.Next(35, 50),
                        1f,
                        1.3f,
                        hueShift: 0.01f
                    );
                    PRTLoader.AddParticle(rune);

                    //符文尾迹
                    for (int trail = 0; trail < 3; trail++) {
                        Vector2 trailPos = runePos + new Vector2(0, trail * 5f);
                        Dust d = Dust.NewDustPerfect(trailPos, DustID.GreenFairy,
                            velocity * 0.5f, 100, HealingGreen, 0.6f - trail * 0.15f);
                        d.noGravity = true;
                    }
                }
            }
        }
        #endregion

        #region 天使羽翼
        /// <summary>
        /// 生成天使羽翼效果
        /// </summary>
        private static void SpawnAngelWings(Vector2 center) {
            //左翼
            SpawnWing(center, -1);
            //右翼
            SpawnWing(center, 1);
        }

        /// <summary>
        /// 生成单边翅膀
        /// </summary>
        private static void SpawnWing(Vector2 center, int side) {
            float baseX = side * 25f;
            int featherCount = 8;

            for (int i = 0; i < featherCount; i++) {
                //羽毛角度（从背部向外展开）
                float featherAngle = MathHelper.ToRadians(-60 + i * 15) * side;
                float featherLength = 20f + i * 8f;

                //羽毛起点
                Vector2 featherStart = center + new Vector2(baseX, -10f + i * 3f);

                //沿羽毛长度生成粒子
                for (int j = 0; j < 6; j++) {
                    float t = j / 5f;
                    Vector2 pos = featherStart + featherAngle.ToRotationVector2() * (featherLength * t);

                    //羽毛粒子
                    float scale = 0.15f * (1f - t * 0.5f);
                    Vector2 vel = featherAngle.ToRotationVector2() * 1f + new Vector2(0, -0.5f);

                    BasePRT particle = new PRT_Light(
                        pos,
                        vel,
                        scale,
                        Color.Lerp(PureWhite, SoftYellow, t),
                        Main.rand.Next(20, 35),
                        0.9f,
                        1.1f,
                        hueShift: 0f
                    );
                    PRTLoader.AddParticle(particle);
                }

                //羽毛尖端的闪光
                Vector2 tipPos = featherStart + featherAngle.ToRotationVector2() * featherLength;
                Dust d = Dust.NewDustPerfect(tipPos, DustID.GoldFlame,
                    featherAngle.ToRotationVector2() * 2f,
                    100, HolyGold, 0.8f);
                d.noGravity = true;
            }
        }
        #endregion

        #region 治愈波纹
        /// <summary>
        /// 生成向外扩散的治愈波纹
        /// </summary>
        private static void SpawnHealingRipples(Vector2 center) {
            //三层波纹
            for (int wave = 0; wave < 3; wave++) {
                float waveRadius = 20f + wave * 25f;
                int particleCount = 12 + wave * 6;
                float speed = 4f + wave * 1.5f;

                for (int i = 0; i < particleCount; i++) {
                    float angle = MathHelper.TwoPi * i / particleCount + wave * 0.15f;
                    Vector2 pos = center + angle.ToRotationVector2() * waveRadius;
                    Vector2 velocity = angle.ToRotationVector2() * speed;

                    Color color = wave switch {
                        0 => PureWhite,
                        1 => HolyGold,
                        _ => HealingGreen
                    };

                    BasePRT particle = new PRT_Spark(
                        pos,
                        velocity,
                        false,
                        Main.rand.Next(18, 28),
                        0.9f - wave * 0.15f,
                        color,
                        null
                    );
                    PRTLoader.AddParticle(particle);
                }
            }
        }
        #endregion

        #region 漂浮光点
        /// <summary>
        /// 生成漂浮的治愈光点
        /// </summary>
        private static void SpawnFloatingLights(Vector2 center, int healAmount) {
            //光点数量根据治愈量调整
            int lightCount = 15 + Math.Min(healAmount / 10, 20);

            for (int i = 0; i < lightCount; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = Main.rand.NextFloat(20f, 80f);
                Vector2 pos = center + angle.ToRotationVector2() * dist;

                //缓慢上升的光点
                Vector2 velocity = new Vector2(
                    Main.rand.NextFloat(-1f, 1f),
                    Main.rand.NextFloat(-3f, -1f)
                );

                Color color = Main.rand.Next(3) switch {
                    0 => HolyGold,
                    1 => HealingGreen,
                    _ => PureWhite
                };

                BasePRT particle = new PRT_Light(
                    pos,
                    velocity,
                    Main.rand.NextFloat(0.1f, 0.2f),
                    color,
                    Main.rand.Next(40, 60),
                    0.8f,
                    1f,
                    hueShift: Main.rand.NextFloat(-0.02f, 0.02f)
                );
                PRTLoader.AddParticle(particle);
            }
        }
        #endregion

        #region 门徒光芒
        /// <summary>
        /// 在门徒位置生成光芒
        /// </summary>
        private static void SpawnDiscipleGlow(Vector2 center) {
            //光芒爆发
            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 7f);

                BasePRT particle = new PRT_Light(
                    center + Main.rand.NextVector2Circular(10f, 10f),
                    vel,
                    Main.rand.NextFloat(0.15f, 0.25f),
                    HealingGreen,
                    Main.rand.Next(20, 35),
                    1f,
                    1.2f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }

            //十字架闪光
            for (int arm = 0; arm < 4; arm++) {
                float angle = MathHelper.PiOver2 * arm;
                for (int i = 1; i <= 5; i++) {
                    Vector2 pos = center + angle.ToRotationVector2() * (i * 6f);
                    Dust d = Dust.NewDustPerfect(pos, DustID.GreenFairy,
                        angle.ToRotationVector2() * 1f, 100, HealingGreen, 1f - i * 0.15f);
                    d.noGravity = true;
                }
            }
        }
        #endregion

        #region 被动治愈光环
        /// <summary>
        /// 生成持续的被动治愈光环效果（每帧调用）
        /// </summary>
        /// <param name="playerCenter">玩家中心</param>
        /// <param name="discipleCenter">门徒中心</param>
        /// <param name="timer">计时器</param>
        public static void SpawnPassiveHealingAura(Vector2 playerCenter, Vector2 discipleCenter, int timer) {
            if (VaultUtils.isServer) return;

            //每10帧生成一个小光点
            if (timer % 10 == 0) {
                //从门徒飘向玩家的治愈光点
                Vector2 direction = (playerCenter - discipleCenter).SafeNormalize(Vector2.UnitX);
                Vector2 startPos = discipleCenter + Main.rand.NextVector2Circular(15f, 15f);

                BasePRT particle = new PRT_Light(
                    startPos,
                    direction * 3f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                    0.1f,
                    Color.Lerp(HealingGreen, HolyGold, Main.rand.NextFloat()),
                    Main.rand.Next(30, 50),
                    0.7f,
                    1f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }

            //每30帧在玩家周围生成小十字
            if (timer % 30 == 0) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = playerCenter + angle.ToRotationVector2() * Main.rand.NextFloat(30f, 50f);

                //小型十字形Dust
                for (int arm = 0; arm < 4; arm++) {
                    float crossAngle = MathHelper.PiOver2 * arm;
                    Vector2 dustPos = pos + crossAngle.ToRotationVector2() * 5f;
                    Dust d = Dust.NewDustPerfect(dustPos, DustID.GreenFairy,
                        new Vector2(0, -1f), 100, HealingGreen, 0.6f);
                    d.noGravity = true;
                }
            }
        }
        #endregion
    }
}
