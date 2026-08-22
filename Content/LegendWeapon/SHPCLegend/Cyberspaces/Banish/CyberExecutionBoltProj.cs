using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Banish
{
    /// <summary>Boss 执行天雷，目标身份经 ExtraAI 同步</summary>
    internal class CyberExecutionBoltProj : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

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
        private NetworkNPCIdentity targetIdentity;
        private int pathSeed;
        //fork 终点 localAI[1]/[2]
        private Vector2 forkEndOverride;
        private bool hasForkEnd;

        private bool IsFork => Projectile.localAI[0] > 0.5f;

        internal void InitializeTarget(NetworkNPCIdentity identity, int seed) {
            if (identity.IsValid && seed > 0) {
                targetIdentity = identity;
                pathSeed = seed;
            }
        }

        public override void SendExtraAI(BinaryWriter writer) {
            targetIdentity.Write(writer);
            writer.Write(pathSeed);
            //SyncProjectile 的 damage 字段是 short：处决伤害上限一千万，
            //经生成包会截断成垃圾值，而命中在 owner 客户端结算，ExtraAI 带全量还原
            writer.Write(Projectile.damage);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            NetworkNPCIdentity.TryRead(reader, out targetIdentity);
            int receivedSeed = reader.ReadInt32();
            pathSeed = receivedSeed > 0 ? receivedSeed : 1;
            //ReceiveExtraAI 在 case 27 写完截断 damage 之后执行，覆写生效
            int fullDamage = reader.ReadInt32();
            if (fullDamage > 0) {
                Projectile.damage = fullDamage;
            }
        }

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
            //ai1 延迟，隐藏保活
            if (Projectile.ai[1] > 0) {
                Projectile.ai[1]--;
                Projectile.timeLeft = MaxLife;
                return;
            }

            if (!pathReady) {
                //fork 终点写 localAI[1]/[2]
                if (IsFork && (Projectile.localAI[1] != 0f || Projectile.localAI[2] != 0f)) {
                    forkEndOverride = new Vector2(Projectile.localAI[1], Projectile.localAI[2]);
                    hasForkEnd = true;
                }
                if (pathSeed <= 0) {
                    pathSeed = Main.rand.Next(1, int.MaxValue);
                }
                GeneratePath();
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
            if (!IsFork && !forksSpawned && t > 0.22f && t < 0.5f
                && Main.netMode != Terraria.ID.NetmodeID.Server) {
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

        /// <summary>末端电流火花</summary>
        private void EmitEndpointSparks(float t) {
            if (IsFork || Main.dedServ) return;
            if (!pathReady || points == null || pointCount < 2) return;
            if (fadeAlpha < 0.3f) return;
            //仅在延伸+全亮阶段（t < 0.65）发射，消退时不再补充
            if (t > 0.65f) return;

            //末端=visibleEnd 曲线点
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
            //主轴+垂偏，避漫步折叠
            UnifiedRandom random = new(pathSeed);
            glitchSeed = random.NextFloat();
            Vector2 start = Projectile.Center;
            Vector2 end;
            if (!ResolveEndPoint(random, out end)) {
                //Fallback 沿入射延伸
                float defaultLen = IsFork ? 240f : 900f;
                end = start + Projectile.ai[0].ToRotationVector2() * defaultLen;
            }

            Vector2 axis = end - start;
            float length = axis.Length();
            if (length < 1f) {
                //过短退化为两端直线
                points = new Vector2[2] { start, end };
                pointCount = 2;
                return;
            }
            Vector2 axisUnit = axis / length;
            Vector2 perp = new Vector2(-axisUnit.Y, axisUnit.X);

            int keyCount = IsFork
                ? random.Next(ForkKeyCountMin, ForkKeyCountMax)
                : random.Next(MainKeyCountMin, MainKeyCountMax);
            //至少保留4个关键点供Catmull-Rom插值
            if (keyCount < 4) keyCount = 4;

            //横抖，主干大 fork 小
            float baseAmp = IsFork
                ? MathF.Min(length * 0.18f, 90f)
                : MathF.Min(length * 0.16f, 220f);

            Vector2[] keys = new Vector2[keyCount];
            keys[0] = start;
            keys[keyCount - 1] = end;

            //关键点主轴均分+包络抖，端点0
            //包络采用 sin(t*PI)，中段最大、两端为0
            float prevOffset = 0f;
            for (int i = 1; i < keyCount - 1; i++) {
                float t = (float)i / (keyCount - 1);
                Vector2 onAxis = Vector2.Lerp(start, end, t);
                float envelope = MathF.Sin(t * MathF.PI);
                //惯性格偏移插值
                float target = random.NextFloat(-1f, 1f) * baseAmp * envelope;
                float offset = MathHelper.Lerp(prevOffset, target, 0.65f);
                prevOffset = offset;
                //再叠加一层小幅高频噪声制造电流颤动观感
                float jitter = random.NextFloat(-1f, 1f) * baseAmp * 0.18f * envelope;
                keys[i] = onAxis + perp * (offset + jitter);
            }

            //Catmull-Rom 细分
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

        /// <summary>主干严格身份终点，分叉使用 localAI</summary>
        private bool ResolveEndPoint(UnifiedRandom random, out Vector2 end) {
            if (IsFork) {
                if (hasForkEnd) {
                    end = forkEndOverride;
                    return true;
                }
                end = default;
                return false;
            }
            if (targetIdentity.TryResolve(out NPC npc)) {
                //命中点微抖
                end = npc.Center + NextVector2Circular(random,
                    npc.width * 0.3f, npc.height * 0.3f);
                return true;
            }
            end = default;
            return false;
        }

        private static Vector2 NextVector2Circular(UnifiedRandom random,
            float radiusX, float radiusY) {
            float angle = random.NextFloat(MathHelper.TwoPi);
            float radius = MathF.Sqrt(random.NextFloat());
            return new Vector2(MathF.Cos(angle) * radiusX,
                MathF.Sin(angle) * radiusY) * radius;
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
                //fork 沿角外延
                Vector2 forkEnd = origin + forkAngle.ToRotationVector2() * Main.rand.NextFloat(180f, 320f);

                //fork纯视觉，不造成伤害
                int idx = Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                    origin, Vector2.Zero,
                    Type, 0, 0f,
                    Main.maxPlayers,
                    ai0: forkAngle,
                    ai1: 0f,
                    ai2: -1f);
                if (idx >= 0 && idx < Main.maxProjectiles) {
                    Projectile fork = Main.projectile[idx];
                    fork.localAI[0] = 1f;
                    fork.localAI[1] = forkEnd.X;
                    fork.localAI[2] = forkEnd.Y;
                    fork.damage = 0;
                    fork.friendly = false;
                    fork.hostile = false;
                    fork.netUpdate = false;
                }
            }
        }

        public override bool? CanDamage() => IsFork ? false : null;

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
            //uTime 取主人领域时间
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
            //双绘光晕
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
