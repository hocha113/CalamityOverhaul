using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.Wraiths.VFX
{
    /// <summary>
    /// 替死触发的血臂。一次性本地演出，不保存、不参与判定；<br/>
    /// 判定端只提交两端快照，渲染端负责路径、粒子、收束和屏幕反馈。
    /// </summary>
    internal sealed class ScapeArmRenderer : RenderHandle
    {
        private const int LifeMax = 58;
        private const int RawPointCount = 11;
        private const float MaxArmWidth = 24f;
        private const float NoiseTilePx = 260f;

        //屏幕红晕：触发时写入，每帧在 EndEntityDraw 绘制后衰减
        private static float screenRedFlash;

        private static readonly List<ScapeArmEvent> active = [];
        private static readonly List<ScapeArmEvent> removeBuffer = [];

        private sealed class ScapeArmEvent
        {
            public Vector2[] Points;
            public float Seed;
            public int Age;
            //每帧缓存顶点（仅首帧或需要时重建）
            public VertexPositionColorTexture[] CachedVerts;
            public float TotalLength;
        }

        public override float Weight => 1.24f;

        /// <summary>由死亡钩子或网络演出包调用；不在服务端生成粒子。</summary>
        public static void Trigger(Vector2 from, Vector2 to) {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }

            Vector2 delta = to - from;
            if (delta.LengthSquared() < 36f) {
                return;
            }

            ScapeArmEvent arm = new() {
                Points = BuildPath(from, to),
                Seed = Main.rand.NextFloat(0f, 100f),
                Age = 0,
            };
            active.Add(arm);

            SpawnBloodAlongPath(arm.Points);
            SpawnPlayerSideBurst(from, (to - from).SafeNormalize(Vector2.UnitX));
            SpawnTargetSideBurst(to, (from - to).SafeNormalize(Vector2.UnitX));

            screenRedFlash = Math.Max(screenRedFlash, 0.32f);

            SoundEngine.PlaySound(SoundID.NPCDeath13 with { Pitch = -0.72f, Volume = 0.58f, MaxInstances = 3 }, from);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Pitch = -0.62f, Volume = 0.42f, MaxInstances = 3 }, to);
            if (Main.LocalPlayer?.active == true
                && Math.Min(Vector2.DistanceSquared(Main.LocalPlayer.Center, from)
                    , Vector2.DistanceSquared(Main.LocalPlayer.Center, to)) < 1400f * 1400f) {
                Main.LocalPlayer.CWR()?.GetScreenShake(6.5f);
            }
        }

        public override void UpdateBySystem(int index) {
            if (Main.gameMenu) {
                active.Clear();
                screenRedFlash = 0f;
                return;
            }

            removeBuffer.Clear();
            for (int i = 0; i < active.Count; i++) {
                ScapeArmEvent arm = active[i];
                arm.Age++;

                //持续拖尾粒子
                if (!Main.dedServ && arm.Points != null && arm.Age % 3 == 0) {
                    bool dissolving = arm.Age > LifeMax * 0.68f;
                    SpawnTrailParticle(arm.Points, dissolving);
                }

                if (arm.Age >= LifeMax) {
                    removeBuffer.Add(arm);
                }
            }
            foreach (ScapeArmEvent arm in removeBuffer) {
                active.Remove(arm);
            }

            //屏幕红晕衰减（约18帧归零）
            if (screenRedFlash > 0f) {
                screenRedFlash = Math.Max(0f, screenRedFlash - 0.32f / 18f);
            }
        }

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main
            , GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.dedServ) {
                return;
            }

            //屏幕红晕叠加（先于血臂绘制，确保在UI层之下）
            if (screenRedFlash > 0.004f) {
                DrawScreenFlash(spriteBatch);
            }

            if (active.Count == 0) {
                return;
            }

            Effect effect = EffectLoader.WraithScapeArm?.Value;
            Texture2D noise = CWRAsset.NoiseSoft01?.Value;
            if (effect == null || noise == null) {
                DrawFallback(spriteBatch);
                return;
            }

            BlendState previousBlend = graphicsDevice.BlendState;
            RasterizerState previousRasterizer = graphicsDevice.RasterizerState;
            DepthStencilState previousDepth = graphicsDevice.DepthStencilState;
            graphicsDevice.BlendState = BlendState.AlphaBlend;
            graphicsDevice.RasterizerState = RasterizerState.CullNone;
            graphicsDevice.DepthStencilState = DepthStencilState.None;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            effect.Parameters["uColBase"]?.SetValue(new Vector3(0.20f, 0.018f, 0.028f));
            effect.Parameters["uColVein"]?.SetValue(new Vector3(0.82f, 0.055f, 0.075f));
            effect.Parameters["uColHot"]?.SetValue(new Vector3(1.05f, 0.17f, 0.12f));

            for (int i = 0; i < active.Count; i++) {
                ScapeArmEvent arm = active[i];
                float progress = MathHelper.Clamp(arm.Age / (float)(LifeMax - 1), 0f, 1f);
                float reveal = MathHelper.Clamp(progress / 0.24f, 0f, 1f);
                reveal = reveal * reveal * (3f - 2f * reveal);
                float retract = 1f - reveal;
                float fade = progress <= 0.68f
                    ? 1f
                    : MathHelper.Clamp(1f - (progress - 0.68f) / 0.32f, 0f, 1f);
                fade *= 0.94f + 0.06f * MathF.Sin(progress * MathHelper.Pi);

                float pulse = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + arm.Seed);

                //首帧或缓存未建立时重建顶点
                if (arm.CachedVerts == null) {
                    arm.CachedVerts = BuildVertices(arm.Points, out float len);
                    arm.TotalLength = len;
                }

                effect.Parameters["uOpacity"]?.SetValue(fade);
                effect.Parameters["uRetract"]?.SetValue(retract);
                effect.Parameters["uSeed"]?.SetValue(arm.Seed);
                effect.Parameters["uTearAmp"]?.SetValue(0.85f + progress * 0.55f);
                effect.Parameters["uPulse"]?.SetValue(pulse);
                effect.Parameters["uPulseAmp"]?.SetValue(pulse);
                effect.Parameters["uLenScale"]?.SetValue(arm.TotalLength / NoiseTilePx);

                VertexPositionColorTexture[] verts = arm.CachedVerts;
                foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                    pass.Apply();
                    graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);
                }
            }

            graphicsDevice.BlendState = previousBlend;
            graphicsDevice.RasterizerState = previousRasterizer;
            graphicsDevice.DepthStencilState = previousDepth;
        }

        private static void DrawScreenFlash(SpriteBatch spriteBatch) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp
                , DepthStencilState.None, RasterizerState.CullNone);
            Color tint = new Color(160, 20, 30) * (screenRedFlash * 0.7f);
            spriteBatch.Draw(pixel, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight)
                , tint);
            spriteBatch.End();
        }

        private static VertexPositionColorTexture[] BuildVertices(IReadOnlyList<Vector2> points, out float totalLength) {
            int count = points.Count;
            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[count * 2];
            totalLength = 0f;
            for (int i = 1; i < count; i++) {
                totalLength += Vector2.Distance(points[i - 1], points[i]);
            }
            totalLength = Math.Max(totalLength, 1f);

            float distance = 0f;
            for (int i = 0; i < count; i++) {
                if (i > 0) {
                    distance += Vector2.Distance(points[i - 1], points[i]);
                }
                float u = distance / totalLength;
                Vector2 tangent = i == 0
                    ? points[1] - points[0]
                    : i == count - 1
                        ? points[i] - points[i - 1]
                        : points[i + 1] - points[i - 1];
                tangent = tangent.SafeNormalize(Vector2.UnitX);
                Vector2 normal = tangent.RotatedBy(MathHelper.PiOver2);

                //Sin^0.5 包络：中段更饱满，两端自然收束
                float envelope = MathF.Sin(u * MathHelper.Pi);
                envelope = MathF.Pow(MathHelper.Clamp(envelope, 0f, 1f), 0.5f);
                float width = MathHelper.Lerp(2.2f, MaxArmWidth, envelope);
                Vector2 center = points[i];
                verts[i * 2] = new VertexPositionColorTexture(
                    (center - normal * width).ToVector3(), Color.White, new Vector2(u, 0f));
                verts[i * 2 + 1] = new VertexPositionColorTexture(
                    (center + normal * width).ToVector3(), Color.White, new Vector2(u, 1f));
            }
            return verts;
        }

        private static void DrawFallback(SpriteBatch spriteBatch) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            for (int i = 0; i < active.Count; i++) {
                ScapeArmEvent arm = active[i];
                float progress = MathHelper.Clamp(arm.Age / (float)LifeMax, 0f, 1f);
                float visible = MathHelper.Clamp(progress / 0.24f, 0f, 1f);
                float fade = progress > 0.68f ? 1f - (progress - 0.68f) / 0.32f : 1f;
                int end = Math.Max(1, (int)((arm.Points.Length - 1) * visible));
                for (int j = 1; j <= end; j++) {
                    Vector2 a = arm.Points[j - 1] - Main.screenPosition;
                    Vector2 b = arm.Points[j] - Main.screenPosition;
                    Vector2 delta = b - a;
                    float len = delta.Length();
                    if (len < 1f) { continue; }
                    float u = j / (float)(arm.Points.Length - 1);
                    float width = MathHelper.Lerp(2f, MaxArmWidth, MathF.Sin(u * MathHelper.Pi));
                    spriteBatch.Draw(pixel, a, new Rectangle(0, 0, 1, 1), new Color(150, 18, 28) * fade
                        , delta.ToRotation(), Vector2.Zero, new Vector2(len, width * 2f), SpriteEffects.None, 0f);
                }
            }
            spriteBatch.End();
        }

        // ===== 路径构建 =====

        /// <summary>
        /// 三步流程对齐 OniKamuiFlowRenderer.ShapePath：<br/>
        /// 1. 剔短段（≥10px）→ 2. Chaikin 两轮（0.25/0.75）→ 3. 细分补密（≤44px/段）
        /// </summary>
        private static Vector2[] BuildPath(Vector2 from, Vector2 to) {
            //生成原始控制点（11个，中段弯曲幅度增大）
            Vector2 delta = to - from;
            float length = delta.Length();
            Vector2 direction = delta.SafeNormalize(Vector2.UnitX);
            Vector2 normal = direction.RotatedBy(MathHelper.PiOver2);
            float bend = MathHelper.Clamp(length * 0.085f, 20f, 130f);

            Vector2[] raw = new Vector2[RawPointCount];
            for (int i = 0; i < RawPointCount; i++) {
                float t = i / (float)(RawPointCount - 1);
                float envelope = MathF.Sin(t * MathHelper.Pi);
                float wave = MathF.Sin(t * MathHelper.TwoPi * 1.35f + Main.rand.NextFloat(-0.35f, 0.35f))
                    * bend * envelope;
                float jitter = Main.rand.NextFloat(-bend * 0.18f, bend * 0.18f) * envelope;
                raw[i] = Vector2.Lerp(from, to, t) + normal * (wave + jitter);
            }
            raw[0] = from;
            raw[^1] = to;

            return ShapePath(raw);
        }

        private const float MinSeg = 10f;
        private const float MaxSeg = 44f;

        private static Vector2[] ShapePath(IReadOnlyList<Vector2> raw) {
            //Step1: 剔短段（末点替换前点而非丢弃）
            List<Vector2> culled = new(raw.Count) { raw[0] };
            for (int i = 1; i < raw.Count; i++) {
                if (Vector2.DistanceSquared(culled[^1], raw[i]) >= MinSeg * MinSeg) {
                    culled.Add(raw[i]);
                }
                else if (i == raw.Count - 1) {
                    if (culled.Count > 1) { culled[^1] = raw[i]; }
                    else { culled.Add(raw[i]); }
                }
            }

            //Step2: Chaikin 两轮（0.25/0.75）
            List<Vector2> smooth = culled;
            for (int round = 0; round < 2; round++) {
                List<Vector2> next = new(smooth.Count * 2) { smooth[0] };
                for (int i = 0; i < smooth.Count - 1; i++) {
                    next.Add(Vector2.Lerp(smooth[i], smooth[i + 1], 0.25f));
                    next.Add(Vector2.Lerp(smooth[i], smooth[i + 1], 0.75f));
                }
                next.Add(smooth[^1]);
                smooth = next;
            }

            //Step3: 细分补密（每段≤44px）
            List<Vector2> dense = new(smooth.Count * 2) { smooth[0] };
            for (int i = 1; i < smooth.Count; i++) {
                Vector2 a = smooth[i - 1], b = smooth[i];
                float len = Vector2.Distance(a, b);
                int cuts = (int)(len / MaxSeg);
                for (int k = 1; k <= cuts; k++) {
                    dense.Add(Vector2.Lerp(a, b, k / (float)(cuts + 1)));
                }
                dense.Add(b);
            }

            return dense.ToArray();
        }

        // ===== 粒子 =====

        private static void SpawnBloodAlongPath(IReadOnlyList<Vector2> points) {
            if (Main.dedServ || points.Count < 2) { return; }

            Vector2 start = points[0];
            Vector2 end = points[^1];
            Vector2 direction = (end - start).SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 16; i++) {
                float t = Main.rand.NextFloat(0.08f, 0.98f);
                Vector2 pos = PointAlong(points, t) + Main.rand.NextVector2Circular(8f, 8f);
                Vector2 velocity = direction.RotatedByRandom(0.95f) * Main.rand.NextFloat(1.8f, 6.8f);
                velocity.Y -= Main.rand.NextFloat(0f, 1.6f);
                Color color = Main.rand.NextBool(4)
                    ? CrimsonRendHitVFX.Arterial
                    : (Main.rand.NextBool() ? CrimsonRendHitVFX.Blood : CrimsonRendHitVFX.BloodDeep);
                if (Main.rand.NextBool(3)) {
                    PRTLoader.NewParticle<PRT_CrimsonBloodStain>(pos, velocity, color
                        , Main.rand.NextFloat(0.55f, 1.15f))
                        ?.Configure(Main.rand.Next(24, 42), 0.28f, 0.988f, Main.rand.Next(30, 52));
                }
                else {
                    PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, velocity, color
                        , Main.rand.NextFloat(0.65f, 1.2f))
                        ?.Configure(Main.rand.Next(18, 32), 0.26f, 0.988f);
                }
            }
        }

        private static void SpawnPlayerSideBurst(Vector2 pos, Vector2 direction) {
            //玩家端：血臂从身上抽出，向外大角度喷溅
            for (int i = 0; i < 6; i++) {
                Vector2 velocity = direction.RotatedByRandom(1.2f) * Main.rand.NextFloat(3.5f, 10f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(
                    pos + Main.rand.NextVector2Circular(6f, 6f), velocity
                    , CrimsonRendHitVFX.Blood, Main.rand.NextFloat(0.8f, 1.3f))
                    ?.Configure(Main.rand.Next(14, 22), 0.35f);
            }
        }

        private static void SpawnTargetSideBurst(Vector2 pos, Vector2 direction) {
            //目标端：较大血染 + 径向水滴环
            for (int i = 0; i < 7; i++) {
                Vector2 velocity = direction.RotatedByRandom(0.8f) * Main.rand.NextFloat(2f, 7f);
                Color color = Main.rand.NextBool() ? CrimsonRendHitVFX.Blood : CrimsonRendHitVFX.BloodDeep;
                PRTLoader.NewParticle<PRT_CrimsonBloodStain>(
                    pos + Main.rand.NextVector2Circular(7f, 7f), velocity, color
                    , Main.rand.NextFloat(0.9f, 1.6f))
                    ?.Configure(Main.rand.Next(28, 46), 0.32f, 0.988f, Main.rand.Next(35, 60));
            }
            //径向短爆
            for (int i = 0; i < 6; i++) {
                float angle = i / 6f * MathHelper.TwoPi;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(2.5f, 5.5f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(
                    pos + Main.rand.NextVector2Circular(5f, 5f), velocity
                    , CrimsonRendHitVFX.Arterial, Main.rand.NextFloat(0.7f, 1.1f))
                    ?.Configure(Main.rand.Next(16, 26), 0.3f);
            }
        }

        private static void SpawnTrailParticle(IReadOnlyList<Vector2> points, bool dissolving) {
            if (Main.dedServ || points.Count < 2) { return; }
            float t = Main.rand.NextFloat(0.1f, 0.9f);
            Vector2 pos = PointAlong(points, t) + Main.rand.NextVector2Circular(5f, 5f);
            Vector2 vel = new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(-0.8f, -0.1f));
            if (!dissolving) {
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, vel
                    , CrimsonRendHitVFX.Blood, Main.rand.NextFloat(0.4f, 0.7f))
                    ?.Configure(Main.rand.Next(10, 18), 0.22f);
            }
            else {
                PRTLoader.NewParticle<PRT_CrimsonBloodStain>(pos, vel * 0.5f
                    , CrimsonRendHitVFX.BloodDeep, Main.rand.NextFloat(0.3f, 0.6f))
                    ?.Configure(Main.rand.Next(14, 22), 0.18f, 0.992f);
            }
        }

        private static Vector2 PointAlong(IReadOnlyList<Vector2> points, float t) {
            if (points.Count == 0) { return Vector2.Zero; }
            if (points.Count == 1 || t <= 0f) { return points[0]; }
            if (t >= 1f) { return points[^1]; }

            float total = 0f;
            for (int i = 1; i < points.Count; i++) { total += Vector2.Distance(points[i - 1], points[i]); }
            float goal = total * t, current = 0f;
            for (int i = 1; i < points.Count; i++) {
                float seg = Vector2.Distance(points[i - 1], points[i]);
                if (current + seg >= goal && seg > 0f) {
                    return Vector2.Lerp(points[i - 1], points[i], (goal - current) / seg);
                }
                current += seg;
            }
            return points[^1];
        }
    }
}