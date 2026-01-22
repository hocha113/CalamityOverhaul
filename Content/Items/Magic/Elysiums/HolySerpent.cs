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
    /// 神圣之蛇弹幕 - 由化蛇术转化敌人生成
    /// 摩西之杖的化蛇奇迹风格，金色神圣蛇形追踪弹幕
    /// </summary>
    internal class HolySerpent : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        #region 字段与属性

        private Player Owner => Main.player[Projectile.owner];

        /// <summary>蛇的强度（基于原敌人生命值）</summary>
        private ref float SerpentPower => ref Projectile.ai[0];

        /// <summary>蛇的初始方向</summary>
        private ref float InitialAngle => ref Projectile.ai[1];

        /// <summary>AI状态</summary>
        private ref float AIState => ref Projectile.localAI[0];

        /// <summary>AI计时器</summary>
        private ref float AITimer => ref Projectile.localAI[1];

        //身体段
        private const int BodySegments = 12;
        private readonly List<Vector2> bodyPositions = [];
        private readonly List<float> bodyRotations = [];

        //蛇形运动参数
        private float slitherPhase = 0f;
        private float slitherSpeed = 0.12f;
        private float slitherAmplitude = 15f;

        //追踪参数
        private int targetNPC = -1;
        private float homingStrength = 0.08f;

        //视觉效果
        private float glowIntensity = 1f;
        private readonly List<SerpentParticle> particles = [];

        //状态枚举
        private enum State
        {
            Emerging = 0,   //出现阶段
            Seeking = 1,    //寻找目标
            Hunting = 2,    //追猎
            Striking = 3    //攻击
        }

        #endregion

        #region 颜色定义
        /// <summary>神圣金 - 主色</summary>
        public static Color HolyGold => new Color(255, 215, 100);
        /// <summary>纯净白 - 高光</summary>
        public static Color PureWhite => new Color(255, 255, 240);
        /// <summary>蛇鳞绿 - 辅助</summary>
        public static Color ScaleGreen => new Color(150, 200, 120);
        /// <summary>神秘紫 - 神圣</summary>
        public static Color MysticPurple => new Color(180, 150, 220);
        #endregion

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = BodySegments;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 600; //10秒
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override void AI() {
            AITimer++;

            //初始化身体段
            if (bodyPositions.Count == 0) {
                InitializeBody();
            }

            //更新蛇形运动
            slitherPhase += slitherSpeed;

            //状态机
            State currentState = (State)AIState;
            switch (currentState) {
                case State.Emerging:
                    EmergingAI();
                    break;
                case State.Seeking:
                    SeekingAI();
                    break;
                case State.Hunting:
                    HuntingAI();
                    break;
                case State.Striking:
                    StrikingAI();
                    break;
            }

            //更新身体段位置
            UpdateBodySegments();

            //更新粒子
            UpdateParticles();

            //生成神圣光芒
            SpawnHolyTrail();

            //发光
            float intensity = glowIntensity * 0.6f;
            Lighting.AddLight(Projectile.Center, HolyGold.R / 255f * intensity, HolyGold.G / 255f * intensity, HolyGold.B / 255f * intensity);
        }

        #region 初始化

        private void InitializeBody() {
            Vector2 backDir = -Projectile.velocity.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < BodySegments; i++) {
                bodyPositions.Add(Projectile.Center + backDir * (i * 8f));
                bodyRotations.Add(Projectile.velocity.ToRotation());
            }

            slitherPhase = Main.rand.NextFloat(MathHelper.TwoPi);

            //出现特效
            SpawnEmergenceEffect();
        }

        private void SpawnEmergenceEffect() {
            if (VaultUtils.isServer) return;

            SoundEngine.PlaySound(SoundID.Item8 with {
                Volume = 0.8f,
                Pitch = 0.3f
            }, Projectile.Center);

            //神圣光芒爆发
            for (int i = 0; i < 20; i++) {
                float angle = MathHelper.TwoPi * i / 20f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(3f, 7f);

                BasePRT particle = new PRT_Light(
                    Projectile.Center,
                    vel,
                    Main.rand.NextFloat(0.15f, 0.25f),
                    HolyGold,
                    Main.rand.Next(20, 35),
                    1f,
                    1.3f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }

            //蛇形轮廓
            for (int i = 0; i < 8; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame,
                    Main.rand.NextVector2Circular(4f, 4f), 100, HolyGold, 1.2f);
                d.noGravity = true;
            }
        }

        #endregion

        #region AI状态

        private void EmergingAI() {
            //出现阶段：快速移动并展开
            Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * 12f;

            if (AITimer > 20) {
                AIState = (float)State.Seeking;
                AITimer = 0;
            }
        }

        private void SeekingAI() {
            //寻找目标
            targetNPC = FindTarget();

            //缓慢盘旋
            float seekAngle = InitialAngle + AITimer * 0.03f;
            Vector2 seekDir = seekAngle.ToRotationVector2();
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, seekDir * 6f, 0.05f);

            if (targetNPC >= 0) {
                AIState = (float)State.Hunting;
                AITimer = 0;

                SoundEngine.PlaySound(SoundID.Item103 with {
                    Volume = 0.5f,
                    Pitch = 0.5f
                }, Projectile.Center);
            }

            //长时间无目标则消散
            if (AITimer > 180) {
                glowIntensity *= 0.95f;
                if (glowIntensity < 0.1f) {
                    Projectile.Kill();
                }
            }
        }

        private void HuntingAI() {
            //验证目标
            if (targetNPC < 0 || targetNPC >= Main.maxNPCs || !Main.npc[targetNPC].active || !Main.npc[targetNPC].CanBeChasedBy()) {
                targetNPC = FindTarget();
                if (targetNPC < 0) {
                    AIState = (float)State.Seeking;
                    AITimer = 0;
                    return;
                }
            }

            NPC target = Main.npc[targetNPC];
            Vector2 toTarget = target.Center - Projectile.Center;
            float distance = toTarget.Length();

            //蛇形追踪（带蜿蜒）
            float baseAngle = toTarget.ToRotation();
            float slither = MathF.Sin(slitherPhase * 2f) * 0.3f; //左右摆动
            float targetAngle = baseAngle + slither;

            //平滑转向
            float currentAngle = Projectile.velocity.ToRotation();
            float angleDiff = MathHelper.WrapAngle(targetAngle - currentAngle);
            float newAngle = currentAngle + angleDiff * homingStrength;

            //速度随距离变化
            float speed = MathHelper.Clamp(8f + (distance / 50f), 8f, 18f);
            Projectile.velocity = newAngle.ToRotationVector2() * speed;

            //接近时进入攻击状态
            if (distance < 80f) {
                AIState = (float)State.Striking;
                AITimer = 0;

                SoundEngine.PlaySound(SoundID.Item103 with {
                    Volume = 0.7f,
                    Pitch = 0.8f
                }, Projectile.Center);
            }
        }

        private void StrikingAI() {
            //攻击冲刺
            if (AITimer < 10) {
                //短暂蓄力
                Projectile.velocity *= 0.9f;
                glowIntensity = 1f + AITimer * 0.1f;
            }
            else if (AITimer < 25) {
                //冲刺
                if (targetNPC >= 0 && targetNPC < Main.maxNPCs && Main.npc[targetNPC].active) {
                    NPC target = Main.npc[targetNPC];
                    Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                    Projectile.velocity = toTarget * 25f;
                }
                glowIntensity = 1.5f;
            }
            else {
                //冲刺后恢复
                AIState = (float)State.Hunting;
                AITimer = 0;
                glowIntensity = 1f;
            }
        }

        #endregion

        #region 辅助方法

        private int FindTarget() {
            float maxDist = 500f;
            int closest = -1;
            float closestDist = maxDist;

            foreach (NPC npc in Main.npc) {
                if (!npc.CanBeChasedBy()) continue;

                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist < closestDist) {
                    closestDist = dist;
                    closest = npc.whoAmI;
                }
            }

            return closest;
        }

        private void UpdateBodySegments() {
            if (bodyPositions.Count == 0) return;

            //头部位置
            bodyPositions[0] = Projectile.Center;
            bodyRotations[0] = Projectile.velocity.ToRotation();

            //身体段跟随
            for (int i = 1; i < bodyPositions.Count; i++) {
                Vector2 prevPos = bodyPositions[i - 1];
                Vector2 toHead = prevPos - bodyPositions[i];
                float targetDist = 8f; //段间距

                //蛇形蜿蜒
                float slitherOffset = MathF.Sin(slitherPhase - i * 0.5f) * slitherAmplitude * (i / (float)BodySegments);
                Vector2 perpendicular = toHead.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);

                Vector2 targetPos = prevPos - toHead.SafeNormalize(Vector2.Zero) * targetDist + perpendicular * slitherOffset * 0.3f;
                bodyPositions[i] = Vector2.Lerp(bodyPositions[i], targetPos, 0.4f);
                bodyRotations[i] = (prevPos - bodyPositions[i]).ToRotation();
            }
        }

        private void SpawnHolyTrail() {
            if (VaultUtils.isServer) return;
            if (Main.rand.NextBool(3)) return;

            //神圣尾迹粒子
            int segmentIndex = Main.rand.Next(bodyPositions.Count);
            if (segmentIndex < bodyPositions.Count) {
                Vector2 pos = bodyPositions[segmentIndex];

                particles.Add(new SerpentParticle {
                    Position = pos + Main.rand.NextVector2Circular(5f, 5f),
                    Velocity = Main.rand.NextVector2Circular(1f, 1f) + new Vector2(0, -0.5f),
                    Life = 0,
                    MaxLife = Main.rand.NextFloat(20f, 35f),
                    Scale = Main.rand.NextFloat(0.1f, 0.2f) * (1f - segmentIndex / (float)BodySegments * 0.5f),
                    Color = Main.rand.NextBool() ? HolyGold : ScaleGreen
                });
            }

            //头部神圣光芒
            if (Main.rand.NextBool(5)) {
                BasePRT particle = new PRT_Light(
                    Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(1f, 1f),
                    0.1f * glowIntensity,
                    HolyGold,
                    Main.rand.Next(10, 20),
                    0.8f,
                    1f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }
        }

        private void UpdateParticles() {
            for (int i = particles.Count - 1; i >= 0; i--) {
                var p = particles[i];
                p.Life++;
                p.Position += p.Velocity;
                p.Velocity *= 0.95f;
                p.Scale *= 0.97f;

                if (p.Life >= p.MaxLife) {
                    particles.RemoveAt(i);
                }
            }
        }

        #endregion

        #region 碰撞与伤害

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //击中特效
            SpawnHitEffect(target.Center);

            //击中后短暂加速离开
            Vector2 awayDir = (Projectile.Center - target.Center).SafeNormalize(Main.rand.NextVector2Unit());
            Projectile.velocity = awayDir * 15f;

            //回到追猎状态
            AIState = (float)State.Hunting;
            AITimer = 0;
        }

        private void SpawnHitEffect(Vector2 pos) {
            if (VaultUtils.isServer) return;

            SoundEngine.PlaySound(SoundID.Item103 with {
                Volume = 0.6f,
                Pitch = 0.3f
            }, pos);

            //神圣爆发
            for (int i = 0; i < 15; i++) {
                float angle = MathHelper.TwoPi * i / 15f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 8f);

                BasePRT particle = new PRT_Light(
                    pos,
                    vel,
                    Main.rand.NextFloat(0.12f, 0.2f),
                    Main.rand.NextBool() ? HolyGold : PureWhite,
                    Main.rand.Next(15, 25),
                    1f,
                    1.2f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }

            //十字标记
            for (int arm = 0; arm < 4; arm++) {
                float crossAngle = MathHelper.PiOver2 * arm;
                for (int i = 1; i <= 4; i++) {
                    Vector2 crossPos = pos + crossAngle.ToRotationVector2() * (i * 6f);
                    Dust d = Dust.NewDustPerfect(crossPos, DustID.GoldFlame,
                        crossAngle.ToRotationVector2() * 1f, 100, HolyGold, 0.8f - i * 0.1f);
                    d.noGravity = true;
                }
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) return;

            //消散特效
            SoundEngine.PlaySound(SoundID.Item4 with {
                Volume = 0.5f,
                Pitch = 0.5f
            }, Projectile.Center);

            //沿身体生成消散粒子
            foreach (Vector2 pos in bodyPositions) {
                for (int i = 0; i < 3; i++) {
                    Dust d = Dust.NewDustPerfect(pos + Main.rand.NextVector2Circular(5f, 5f),
                        DustID.GoldFlame, Main.rand.NextVector2Circular(3f, 3f),
                        100, HolyGold, 0.8f);
                    d.noGravity = true;
                }
            }
        }

        #endregion

        #region 绘制

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;

            //绘制粒子
            DrawParticles(sb);

            //绘制身体
            DrawSerpentBody(sb);

            //绘制头部
            DrawSerpentHead(sb);

            return false;
        }

        private void DrawParticles(SpriteBatch sb) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return;

            foreach (var p in particles) {
                float alpha = 1f - (p.Life / p.MaxLife);
                Color drawColor = p.Color with { A = 0 } * alpha * 0.6f;
                Vector2 drawPos = p.Position - Main.screenPosition;
                sb.Draw(glow, drawPos, null, drawColor, 0, glow.Size() / 2, p.Scale, SpriteEffects.None, 0);
            }
        }

        private void DrawSerpentBody(SpriteBatch sb) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D pixel = CWRAsset.Placeholder_White?.Value;
            if (glow == null || pixel == null) return;

            //从尾到头绘制身体段
            for (int i = bodyPositions.Count - 1; i >= 1; i--) {
                Vector2 pos = bodyPositions[i] - Main.screenPosition;
                float rotation = bodyRotations[i];
                float segmentRatio = 1f - (i / (float)bodyPositions.Count);

                //段大小（头大尾小）
                float segmentScale = 0.3f + segmentRatio * 0.4f;
                segmentScale *= glowIntensity;

                //颜色渐变
                Color segmentColor = Color.Lerp(ScaleGreen, HolyGold, segmentRatio);

                //光晕
                Color glowColor = segmentColor with { A = 0 } * 0.5f * glowIntensity;
                sb.Draw(glow, pos, null, glowColor, rotation, glow.Size() / 2, segmentScale * 1.5f, SpriteEffects.None, 0);

                //核心
                Color coreColor = Color.Lerp(segmentColor, PureWhite, 0.3f) with { A = 0 } * glowIntensity;
                sb.Draw(glow, pos, null, coreColor, rotation, glow.Size() / 2, segmentScale * 0.8f, SpriteEffects.None, 0);

                //鳞片纹理（用小线段模拟）
                if (i % 2 == 0) {
                    float scaleAngle = rotation + MathHelper.PiOver2;
                    Color scaleColor = HolyGold with { A = 50 } * 0.4f * glowIntensity;
                    sb.Draw(pixel, pos, null, scaleColor, scaleAngle, new Vector2(0.5f), new Vector2(segmentScale * 20f, 1f), SpriteEffects.None, 0);
                }
            }

            //身体连接线
            for (int i = 0; i < bodyPositions.Count - 1; i++) {
                Vector2 start = bodyPositions[i] - Main.screenPosition;
                Vector2 end = bodyPositions[i + 1] - Main.screenPosition;
                Vector2 diff = end - start;
                float length = diff.Length();
                if (length < 1f) continue;

                float lineAlpha = (1f - i / (float)bodyPositions.Count) * 0.3f * glowIntensity;
                Color lineColor = HolyGold with { A = 0 } * lineAlpha;

                sb.Draw(pixel, start, new Rectangle(0, 0, 1, 1), lineColor, diff.ToRotation(), Vector2.Zero, new Vector2(length, 3f), SpriteEffects.None, 0f);
            }
        }

        private void DrawSerpentHead(SpriteBatch sb) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D pixel = CWRAsset.Placeholder_White?.Value;
            if (glow == null || pixel == null) return;

            Vector2 headPos = Projectile.Center - Main.screenPosition;
            float headRot = Projectile.velocity.ToRotation();

            //头部光晕
            Color headGlow = HolyGold with { A = 0 } * 0.7f * glowIntensity;
            sb.Draw(glow, headPos, null, headGlow, headRot, glow.Size() / 2, 0.8f * glowIntensity, SpriteEffects.None, 0);

            //头部核心
            Color headCore = PureWhite with { A = 0 } * 0.8f * glowIntensity;
            sb.Draw(glow, headPos, null, headCore, headRot, glow.Size() / 2, 0.4f * glowIntensity, SpriteEffects.None, 0);

            //眼睛（两个小点）
            Vector2 headDir = headRot.ToRotationVector2();
            Vector2 perpendicular = headDir.RotatedBy(MathHelper.PiOver2);
            Vector2 eyeOffset = headDir * 5f;

            for (int side = -1; side <= 1; side += 2) {
                Vector2 eyePos = headPos + eyeOffset + perpendicular * side * 4f;
                Color eyeColor = MysticPurple with { A = 0 } * glowIntensity;
                sb.Draw(glow, eyePos, null, eyeColor, 0, glow.Size() / 2, 0.15f * glowIntensity, SpriteEffects.None, 0);
            }

            //头部冠冕（十字形）
            float crownAlpha = 0.5f * glowIntensity;
            Color crownColor = HolyGold with { A = 50 } * crownAlpha;
            Vector2 crownPos = headPos + headDir * 8f;

            //水平线
            sb.Draw(pixel, crownPos, null, crownColor, headRot, new Vector2(0.5f), new Vector2(12f, 2f), SpriteEffects.None, 0);
            //垂直线
            sb.Draw(pixel, crownPos, null, crownColor, headRot + MathHelper.PiOver2, new Vector2(0.5f), new Vector2(8f, 2f), SpriteEffects.None, 0);
        }

        #endregion

        private class SerpentParticle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
            public float Scale;
            public Color Color;
        }
    }
}
