using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Banish
{
    /// <summary>
    /// Boss执行级故障天雷
    /// <br/>放逐对Boss级目标改为召唤大量该弹幕进行高伤打击
    /// <br/>视觉相比 <see cref="CyberGlitchBoltProj"/> 更庞大更帅气：更长更粗的主干、自动锁向Boss末端、附带分叉子雷
    /// <br/>ai[0] 入射角度  ai[1] 起始延迟  ai[2] 目标NPC索引
    /// <br/>localAI[0] 是否为分叉子雷（0=主干 1=分叉）
    /// </summary>
    internal class CyberExecutionBoltProj : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        private const int MaxLife = 38;
        private const int MainKeyCountMin = 14;
        private const int MainKeyCountMax = 20;
        private const float MainPeakWidth = 80f;

        private const int ForkKeyCountMin = 6;
        private const int ForkKeyCountMax = 10;
        private const float ForkPeakWidth = 36f;

        private Vector2[] points;
        private int pointCount;
        private bool pathReady;
        private bool forksSpawned;
        private float glitchSeed;
        private Trail trail;

        private float visibleStart;
        private float visibleEnd;
        private float fadeAlpha;
        //仅fork在生成时携带，由主干通过NewProjectile.ai[2]传入用于覆写目标终点（forkEndPoint X分量与Y分量打包到localAI[1]/[2]）
        private Vector2 forkEndOverride;
        private bool hasForkEnd;

        private bool IsFork => Projectile.localAI[0] > 0.5f;

        public override void SetDefaults() {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = MaxLife;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override void AI() {
            //ai[1]延迟阶段：保持隐藏并刷新存活时间
            if (Projectile.ai[1] > 0) {
                Projectile.ai[1]--;
                Projectile.timeLeft = MaxLife;
                return;
            }

            if (!pathReady) {
                //fork自带终点覆写：由生成方将终点坐标编码到localAI[1]/[2]
                if (IsFork && (Projectile.localAI[1] != 0f || Projectile.localAI[2] != 0f)) {
                    forkEndOverride = new Vector2(Projectile.localAI[1], Projectile.localAI[2]);
                    hasForkEnd = true;
                }
                GeneratePath();
                glitchSeed = Main.rand.NextFloat();
                pathReady = true;
                ResizeToBounds();
                if (!IsFork && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(CWRSound.Thunder with {
                        Volume = 0.55f,
                        Pitch = -0.2f + Main.rand.NextFloat(-0.15f, 0.15f),
                        PitchVariance = 0.12f,
                    }, Projectile.Center);
                }
            }

            float life = (float)Projectile.timeLeft / MaxLife;
            float t = 1f - life;
            ComputeAnimation(t);

            //主干在延伸到一半左右时分裂出fork，仅生成一次
            if (!IsFork && !forksSpawned && t > 0.22f && t < 0.5f) {
                SpawnForks();
                forksSpawned = true;
            }

            EmitLight();
            EmitEndpointSparks(t);
        }

        private void EmitLight() {
            if (!pathReady || points == null) return;
            int idx = (int)(MathHelper.Clamp((visibleStart + visibleEnd) * 0.5f, 0f, 1f) * (pointCount - 1));
            Vector2 lightPos = points[idx];
            float intensity = fadeAlpha * (IsFork ? 0.8f : 1.4f);
            Lighting.AddLight(lightPos, new Vector3(0.55f, 0.85f, 1f) * intensity);
        }

        /// <summary>
        /// 主干可见期间每帧在末端散出少量电流火花，持续强化电击穿透感
        /// </summary>
        private void EmitEndpointSparks(float t) {
            if (IsFork || Main.dedServ) return;
            if (!pathReady || points == null || pointCount < 2) return;
            if (fadeAlpha < 0.3f) return;
            //仅在延伸+全亮阶段（t < 0.65）发射，消退时不再补充
            if (t > 0.65f) return;

            //末端位置：当前visibleEnd对应的曲线点
            int endIdx = Math.Clamp((int)(visibleEnd * (pointCount - 1)), 0, pointCount - 1);
            Vector2 endPos = points[endIdx];

            //每帧随机1-2颗火花，沿末端切线方向散射
            int count = Main.rand.Next(1, 3);
            Vector2 tangent = endIdx > 0 ? (endPos - points[endIdx - 1]) : Vector2.UnitX;
            float baseAngle = tangent.LengthSquared() > 0.01f ? tangent.ToRotation() : 0f;

            for (int i = 0; i < count; i++) {
                float angle = baseAngle + Main.rand.NextFloat(-1.0f, 1.0f);
                float speed = Main.rand.NextFloat(3f, 9f);
                Vector2 vel = angle.ToRotationVector2() * speed;
                Color col = Color.Lerp(new Color(120, 230, 255), Color.White, Main.rand.NextFloat());
                PRTLoader.NewParticle<PRT_Spark>(endPos + Main.rand.NextVector2Circular(8f, 8f), vel, col, Main.rand.NextFloat(0.6f, 1.4f)).Configure(false, Main.rand.Next(10, 22));
            }
        }

        private void ComputeAnimation(float t) {
            if (t < 0.30f) {
                //快速延伸（缓出）
                float ext = t / 0.30f;
                visibleEnd = 1f - MathF.Pow(1f - ext, 3.4f);
                visibleStart = 0f;
                fadeAlpha = MathHelper.SmoothStep(0.4f, 1f, ext);
            }
            else if (t < 0.55f) {
                //全亮+连续闪烁，进入伤害高发段
                visibleEnd = 1f;
                visibleStart = 0f;
                float flash = MathF.Sin((t - 0.30f) / 0.25f * MathF.PI * 2f);
                fadeAlpha = 1.1f + flash * 0.35f;
            }
            else {
                //从尾部收缩消失
                float retract = (t - 0.55f) / 0.45f;
                visibleEnd = 1f;
                visibleStart = MathF.Pow(retract, 0.85f);
                fadeAlpha = 1f - retract;
            }
            fadeAlpha = MathHelper.Clamp(fadeAlpha, 0f, 1.5f);
        }

        private void GeneratePath() {
            //先确定起点与终点：用"主轴+垂直偏移"模型，避免随机漫步导致的折叠/扭曲与急拐断裂
            Vector2 start = Projectile.Center;
            Vector2 end;
            if (!ResolveEndPoint(out end)) {
                //Fallback：沿入射角延伸一个默认长度
                float defaultLen = IsFork ? 240f : 900f;
                end = start + Projectile.ai[0].ToRotationVector2() * defaultLen;
            }

            Vector2 axis = end - start;
            float length = axis.Length();
            if (length < 1f) {
                //长度过小时退化为短直线，至少保证两个端点，避免后续除零
                points = new Vector2[2] { start, end };
                pointCount = 2;
                return;
            }
            Vector2 axisUnit = axis / length;
            Vector2 perp = new Vector2(-axisUnit.Y, axisUnit.X);

            int keyCount = IsFork
                ? Main.rand.Next(ForkKeyCountMin, ForkKeyCountMax)
                : Main.rand.Next(MainKeyCountMin, MainKeyCountMax);
            //至少保留4个关键点供Catmull-Rom插值
            if (keyCount < 4) keyCount = 4;

            //横向抖动幅度：主干更大、fork更小；并随长度成比例缩放但有上限
            float baseAmp = IsFork
                ? MathF.Min(length * 0.18f, 90f)
                : MathF.Min(length * 0.16f, 220f);

            Vector2[] keys = new Vector2[keyCount];
            keys[0] = start;
            keys[keyCount - 1] = end;

            //中间关键点：沿主轴均匀分布 + 垂直方向带"包络抖动"，端点处包络为0以保证精确连接
            //包络采用 sin(t*PI)，中段最大、两端为0
            float prevOffset = 0f;
            for (int i = 1; i < keyCount - 1; i++) {
                float t = (float)i / (keyCount - 1);
                Vector2 onAxis = Vector2.Lerp(start, end, t);
                float envelope = MathF.Sin(t * MathF.PI);
                //生成带轻微"惯性"的偏移：本次目标 = 随机偏移；通过插值使曲线相对平滑而非锯齿
                float target = Main.rand.NextFloat(-1f, 1f) * baseAmp * envelope;
                float offset = MathHelper.Lerp(prevOffset, target, 0.65f);
                prevOffset = offset;
                //再叠加一层小幅高频噪声制造电流颤动观感
                float jitter = Main.rand.NextFloat(-1f, 1f) * baseAmp * 0.18f * envelope;
                keys[i] = onAxis + perp * (offset + jitter);
            }

            //Catmull-Rom 细分：每段插入若干子点，得到光滑无急拐的曲线
            int subPerSeg = IsFork ? 4 : 6;
            int segCount = keyCount - 1;
            pointCount = segCount * subPerSeg + 1;
            points = new Vector2[pointCount];
            int writeIdx = 0;
            for (int i = 0; i < segCount; i++) {
                Vector2 p0 = keys[Math.Max(i - 1, 0)];
                Vector2 p1 = keys[i];
                Vector2 p2 = keys[i + 1];
                Vector2 p3 = keys[Math.Min(i + 2, keyCount - 1)];
                for (int s = 0; s < subPerSeg; s++) {
                    float u = (float)s / subPerSeg;
                    points[writeIdx++] = CatmullRom(p0, p1, p2, p3, u);
                }
            }
            points[writeIdx] = keys[keyCount - 1];
        }

        /// <summary>
        /// 解析当前弹幕的终点：主干用ai[2]指向的目标NPC中心；fork用localAI[1]/[2]携带的覆写终点
        /// </summary>
        private bool ResolveEndPoint(out Vector2 end) {
            if (IsFork) {
                if (hasForkEnd) {
                    end = forkEndOverride;
                    return true;
                }
                end = default;
                return false;
            }
            int targetIdx = (int)Projectile.ai[2];
            if (targetIdx >= 0 && targetIdx < Main.maxNPCs) {
                NPC npc = Main.npc[targetIdx];
                if (npc.active) {
                    //命中点带轻微抖动避免所有雷打到同一像素
                    end = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.3f, npc.height * 0.3f);
                    return true;
                }
            }
            end = default;
            return false;
        }

        private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t) {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * ((2f * p1)
                + (-p0 + p2) * t
                + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private void ResizeToBounds() {
            if (points == null || pointCount == 0) return;
            Vector2 min = points[0];
            Vector2 max = points[0];
            for (int i = 1; i < pointCount; i++) {
                min = Vector2.Min(min, points[i]);
                max = Vector2.Max(max, points[i]);
            }
            //外扩一点容纳粗细
            float pad = (IsFork ? ForkPeakWidth : MainPeakWidth) * 0.6f;
            min -= new Vector2(pad);
            max += new Vector2(pad);

            Vector2 center = (min + max) * 0.5f;
            Vector2 size = max - min;
            int w = Math.Max(8, (int)size.X);
            int h = Math.Max(8, (int)size.Y);
            //保持几何中心不变
            Projectile.position = center - new Vector2(w * 0.5f, h * 0.5f);
            Projectile.width = w;
            Projectile.height = h;
        }

        private void SpawnForks() {
            //2-3条分叉，从主干靠中间的关键点向外抛出，长度约主干一半
            int forkCount = Main.rand.Next(2, 4);
            for (int i = 0; i < forkCount; i++) {
                int branchIdx = Main.rand.Next(pointCount / 4, pointCount * 3 / 4);
                Vector2 origin = points[branchIdx];
                //取该处主干切线，再在垂直方向上偏转较大角度形成分叉
                int aIdx = Math.Max(branchIdx - 1, 0);
                int bIdx = Math.Min(branchIdx + 1, pointCount - 1);
                Vector2 tangent = points[bIdx] - points[aIdx];
                float baseAngle = tangent.LengthSquared() < 1f ? Projectile.ai[0] : tangent.ToRotation();
                float forkAngle = baseAngle + Main.rand.NextFloat(-1.4f, 1.4f);
                //fork终点：沿forkAngle再外延一段，制造"四散电弧"的观感
                Vector2 forkEnd = origin + forkAngle.ToRotationVector2() * Main.rand.NextFloat(180f, 320f);

                //fork纯视觉，不造成伤害
                int idx = Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                    origin, Vector2.Zero,
                    Type, 0, 0f,
                    Projectile.owner,
                    ai0: forkAngle,
                    ai1: 0f,
                    ai2: -1f);
                if (idx >= 0 && idx < Main.maxProjectiles) {
                    Projectile fork = Main.projectile[idx];
                    fork.localAI[0] = 1f;
                    fork.localAI[1] = forkEnd.X;
                    fork.localAI[2] = forkEnd.Y;
                }
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!pathReady || points == null || Projectile.ai[1] > 0) return false;
            //仅在可见且亮度>0时才允许造成伤害
            if (fadeAlpha < 0.4f) return false;
            if (visibleEnd <= visibleStart + 0.001f) return false;

            int startIdx = Math.Clamp((int)MathF.Floor(visibleStart * (pointCount - 1)), 0, pointCount - 2);
            int endIdx = Math.Clamp((int)MathF.Ceiling(visibleEnd * (pointCount - 1)), 1, pointCount - 1);
            float radius = (IsFork ? ForkPeakWidth : MainPeakWidth) * 0.45f;
            Vector2 boxPos = targetHitbox.TopLeft();
            Vector2 boxSize = targetHitbox.Size();
            float collisionPoint = 0f;

            for (int i = startIdx; i < endIdx; i++) {
                if (Collision.CheckAABBvLineCollision(boxPos, boxSize, points[i], points[i + 1], radius, ref collisionPoint)) {
                    return true;
                }
            }
            return false;
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (!pathReady || points == null || fadeAlpha < 0.01f || Projectile.ai[1] > 0) {
                return;
            }

            Effect shader = EffectLoader.CyberGlitchBolt?.Value;
            if (shader == null) return;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (noise == null) return;

            trail ??= new Trail(points, WidthFunction, ColorFunction);
            trail.TrailPositions = points;

            shader.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            //取主人玩家的领域时间；远端客户端不能读 Local，否则雷击节奏与主人不一致
            CyberspacePlayer ownerCp = Cyberspace.For(Projectile.owner);
            float effectTime = ownerCp != null && ownerCp.Active
                ? ownerCp.EffectTime
                : (float)Main.timeForVisualEffects * 0.04f;
            shader.Parameters["uTime"]?.SetValue(effectTime);
            shader.Parameters["fadeAlpha"]?.SetValue(MathHelper.Clamp(fadeAlpha, 0f, 1f));
            shader.Parameters["visibleStart"]?.SetValue(visibleStart);
            shader.Parameters["visibleEnd"]?.SetValue(visibleEnd);
            shader.Parameters["glitchSeed"]?.SetValue(glitchSeed);
            shader.Parameters["uNoiseTex"]?.SetValue(noise);

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            device.BlendState = BlendState.Additive;
            //叠加两次绘制制造光晕：第一次正常宽度，第二次加宽降亮做外辉光
            trail.DrawTrail(shader);

            shader.Parameters["fadeAlpha"]?.SetValue(MathHelper.Clamp(fadeAlpha * 0.55f, 0f, 1f));
            trail.DrawTrail(shader);
            device.BlendState = BlendState.AlphaBlend;
        }

        private float WidthFunction(float progress) {
            float taper = MathF.Sin(progress * MathF.PI);
            taper = MathF.Max(taper, 0.08f);
            return (IsFork ? ForkPeakWidth : MainPeakWidth) * taper;
        }

        private Color ColorFunction(Vector2 _) => Color.White;

        public override bool ShouldUpdatePosition() => false;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) return;
            Vector2 impactPos = target.Center;

            //径向爆发电弧火花
            int sparkCount = Main.rand.Next(14, 22);
            for (int i = 0; i < sparkCount; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float speed = Main.rand.NextFloat(5f, 18f);
                Vector2 vel = angle.ToRotationVector2() * speed;
                Color col = Color.Lerp(new Color(100, 210, 255), Color.White, Main.rand.NextFloat(0.3f, 1f));
                PRTLoader.NewParticle<PRT_Spark>(impactPos + Main.rand.NextVector2Circular(10f, 10f), vel, col, Main.rand.NextFloat(0.8f, 2.0f)).Configure(true, Main.rand.Next(18, 38));
            }

            //故障碎块四散
            int glitchCount = Main.rand.Next(8, 14);
            for (int i = 0; i < glitchCount; i++) {
                float speed = Main.rand.NextFloat(3f, 10f);
                Vector2 vel = Main.rand.NextVector2CircularEdge(speed, speed);
                float scale = Main.rand.NextFloat(0.7f, 1.6f);
                PRTLoader.NewParticle<PRT_CyberSquare>(impactPos + Main.rand.NextVector2Circular(12f, 12f), vel, new Color(80, 200, 255), scale).Configure(Color.White, Main.rand.Next(20, 40));
            }

            //中心瞬间光爆
            PRTLoader.NewParticle<PRT_Light>(impactPos, Vector2.Zero,
                new Color(160, 230, 255), Main.rand.NextFloat(1.2f, 1.8f)).Configure(12);
        }
    }
}
