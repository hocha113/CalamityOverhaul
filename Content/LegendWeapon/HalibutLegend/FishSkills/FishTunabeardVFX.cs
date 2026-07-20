using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>激流一闪域内 shader 资源（域内加载器，不经 EffectLoader）</summary>
    internal class FishTunabeardAssets
    {
        /// <summary>水绸带刀光：突刺路径的青蓝液体条带</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect FishTunaRibbon { get; private set; }
    }

    /// <summary>
    /// 激流一闪共享演出协作。<br/>
    /// 材质：半透明青蓝液体（水刃居合），签名行为=水绸带被刃体犁开甩在身后、
    /// 水珠受重力沿路甩落、余韵水雾悬浮慢落；
    /// 色彩脚本：深海暗蓝压底+饱和青蓝主色+亮水青头段，白沫只作小面积转瞬亮斑，
    /// 无常驻纯白、无彩虹（虹光穿刺归 FishUnicorn）、无荧光品红（归 FishNeonTetra）
    /// </summary>
    internal static class FishTunabeardVFX
    {
        //==== 色彩脚本 ====
        /// <summary>深海暗蓝（压底/尾段/暗涡）</summary>
        public static readonly Color Deep = new(10, 38, 66);
        /// <summary>饱和青蓝（主色）</summary>
        public static readonly Color Mid = new(28, 126, 186);
        /// <summary>亮水青（头段/水丝）</summary>
        public static readonly Color Bright = new(108, 208, 236);
        /// <summary>白沫（偏青近白，仅小面积转瞬）</summary>
        public static readonly Color Foam = new(218, 244, 250);
        /// <summary>水雾灰蓝（余韵悬浮雾）</summary>
        public static readonly Color MistGray = new(150, 190, 210);

        /// <summary>
        /// 入水/出水水花：压扁扩散环 + 重力水珠扇 + 轻水沫 + 一两团水雾。
        /// dir 为水花主要甩出方向；scale 起点传 ~0.55、终点传 1
        /// </summary>
        public static void SplashBurst(Vector2 pos, Vector2 dir, float scale = 1f) {
            if (Main.dedServ) {
                return;
            }
            dir = dir.SafeNormalize(Vector2.UnitX);
            //压扁水环（加色但低透明度短命，只给一拍轮廓）
            PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, Mid * 0.75f, 0.09f * scale)
                ?.Configure(new Vector2(1f, 0.55f), dir.ToRotation(), 0.72f * scale, 12);
            //主水珠扇：沿 dir 偏上抛出，重力弧线
            int drops = (int)(6 + 7 * scale);
            for (int i = 0; i < drops; i++) {
                Vector2 vel = dir.RotatedByRandom(0.85f) * Main.rand.NextFloat(2.5f, 6.5f + 3.5f * scale)
                    - Vector2.UnitY * Main.rand.NextFloat(0.5f, 2.4f);
                Color col = Color.Lerp(Mid, Deep, Main.rand.NextFloat(0.55f));
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, vel, col, Main.rand.NextFloat(0.75f, 1.2f) * scale)
                    ?.Configure(Main.rand.Next(22, 34), 0.30f, 0.982f);
            }
            //轻水沫：更小更慢坠、短命
            for (int i = 0; i < 3; i++) {
                Vector2 vel = dir.RotatedByRandom(1.1f) * Main.rand.NextFloat(1.6f, 4f) * scale;
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, vel, Foam * 0.9f, Main.rand.NextFloat(0.35f, 0.5f) * scale)
                    ?.Configure(Main.rand.Next(12, 18), 0.10f, 0.93f);
            }
            //水雾团
            int mists = scale > 0.8f ? 3 : 1;
            for (int i = 0; i < mists; i++) {
                Vector2 vel = dir * Main.rand.NextFloat(0.4f, 1.2f) + Main.rand.NextVector2Circular(0.5f, 0.4f);
                PRTLoader.NewParticle<PRT_FishTunaMist>(pos + Main.rand.NextVector2Circular(16f, 12f)
                    , vel, MistGray, Main.rand.NextFloat(0.10f, 0.16f) * scale)
                    ?.Configure(Main.rand.Next(38, 56));
            }
        }

        /// <summary>蓄势聚水：几粒水珠向玩家收拢（无重力短命，读作吸水而非发光）</summary>
        public static void GatherDrop(Vector2 center) {
            if (Main.dedServ) {
                return;
            }
            Vector2 offset = Main.rand.NextVector2Unit() * Main.rand.NextFloat(26f, 42f);
            PRTLoader.NewParticle<PRT_HeartcarverDroplet>(center + offset, -offset * 0.09f
                , Color.Lerp(Mid, Bright, Main.rand.NextFloat(0.5f)), Main.rand.NextFloat(0.5f, 0.8f))
                ?.Configure(Main.rand.Next(10, 14), 0f, 0.92f);
        }

        /// <summary>穿身撕水：沿突刺向的水珠锥 + 水沫，命中反馈</summary>
        public static void TearSpray(Vector2 pos, Vector2 dir) {
            if (Main.dedServ) {
                return;
            }
            dir = dir.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 7; i++) {
                Vector2 vel = dir.RotatedByRandom(0.6f) * Main.rand.NextFloat(3.5f, 9f)
                    - Vector2.UnitY * Main.rand.NextFloat(1.5f);
                Color col = Color.Lerp(Mid, Deep, Main.rand.NextFloat(0.5f));
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, vel, col, Main.rand.NextFloat(0.7f, 1.1f))
                    ?.Configure(Main.rand.Next(20, 30), 0.30f, 0.982f);
            }
            for (int i = 0; i < 2; i++) {
                Vector2 vel = dir.RotatedByRandom(0.9f) * Main.rand.NextFloat(2f, 4.5f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, vel, Foam * 0.9f, Main.rand.NextFloat(0.35f, 0.5f))
                    ?.Configure(Main.rand.Next(12, 16), 0.10f, 0.93f);
            }
        }

        /// <summary>刹停时沿整条路径播种悬浮水雾：活得比条带久，缓慢下落（余韵主角）</summary>
        public static void SeedPathMist(IReadOnlyList<Vector2> pts) {
            if (Main.dedServ || pts.Count < 2) {
                return;
            }
            const int Count = 14;
            for (int i = 0; i < Count; i++) {
                float u = (i + Main.rand.NextFloat(0.9f)) / Count;
                Vector2 pos = FishTunaRibbonRenderer.PointAlong(pts, u) + Main.rand.NextVector2Circular(24f, 18f);
                Vector2 vel = new(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(0f, 0.15f));
                PRTLoader.NewParticle<PRT_FishTunaMist>(pos, vel, MistGray
                    , Main.rand.NextFloat(0.10f, 0.17f))
                    ?.Configure(Main.rand.Next(50, 90));
            }
        }

        /// <summary>消散前沿的碎水：一粒下坠水珠 + 一小团雾，条带"化雾散掉"而非原地淡出</summary>
        public static void FrontWisp(Vector2 front) {
            if (Main.dedServ) {
                return;
            }
            Vector2 pos = front + Main.rand.NextVector2Circular(22f, 30f);
            PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos
                , new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(-0.4f, 0.6f))
                , Color.Lerp(Mid, Deep, Main.rand.NextFloat(0.6f)), Main.rand.NextFloat(0.5f, 0.8f))
                ?.Configure(Main.rand.Next(16, 26), 0.22f, 0.985f);
            if (Main.rand.NextBool()) {
                PRTLoader.NewParticle<PRT_FishTunaMist>(pos, Vector2.UnitY * 0.1f, MistGray
                    , Main.rand.NextFloat(0.08f, 0.13f))
                    ?.Configure(Main.rand.Next(36, 54));
            }
        }
    }

    /// <summary>
    /// 水绸带渲染：沿突刺路径铺三角带，交给 <see cref="FishTunabeardAssets.FishTunaRibbon"/>
    /// 画成半透明青蓝水绸。路径近直线（几何弧度只来自 C# 端的重力下垂），
    /// 只做剔短段+细分，不需要 Chaikin 切角；一条路径叠多股子带
    /// （垂直偏移+各自种子/流速/撕裂度）构成水层视差
    /// </summary>
    internal static class FishTunaRibbonRenderer
    {
        /// <summary>子带静态定义（一次突刺内不变，动态量走 DrawRibbon 参数）</summary>
        public struct RibbonDef
        {
            public float HalfWidth;   //半幅宽(px)
            public float PerpOffset;  //垂直路径的平行偏移(px)
            public float Seed;        //噪声相位
            public float FlowMul;     //流速倍率（子带各异 → 层间视差）
            public float TearAmp;     //轮廓撕裂幅度
            public float HeadBoost;   //头段白沫窄脊强度
            public float OpacityMul;  //相对整体的透明度
        }

        /// <summary>沿带噪声瓦片长度(px)：uLenScale=路径长/此值，水纹钉在世界空间</summary>
        private const float NoiseTilePx = 230f;
        /// <summary>剔除阈值(px)：短于此的段切向是噪声</summary>
        private const float MinSeg = 10f;
        /// <summary>细分上限(px)：收束尖与下垂曲线需要足够采样</summary>
        private const float MaxSeg = 40f;

        /// <summary>设备状态 + 帧级公共 uniform；返回 false 表示资产未就绪</summary>
        public static bool BeginDraw(GraphicsDevice device, out Effect fx
            , out BlendState prevBlend, out RasterizerState prevRaster, out DepthStencilState prevDepth) {
            fx = FishTunabeardAssets.FishTunaRibbon;
            Texture2D noise = CWRAsset.NoiseSoft01?.Value;
            prevBlend = device.BlendState;
            prevRaster = device.RasterizerState;
            prevDepth = device.DepthStencilState;
            if (fx == null || noise == null) {
                return false;
            }

            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.None;

            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uColDeep"]?.SetValue(FishTunabeardVFX.Deep.ToVector3());
            fx.Parameters["uColMid"]?.SetValue(FishTunabeardVFX.Mid.ToVector3());
            fx.Parameters["uColBright"]?.SetValue(FishTunabeardVFX.Bright.ToVector3());
            fx.Parameters["uColFoam"]?.SetValue(FishTunabeardVFX.Foam.ToVector3());
            fx.Parameters["uNoiseTex"]?.SetValue(noise);
            return true;
        }

        public static void EndDraw(GraphicsDevice device
            , BlendState prevBlend, RasterizerState prevRaster, DepthStencilState prevDepth) {
            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
            device.DepthStencilState = prevDepth;
        }

        /// <summary>
        /// 绘制一股子带。points 首元素=起点(尾)、末元素=头端；
        /// retract 0..1 从尾向头消散，flash 出手过曝帧，opacity 整体包络
        /// </summary>
        public static void DrawRibbon(GraphicsDevice device, Effect fx
            , IReadOnlyList<Vector2> rawPoints, in RibbonDef def
            , float retract, float flash, float opacity) {
            if (rawPoints.Count < 2) {
                return;
            }

            List<Vector2> points = ShapePath(rawPoints);
            if (points.Count < 2) {
                return;
            }
            int count = points.Count;

            float totalLen = 0f;
            for (int i = 1; i < count; i++) {
                totalLen += Vector2.Distance(points[i - 1], points[i]);
            }
            if (totalLen < 12f) {
                return;
            }

            float a = opacity * def.OpacityMul;
            if (a <= 0.01f) {
                return;
            }

            fx.Parameters["uOpacity"]?.SetValue(MathHelper.Clamp(a, 0f, 1f));
            fx.Parameters["uRetract"]?.SetValue(MathHelper.Clamp(retract, 0f, 1f));
            fx.Parameters["uLenScale"]?.SetValue(totalLen / NoiseTilePx);
            fx.Parameters["uSeed"]?.SetValue(def.Seed);
            fx.Parameters["uFlowMul"]?.SetValue(def.FlowMul);
            fx.Parameters["uTearAmp"]?.SetValue(def.TearAmp);
            fx.Parameters["uHeadBoost"]?.SetValue(def.HeadBoost);
            fx.Parameters["uFlash"]?.SetValue(MathHelper.Clamp(flash, 0f, 1.2f));

            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[count * 2];
            float cum = 0f;
            for (int i = 0; i < count; i++) {
                if (i > 0) {
                    cum += Vector2.Distance(points[i - 1], points[i]);
                }
                float u = cum / totalLen;

                //切向取邻段平均，端点用单侧
                Vector2 dir = i == 0
                    ? points[1] - points[0]
                    : i == count - 1
                        ? points[i] - points[i - 1]
                        : points[i + 1] - points[i - 1];
                dir = dir.SafeNormalize(Vector2.UnitX);
                Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);

                //幅宽包络：尾端略收、头端全宽（收束尖与撕裂舌由 shader 负责）
                float hw = def.HalfWidth * MathHelper.Lerp(0.62f, 1f, u);
                //平行偏移在头段漏斗式归零：多股水层汇入刃尖
                float funnel = MathHelper.Clamp((1f - u) / 0.30f, 0f, 1f);
                funnel = funnel * (2f - funnel);   //easeOut，汇入平滑无折角
                Vector2 center = points[i] + perp * (def.PerpOffset * funnel);

                verts[i * 2] = new VertexPositionColorTexture(
                    (center - perp * hw).ToVector3(), Color.White, new Vector2(u, 0f));
                verts[i * 2 + 1] = new VertexPositionColorTexture(
                    (center + perp * hw).ToVector3(), Color.White, new Vector2(u, 1f));
            }

            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, count * 2 - 2);
            }
        }

        /// <summary>路径整形：剔退化短段 → 细分到 ≤40px 段（近直线，无需切角圆滑）</summary>
        private static List<Vector2> ShapePath(IReadOnlyList<Vector2> raw) {
            List<Vector2> culled = new(raw.Count);
            culled.Add(raw[0]);
            for (int i = 1; i < raw.Count; i++) {
                if (Vector2.DistanceSquared(culled[^1], raw[i]) >= MinSeg * MinSeg) {
                    culled.Add(raw[i]);
                }
                else if (i == raw.Count - 1) {
                    //末点承载头端语义：距离不足时顶替前点而不是丢弃
                    if (culled.Count > 1) {
                        culled[^1] = raw[i];
                    }
                    else {
                        culled.Add(raw[i]);
                    }
                }
            }
            return SubdividePath(culled);
        }

        /// <summary>路径细分：超过 40px 的段插入等分点（原点集不变，仅补密）</summary>
        private static List<Vector2> SubdividePath(IReadOnlyList<Vector2> raw) {
            if (raw.Count < 2) {
                return [.. raw];
            }
            List<Vector2> outPts = new(raw.Count * 4);
            outPts.Add(raw[0]);
            for (int i = 1; i < raw.Count; i++) {
                Vector2 a = raw[i - 1];
                Vector2 b = raw[i];
                float len = Vector2.Distance(a, b);
                int cuts = (int)(len / MaxSeg);
                for (int k = 1; k <= cuts; k++) {
                    outPts.Add(Vector2.Lerp(a, b, k / (float)(cuts + 1)));
                }
                outPts.Add(b);
            }
            return outPts;
        }

        /// <summary>按弧长比例取路径上一点（0=尾 1=头），供消散前沿与播雾定位</summary>
        public static Vector2 PointAlong(IReadOnlyList<Vector2> points, float t) {
            int count = points.Count;
            if (count == 0) {
                return Vector2.Zero;
            }
            if (count == 1 || t <= 0f) {
                return points[0];
            }
            if (t >= 1f) {
                return points[count - 1];
            }

            float totalLen = 0f;
            for (int i = 1; i < count; i++) {
                totalLen += Vector2.Distance(points[i - 1], points[i]);
            }
            float goal = totalLen * t;
            float cum = 0f;
            for (int i = 1; i < count; i++) {
                float seg = Vector2.Distance(points[i - 1], points[i]);
                if (cum + seg >= goal && seg > 0f) {
                    return Vector2.Lerp(points[i - 1], points[i], (goal - cum) / seg);
                }
                cum += seg;
            }
            return points[count - 1];
        }
    }

    /// <summary>
    /// 激流余韵水雾：AlphaBlend 灰蓝雾团，悬浮后缓慢下落、微涨、快进慢出，
    /// 活得比水绸带久（余韵主角）。SmokeSheet01 为白RGB+真alpha，可安全染色直绘
    /// </summary>
    internal class PRT_FishTunaMist : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SmokeSheet01";
        public override bool CanPool => true;

        private float fall;
        private float spin;
        private Color baseColor;

        public PRT_FishTunaMist Configure(int lifetime, float fallAccel = 0.012f) {
            Lifetime = lifetime;
            fall = fallAccel;
            baseColor = Color;
            return this;
        }

        public override void Reset() {
            base.Reset();
            fall = 0f;
            spin = 0f;
            baseColor = default;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            ai[0] = Main.rand.Next(4);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            spin = Main.rand.NextFloat(0.004f, 0.012f) * (Main.rand.NextBool() ? 1f : -1f);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(40, 70);
                baseColor = FishTunabeardVFX.MistGray;
            }
        }

        public override void AI() {
            float t = LifetimeCompletion;
            //悬浮 → 缓慢下落：水雾比烟重
            Velocity.X *= 0.97f;
            Velocity.Y += fall;
            if (Velocity.Y > 1.1f) {
                Velocity.Y = 1.1f;
            }
            Scale *= 1.004f;
            Rotation += spin;

            //雾色随沉降转暗转灰
            Color = Color.Lerp(baseColor, FishTunabeardVFX.Deep, t * 0.55f);
            //快进慢出，峰值压低：雾是垫底介质不是主角光
            Opacity = MathF.Min(t / 0.16f, 1f) * (1f - SmoothStep01((t - 0.42f) / 0.56f)) * 0.34f;
        }

        private static float SmoothStep01(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            int index = (int)ai[0];
            int frameSize = tex.Width / 2;
            Rectangle frame = new(index % 2 * frameSize, index / 2 * frameSize, frameSize, frameSize);
            spriteBatch.Draw(tex, Position - Main.screenPosition, frame, Color * Opacity, Rotation
                , frame.Size() * 0.5f, Scale * 0.5f, SpriteEffects.None, 0);
            return false;
        }
    }
}
