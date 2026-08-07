using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniSlashs
{
    /// <summary>
    /// 鬼门开缝共享渲染,斩缝定义/真投影几何/曝光表生命周期<br/>
    /// 斩痕=世界被劈开的缝,几何为倾斜 3D 圆(或带 z 梯度直线)的透视投影条带,
    /// 刀身/碰撞/缝带全部从 <see cref="ProjectLocal"/> 同源采样<br/>
    /// 时间语法为曝光表:S0 蓄势(应力线)→S1 一帧撕满(过冲+毛刺白)→S2 冻结保持
    /// (细节步进重掷)→S3 鬼门闭合(张口收窄+端部捏合,路径不动)
    /// </summary>
    internal static class OniSlashRenderer
    {
        /// <summary>斩缝定义(确定性,各端一致)</summary>
        public struct RiftDef
        {
            //==== 几何 ====
            public float Mode;           //0=弧(倾斜3D圆) 1=直线
            public float Rot;            //弧:挥动平面基准角(指向弧腹) 直:刃方向角
            public float Span;           //弧跨度(rad),斩切语法须<π
            public float R;              //弧半径(px);直线=半刃长
            public float Tilt;           //倾斜角(rad,带符号),cos=压扁率 sin=z幅度;0=无深度
            public float ZPhase;         //0=贯通面(z∝sin,深→近单调) 1=舀击面(z∝cos,中段沉底)
            public float LineZSlope;     //直线模式沿刃 z 梯度(带符号)
            public float Flip;           //扫掠方向镜像(深度剖面不随其镜像)
            public float OffsetAlongAim; //中心沿瞄准偏移
            //==== 张口(力点) ====
            public float GapeMax;        //最大张口半宽(px)
            public float GapePeakU;      //张口峰值位置(0..1沿缝)
            public float GapePowIn;      //入锋锐度指数(大=更尖更久)
            public float GapePowOut;     //收锋锐度指数(小=肥撕)
            //==== 时间轴(帧,相对出生) ====
            public int GatherFrames;     //S0 蓄势帧数,撕开帧=该帧
            public int HoldFrames;       //S2 保持帧数(S1 固定 1 帧)
            public int Life;             //总寿命
            public int DamageStart;      //伤害窗起(=GatherFrames)
            public int DamageEnd;        //伤害窗止(=撕开+保持+1)
            //==== 材质 ====
            public float Seed;           //宏观噪声相位,出生冻结
            public float TelegraphAmt;   //S0 应力线强度(0=无预告)
            public float EmberAmt;       //魂火密度(终结拍)
            public float FarDim;         //>0 远近半侧分层,远半侧压暗
            public float Opacity;
        }

        /// <summary>斩缝单帧曝光状态</summary>
        public struct RiftAnim
        {
            public float Reveal;       //0=未撕开 1=已撕开(整形一次出现,无揭开wipe)
            public float Overshoot;    //几何过冲倍率,撕开帧1.06下一帧落定1.0
            public float Telegraph;    //S0 应力线强度
            public float Burr;         //撕开毛刺白包络(1~3帧速落)
            public float GapeT;        //张口保持度,S3 平滑收窄归零
            public float PinchT;       //端部向力点捏合进度
            public float InteriorGlow; //门缝幽光(闭合期熄灭)
            public float DetailSeed;   //细节通道种子,仅 S2 破碎步进重掷
            public float Alpha;
        }

        public readonly struct RiftBandSample
        {
            public readonly Vector2 Center;
            public readonly float Width;

            public RiftBandSample(Vector2 center, float width) {
                Center = center;
                Width = width;
            }
        }

        //==== 调色(内冷外热:缝内异界幽冷,世界侧灼红承接鬼切系列色板) ====
        public static readonly Vector3 ColVoid = new(0.032f, 0.016f, 0.036f);
        public static readonly Vector3 ColGlow = new(0.46f, 0.70f, 0.72f);
        public static readonly Vector3 ColRim = new(1.38f, 1.30f, 1.18f);
        public static readonly Vector3 ColBurn = new(1.30f, 0.16f, 0.10f);
        public static readonly Vector3 ColDeep = new(0.62f, 0.05f, 0.07f);

        //==== 缓动 ====

        public static float EaseOutCubic(float x) => 1f - MathF.Pow(1f - MathHelper.Clamp(x, 0f, 1f), 3f);

        public static float EaseInQuad(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            return x * x;
        }

        public static float SmoothStep01(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        //==== 曝光表采样 ====

        /// <summary>撕开帧(相对出生)</summary>
        public static int RipFrame(in RiftDef d) => d.GatherFrames;

        /// <summary>闭合起始帧(相对出生)</summary>
        public static int CloseStart(in RiftDef d) => d.GatherFrames + d.HoldFrames;

        /// <summary>细节步进间隔(帧),S2 内每步只重掷细节通道</summary>
        private const int DetailStepFrames = 3;
        private const float DetailStepPhase = 0.1573f;

        /// <summary>曝光表状态,几何撕开后冻结,材质允许继续演化</summary>
        public static RiftAnim Anim(in RiftDef d, int lt) {
            RiftAnim a = default;
            a.DetailSeed = d.Seed;
            a.Alpha = d.Opacity;
            a.Overshoot = 1f;
            int rip = RipFrame(in d);
            int closeStart = CloseStart(in d);

            if (lt < rip) {
                //S0 蓄势,全藏,重拍沿未来路径缓推应力线
                a.Telegraph = d.TelegraphAmt * EaseInQuad((lt + 1) / (float)(rip + 1));
                return a;
            }

            a.Reveal = 1f;
            a.GapeT = 1f;
            a.InteriorGlow = 1f;
            int since = lt - rip;
            //S1 一帧撕满,106%过冲,下一帧一步落定后几何冻结
            if (since == 0) {
                a.Overshoot = 1.06f;
            }
            //毛刺白,撕开帧全亮后1~3帧速落
            a.Burr = since <= 0 ? 1f : MathF.Pow(0.42f, since);
            if (a.Burr < 0.03f) {
                a.Burr = 0f;
            }

            if (since > 0 && lt <= closeStart) {
                //S2 破碎步进,宏观轮廓锁死只重掷细节通道
                a.DetailSeed = d.Seed + (1 + (since - 1) / DetailStepFrames) * DetailStepPhase;
                return a;
            }
            if (lt <= closeStart) {
                return a;
            }

            //S3 鬼门闭合,细节冻结在最后一次步进,张口平滑收窄+端部捏合+幽光熄灭
            int lastStep = d.HoldFrames > 0 ? 1 + (d.HoldFrames - 1) / DetailStepFrames : 0;
            a.DetailSeed = d.Seed + lastStep * DetailStepPhase;
            float closeT = MathHelper.Clamp((lt - closeStart) / (float)Math.Max(d.Life - closeStart, 1), 0f, 1f);
            a.GapeT = 1f - SmoothStep01(closeT);
            a.PinchT = MathF.Pow(closeT, 1.35f);
            a.InteriorGlow = MathF.Max(0f, 1f - closeT * 1.15f);
            a.Alpha *= 1f - SmoothStep01((closeT - 0.86f) / 0.14f);
            return a;
        }

        //==== 真投影(单一几何源:缝带/刀身/碰撞共用) ====

        public const float BaseViewZ = 900f;

        private static float ViewZOf(in RiftDef d) => MathF.Max(BaseViewZ, d.R * 2.6f);

        /// <summary>透视因子,z 朝观者(+)放大远离(−)缩小,巨弧下夹紧防爆</summary>
        public static float PerspK(float z, float viewZ)
            => MathHelper.Clamp(viewZ / MathF.Max(viewZ - z, 60f), 0.74f, 1.32f);

        /// <summary>z 幅度(px),归一化深度用</summary>
        public static float DepthAmp(in RiftDef d, float scaleMul = 1f) => d.Mode > 0.5f
            ? MathF.Abs(d.R * d.LineZSlope * scaleMul)
            : MathF.Abs(d.R * MathF.Sin(d.Tilt) * scaleMul);

        /// <summary>
        /// 缝中线点(相对中心偏移)与深度;弧=倾斜3D圆上取点→屏面旋转→透视除法,
        /// 深度剖面用不含 Flip 的基准角(笔画固定,不随朝向镜像)
        /// </summary>
        public static Vector2 ProjectLocal(in RiftDef d, float uc, float scaleMul, out float z, out float k) {
            float viewZ = ViewZOf(in d);
            Vector2 ax = d.Rot.ToRotationVector2();
            if (d.Mode > 0.5f) {
                float s = uc * 2f - 1f;
                z = s * d.R * d.LineZSlope * scaleMul;
                k = PerspK(z, viewZ);
                return ax * (s * d.R * 0.98f * scaleMul * k);
            }
            Vector2 ay = ax.RotatedBy(MathHelper.PiOver2);
            float phi = d.Flip * (uc - 0.5f) * d.Span;
            float phiFixed = (uc - 0.5f) * d.Span;
            float cosT = MathF.Cos(d.Tilt);
            float sinT = MathF.Sin(d.Tilt);
            float lx, ly;
            if (d.ZPhase > 0.5f) {
                //舀击面,长轴⊥Rot,中段沉底
                lx = MathF.Cos(phi) * cosT;
                ly = MathF.Sin(phi);
                z = MathF.Cos(phiFixed) * sinT * d.R * scaleMul;
            }
            else {
                //贯通面,长轴∥Rot,深→近单调
                lx = MathF.Cos(phi);
                ly = MathF.Sin(phi) * cosT;
                z = MathF.Sin(phiFixed) * sinT * d.R * scaleMul;
            }
            k = PerspK(z, viewZ);
            return (ax * lx + ay * ly) * (d.R * scaleMul * k);
        }

        /// <summary>路径 u 处刀身视觉倍率,切向透视缩短×透视因子(轴向长度呼吸=最强3D线索)</summary>
        public static float BladeStretchAt(in RiftDef d, float uc) {
            if (d.Mode > 0.5f) {
                ProjectLocal(in d, uc, 1f, out float zl, out float kl);
                return kl / MathF.Sqrt(1f + d.LineZSlope * d.LineZSlope * 0.7f);
            }
            float phi = d.Flip * (uc - 0.5f) * d.Span;
            float cosT = MathF.Cos(d.Tilt);
            float sin = MathF.Sin(phi);
            float cos = MathF.Cos(phi);
            float tangent = d.ZPhase > 0.5f
                ? MathF.Sqrt(sin * sin * cosT * cosT + cos * cos)
                : MathF.Sqrt(sin * sin + cos * cos * cosT * cosT);
            ProjectLocal(in d, uc, 1f, out float z, out float k);
            return tangent * k;
        }

        /// <summary>张口半宽(px,未含透视),沿缝不对称包络写出力点</summary>
        public static float GapeHalf(in RiftDef d, float uc, float gapeT) {
            float peak = MathHelper.Clamp(d.GapePeakU, 0.05f, 0.95f);
            float t = uc <= peak
                ? MathF.Pow(MathHelper.Clamp(uc / peak, 0f, 1f), MathF.Max(d.GapePowIn, 0.1f))
                : MathF.Pow(MathHelper.Clamp((1f - uc) / (1f - peak), 0f, 1f), MathF.Max(d.GapePowOut, 0.1f));
            return d.GapeMax * t * gapeT;
        }

        /// <summary>缝中线静态点(无过冲,实体刀路径/锚点用)</summary>
        public static Vector2 StaticPointAt(in RiftDef d, Vector2 center, float uc) {
            Vector2 offset = ProjectLocal(in d, uc, 1f, out _, out _);
            return center + offset;
        }

        /// <summary>缝中线静态点带深度(实体刀深度通道用)</summary>
        public static Vector2 StaticPointAt(in RiftDef d, Vector2 center, float uc, out float z) {
            Vector2 offset = ProjectLocal(in d, uc, 1f, out z, out _);
            return center + offset;
        }

        /// <summary>缝中线点,含当帧过冲</summary>
        public static Vector2 PointAt(in RiftDef d, Vector2 center, float uc, int lt) {
            RiftAnim a = Anim(in d, lt);
            Vector2 offset = ProjectLocal(in d, uc, a.Overshoot, out _, out _);
            return center + offset;
        }

        /// <summary>缝中线点与可见带宽,碰撞与视觉共用</summary>
        public static RiftBandSample SampleBand(in RiftDef d, Vector2 center, float uc, int lt) {
            RiftAnim a = Anim(in d, lt);
            Vector2 offset = ProjectLocal(in d, uc, a.Overshoot, out _, out float k);
            float width = GapeHalf(in d, uc, MathF.Max(a.GapeT, 0.05f)) * 2f * k;
            return new RiftBandSample(center + offset, width);
        }

        //==== 绘制 ====

        private const int ArcSlices = 28;
        private const int LineSlices = 14;
        private const int MaxSlices = 32;
        private static readonly VertexPositionColorTexture[] vertexScratch = new VertexPositionColorTexture[MaxSlices * 2];
        private static readonly Vector2[] centerScratch = new Vector2[MaxSlices];
        private static readonly float[] zScratch = new float[MaxSlices];
        private static readonly float[] kScratch = new float[MaxSlices];

        /// <summary>设备状态 + 帧级公共 uniform,false=资产未就绪</summary>
        public static bool BeginDraw(GraphicsDevice device, out Effect fx
            , out BlendState prevBlend, out RasterizerState prevRaster, out DepthStencilState prevDepth) {
            fx = EffectLoader.OniGateRift?.Value;
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
            fx.Parameters["uNoiseTex"]?.SetValue(noise);
            fx.Parameters["uColVoid"]?.SetValue(ColVoid);
            fx.Parameters["uColGlow"]?.SetValue(ColGlow);
            fx.Parameters["uColRim"]?.SetValue(ColRim);
            fx.Parameters["uColBurn"]?.SetValue(ColBurn);
            fx.Parameters["uColDeep"]?.SetValue(ColDeep);
            return true;
        }

        public static void EndDraw(GraphicsDevice device
            , BlendState prevBlend, RasterizerState prevRaster, DepthStencilState prevDepth) {
            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
            device.DepthStencilState = prevDepth;
        }

        /// <summary>绘制一道斩缝,farSel 0=整体 +1=近半侧 -1=远半侧(身后层);应力线只在近层画一次</summary>
        public static void DrawRift(GraphicsDevice device, Effect fx, in RiftDef d
            , Vector2 center, int lt, float farSel) {
            if (lt < 0 || lt >= d.Life) {
                return;
            }
            RiftAnim a = Anim(in d, lt);
            if (a.Alpha <= 0.012f) {
                return;
            }
            if (a.Reveal < 0.5f) {
                if (a.Telegraph <= 0.02f || farSel < -0.5f) {
                    return;
                }
                SubmitStrip(device, fx, in d, center, in a, telegraph: true, farSel: 0f);
                return;
            }
            SubmitStrip(device, fx, in d, center, in a, telegraph: false, farSel);
        }

        /// <summary>构建投影条带并提交,uv=(uc,横越0..1),顶点色R=归一化z</summary>
        private static void SubmitStrip(GraphicsDevice device, Effect fx, in RiftDef d
            , Vector2 center, in RiftAnim a, bool telegraph, float farSel) {
            int slices = d.Mode > 0.5f ? LineSlices : ArcSlices;
            float peak = MathHelper.Clamp(d.GapePeakU, 0.05f, 0.95f);
            //端部捏合窗,纹理仍锚在原uc上(材质被吃掉而非压缩)
            float u0 = MathHelper.Lerp(0f, peak * 0.90f, a.PinchT);
            float u1 = MathHelper.Lerp(1f, peak + (1f - peak) * 0.10f, a.PinchT);
            if (u1 - u0 < 0.02f) {
                return;
            }
            float depthAmp = MathF.Max(DepthAmp(in d, a.Overshoot), 0.001f);

            for (int i = 0; i < slices; i++) {
                float uc = u0 + (u1 - u0) * i / (slices - 1);
                centerScratch[i] = center + ProjectLocal(in d, uc, a.Overshoot, out zScratch[i], out kScratch[i]);
            }

            for (int i = 0; i < slices; i++) {
                float uc = u0 + (u1 - u0) * i / (slices - 1);
                Vector2 tangent = i == 0
                    ? centerScratch[1] - centerScratch[0]
                    : i == slices - 1
                        ? centerScratch[slices - 1] - centerScratch[slices - 2]
                        : centerScratch[i + 1] - centerScratch[i - 1];
                Vector2 normal = tangent.LengthSquared() > 0.0001f
                    ? Vector2.Normalize(tangent).RotatedBy(MathHelper.PiOver2)
                    : d.Rot.ToRotationVector2().RotatedBy(MathHelper.PiOver2);
                float halfW = telegraph
                    ? 2.2f + a.Telegraph * 2.0f
                    : MathF.Max(GapeHalf(in d, uc, a.GapeT) * kScratch[i], 0.6f);
                float zN01 = MathHelper.Clamp(zScratch[i] / depthAmp * 0.5f + 0.5f, 0f, 1f);
                Color data = new((byte)(zN01 * 255f), 255, 255, 255);
                Vector2 pos = centerScratch[i];
                vertexScratch[i * 2] = new VertexPositionColorTexture((pos - normal * halfW).ToVector3()
                    , data, new Vector2(uc, 0f));
                vertexScratch[i * 2 + 1] = new VertexPositionColorTexture((pos + normal * halfW).ToVector3()
                    , data, new Vector2(uc, 1f));
            }

            fx.Parameters["uSeed"]?.SetValue(d.Seed);
            fx.Parameters["uDetailSeed"]?.SetValue(a.DetailSeed);
            fx.Parameters["uBurr"]?.SetValue(a.Burr);
            fx.Parameters["uGlowIn"]?.SetValue(a.InteriorGlow);
            fx.Parameters["uGapeT"]?.SetValue(a.GapeT);
            fx.Parameters["uOpacity"]?.SetValue(a.Alpha);
            fx.Parameters["uFarSel"]?.SetValue(d.FarDim > 0f ? farSel : 0f);
            fx.Parameters["uFarDim"]?.SetValue(d.FarDim);
            fx.Parameters["uU0"]?.SetValue(u0);
            fx.Parameters["uU1"]?.SetValue(u1);
            fx.Parameters["uEmber"]?.SetValue(d.EmberAmt * a.InteriorGlow);
            fx.Parameters["uTelegraph"]?.SetValue(telegraph ? a.Telegraph : 0f);

            fx.CurrentTechnique = fx.Techniques[telegraph ? "TelegraphTech" : "RiftTech"];
            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, vertexScratch, 0, slices * 2 - 2);
            }
        }
    }
}
