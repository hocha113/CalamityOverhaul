using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniSlashs
{
    /// <summary>
    /// 鬼门开缝共享渲染,斩痕定义/真投影几何/扫掠生命周期<br/>
    /// 语法=主流 ARPG 扫掠光刃:刃头带着揭开沿弧扫过(3~6帧,爆发起步减速落位)
    /// →满形定格闪(money frame,过冲落定)→尾端先蚀的定向消散;几何落位后冻结<br/>
    /// 几何=刀刃扫过的环带:外缘=刀尖轨迹(锐利),内缘=软融;径向宽度≈半径40%(阔剑体量)<br/>
    /// 刀身/碰撞/光刃全部从 <see cref="ProjectLocal"/> 同源采样,刃头与刀走同一条揭开曲线<br/>
    /// 鬼门身份只住在终结拍:满形定格时外缘豁开黑缝+魂火,消散初段闭合
    /// </summary>
    internal static class OniSlashRenderer
    {
        /// <summary>斩痕定义(确定性,各端一致)</summary>
        public struct RiftDef
        {
            //==== 几何 ====
            public float Mode;           //0=弧(倾斜3D圆) 1=直线
            public float Rot;            //弧:挥动平面基准角(指向弧腹) 直:刃方向角
            public float Span;           //弧跨度(rad),斩切语法须<π
            public float R;              //弧半径(px);直线=半刃长
            public float Tilt;           //倾斜角(rad,带符号),cos=压扁率 sin=z幅度;0=无深度
            public float ZPhase;         //0=贯通面(z∝sin,深→近单调) 1=舀击面(z∝cos,中段扑近)
            public float LineZSlope;     //直线模式沿刃 z 梯度(带符号)
            public float Flip;           //扫掠方向镜像(深度剖面不随其镜像)
            public float OffsetAlongAim; //中心沿瞄准偏移
            //==== 带宽(力点) ====
            public float BandMax;        //环带最大径向深度(px,自外缘向内,阔剑量级≈0.4R)
            public float PeakU;          //带宽峰值位置(0..1沿刃)
            public float PowIn;          //入锋锐度指数(大=更尖更久)
            public float PowOut;         //收锋锐度指数(小=肥撕)
            //==== 时间轴(帧,相对出生) ====
            public int GatherFrames;     //S0 蓄势帧数
            public int SweepFrames;      //S1 扫掠帧数(刃带头揭开)
            public int HoldFrames;       //S2 满形定格帧数
            public int Life;             //总寿命
            public int DamageStart;      //伤害窗起(=GatherFrames,随刃头推进)
            public int DamageEnd;        //伤害窗止(=蓄势+扫掠+定格+1)
            //==== 材质 ====
            public float Seed;           //宏观噪声相位,出生冻结
            public float TelegraphAmt;   //S0 应力线强度(0=无预告)
            public float GateOpen;       //鬼门大开幅度(仅终结拍,定格期豁开)
            public float EmberAmt;       //魂火密度(终结拍)
            public float FarDim;         //>0 远近半侧分层,远半侧压暗
            public float Opacity;
        }

        /// <summary>斩痕单帧状态</summary>
        public struct RiftAnim
        {
            public float Telegraph;    //S0 应力线强度
            public float HeadU;        //揭开头位置(刃当前所在u,0=未出)
            public float Lead;         //刃头亮线强度(扫掠期1,定格速落)
            public float Overshoot;    //几何过冲倍率,落位帧1.06下一帧落定
            public float Flash;        //满形定格闪(落位帧起1~2帧速落)
            public float ErodeT;       //定向消散进度(尾→头)
            public float GateT;        //鬼门大开包络(仅 GateOpen>0 拍)
            public float DetailSeed;   //细节通道种子,仅定格期步进重掷
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

        //==== 调色(白热核心/亮绯红/深红/近黑沉边;冷渗光只住终结拍鬼门缝) ====
        public static readonly Vector3 ColHot = new(1.55f, 1.34f, 1.10f);
        public static readonly Vector3 ColBurn = new(1.32f, 0.17f, 0.10f);
        public static readonly Vector3 ColDeep = new(0.58f, 0.045f, 0.065f);
        public static readonly Vector3 ColVoid = new(0.055f, 0.018f, 0.030f);
        public static readonly Vector3 ColGlow = new(0.46f, 0.70f, 0.72f);

        //==== 缓动 ====

        public static float EaseOutCubic(float x) => 1f - MathF.Pow(1f - MathHelper.Clamp(x, 0f, 1f), 3f);

        public static float EaseOutQuad(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            return 1f - (1f - x) * (1f - x);
        }

        public static float EaseInQuad(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            return x * x;
        }

        public static float SmoothStep01(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        //==== 生命周期采样 ====

        /// <summary>扫掠起始帧(相对出生),主挥砍音/伤害窗对齐此帧</summary>
        public static int SweepStartFrame(in RiftDef d) => d.GatherFrames;

        /// <summary>满形定格起始帧</summary>
        public static int HoldStartFrame(in RiftDef d) => d.GatherFrames + d.SweepFrames;

        /// <summary>消散起始帧(轻确认对齐此帧)</summary>
        public static int DissolveStartFrame(in RiftDef d) => d.GatherFrames + d.SweepFrames + d.HoldFrames;

        /// <summary>细节步进间隔(帧),定格期每步只重掷细节通道</summary>
        private const int DetailStepFrames = 3;
        private const float DetailStepPhase = 0.1573f;

        /// <summary>刃头揭开曲线,爆发起步减速落位(与实体刀共用)</summary>
        public static float HeadCurve(in RiftDef d, int sinceSweep)
            => EaseOutQuad((sinceSweep + 1) / (float)Math.Max(d.SweepFrames, 1));

        /// <summary>扫掠生命周期状态,落位后几何冻结,消散只走材质</summary>
        public static RiftAnim Anim(in RiftDef d, int lt) {
            RiftAnim a = default;
            a.DetailSeed = d.Seed;
            a.Alpha = d.Opacity;
            a.Overshoot = 1f;
            int sweepStart = SweepStartFrame(in d);
            int holdStart = HoldStartFrame(in d);
            int dissolveStart = DissolveStartFrame(in d);

            if (lt < sweepStart) {
                //S0 蓄势,全藏,重拍沿未来路径缓推应力线
                a.Telegraph = d.TelegraphAmt * EaseInQuad((lt + 1) / (float)(sweepStart + 1));
                return a;
            }

            if (lt < holdStart) {
                //S1 扫掠,刃带着光刃头走;落位帧106%过冲+满形闪起
                int since = lt - sweepStart;
                a.HeadU = HeadCurve(in d, since);
                a.Lead = 1f;
                if (since == d.SweepFrames - 1) {
                    a.Overshoot = 1.06f;
                    a.Flash = 1f;
                }
                return a;
            }

            //满形后刃头推过端点,揭开羽化不再啃收笔端(端部锥形交给带宽包络+窗口羽化)
            a.HeadU = 1.08f;
            a.GateT = d.GateOpen;
            if (lt < dissolveStart) {
                //S2 满形定格(money frame),闪速落,刃头线熄,细节步进重掷
                int hs = lt - holdStart;
                a.Flash = MathF.Pow(0.48f, hs + 1);
                if (a.Flash < 0.04f) {
                    a.Flash = 0f;
                }
                a.Lead = MathF.Max(0f, 1f - (hs + 1) * 0.5f);
                a.DetailSeed = d.Seed + (1 + hs / DetailStepFrames) * DetailStepPhase;
                return a;
            }

            //S3 定向消散,尾端先蚀向刃头,亮度沉降;鬼门大开在初段闭合
            int lastStep = 1 + Math.Max(0, d.HoldFrames - 1) / DetailStepFrames;
            a.DetailSeed = d.Seed + lastStep * DetailStepPhase;
            float dis = MathHelper.Clamp((lt - dissolveStart) / (float)Math.Max(d.Life - dissolveStart, 1), 0f, 1f);
            a.ErodeT = SmoothStep01(dis);
            a.GateT = d.GateOpen * MathF.Max(0f, 1f - SmoothStep01(dis / 0.35f));
            a.Alpha *= 1f - SmoothStep01((dis - 0.80f) / 0.20f);
            return a;
        }

        //==== 真投影(单一几何源:光刃/刀身/碰撞共用) ====

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
        /// 刀尖轨迹点(相对中心偏移)与深度;弧=倾斜3D圆上取点→屏面旋转→透视除法,
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
                //舀击面,长轴⊥Rot,中段扑近
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

        /// <summary>环带径向深度(px,未含透视),幂化不对称包络写出力点,两端归零收尖</summary>
        public static float BandWidth(in RiftDef d, float uc) {
            float peak = MathHelper.Clamp(d.PeakU, 0.05f, 0.95f);
            float t = uc <= peak
                ? MathF.Pow(MathHelper.Clamp(uc / peak, 0f, 1f), MathF.Max(d.PowIn, 0.1f))
                : MathF.Pow(MathHelper.Clamp((1f - uc) / (1f - peak), 0f, 1f), MathF.Max(d.PowOut, 0.1f));
            return d.BandMax * t;
        }

        /// <summary>刀尖轨迹静态点(无过冲,实体刀路径/锚点用)</summary>
        public static Vector2 StaticPointAt(in RiftDef d, Vector2 center, float uc) {
            Vector2 offset = ProjectLocal(in d, uc, 1f, out _, out _);
            return center + offset;
        }

        /// <summary>刀尖轨迹静态点带深度(实体刀深度通道用)</summary>
        public static Vector2 StaticPointAt(in RiftDef d, Vector2 center, float uc, out float z) {
            Vector2 offset = ProjectLocal(in d, uc, 1f, out z, out _);
            return center + offset;
        }

        /// <summary>刀尖轨迹点,含当帧过冲</summary>
        public static Vector2 PointAt(in RiftDef d, Vector2 center, float uc, int lt) {
            RiftAnim a = Anim(in d, lt);
            Vector2 offset = ProjectLocal(in d, uc, a.Overshoot, out _, out _);
            return center + offset;
        }

        /// <summary>刀尖轨迹点与可见带宽,碰撞与视觉共用(带自轨迹向内,外侧余量=擦边宽恕)</summary>
        public static RiftBandSample SampleBand(in RiftDef d, Vector2 center, float uc, int lt) {
            RiftAnim a = Anim(in d, lt);
            Vector2 offset = ProjectLocal(in d, uc, a.Overshoot, out _, out float k);
            float width = BandWidth(in d, uc) * k;
            return new RiftBandSample(center + offset, width);
        }

        //==== 绘制 ====

        private const int ArcSlices = 28;
        private const int LineSlices = 14;
        private const int MaxSlices = 32;
        private static readonly VertexPositionColorTexture[] vertexScratch = new VertexPositionColorTexture[MaxSlices * 2];
        private static readonly Vector2[] outerScratch = new Vector2[MaxSlices];
        private static readonly float[] zScratch = new float[MaxSlices];
        private static readonly float[] kScratch = new float[MaxSlices];

        /// <summary>设备状态 + 帧级公共 uniform,false=资产未就绪</summary>
        public static bool BeginDraw(GraphicsDevice device, out Effect fx
            , out BlendState prevBlend, out RasterizerState prevRaster, out DepthStencilState prevDepth) {
            fx = EffectLoader.OniGateRift?.Value;
            Texture2D noise = CWRAsset.NoiseSoft01?.Value;
            Texture2D brush = CWRAsset.SlashBrush01?.Value;
            prevBlend = device.BlendState;
            prevRaster = device.RasterizerState;
            prevDepth = device.DepthStencilState;
            if (fx == null || noise == null || brush == null) {
                return false;
            }

            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.None;

            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uNoiseTex"]?.SetValue(noise);
            fx.Parameters["uBrushTex"]?.SetValue(brush);
            fx.Parameters["uColHot"]?.SetValue(ColHot);
            fx.Parameters["uColBurn"]?.SetValue(ColBurn);
            fx.Parameters["uColDeep"]?.SetValue(ColDeep);
            fx.Parameters["uColVoid"]?.SetValue(ColVoid);
            fx.Parameters["uColGlow"]?.SetValue(ColGlow);
            return true;
        }

        public static void EndDraw(GraphicsDevice device
            , BlendState prevBlend, RasterizerState prevRaster, DepthStencilState prevDepth) {
            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
            device.DepthStencilState = prevDepth;
        }

        /// <summary>
        /// 绘制一道斩痕,farSel 0=整体 +1=近半侧 -1=远半侧(身后层);应力线只在近层画一次<br/>
        /// inwardRef=内侧参考点(持刀者锚位),直线拍的环带内向由它决定,弧拍用圆心
        /// </summary>
        public static void DrawRift(GraphicsDevice device, Effect fx, in RiftDef d
            , Vector2 center, int lt, float farSel, Vector2 inwardRef) {
            if (lt < 0 || lt >= d.Life) {
                return;
            }
            RiftAnim a = Anim(in d, lt);
            if (a.Alpha <= 0.012f) {
                return;
            }
            if (a.HeadU <= 0.001f) {
                if (a.Telegraph <= 0.02f || farSel < -0.5f) {
                    return;
                }
                SubmitTelegraph(device, fx, in d, center, in a);
                return;
            }
            SubmitBand(device, fx, in d, center, in a, farSel, inwardRef);
        }

        /// <summary>S0 应力线,贴未来刀尖轨迹的对称细线</summary>
        private static void SubmitTelegraph(GraphicsDevice device, Effect fx, in RiftDef d
            , Vector2 center, in RiftAnim a) {
            int slices = d.Mode > 0.5f ? LineSlices : ArcSlices;
            float depthAmp = MathF.Max(DepthAmp(in d, 1f), 0.001f);

            for (int i = 0; i < slices; i++) {
                float uc = i / (float)(slices - 1);
                outerScratch[i] = center + ProjectLocal(in d, uc, 1f, out zScratch[i], out kScratch[i]);
            }
            for (int i = 0; i < slices; i++) {
                float uc = i / (float)(slices - 1);
                Vector2 tangent = i == 0
                    ? outerScratch[1] - outerScratch[0]
                    : i == slices - 1
                        ? outerScratch[slices - 1] - outerScratch[slices - 2]
                        : outerScratch[i + 1] - outerScratch[i - 1];
                Vector2 normal = tangent.LengthSquared() > 0.0001f
                    ? Vector2.Normalize(tangent).RotatedBy(MathHelper.PiOver2)
                    : d.Rot.ToRotationVector2().RotatedBy(MathHelper.PiOver2);
                float halfW = 1.8f + a.Telegraph * 1.6f;
                float zN01 = MathHelper.Clamp(zScratch[i] / depthAmp * 0.5f + 0.5f, 0f, 1f);
                Color data = new((int)(zN01 * 255f), 255, 255, 255);
                vertexScratch[i * 2] = new VertexPositionColorTexture((outerScratch[i] - normal * halfW).ToVector3()
                    , data, new Vector2(uc, 0f));
                vertexScratch[i * 2 + 1] = new VertexPositionColorTexture((outerScratch[i] + normal * halfW).ToVector3()
                    , data, new Vector2(uc, 1f));
            }

            fx.Parameters["uSeed"]?.SetValue(d.Seed);
            fx.Parameters["uOpacity"]?.SetValue(a.Alpha);
            fx.Parameters["uU0"]?.SetValue(0f);
            fx.Parameters["uU1"]?.SetValue(1f);
            fx.Parameters["uTelegraph"]?.SetValue(a.Telegraph);

            fx.CurrentTechnique = fx.Techniques["TelegraphTech"];
            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, vertexScratch, 0, slices * 2 - 2);
            }
        }

        /// <summary>
        /// 环带构建:外顶点=刀尖轨迹(v=1,锐利缘),内顶点=向内收带宽(v=0,软融缘);
        /// 弧拍内向=屏幕径向朝圆心,直线拍内向=垂线朝持刀者;揭开/消散由shader按uc门控
        /// </summary>
        private static void SubmitBand(GraphicsDevice device, Effect fx, in RiftDef d
            , Vector2 center, in RiftAnim a, float farSel, Vector2 inwardRef) {
            int slices = d.Mode > 0.5f ? LineSlices : ArcSlices;
            float depthAmp = MathF.Max(DepthAmp(in d, a.Overshoot), 0.001f);
            bool isLine = d.Mode > 0.5f;

            for (int i = 0; i < slices; i++) {
                float uc = i / (float)(slices - 1);
                outerScratch[i] = center + ProjectLocal(in d, uc, a.Overshoot, out zScratch[i], out kScratch[i]);
            }

            for (int i = 0; i < slices; i++) {
                float uc = i / (float)(slices - 1);
                Vector2 outer = outerScratch[i];
                Vector2 inward;
                if (isLine) {
                    Vector2 tangent = i == 0
                        ? outerScratch[1] - outerScratch[0]
                        : i == slices - 1
                            ? outerScratch[slices - 1] - outerScratch[slices - 2]
                            : outerScratch[i + 1] - outerScratch[i - 1];
                    inward = tangent.LengthSquared() > 0.0001f
                        ? Vector2.Normalize(tangent).RotatedBy(MathHelper.PiOver2)
                        : d.Rot.ToRotationVector2().RotatedBy(MathHelper.PiOver2);
                    if (Vector2.Dot(inward, inwardRef - outer) < 0f) {
                        inward = -inward;
                    }
                }
                else {
                    Vector2 toCenter = center - outer;
                    inward = toCenter.LengthSquared() > 0.0001f
                        ? Vector2.Normalize(toCenter)
                        : -d.Rot.ToRotationVector2();
                }
                float w = MathF.Max(BandWidth(in d, uc) * kScratch[i], 0.4f);
                float zN01 = MathHelper.Clamp(zScratch[i] / depthAmp * 0.5f + 0.5f, 0f, 1f);
                Color data = new((int)(zN01 * 255f), 255, 255, 255);
                vertexScratch[i * 2] = new VertexPositionColorTexture((outer + inward * w).ToVector3()
                    , data, new Vector2(uc, 0f));
                vertexScratch[i * 2 + 1] = new VertexPositionColorTexture(outer.ToVector3()
                    , data, new Vector2(uc, 1f));
            }

            fx.Parameters["uSeed"]?.SetValue(d.Seed);
            fx.Parameters["uDetailSeed"]?.SetValue(a.DetailSeed);
            fx.Parameters["uHead"]?.SetValue(a.HeadU);
            fx.Parameters["uLead"]?.SetValue(a.Lead);
            fx.Parameters["uFlash"]?.SetValue(a.Flash);
            fx.Parameters["uErode"]?.SetValue(a.ErodeT);
            fx.Parameters["uGateT"]?.SetValue(a.GateT);
            fx.Parameters["uOpacity"]?.SetValue(a.Alpha);
            fx.Parameters["uFarSel"]?.SetValue(d.FarDim > 0f ? farSel : 0f);
            fx.Parameters["uFarDim"]?.SetValue(d.FarDim);
            fx.Parameters["uU0"]?.SetValue(0f);
            fx.Parameters["uU1"]?.SetValue(1f);
            fx.Parameters["uEmber"]?.SetValue(d.EmberAmt);
            fx.Parameters["uTelegraph"]?.SetValue(0f);

            fx.CurrentTechnique = fx.Techniques["RiftTech"];
            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, vertexScratch, 0, slices * 2 - 2);
            }
        }
    }
}
