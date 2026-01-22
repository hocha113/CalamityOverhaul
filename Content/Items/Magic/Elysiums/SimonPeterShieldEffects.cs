using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums
{
    /// <summary>
    /// 西门彼得（SimonPeter）门徒的磐石之盾效果
    /// 教会磐石风格 - 天国之钥、神圣护盾、磐石防护
    /// </summary>
    internal static class SimonPeterShieldEffects
    {
        #region 颜色定义
        /// <summary>天国金 - 主色调（钥匙）</summary>
        public static Color HeavenlyGold => new Color(255, 215, 100);
        /// <summary>磐石灰 - 坚固感</summary>
        public static Color RockGray => new Color(180, 180, 190);
        /// <summary>神圣白 - 高光</summary>
        public static Color HolyWhite => new Color(255, 255, 250);
        /// <summary>天蓝色 - 天国</summary>
        public static Color HeavenBlue => new Color(150, 200, 255);
        /// <summary>护盾银 - 防护</summary>
        public static Color ShieldSilver => new Color(220, 225, 235);
        #endregion

        #region 主动技能特效
        /// <summary>
        /// 生成完整的磐石之盾特效
        /// </summary>
        public static void SpawnRockShieldEffect(Player player, Vector2 discipleCenter, float shieldStrength) {
            if (VaultUtils.isServer) return;

            Vector2 playerCenter = player.Center;

            //播放护盾音效
            PlayShieldSounds(playerCenter);

            //阶段1：从门徒发射神圣光束
            SpawnHolyBeam(discipleCenter, playerCenter);

            //阶段2：天国之钥显现
            SpawnHeavenlyKey(playerCenter);

            //阶段3：磐石护盾环绕
            SpawnRockShieldRing(playerCenter, shieldStrength);

            //阶段4：十字架光芒
            SpawnCrossRadiance(playerCenter);

            //阶段5：护盾符文
            SpawnShieldRunes(playerCenter);

            //阶段6：神圣光柱
            SpawnDivinePillar(playerCenter);

            //在门徒位置生成光芒
            SpawnDiscipleGlow(discipleCenter);
        }
        #endregion

        #region 音效
        private static void PlayShieldSounds(Vector2 center) {
            //护盾激活音效
            SoundEngine.PlaySound(SoundID.Item29 with {
                Volume = 0.9f,
                Pitch = 0.2f
            }, center);

            //金属回响
            SoundEngine.PlaySound(SoundID.Item37 with {
                Volume = 0.5f,
                Pitch = 0.4f
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
            int particleCount = (int)(distance / 6f);

            for (int i = 0; i <= particleCount; i++) {
                float t = i / (float)particleCount;
                Vector2 pos = Vector2.Lerp(from, to, t);

                //双螺旋偏移
                float spiral1 = MathF.Sin(t * MathHelper.Pi * 4f) * 8f;
                float spiral2 = MathF.Cos(t * MathHelper.Pi * 4f) * 8f;
                Vector2 perpendicular = direction.RotatedBy(MathHelper.PiOver2);

                //金色螺旋
                Vector2 pos1 = pos + perpendicular * spiral1;
                BasePRT particle1 = new PRT_Light(
                    pos1,
                    direction * 2f,
                    0.12f * (1f - t * 0.3f),
                    HeavenlyGold,
                    Main.rand.Next(12, 20),
                    1f,
                    1.2f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle1);

                //银色螺旋
                Vector2 pos2 = pos + perpendicular * spiral2;
                BasePRT particle2 = new PRT_Light(
                    pos2,
                    direction * 2f,
                    0.1f * (1f - t * 0.3f),
                    ShieldSilver,
                    Main.rand.Next(12, 20),
                    1f,
                    1.1f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle2);
            }
        }
        #endregion

        #region 天国之钥
        /// <summary>
        /// 生成天国之钥效果（彼得的象征物）
        /// </summary>
        private static void SpawnHeavenlyKey(Vector2 center) {
            Vector2 keyCenter = center - new Vector2(0, 50f);
            float keyScale = 1.2f;

            //钥匙柄（圆环）
            float handleRadius = 12f * keyScale;
            int handleSegments = 16;
            for (int i = 0; i < handleSegments; i++) {
                float angle = MathHelper.TwoPi * i / handleSegments;
                Vector2 pos = keyCenter + angle.ToRotationVector2() * handleRadius;

                BasePRT particle = new PRT_Light(
                    pos,
                    angle.ToRotationVector2() * 1f,
                    0.12f,
                    HeavenlyGold,
                    Main.rand.Next(25, 40),
                    1f,
                    1.2f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }

            //钥匙杆
            float shaftLength = 40f * keyScale;
            for (int i = 0; i < 12; i++) {
                float t = i / 11f;
                Vector2 pos = keyCenter + new Vector2(0, handleRadius + shaftLength * t);

                float width = t > 0.7f ? 0.15f : 0.12f;
                BasePRT particle = new PRT_Light(
                    pos,
                    new Vector2(0, 1f),
                    width,
                    Color.Lerp(HeavenlyGold, ShieldSilver, t * 0.3f),
                    Main.rand.Next(25, 40),
                    1f,
                    1.1f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }

            //钥匙齿（底部横向）
            Vector2 teethBase = keyCenter + new Vector2(0, handleRadius + shaftLength);
            //横向基础
            for (int i = -2; i <= 2; i++) {
                Vector2 pos = teethBase + new Vector2(i * 5f * keyScale, 0);
                Dust d = Dust.NewDustPerfect(pos, DustID.GoldFlame,
                    new Vector2(0, 1f), 100, HeavenlyGold, 0.8f);
                d.noGravity = true;
            }
            //齿
            for (int tooth = 0; tooth < 3; tooth++) {
                float x = (-1 + tooth) * 8f * keyScale;
                float length = (tooth == 1) ? 12f : 8f;
                for (int i = 0; i < 4; i++) {
                    Vector2 pos = teethBase + new Vector2(x, i * length / 3f);
                    Dust d = Dust.NewDustPerfect(pos, DustID.GoldFlame,
                        new Vector2(0, 0.5f), 100, HeavenlyGold, 0.6f);
                    d.noGravity = true;
                }
            }

            //钥匙光芒
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(2f, 4f);

                BasePRT glow = new PRT_Light(
                    keyCenter,
                    vel,
                    0.15f,
                    HolyWhite,
                    Main.rand.Next(15, 25),
                    1f,
                    1.3f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(glow);
            }
        }
        #endregion

        #region 磐石护盾环
        /// <summary>
        /// 生成磐石护盾环绕效果
        /// </summary>
        private static void SpawnRockShieldRing(Vector2 center, float strength) {
            float baseRadius = 55f + strength * 10f;

            //三层护盾环
            for (int layer = 0; layer < 3; layer++) {
                float layerRadius = baseRadius * (0.7f + layer * 0.2f);
                int segments = 20 + layer * 8;
                float rotationOffset = layer * MathHelper.Pi / 6f;

                Color layerColor = layer switch {
                    0 => RockGray,
                    1 => ShieldSilver,
                    _ => HeavenlyGold
                };

                for (int i = 0; i < segments; i++) {
                    float angle = MathHelper.TwoPi * i / segments + rotationOffset;
                    Vector2 pos = center + angle.ToRotationVector2() * layerRadius;

                    //向外扩散
                    Vector2 velocity = angle.ToRotationVector2() * (2f + layer);

                    BasePRT particle = new PRT_Light(
                        pos,
                        velocity,
                        0.15f - layer * 0.02f,
                        layerColor,
                        Main.rand.Next(20, 35),
                        1f,
                        1.2f,
                        hueShift: 0f
                    );
                    PRTLoader.AddParticle(particle);

                    //每隔几个生成火花
                    if (i % 4 == 0) {
                        BasePRT spark = new PRT_Spark(
                            pos,
                            velocity * 1.5f,
                            false,
                            Main.rand.Next(15, 25),
                            0.6f,
                            layerColor,
                            null
                        );
                        PRTLoader.AddParticle(spark);
                    }
                }
            }

            //磐石碎片效果
            for (int i = 0; i < 15; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = Main.rand.NextFloat(baseRadius * 0.5f, baseRadius * 1.2f);
                Vector2 pos = center + angle.ToRotationVector2() * dist;

                Dust d = Dust.NewDustPerfect(pos, DustID.Stone,
                    angle.ToRotationVector2() * Main.rand.NextFloat(2f, 5f),
                    100, RockGray, Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = true;
            }
        }
        #endregion

        #region 十字架光芒
        /// <summary>
        /// 生成十字架光芒效果
        /// </summary>
        private static void SpawnCrossRadiance(Vector2 center) {
            float crossSize = 70f;

            //四个方向的光芒臂
            for (int arm = 0; arm < 4; arm++) {
                float baseAngle = MathHelper.PiOver2 * arm;

                for (int i = 1; i <= 10; i++) {
                    float t = i / 10f;
                    float dist = crossSize * t;
                    Vector2 pos = center + baseAngle.ToRotationVector2() * dist;

                    //光芒渐变
                    float scale = 0.2f * (1f - t * 0.5f);
                    Color color = Color.Lerp(HolyWhite, HeavenlyGold, t);

                    BasePRT particle = new PRT_Light(
                        pos,
                        baseAngle.ToRotationVector2() * 2f,
                        scale,
                        color,
                        Main.rand.Next(20, 35),
                        1.1f,
                        1.3f,
                        hueShift: 0f
                    );
                    PRTLoader.AddParticle(particle);
                }

                //臂端闪光
                Vector2 tipPos = center + baseAngle.ToRotationVector2() * crossSize;
                for (int j = 0; j < 5; j++) {
                    Vector2 vel = baseAngle.ToRotationVector2() * Main.rand.NextFloat(3f, 6f) +
                                  Main.rand.NextVector2Circular(2f, 2f);

                    BasePRT spark = new PRT_Spark(
                        tipPos,
                        vel,
                        false,
                        Main.rand.Next(12, 20),
                        0.6f,
                        HeavenlyGold,
                        null
                    );
                    PRTLoader.AddParticle(spark);
                }
            }

            //十字架中心爆发
            for (int i = 0; i < 20; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 7f);

                BasePRT particle = new PRT_Light(
                    center + Main.rand.NextVector2Circular(5f, 5f),
                    vel,
                    Main.rand.NextFloat(0.15f, 0.25f),
                    HolyWhite,
                    Main.rand.Next(15, 25),
                    1.2f,
                    1.4f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }
        }
        #endregion

        #region 护盾符文
        /// <summary>
        /// 生成护盾符文效果
        /// </summary>
        private static void SpawnShieldRunes(Vector2 center) {
            int runeCount = 6;
            float runeRadius = 80f;

            for (int i = 0; i < runeCount; i++) {
                float angle = MathHelper.TwoPi * i / runeCount;
                Vector2 runeCenter = center + angle.ToRotationVector2() * runeRadius;

                //每个符文是一个小盾牌形状
                SpawnMiniShieldRune(runeCenter, angle);
            }
        }

        /// <summary>
        /// 生成单个小盾牌符文
        /// </summary>
        private static void SpawnMiniShieldRune(Vector2 center, float rotation) {
            float size = 10f;

            //盾牌顶部（弧形）
            for (int i = -3; i <= 3; i++) {
                float t = i / 3f;
                float x = t * size;
                float y = -MathF.Sqrt(1f - t * t) * size * 0.5f - size * 0.3f;
                Vector2 pos = center + new Vector2(x, y).RotatedBy(rotation);

                Dust d = Dust.NewDustPerfect(pos, DustID.GoldFlame,
                    new Vector2(0, -1f), 100, ShieldSilver, 0.6f);
                d.noGravity = true;
            }

            //盾牌底部（尖端）
            for (int i = 0; i < 5; i++) {
                float t = i / 4f;
                Vector2 left = center + new Vector2(-size, 0).RotatedBy(rotation);
                Vector2 right = center + new Vector2(size, 0).RotatedBy(rotation);
                Vector2 bottom = center + new Vector2(0, size * 1.2f).RotatedBy(rotation);

                Vector2 posL = Vector2.Lerp(left, bottom, t);
                Vector2 posR = Vector2.Lerp(right, bottom, t);

                Dust dL = Dust.NewDustPerfect(posL, DustID.GoldFlame,
                    new Vector2(0, -0.5f), 100, ShieldSilver, 0.5f);
                dL.noGravity = true;

                Dust dR = Dust.NewDustPerfect(posR, DustID.GoldFlame,
                    new Vector2(0, -0.5f), 100, ShieldSilver, 0.5f);
                dR.noGravity = true;
            }

            //盾牌中心十字
            BasePRT cross = new PRT_Light(
                center,
                new Vector2(0, -1f),
                0.15f,
                HeavenlyGold,
                25,
                1f,
                1.1f,
                hueShift: 0f
            );
            PRTLoader.AddParticle(cross);
        }
        #endregion

        #region 神圣光柱
        /// <summary>
        /// 生成神圣光柱效果
        /// </summary>
        private static void SpawnDivinePillar(Vector2 center) {
            float pillarHeight = 120f;

            //从中心向上的光柱
            for (int i = 0; i < 25; i++) {
                float t = i / 24f;
                float height = pillarHeight * t;
                float xOffset = Main.rand.NextFloat(-8f, 8f) * (1f - t);
                Vector2 pos = center - new Vector2(xOffset, height);

                Vector2 velocity = new Vector2(0, -3f - t * 2f);
                float alpha = 1f - t * 0.6f;

                BasePRT particle = new PRT_Light(
                    pos,
                    velocity,
                    (0.18f - t * 0.08f),
                    HeavenBlue * alpha,
                    Main.rand.Next(20, 35),
                    0.9f,
                    1.1f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }

            //光柱底部光环
            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12f;
                Vector2 vel = angle.ToRotationVector2() * 2f + new Vector2(0, -3f);

                Dust d = Dust.NewDustPerfect(center, DustID.BlueTorch,
                    vel, 100, HeavenBlue, 1f);
                d.noGravity = true;
            }
        }
        #endregion

        #region 门徒光芒
        private static void SpawnDiscipleGlow(Vector2 center) {
            //金色光芒爆发
            for (int i = 0; i < 15; i++) {
                float angle = MathHelper.TwoPi * i / 15f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(3f, 6f);

                BasePRT particle = new PRT_Light(
                    center,
                    vel,
                    0.15f,
                    HeavenlyGold,
                    Main.rand.Next(15, 25),
                    1f,
                    1.2f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }

            //钥匙形闪光
            Dust key = Dust.NewDustPerfect(center + new Vector2(0, -10f), DustID.GoldFlame,
                new Vector2(0, -1f), 100, HeavenlyGold, 1.2f);
            key.noGravity = true;
        }
        #endregion

        #region 持续护盾效果
        /// <summary>
        /// 持续的被动护盾光环（每帧调用）
        /// </summary>
        public static void SpawnPassiveShieldAura(Vector2 playerCenter, Vector2 discipleCenter, int timer, float shieldStrength) {
            if (VaultUtils.isServer) return;

            //旋转护盾环
            if (timer % 3 == 0) {
                float angle = timer * 0.05f;
                float radius = 50f + shieldStrength * 5f;
                Vector2 pos = playerCenter + angle.ToRotationVector2() * radius;

                BasePRT particle = new PRT_Spark(
                    pos,
                    angle.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 2f,
                    false,
                    Main.rand.Next(8, 15),
                    0.5f,
                    ShieldSilver,
                    null
                );
                PRTLoader.AddParticle(particle);
            }

            //对向旋转环
            if (timer % 4 == 0) {
                float angle = -timer * 0.04f;
                float radius = 45f + shieldStrength * 3f;
                Vector2 pos = playerCenter + angle.ToRotationVector2() * radius;

                Dust d = Dust.NewDustPerfect(pos, DustID.GoldFlame,
                    angle.ToRotationVector2().RotatedBy(-MathHelper.PiOver2) * 1f,
                    100, HeavenlyGold, 0.5f);
                d.noGravity = true;
            }

            //每30帧生成小十字
            if (timer % 30 == 0) {
                float crossAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 crossPos = playerCenter + crossAngle.ToRotationVector2() * Main.rand.NextFloat(35f, 55f);

                for (int arm = 0; arm < 4; arm++) {
                    float armAngle = MathHelper.PiOver2 * arm;
                    Vector2 armPos = crossPos + armAngle.ToRotationVector2() * 5f;

                    Dust d = Dust.NewDustPerfect(armPos, DustID.GoldFlame,
                        new Vector2(0, -0.5f), 100, HeavenlyGold, 0.4f);
                    d.noGravity = true;
                }
            }

            //每20帧从门徒飘向玩家的守护光点
            if (timer % 20 == 0) {
                Vector2 direction = (playerCenter - discipleCenter).SafeNormalize(Vector2.UnitX);
                Vector2 startPos = discipleCenter + Main.rand.NextVector2Circular(10f, 10f);

                BasePRT particle = new PRT_Light(
                    startPos,
                    direction * 4f,
                    0.1f,
                    Color.Lerp(HeavenlyGold, ShieldSilver, Main.rand.NextFloat()),
                    Main.rand.Next(25, 40),
                    0.7f,
                    0.9f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }
        }

        /// <summary>
        /// 受到伤害时的护盾反应效果
        /// </summary>
        public static void SpawnDamageBlockEffect(Vector2 playerCenter, Vector2 hitDirection) {
            if (VaultUtils.isServer) return;

            //播放护盾格挡音效
            SoundEngine.PlaySound(SoundID.Item37 with {
                Volume = 0.6f,
                Pitch = 0.5f
            }, playerCenter);

            //冲击方向的护盾闪光
            Vector2 shieldPos = playerCenter + hitDirection * 40f;

            //护盾冲击波
            for (int i = 0; i < 15; i++) {
                float angle = MathHelper.TwoPi * i / 15f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(3f, 7f);

                //偏向冲击方向
                vel -= hitDirection * 2f;

                BasePRT particle = new PRT_Light(
                    shieldPos,
                    vel,
                    0.15f,
                    Main.rand.NextBool() ? ShieldSilver : HeavenlyGold,
                    Main.rand.Next(15, 25),
                    1f,
                    1.3f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }

            //磐石碎片
            for (int i = 0; i < 8; i++) {
                Vector2 vel = -hitDirection * Main.rand.NextFloat(2f, 5f) + Main.rand.NextVector2Circular(3f, 3f);

                Dust d = Dust.NewDustPerfect(shieldPos, DustID.Stone,
                    vel, 100, RockGray, Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = false; //石头会下落
            }

            //十字闪光
            for (int arm = 0; arm < 4; arm++) {
                float crossAngle = MathHelper.PiOver2 * arm;
                Vector2 crossPos = shieldPos + crossAngle.ToRotationVector2() * 15f;

                BasePRT spark = new PRT_Spark(
                    crossPos,
                    crossAngle.ToRotationVector2() * 3f,
                    false,
                    Main.rand.Next(10, 18),
                    0.6f,
                    HolyWhite,
                    null
                );
                PRTLoader.AddParticle(spark);
            }
        }
        #endregion

        #region 绘制辅助
        /// <summary>
        /// 绘制持续的护盾视觉（在PreDraw中调用）
        /// </summary>
        public static void DrawShieldAura(SpriteBatch sb, Vector2 playerCenter, int timer, float shieldStrength, float alpha) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D pixel = CWRAsset.Placeholder_White?.Value;
            if (glow == null || pixel == null) return;

            Vector2 drawPos = playerCenter - Main.screenPosition;
            float pulse = 0.8f + MathF.Sin(timer * 0.08f) * 0.2f;
            float radius = 50f + shieldStrength * 5f;

            //外层护盾光晕
            Color outerColor = ShieldSilver with { A = 0 } * alpha * 0.3f * pulse;
            sb.Draw(glow, drawPos, null, outerColor, 0, glow.Size() / 2, radius / 30f, SpriteEffects.None, 0);

            //内层金色光晕
            Color innerColor = HeavenlyGold with { A = 0 } * alpha * 0.2f * pulse;
            sb.Draw(glow, drawPos, null, innerColor, 0, glow.Size() / 2, radius / 50f, SpriteEffects.None, 0);

            //旋转的护盾环
            float ringRotation = timer * 0.02f;
            DrawShieldRing(sb, pixel, drawPos, radius, ringRotation, ShieldSilver with { A = 0 } * alpha * 0.4f);
            DrawShieldRing(sb, pixel, drawPos, radius * 0.8f, -ringRotation * 0.8f, HeavenlyGold with { A = 0 } * alpha * 0.3f);
        }

        private static void DrawShieldRing(SpriteBatch sb, Texture2D pixel, Vector2 center, float radius, float rotation, Color color) {
            int segments = 24;
            for (int i = 0; i < segments; i++) {
                //间断的环（盾牌感）
                if (i % 3 == 2) continue;

                float angle = MathHelper.TwoPi * i / segments + rotation;
                float nextAngle = MathHelper.TwoPi * (i + 1) / segments + rotation;

                Vector2 start = center + angle.ToRotationVector2() * radius;
                Vector2 end = center + nextAngle.ToRotationVector2() * radius;

                Vector2 diff = end - start;
                float length = diff.Length();
                if (length < 1f) continue;

                sb.Draw(pixel, start, new Rectangle(0, 0, 1, 1), color, diff.ToRotation(), Vector2.Zero, new Vector2(length, 2f), SpriteEffects.None, 0f);
            }
        }
        #endregion
    }
}
