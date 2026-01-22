using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums
{
    /// <summary>
    /// 化蛇术波动 - 摩西之杖的神迹
    /// 将范围内的敌怪转化为神圣之蛇，为玩家而战
    /// </summary>
    internal class SnakeConversionWave : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private Player Owner => Main.player[Projectile.owner];

        //最大半径
        private ref float MaxRadius => ref Projectile.ai[0];

        //蓄力比例
        private ref float ChargeRatio => ref Projectile.ai[1];

        //当前扩散半径
        private float currentRadius = 0f;

        //波动速度
        private const float ExpansionSpeed = 18f;

        //已转化的NPC列表(防止重复转化)
        private readonly HashSet<int> convertedNPCs = [];

        //转化的蛇数量
        private int serpentCount = 0;

        //视觉效果
        private readonly List<ConversionParticle> particles = [];
        private readonly List<MosesStaffRune> runes = [];
        private float waveAlpha = 1f;
        private float runeRotation = 0f;

        #region 颜色定义
        private static Color HolyGold => new Color(255, 215, 100);
        private static Color PureWhite => new Color(255, 255, 240);
        private static Color DivinePurple => new Color(180, 150, 220);
        private static Color SerpentGreen => new Color(150, 200, 120);
        #endregion

        private class ConversionParticle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
            public float Scale;
            public float Rotation;
            public Color ParticleColor;
            public bool IsRune;
        }

        private class MosesStaffRune
        {
            public float Angle;
            public float Distance;
            public float Scale;
            public float Alpha;
            public int RuneType;
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 150;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override void AI() {
            Projectile.Center = Owner.Center;

            //扩散波动
            currentRadius += ExpansionSpeed;
            runeRotation += 0.02f;

            //检测并转化敌人
            ConvertEnemiesInRange();

            //生成视觉粒子
            SpawnWaveParticles();

            //更新符文
            UpdateRunes();

            //更新粒子
            UpdateParticles();

            //淡出
            if (currentRadius > MaxRadius * 0.7f) {
                waveAlpha = MathHelper.Lerp(waveAlpha, 0f, 0.04f);
            }

            //结束条件
            if (currentRadius >= MaxRadius) {
                //最终转化统计
                if (serpentCount > 0) {
                    CombatText.NewText(Owner.Hitbox, HolyGold, $"化蛇 ×{serpentCount}", true);
                }
                Projectile.Kill();
            }

            //发光
            float intensity = waveAlpha * 0.8f;
            Lighting.AddLight(Projectile.Center, HolyGold.R / 255f * intensity, HolyGold.G / 255f * intensity, HolyGold.B / 255f * intensity * 0.8f);
        }

        /// <summary>
        /// 转化范围内的敌人
        /// </summary>
        private void ConvertEnemiesInRange() {
            foreach (NPC npc in Main.npc) {
                if (!npc.active) continue;
                if (convertedNPCs.Contains(npc.whoAmI)) continue;

                float dist = Vector2.Distance(npc.Center, Projectile.Center);

                //检查是否在波动范围内(环形区域)
                if (dist < currentRadius && dist > currentRadius - 40f) {
                    //检查是否可以被转化
                    if (CanBeConverted(npc)) {
                        ConvertToHolySerpent(npc);
                        convertedNPCs.Add(npc.whoAmI);
                        serpentCount++;
                    }
                    else if (npc.CanBeChasedBy()) {
                        //对无法转化的敌人造成伤害并产生神圣冲击
                        int damage = (int)(Projectile.damage * (1f + ChargeRatio * 0.5f));
                        npc.SimpleStrikeNPC(damage, 0, false, 0, DamageClass.Magic);

                        //神圣冲击特效
                        SpawnDivineImpact(npc.Center);

                        convertedNPCs.Add(npc.whoAmI);
                    }
                }
            }
        }

        /// <summary>
        /// 检查NPC是否可以被转化为蛇
        /// </summary>
        private bool CanBeConverted(NPC npc) {
            //Boss不能转化
            if (npc.boss) return false;

            //精英/小Boss不能转化
            if (NPCID.Sets.ShouldBeCountedAsBoss[npc.type]) return false;

            //某些特殊敌怪不能转化
            if (npc.immortal || npc.dontTakeDamage) return false;

            //友好NPC不能转化
            if (npc.friendly || npc.townNPC) return false;

            //必须是可攻击的敌怪
            if (!npc.CanBeChasedBy()) return false;

            //生命值过高的强力敌怪不转化(改为造成伤害)
            //蓄力越久，可转化的生命上限越高
            int maxLife = 3000 + (int)(ChargeRatio * 5000);
            if (npc.lifeMax > maxLife) return false;

            return true;
        }

        /// <summary>
        /// 将敌怪转化为神圣之蛇
        /// </summary>
        private void ConvertToHolySerpent(NPC npc) {
            //播放转化音效
            SoundEngine.PlaySound(SoundID.Item29 with {
                Volume = 0.8f,
                Pitch = 0.4f,
                PitchVariance = 0.1f
            }, npc.Center);

            //记录位置和属性
            Vector2 spawnPos = npc.Center;
            float npcPower = npc.lifeMax / 100f; //蛇的强度基于原NPC生命值
            float initialAngle = (Owner.Center - npc.Center).ToRotation(); //朝向玩家的反方向

            //生成转化特效
            SpawnConversionEffect(spawnPos, npc.width);

            //计算蛇的伤害（基于原敌人+蓄力）
            int serpentDamage = (int)(30 + npc.damage * 0.5f + ChargeRatio * 20);

            //杀死原NPC
            npc.life = 0;
            npc.active = false;
            npc.netUpdate = true;

            //生成神圣之蛇弹幕
            if (Projectile.IsOwnedByLocalPlayer()) {
                Projectile serpent = Projectile.NewProjectileDirect(
                    Projectile.GetSource_FromThis(),
                    spawnPos,
                    initialAngle.ToRotationVector2() * 10f,
                    ModContent.ProjectileType<HolySerpent>(),
                    serpentDamage,
                    3f,
                    Owner.whoAmI,
                    npcPower, //ai[0] = 蛇的强度
                    initialAngle //ai[1] = 初始角度
                );
            }
        }

        /// <summary>
        /// 生成转化特效
        /// </summary>
        private void SpawnConversionEffect(Vector2 pos, float size) {
            if (VaultUtils.isServer) return;

            float effectScale = Math.Max(1f, size / 40f);

            //神圣光芒爆发
            for (int i = 0; i < 25; i++) {
                float angle = MathHelper.TwoPi * i / 25f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 10f) * effectScale;

                Color color = Main.rand.Next(3) switch {
                    0 => HolyGold,
                    1 => PureWhite,
                    _ => SerpentGreen
                };

                BasePRT particle = new PRT_Light(
                    pos,
                    vel,
                    Main.rand.NextFloat(0.15f, 0.3f) * effectScale,
                    color,
                    Main.rand.Next(20, 35),
                    1f,
                    1.3f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }

            //蛇形螺旋
            for (int i = 0; i < 15; i++) {
                float spiralAngle = i * 0.5f;
                float spiralRadius = i * 3f * effectScale;
                Vector2 spiralPos = pos + spiralAngle.ToRotationVector2() * spiralRadius;
                Vector2 spiralVel = spiralAngle.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 2f;

                BasePRT spark = new PRT_Spark(
                    spiralPos,
                    spiralVel + new Vector2(0, -1f),
                    false,
                    Main.rand.Next(15, 25),
                    0.6f,
                    SerpentGreen,
                    null
                );
                PRTLoader.AddParticle(spark);
            }

            //十字架标记
            for (int arm = 0; arm < 4; arm++) {
                float crossAngle = MathHelper.PiOver2 * arm;
                for (int i = 1; i <= 5; i++) {
                    Vector2 crossPos = pos + crossAngle.ToRotationVector2() * (i * 8f * effectScale);

                    Dust d = Dust.NewDustPerfect(crossPos, DustID.GoldFlame,
                        crossAngle.ToRotationVector2() * 1f, 100, HolyGold, 1f - i * 0.15f);
                    d.noGravity = true;
                }
            }

            //添加转化符文
            runes.Add(new MosesStaffRune {
                Angle = Main.rand.NextFloat(MathHelper.TwoPi),
                Distance = Vector2.Distance(pos, Projectile.Center),
                Scale = effectScale,
                Alpha = 1f,
                RuneType = Main.rand.Next(3)
            });
        }

        /// <summary>
        /// 生成神圣冲击特效（对Boss等无法转化的敌人）
        /// </summary>
        private void SpawnDivineImpact(Vector2 pos) {
            if (VaultUtils.isServer) return;

            SoundEngine.PlaySound(SoundID.Item29 with {
                Volume = 0.5f,
                Pitch = 0.6f
            }, pos);

            //冲击波
            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 8f);

                BasePRT particle = new PRT_Light(
                    pos,
                    vel,
                    0.15f,
                    DivinePurple,
                    Main.rand.Next(15, 25),
                    1f,
                    1.2f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }

            //十字闪光
            for (int arm = 0; arm < 4; arm++) {
                float angle = MathHelper.PiOver2 * arm;
                Dust d = Dust.NewDustPerfect(pos + angle.ToRotationVector2() * 15f,
                    DustID.PurpleTorch, angle.ToRotationVector2() * 2f, 100, DivinePurple, 1f);
                d.noGravity = true;
            }
        }

        /// <summary>
        /// 生成波动粒子
        /// </summary>
        private void SpawnWaveParticles() {
            if (waveAlpha < 0.2f) return;

            int particleCount = (int)(8 * ChargeRatio) + 3;
            for (int i = 0; i < particleCount; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Projectile.Center + angle.ToRotationVector2() * currentRadius;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(1f, 3f);

                Color color = Main.rand.Next(4) switch {
                    0 => HolyGold,
                    1 => PureWhite,
                    2 => SerpentGreen,
                    _ => DivinePurple
                };

                particles.Add(new ConversionParticle {
                    Position = pos,
                    Velocity = vel,
                    Life = 0,
                    MaxLife = Main.rand.NextFloat(25f, 45f),
                    Scale = Main.rand.NextFloat(0.5f, 1.2f),
                    Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                    ParticleColor = color,
                    IsRune = Main.rand.NextBool(5)
                });
            }
        }

        /// <summary>
        /// 更新符文
        /// </summary>
        private void UpdateRunes() {
            for (int i = runes.Count - 1; i >= 0; i--) {
                var r = runes[i];
                r.Alpha *= 0.96f;
                r.Angle += 0.02f;

                if (r.Alpha < 0.05f) {
                    runes.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 更新粒子
        /// </summary>
        private void UpdateParticles() {
            for (int i = particles.Count - 1; i >= 0; i--) {
                var p = particles[i];
                p.Life++;
                p.Position += p.Velocity;
                p.Rotation += 0.08f;
                p.Velocity *= 0.96f;

                if (p.Life >= p.MaxLife) {
                    particles.RemoveAt(i);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 center = Projectile.Center - Main.screenPosition;

            Texture2D glowTex = CWRAsset.SoftGlow?.Value;
            Texture2D pixel = CWRAsset.Placeholder_White?.Value;

            if (pixel != null) {
                //绘制扩散波动环 - 多层渐变
                DrawWaveRing(sb, pixel, center, currentRadius, 5f, HolyGold with { A = 0 } * waveAlpha * 0.9f);
                DrawWaveRing(sb, pixel, center, currentRadius - 8f, 3f, PureWhite with { A = 0 } * waveAlpha * 0.6f);
                DrawWaveRing(sb, pixel, center, currentRadius + 12f, 2f, SerpentGreen with { A = 0 } * waveAlpha * 0.4f);
                DrawWaveRing(sb, pixel, center, currentRadius - 20f, 1f, DivinePurple with { A = 0 } * waveAlpha * 0.3f);

                //绘制蛇形符文在波动边缘
                int runeCount = 6 + (int)(ChargeRatio * 6);
                for (int i = 0; i < runeCount; i++) {
                    float angle = MathHelper.TwoPi * i / runeCount + runeRotation;
                    Vector2 runePos = center + angle.ToRotationVector2() * currentRadius;
                    DrawSerpentRune(sb, pixel, runePos, angle, waveAlpha * 0.7f);
                }

                //绘制十字架标记
                for (int i = 0; i < 4; i++) {
                    float crossAngle = MathHelper.PiOver2 * i + runeRotation * 0.5f;
                    Vector2 crossPos = center + crossAngle.ToRotationVector2() * (currentRadius * 0.7f);
                    DrawHolyCross(sb, pixel, crossPos, 15f * waveAlpha, HolyGold with { A = 0 } * waveAlpha * 0.5f);
                }
            }

            //绘制转化符文
            if (glowTex != null) {
                foreach (var r in runes) {
                    Vector2 runePos = Projectile.Center + r.Angle.ToRotationVector2() * r.Distance - Main.screenPosition;
                    Color runeColor = HolyGold with { A = 0 } * r.Alpha * 0.8f;
                    sb.Draw(glowTex, runePos, null, runeColor, r.Angle, glowTex.Size() / 2, r.Scale * 0.5f, SpriteEffects.None, 0);
                }
            }

            //绘制粒子
            if (glowTex != null) {
                foreach (var p in particles) {
                    float alpha = 1f - (p.Life / p.MaxLife);
                    Color pColor = p.ParticleColor with { A = 0 } * alpha * 0.7f;
                    Vector2 drawPos = p.Position - Main.screenPosition;

                    if (p.IsRune) {
                        //符文粒子带旋转
                        sb.Draw(glowTex, drawPos, null, pColor, p.Rotation, glowTex.Size() / 2, p.Scale * 0.25f, SpriteEffects.None, 0);
                    }
                    else {
                        sb.Draw(glowTex, drawPos, null, pColor, 0, glowTex.Size() / 2, p.Scale * 0.2f, SpriteEffects.None, 0);
                    }
                }
            }

            //绘制中心摩西之杖光晕
            if (glowTex != null) {
                float centerPulse = (float)Math.Sin(currentRadius * 0.08f) * 0.25f + 0.75f;

                //外层金色光晕
                Color outerGlow = HolyGold with { A = 0 } * waveAlpha * 0.5f * centerPulse;
                sb.Draw(glowTex, center, null, outerGlow, 0, glowTex.Size() / 2, 2.5f, SpriteEffects.None, 0);

                //中层蛇绿光晕
                Color midGlow = SerpentGreen with { A = 0 } * waveAlpha * 0.3f * centerPulse;
                sb.Draw(glowTex, center, null, midGlow, 0, glowTex.Size() / 2, 1.5f, SpriteEffects.None, 0);

                //内层白色核心
                Color coreGlow = PureWhite with { A = 0 } * waveAlpha * 0.4f * centerPulse;
                sb.Draw(glowTex, center, null, coreGlow, 0, glowTex.Size() / 2, 0.8f, SpriteEffects.None, 0);
            }

            return false;
        }

        /// <summary>
        /// 绘制波动环
        /// </summary>
        private static void DrawWaveRing(SpriteBatch sb, Texture2D pixel, Vector2 center, float radius, float thickness, Color color) {
            if (radius <= 0) return;

            int segments = (int)(radius / 4f);
            segments = Math.Clamp(segments, 16, 90);

            for (int i = 0; i < segments; i++) {
                float angle = MathHelper.TwoPi * i / segments;
                float nextAngle = MathHelper.TwoPi * (i + 1) / segments;

                Vector2 start = center + angle.ToRotationVector2() * radius;
                Vector2 end = center + nextAngle.ToRotationVector2() * radius;

                Vector2 diff = end - start;
                float length = diff.Length();
                if (length < 1f) continue;

                sb.Draw(pixel, start, new Rectangle(0, 0, 1, 1), color, diff.ToRotation(), Vector2.Zero, new Vector2(length, thickness), SpriteEffects.None, 0f);
            }
        }

        /// <summary>
        /// 绘制蛇形符文
        /// </summary>
        private static void DrawSerpentRune(SpriteBatch sb, Texture2D pixel, Vector2 center, float rotation, float alpha) {
            Color runeColor = SerpentGreen with { A = 0 } * alpha;

            //简化的蛇形 - S形曲线
            float size = 12f;
            for (int i = 0; i < 6; i++) {
                float t = i / 5f;
                float x = MathF.Sin(t * MathHelper.Pi * 2f) * size * 0.5f;
                float y = (t - 0.5f) * size * 2f;

                Vector2 offset = new Vector2(x, y).RotatedBy(rotation);
                float segmentSize = 3f - t * 1.5f;

                sb.Draw(pixel, center + offset, null, runeColor, rotation, new Vector2(0.5f), new Vector2(segmentSize, segmentSize), SpriteEffects.None, 0);
            }

            //蛇眼
            Vector2 headPos = center + new Vector2(0, -size).RotatedBy(rotation);
            Color eyeColor = HolyGold with { A = 0 } * alpha;
            sb.Draw(pixel, headPos + new Vector2(-2f, 0).RotatedBy(rotation), null, eyeColor, 0, new Vector2(0.5f), 2f, SpriteEffects.None, 0);
            sb.Draw(pixel, headPos + new Vector2(2f, 0).RotatedBy(rotation), null, eyeColor, 0, new Vector2(0.5f), 2f, SpriteEffects.None, 0);
        }

        /// <summary>
        /// 绘制神圣十字架
        /// </summary>
        private static void DrawHolyCross(SpriteBatch sb, Texture2D pixel, Vector2 center, float size, Color color) {
            float thickness = 2f;
            //垂直
            sb.Draw(pixel, center - new Vector2(thickness / 2, size / 2), null, color, 0, Vector2.Zero, new Vector2(thickness, size), SpriteEffects.None, 0);
            //水平
            sb.Draw(pixel, center - new Vector2(size / 2, thickness / 2), null, color, 0, Vector2.Zero, new Vector2(size, thickness), SpriteEffects.None, 0);
        }

        public override bool? CanDamage() => false; //伤害由ConvertEnemiesInRange处理
    }
}
