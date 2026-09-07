using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFlashSteps;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.EyeOfCthulhu
{
    /// <summary>
    /// 血雾之瞳演出库：只用液态血材质（血珠/血带/血盾），不再有 Fog 雾团。<br/>
    /// 与克眼本体的 EocMotion 分家：Boss 侧仍带雾，饰品侧一颗雾粒都不生成
    /// </summary>
    internal static class BloodfogIrisFX
    {
        /// <summary>着色器像素量化格 px，2=泰拉像素；0 关</summary>
        internal const float PixelCell = 2f;

        /// <summary>起步血爆：正交冲击环 + 逆向血珠 + 湿吼 + 方向震屏</summary>
        public static void LaunchBurst(Vector2 pos, Vector2 dir, float strength = 1f) {
            if (!VaultUtils.isServer) {
                PRTLoader.NewParticle<PRT_DWave>(pos, dir * 1.4f, EocMotion.Arterial, 0.24f * strength)?
                    .Configure(new Vector2(1.5f, 0.5f), dir.ToRotation() + MathHelper.PiOver2, 1.05f * strength, 15);
                for (int i = 0; i < 12; i++) {
                    Vector2 vel = -dir.RotatedBy(Main.rand.NextFloat(-0.85f, 0.85f)) * Main.rand.NextFloat(3f, 12f) * strength;
                    PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos + Main.rand.NextVector2Circular(24f, 24f), vel,
                        Color.Lerp(EocMotion.Arterial, EocMotion.BrightBlood, Main.rand.NextFloat()), Main.rand.NextFloat(1f, 1.9f))?
                        .Configure(Main.rand.Next(22, 38), 0.3f, 0.985f);
                }
                SoundEngine.PlaySound(SoundID.NPCDeath13 with { Volume = 0.7f * strength, Pitch = -0.35f }, pos);
                SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.55f * strength, Pitch = -0.1f }, pos);
            }
            EocMotion.Shake(pos, 4.2f * strength, 9, dir);
        }

        /// <summary>大血爆：环 + 放射血珠 + 光 + 湿爆音，无雾</summary>
        public static void BloodBurst(Vector2 pos, float strength = 1f, bool playSound = true) {
            if (VaultUtils.isServer) {
                return;
            }
            PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, EocMotion.Arterial, 0.22f * strength)?
                .Configure(Vector2.One, Main.rand.NextFloat(MathHelper.TwoPi), 1.35f * strength, 18);
            int drops = (int)(22 * strength);
            for (int i = 0; i < drops; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 13f) * strength;
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, vel,
                    Color.Lerp(EocMotion.Arterial, EocMotion.BrightBlood, Main.rand.NextFloat()), Main.rand.NextFloat(1f, 2.1f))?
                    .Configure(Main.rand.Next(24, 44), 0.34f, 0.985f);
            }
            Lighting.AddLight(pos, EocMotion.Arterial.ToVector3() * 1.2f * strength);
            if (playSound) {
                SoundEngine.PlaySound(SoundID.NPCDeath12 with { Volume = 0.85f * MathHelper.Clamp(strength, 0.4f, 1.2f), Pitch = -0.25f }, pos);
            }
        }

        /// <summary>盾碎：沿弧甩血珠，顺冲刺方向为主、翼尖多；湿裂响 + 短震</summary>
        public static void ShieldShatter(Vector2 center, Vector2 dir, float radius, float span, float strength = 1f) {
            EocMotion.Shake(center, 3f * strength, 7, dir);
            if (VaultUtils.isServer) {
                return;
            }
            float baseAng = dir.ToRotation();
            int count = (int)(18 * strength);
            for (int i = 0; i < count; i++) {
                float t = Main.rand.NextFloat();
                float phi = (t - 0.5f) * span;
                Vector2 n = (baseAng + phi).ToRotationVector2();
                Vector2 pos = center + n * (radius + Main.rand.NextFloat(-2f, 8f));
                //弧顶碎片顺冲刺飞、翼尖碎片沿法线甩开
                Vector2 vel = Vector2.Lerp(n, dir, 0.55f).SafeNormalize(dir) * Main.rand.NextFloat(4f, 11f) * strength;
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, vel,
                    Color.Lerp(EocMotion.Arterial, EocMotion.BrightBlood, Main.rand.NextFloat()), Main.rand.NextFloat(0.9f, 1.7f))?
                    .Configure(Main.rand.Next(20, 34), 0.32f, 0.984f);
            }
            SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.8f * strength, Pitch = -0.2f }, center);
            SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.4f * strength, Pitch = 0.25f }, center);
        }

        /// <summary>翼尖甩血：持盾途中每帧从两翼尖向后甩 1 颗</summary>
        public static void TipShed(Vector2 center, Vector2 dir, float radius, float span) {
            if (VaultUtils.isServer) {
                return;
            }
            float baseAng = dir.ToRotation();
            float side = Main.rand.NextBool() ? 1f : -1f;
            Vector2 n = (baseAng + side * span * 0.5f).ToRotationVector2();
            Vector2 pos = center + n * radius;
            Vector2 vel = (-dir).RotatedBy(Main.rand.NextFloat(-0.35f, 0.35f)) * Main.rand.NextFloat(2f, 5f) + n * 1.5f;
            PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, vel,
                Color.Lerp(EocMotion.Arterial, EocMotion.BrightBlood, Main.rand.NextFloat()), Main.rand.NextFloat(0.8f, 1.3f))?
                .Configure(Main.rand.Next(16, 26), 0.3f, 0.98f);
        }

        /// <summary>雾态血滴：身上随机一点滴落一颗，读作"刚由血凝形、还在往下淌"</summary>
        public static void Drip(Player player) {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 pos = player.Center + new Vector2(Main.rand.NextFloat(-8f, 8f), Main.rand.NextFloat(-16f, 14f));
            Vector2 vel = new Vector2(player.velocity.X * 0.3f, Main.rand.NextFloat(0.5f, 1.5f));
            PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, vel,
                Color.Lerp(EocMotion.VenousDark, EocMotion.Arterial, Main.rand.NextFloat()), Main.rand.NextFloat(0.7f, 1.1f))?
                .Configure(Main.rand.Next(18, 30), 0.3f, 0.99f);
        }

        /// <summary>血珠向心汇聚：外圈生成、按 frames 帧命中中心</summary>
        public static void Converge(Vector2 center, float radius, int count, int frames = 16) {
            if (VaultUtils.isServer) {
                return;
            }
            frames = Math.Max(frames, 2);
            for (int i = 0; i < count; i++) {
                Vector2 spawnPos = center + Main.rand.NextVector2CircularEdge(radius, radius) * Main.rand.NextFloat(0.7f, 1f);
                Vector2 vel = (center - spawnPos) / frames;
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(spawnPos, vel,
                    Color.Lerp(EocMotion.Arterial, EocMotion.BrightBlood, Main.rand.NextFloat()), Main.rand.NextFloat(0.9f, 1.5f))?
                    .Configure(frames, 0f, 1f);
            }
        }
    }

    /// <summary>
    /// 血带顶点渲染器（BRelicIrisTrail.fx）：多股条带、路径整形复用神威流带的 ShapePath，
    /// 点龄写进顶点色 B 由着色器龄蚀成珠链。Begin/End 之间可画多股
    /// </summary>
    internal static class BloodRibbonRenderer
    {
        /// <summary>子带静态定义</summary>
        public struct StrandDef
        {
            public float HalfWidth;   //满热半幅 px
            public float PerpOffset;  //平行偏移 px（头端汇零）
            public float Seed;        //噪声相位
            public float FlowMul;     //淌流倍率
            public float TearPx;      //撕边幅度 px
            public float OpacityMul;  //相对整体透明度
            public float TailKeep;    //尾端保留的偏移比例，0=汇于一点
        }

        /// <summary>顶点色 R 的半幅折算基准 px，与 .fx 内 *64 对应</summary>
        private const float HalfWidthScale = 64f;

        private static VertexPositionColorTexture[] vertBuf = new VertexPositionColorTexture[256];
        private static readonly List<float> rawCum = new(64);

        public static bool Begin(GraphicsDevice gd, out Effect fx
            , out BlendState prevBlend, out RasterizerState prevRaster, out DepthStencilState prevDepth) {
            fx = EffectLoader.BRelicIrisTrail?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            prevBlend = gd.BlendState;
            prevRaster = gd.RasterizerState;
            prevDepth = gd.DepthStencilState;
            if (fx == null || noise == null) {
                return false;
            }
            gd.BlendState = BlendState.AlphaBlend;
            gd.RasterizerState = RasterizerState.CullNone;
            gd.DepthStencilState = DepthStencilState.None;
            //噪声显式绑 s1（shader 内 register(s1)）
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uPixel"]?.SetValue(BloodfogIrisFX.PixelCell);
            return true;
        }

        public static void End(GraphicsDevice gd, BlendState prevBlend, RasterizerState prevRaster, DepthStencilState prevDepth) {
            gd.Textures[1] = null;
            gd.BlendState = prevBlend;
            gd.RasterizerState = prevRaster;
            gd.DepthStencilState = prevDepth;
        }

        /// <summary>画一股。rawPoints 首元素=尾(最老)、末元素=头；rawAges 同长 0 新→1 亡；headReveal&lt;1 时头端收尖</summary>
        public static void DrawStrand(GraphicsDevice gd, Effect fx
            , IReadOnlyList<Vector2> rawPoints, IReadOnlyList<float> rawAges, in StrandDef def
            , float heat, float opacity, float headReveal = 1f) {
            if (rawPoints.Count < 2 || rawAges.Count != rawPoints.Count) {
                return;
            }
            float a = opacity * def.OpacityMul;
            if (a <= 0.01f) {
                return;
            }

            //原始弧长表，供整形后按弧长比例回查点龄
            rawCum.Clear();
            rawCum.Add(0f);
            float rawTotal = 0f;
            for (int i = 1; i < rawPoints.Count; i++) {
                rawTotal += Vector2.Distance(rawPoints[i - 1], rawPoints[i]);
                rawCum.Add(rawTotal);
            }
            if (rawTotal < 8f) {
                return;
            }

            List<Vector2> pts = OniKamuiFlowRenderer.ShapePath(rawPoints);
            int count = pts.Count;
            if (count < 2) {
                return;
            }
            float total = 0f;
            for (int i = 1; i < count; i++) {
                total += Vector2.Distance(pts[i - 1], pts[i]);
            }
            if (total < 12f) {
                return;
            }

            fx.Parameters["uOpacity"]?.SetValue(MathHelper.Clamp(a, 0f, 1f));
            fx.Parameters["uHeadReveal"]?.SetValue(MathHelper.Clamp(headReveal, 0f, 1f));
            fx.Parameters["uLenPx"]?.SetValue(total);
            fx.Parameters["uSeed"]?.SetValue(def.Seed);
            fx.Parameters["uFlowMul"]?.SetValue(def.FlowMul);
            fx.Parameters["uTearPx"]?.SetValue(def.TearPx);
            fx.Parameters["uHeat"]?.SetValue(MathHelper.Clamp(heat, 0f, 1f));

            if (vertBuf.Length < count * 2) {
                vertBuf = new VertexPositionColorTexture[count * 2];
            }

            float cum = 0f;
            for (int i = 0; i < count; i++) {
                if (i > 0) {
                    cum += Vector2.Distance(pts[i - 1], pts[i]);
                }
                float u = cum / total;
                float age = SampleAge(rawAges, u * rawTotal);

                Vector2 dir = i == 0
                    ? pts[1] - pts[0]
                    : i == count - 1
                        ? pts[i] - pts[i - 1]
                        : pts[i + 1] - pts[i - 1];
                dir = dir.SafeNormalize(Vector2.UnitX);
                Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);

                //幅宽：尾端从三成长起，头端满幅（头端藏在本体下或由 shader 收尖）
                float tailGrow = MathHelper.Clamp(u / 0.22f, 0f, 1f);
                tailGrow *= 2f - tailGrow;
                float hw = def.HalfWidth * MathHelper.Lerp(0.35f, 1f, tailGrow) * MathHelper.Lerp(0.55f, 1f, heat);
                hw = Math.Max(hw, 1.5f);

                //偏移双向漏斗：头端汇零、尾端按 TailKeep 收
                float headFunnel = MathHelper.Clamp((1f - u) / 0.30f, 0f, 1f);
                headFunnel *= 2f - headFunnel;
                float tailFunnel = MathHelper.Clamp(u / 0.25f, 0f, 1f);
                tailFunnel = MathHelper.Lerp(def.TailKeep, 1f, tailFunnel * (2f - tailFunnel));
                Vector2 center = pts[i] + perp * (def.PerpOffset * headFunnel * tailFunnel);

                //R=半幅 G=uv.y=1 侧朝上度 B=点龄
                Color c = new(hw / HalfWidthScale, (-perp.Y + 1f) * 0.5f, age, 1f);
                vertBuf[i * 2] = new VertexPositionColorTexture((center - perp * hw).ToVector3(), c, new Vector2(u, 0f));
                vertBuf[i * 2 + 1] = new VertexPositionColorTexture((center + perp * hw).ToVector3(), c, new Vector2(u, 1f));
            }

            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, vertBuf, 0, count * 2 - 2);
            }
        }

        /// <summary>按原始弧长位置线性回查点龄</summary>
        private static float SampleAge(IReadOnlyList<float> ages, float dist) {
            int n = rawCum.Count;
            if (dist <= 0f) {
                return ages[0];
            }
            for (int i = 1; i < n; i++) {
                if (rawCum[i] >= dist) {
                    float seg = rawCum[i] - rawCum[i - 1];
                    float f = seg > 0f ? (dist - rawCum[i - 1]) / seg : 1f;
                    return MathHelper.Lerp(ages[i - 1], ages[i], f);
                }
            }
            return ages[n - 1];
        }
    }

    /// <summary>
    /// 血盾网格（BRelicIrisShield.fx）：绕中心的弧形三角带，弓形（弧顶厚翼尖薄、翼尖后掠）
    /// 或环形（均厚，重凝血茧）。uv.x 沿弧、uv.y 内→外；顶点色 R=厚度比 G=外法线朝上度
    /// </summary>
    internal static class BloodShieldMesh
    {
        private const int Segs = 40;
        private static readonly VertexPositionColorTexture[] verts = new VertexPositionColorTexture[(Segs + 1) * 2];

        public static bool Begin(GraphicsDevice gd, out Effect fx
            , out BlendState prevBlend, out RasterizerState prevRaster, out DepthStencilState prevDepth) {
            fx = EffectLoader.BRelicIrisShield?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            prevBlend = gd.BlendState;
            prevRaster = gd.RasterizerState;
            prevDepth = gd.DepthStencilState;
            if (fx == null || noise == null) {
                return false;
            }
            gd.BlendState = BlendState.AlphaBlend;
            gd.RasterizerState = RasterizerState.CullNone;
            gd.DepthStencilState = DepthStencilState.None;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uPixel"]?.SetValue(BloodfogIrisFX.PixelCell);
            return true;
        }

        public static void End(GraphicsDevice gd, BlendState prevBlend, RasterizerState prevRaster, DepthStencilState prevDepth) {
            gd.Textures[1] = null;
            gd.BlendState = prevBlend;
            gd.RasterizerState = prevRaster;
            gd.DepthStencilState = prevDepth;
        }

        /// <summary>翼尖后掠的半径放大率（crescent=1 时翼尖半径 = radius × (1+此值)）</summary>
        public const float TipFlare = 0.22f;

        /// <param name="dir">弧顶朝向（冲刺方向）</param>
        /// <param name="radius">内缘半径 px</param>
        /// <param name="span">弧张角 rad，2π 为闭环</param>
        /// <param name="maxThick">弧顶满成形厚度 px</param>
        /// <param name="crescent">1=弓形 0=环形</param>
        public static void Draw(GraphicsDevice gd, Effect fx, Vector2 center, Vector2 dir
            , float radius, float span, float maxThick, float crescent
            , float form, float brk, float flash, float flowMul, float seed, float opacity) {
            if (opacity <= 0.01f || form <= 0.001f || brk >= 0.999f) {
                return;
            }
            float half = span * 0.5f;
            float baseAng = dir.ToRotation();
            float formEase = VaultUtils.EaseOutCubic(MathHelper.Clamp(form, 0f, 1f));

            fx.Parameters["uOpacity"]?.SetValue(MathHelper.Clamp(opacity, 0f, 1f));
            fx.Parameters["uMaxThickPx"]?.SetValue(maxThick);
            fx.Parameters["uArcPx"]?.SetValue(radius * span);
            fx.Parameters["uForm"]?.SetValue(MathHelper.Clamp(form, 0f, 1f));
            fx.Parameters["uBreak"]?.SetValue(MathHelper.Clamp(brk, 0f, 1f));
            fx.Parameters["uFlash"]?.SetValue(MathHelper.Clamp(flash, 0f, 1f));
            fx.Parameters["uFlowMul"]?.SetValue(flowMul);
            fx.Parameters["uSeed"]?.SetValue(seed);
            fx.Parameters["uCrescent"]?.SetValue(MathHelper.Clamp(crescent, 0f, 1f));

            for (int i = 0; i <= Segs; i++) {
                float t = i / (float)Segs;
                float phi = -half + span * t;
                float apex = 1f - Math.Abs(t * 2f - 1f);
                float apexW = MathHelper.Lerp(1f, MathF.Pow(apex, 0.8f), crescent);
                float thick = maxThick * MathHelper.Lerp(0.22f, 1f, apexW) * (0.35f + 0.65f * formEase);
                float r = radius * (1f + crescent * TipFlare * MathF.Pow(1f - apex, 2f));
                Vector2 n = (baseAng + phi).ToRotationVector2();
                Vector2 inner = center + n * r;
                Vector2 outer = center + n * (r + thick);
                Color c = new(thick / maxThick, (-n.Y + 1f) * 0.5f, 0f, 1f);
                verts[i * 2] = new VertexPositionColorTexture(inner.ToVector3(), c, new Vector2(t, 0f));
                verts[i * 2 + 1] = new VertexPositionColorTexture(outer.ToVector3(), c, new Vector2(t, 1f));
            }

            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, (Segs + 1) * 2 - 2);
            }
        }
    }
}
