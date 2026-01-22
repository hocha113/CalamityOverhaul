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
    /// 化蛇术波动，将范围内的非Boss/精英敌怪变成无害的蛇
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
        private const float ExpansionSpeed = 15f;

        //已转化的NPC列表(防止重复转化)
        private HashSet<int> convertedNPCs = [];

        //视觉效果
        private List<ConversionParticle> particles = [];
        private float waveAlpha = 1f;

        private class ConversionParticle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
            public float Scale;
            public float Rotation;
            public bool IsWhite;
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override void AI() {
            Projectile.Center = Owner.Center;

            //扩散波动
            currentRadius += ExpansionSpeed;

            //检测并转化敌人
            ConvertEnemiesInRange();

            //生成视觉粒子
            SpawnWaveParticles();

            //更新粒子
            UpdateParticles();

            //淡出
            if (currentRadius > MaxRadius * 0.7f) {
                waveAlpha = MathHelper.Lerp(waveAlpha, 0f, 0.05f);
            }

            //结束条件
            if (currentRadius >= MaxRadius) {
                Projectile.Kill();
            }

            //发光
            float intensity = waveAlpha * 0.8f;
            Lighting.AddLight(Projectile.Center, intensity, intensity, intensity * 0.8f);
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
                if (dist < currentRadius && dist > currentRadius - 30f) {
                    //检查是否可以被转化(非Boss、非精英、可攻击)
                    if (CanBeConverted(npc)) {
                        ConvertToSnake(npc);
                        convertedNPCs.Add(npc.whoAmI);
                    }
                    else if (npc.CanBeChasedBy()) {
                        //对无法转化的敌人造成伤害
                        npc.SimpleStrikeNPC(Projectile.damage, 0, false, 0, DamageClass.Magic);
                        //标记已处理
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
            if (npc.lifeMax > 5000) return false;

            return true;
        }

        /// <summary>
        /// 将敌怪转化为无害的蛇
        /// </summary>
        private void ConvertToSnake(NPC npc) {
            //播放转化音效
            SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.6f, Pitch = 0.5f }, npc.Center);

            //转化特效
            for (int i = 0; i < 20; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(5f, 5f);
                int dustType = Main.rand.NextBool() ? DustID.SilverFlame : DustID.Shadowflame;
                Dust d = Dust.NewDustPerfect(npc.Center, dustType, vel, 100, default, 1.5f);
                d.noGravity = true;
            }

            //记录位置
            Vector2 spawnPos = npc.Center;

            //杀死原NPC(不掉落物品)
            npc.life = 0;
            npc.active = false;
            npc.netUpdate = true;

            //生成无害的蛇(使用丛林蛇，它是无敌的小动物)
            //NPC蛇没有合适的，改用生成一个蛇形的粒子效果
            SpawnSnakeVisual(spawnPos);

            //或者生成一个真正的无害生物
            int snake = NPC.NewNPC(Projectile.GetSource_FromThis(), (int)spawnPos.X, (int)spawnPos.Y, NPCID.Worm);
            if (snake >= 0 && snake < Main.maxNPCs) {
                Main.npc[snake].friendly = true;
                Main.npc[snake].damage = 0;
                Main.npc[snake].defense = 0;
                Main.npc[snake].lifeMax = 5;
                Main.npc[snake].life = 5;
            }
        }

        /// <summary>
        /// 生成蛇形视觉效果
        /// </summary>
        private void SpawnSnakeVisual(Vector2 pos) {
            //蛇形粒子轨迹
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            for (int i = 0; i < 8; i++) {
                Vector2 offset = angle.ToRotationVector2() * (i * 5f);
                angle += Main.rand.NextFloat(-0.3f, 0.3f);

                Dust d = Dust.NewDustPerfect(pos + offset, DustID.Smoke, Main.rand.NextVector2Circular(1f, 1f), 100, Color.DarkGreen, 1f);
                d.noGravity = true;
            }
        }

        /// <summary>
        /// 生成波动粒子
        /// </summary>
        private void SpawnWaveParticles() {
            if (waveAlpha < 0.3f) return;

            int particleCount = (int)(6 * ChargeRatio) + 2;
            for (int i = 0; i < particleCount; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Projectile.Center + angle.ToRotationVector2() * currentRadius;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(1f, 3f);

                particles.Add(new ConversionParticle {
                    Position = pos,
                    Velocity = vel,
                    Life = 0,
                    MaxLife = Main.rand.NextFloat(20f, 40f),
                    Scale = Main.rand.NextFloat(0.5f, 1.5f),
                    Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                    IsWhite = Main.rand.NextBool()
                });
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
                p.Rotation += 0.1f;
                p.Velocity *= 0.95f;

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
                //绘制扩散波动环
                DrawWaveRing(sb, pixel, center, currentRadius, 4f, Color.White with { A = 0 } * waveAlpha * 0.8f);
                DrawWaveRing(sb, pixel, center, currentRadius - 10f, 2f, Color.Gold with { A = 0 } * waveAlpha * 0.5f);
                DrawWaveRing(sb, pixel, center, currentRadius + 10f, 2f, new Color(50, 50, 50) with { A = 0 } * waveAlpha * 0.3f);

                //绘制十字架标记在波动边缘
                for (int i = 0; i < 8; i++) {
                    float angle = MathHelper.TwoPi * i / 8f + currentRadius * 0.01f;
                    Vector2 crossPos = center + angle.ToRotationVector2() * currentRadius;
                    DrawCross(sb, pixel, crossPos, 12f, Color.Gold with { A = 0 } * waveAlpha * 0.6f);
                }
            }

            //绘制粒子
            if (glowTex != null) {
                foreach (var p in particles) {
                    float alpha = 1f - (p.Life / p.MaxLife);
                    Color pColor = (p.IsWhite ? Color.White : new Color(30, 30, 30)) with { A = 0 } * alpha * 0.6f;
                    Vector2 drawPos = p.Position - Main.screenPosition;
                    sb.Draw(glowTex, drawPos, null, pColor, p.Rotation, glowTex.Size() / 2, p.Scale * 0.3f, SpriteEffects.None, 0);
                }
            }

            //绘制中心光晕
            if (glowTex != null) {
                float centerPulse = (float)Math.Sin(currentRadius * 0.1f) * 0.2f + 0.8f;
                Color centerColor = Color.Gold with { A = 0 } * waveAlpha * 0.4f * centerPulse;
                sb.Draw(glowTex, center, null, centerColor, 0, glowTex.Size() / 2, 2f, SpriteEffects.None, 0);

                Color whiteCore = Color.White with { A = 0 } * waveAlpha * 0.3f * centerPulse;
                sb.Draw(glowTex, center, null, whiteCore, 0, glowTex.Size() / 2, 1f, SpriteEffects.None, 0);
            }

            return false;
        }

        /// <summary>
        /// 绘制波动环
        /// </summary>
        private static void DrawWaveRing(SpriteBatch sb, Texture2D pixel, Vector2 center, float radius, float thickness, Color color) {
            if (radius <= 0) return;

            int segments = (int)(radius / 5f);
            segments = Math.Clamp(segments, 12, 72);

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
        /// 绘制十字架
        /// </summary>
        private static void DrawCross(SpriteBatch sb, Texture2D pixel, Vector2 center, float size, Color color) {
            float half = size / 2;
            float thickness = 2f;
            //垂直
            sb.Draw(pixel, center - new Vector2(thickness / 2, half), null, color, 0, Vector2.Zero, new Vector2(thickness, size), SpriteEffects.None, 0);
            //水平
            sb.Draw(pixel, center - new Vector2(half, thickness / 2), null, color, 0, Vector2.Zero, new Vector2(size, thickness), SpriteEffects.None, 0);
        }

        public override bool? CanDamage() => false; //伤害由ConvertEnemiesInRange处理
    }
}
