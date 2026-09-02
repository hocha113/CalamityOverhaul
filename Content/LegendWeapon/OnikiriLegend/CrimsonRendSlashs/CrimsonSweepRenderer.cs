using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs
{
    /// <summary>
    /// 绯红裂空斩·扫掠刀光共享渲染:刀光=刀身扫过的体积<br/>
    /// 拍表只写刀的关键帧(θ 起止/拉背/倾角/节拍帧数),条带每帧在 θ 域解析重建 [tail, head],
    /// 外缘=刀尖轨迹(倾斜圆真投影),内缘=刀身内侧点按 u 收成月牙;刀身/碰撞/粒子全部从同一投影采样<br/>
    /// 时间轴:蓄势(拉背)→死寂→爆发(过冲回坐,落位帧闪)→体向刀收缩→只剩刃痕蚀退;
    /// 材质在 OniCrimsonSweep.fx,本类只管几何与 uniform
    /// </summary>
    internal static class CrimsonSweepRenderer
    {
        /// <summary>一拍的编排(开火时冻结,各端确定性一致)</summary>
        public struct SweepDef
        {
            public float Aim;            //瞄准角
            public int Facing;           //±1 屏面镜像
            public float ThetaStart;     //挥动平面内相对 aim 的起笔角(未镜像,rad)
            public float ThetaEnd;       //收笔角;符号差=挥向
            public float WindupRad;      //拉背角(逆挥向退出起笔位,rad)
            public float Reach;          //px 刀光外缘半径=爆发帧刀尖轨迹(刀尖钉在剃刀线上)
            public float RestFrac;       //静止持刀长/Reach;爆发帧刀拉长到 Reach(涂抹帧拉长),落位后回坐到此
            public float Tilt;           //rad 挥动平面绕 aim 轴倾角,符号选哪半沉入身后
            public float Roll;           //rad 投影后屏面滚转(已含朝向)
            public float InnerHead;      //刃头处内缘刀身比例(0..1 of Reach)
            public float InnerTail;      //起笔处内缘刀身比例
            public int GatherFrames;
            public int StillFrames;
            public int BurstFrames;
            public int SettleFrames;     //过冲回坐(只影响刀,体已开始收缩)
            public int CollapseFrames;
            public int ScarFrames;
            public float Overshoot;      //爆发末 p 过冲(1.05)
            public float FlashPeak;
            public float FarDim;
            public float Seed;
            public float Opacity;
            public float LeanBack;       //蓄势后仰(rad)
            public float LeanFwd;        //爆发前甩(rad)
            public float StepPx;         //爆发首帧踏步(px)
            public float HopVy;          //蓄势末小跳(负=向上)

            public readonly float Span => ThetaEnd - ThetaStart;
            public readonly int SweepStart => GatherFrames + StillFrames;
            public readonly int LandFrame => SweepStart + BurstFrames - 1;
            public readonly int CollapseStart => SweepStart + BurstFrames;
            public readonly int ScarStart => CollapseStart + CollapseFrames;
            public readonly int Life => ScarStart + ScarFrames;
            public readonly int DamageStart => SweepStart;
            /// <summary>伤害窗盖住爆发+收缩前两帧,与可见体同步收窄</summary>
            public readonly int DamageEnd => SweepStart + BurstFrames + 2;
            /// <summary>拉背位 p(负值)</summary>
            public readonly float WindupP => -WindupRad / MathF.Max(MathF.Abs(Span), 0.1f);
            /// <summary>反向拍须翻刃,刃口镜像到挥动前缘</summary>
            public readonly bool EdgeFlip => Span < 0f;
            /// <summary>静止持刀长(px,轴心到刀尖)</summary>
            public readonly float RestLen => Reach * RestFrac;
        }

        /// <summary>单帧时间轴采样</summary>
        public struct SweepAnim
        {
            public float BladeP;     //刀行程 p,拉背为负、过冲>1
            public float HeadP;      //条带刃头 0..1
            public float TailP;      //条带体收缩前沿 0..1(>1 体全收)
            public float Flash;
            public float Erode;
            public float Alpha;
            public bool InBurst;
            public bool BodyAlive;
        }

        //==== 调色(与 CrimsonSlashRenderer 同源绯红 + 灼烧橙) ====
        public static readonly Vector3 ColHot = new(1.60f, 1.32f, 1.08f);
        public static readonly Vector3 ColBright = new(1.30f, 0.16f, 0.10f);
        public static readonly Vector3 ColDeep = new(0.62f, 0.05f, 0.07f);
        public static readonly Vector3 ColDark = new(0.16f, 0.015f, 0.035f);
        public static readonly Vector3 ColEmber = new(1.22f, 0.34f, 0.12f);

        public const float ViewZ = 900f;
        /// <summary>几何外缘超出刀尖轨迹的余量(px),剃刀线居中在此、外晕住外侧,几何边不裁线</summary>
        public const float EdgePadPx = 8f;
        private const int Slices = 40;
        /// <summary>爆发拉长完成的行程比例:p 到此刀尖已钉在剃刀线上</summary>
        public const float StretchDoneP = 0.35f;

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

        //==== 拍表 ====

        /// <summary>
        /// 五拍编排(×sizeMul)。跨度只有终结拍 >180°;倾角逐拍换号,刀从身后劈到身前、再从身后撩到身前;
        /// 刀光=刀扫过的体积:外缘=爆发帧刀尖轨迹(Reach),内缘在刀身 ~45~55% 处;
        /// 静止持刀 0.84 Reach,爆发帧拉长到 Reach(涂抹帧拉长),落位后 3~5 帧回坐——刀尖永远在剃刀线上而不是带的内侧
        /// </summary>
        public static SweepDef BuildBeat(int beat, float aim, int facing, float s, float seed) {
            SweepDef d = beat switch {
                //0 顺斩:身后上方劈到身前下方
                0 => new SweepDef {
                    ThetaStart = -1.22f, ThetaEnd = 0.70f, WindupRad = 0.90f,
                    Reach = 206f * s, RestFrac = 0.84f, Tilt = 0.55f,
                    InnerHead = 0.50f, InnerTail = 0.84f,
                    GatherFrames = 2, StillFrames = 0, BurstFrames = 2, SettleFrames = 2, CollapseFrames = 6, ScarFrames = 12,
                    Overshoot = 1.05f, FlashPeak = 0.70f, FarDim = 0.70f, Opacity = 1f,
                    LeanBack = 0.06f, LeanFwd = 0.10f, StepPx = 2.5f,
                },
                //1 反撩:身后下方撩到身前上方
                1 => new SweepDef {
                    ThetaStart = 1.13f, ThetaEnd = -0.96f, WindupRad = 0.90f,
                    Reach = 221f * s, RestFrac = 0.84f, Tilt = -0.55f,
                    InnerHead = 0.50f, InnerTail = 0.84f,
                    GatherFrames = 2, StillFrames = 0, BurstFrames = 2, SettleFrames = 2, CollapseFrames = 6, ScarFrames = 12,
                    Overshoot = 1.05f, FlashPeak = 0.70f, FarDim = 0.70f, Opacity = 1f,
                    LeanBack = 0.06f, LeanFwd = 0.10f, StepPx = 2.5f,
                },
                //2 横斩:压扁大弧,腹部朝目标
                2 => new SweepDef {
                    ThetaStart = -1.40f, ThetaEnd = 0.96f, WindupRad = 1.10f,
                    Reach = 250f * s, RestFrac = 0.84f, Tilt = 0.35f,
                    InnerHead = 0.48f, InnerTail = 0.84f,
                    GatherFrames = 3, StillFrames = 0, BurstFrames = 2, SettleFrames = 2, CollapseFrames = 7, ScarFrames = 14,
                    Overshoot = 1.05f, FlashPeak = 0.80f, FarDim = 0.70f, Opacity = 1f,
                    LeanBack = 0.10f, LeanFwd = 0.14f, StepPx = 3f,
                },
                //3 重斩:蓄势后一记大上撩,死寂一帧框住爆发
                3 => new SweepDef {
                    ThetaStart = 1.48f, ThetaEnd = -1.13f, WindupRad = 1.40f,
                    Reach = 298f * s, RestFrac = 0.84f, Tilt = -0.75f,
                    InnerHead = 0.46f, InnerTail = 0.82f,
                    GatherFrames = 5, StillFrames = 1, BurstFrames = 3, SettleFrames = 2, CollapseFrames = 8, ScarFrames = 16,
                    Overshoot = 1.05f, FlashPeak = 1f, FarDim = 0.68f, Opacity = 1f,
                    LeanBack = 0.16f, LeanFwd = 0.22f, StepPx = 5f,
                },
                //4 终结:230° 巨弧,起笔在身后头顶、后半沉入身后,压过头顶劈到身前
                _ => new SweepDef {
                    ThetaStart = -2.44f, ThetaEnd = 1.57f, WindupRad = 1.70f,
                    Reach = 344f * s, RestFrac = 0.84f, Tilt = 0.65f,
                    InnerHead = 0.44f, InnerTail = 0.82f,
                    GatherFrames = 6, StillFrames = 2, BurstFrames = 4, SettleFrames = 3, CollapseFrames = 10, ScarFrames = 22,
                    Overshoot = 1.05f, FlashPeak = 1f, FarDim = 0.64f, Opacity = 1f,
                    LeanBack = 0.22f, LeanFwd = 0.30f, StepPx = 7f, HopVy = -3f,
                },
            };
            d.Aim = aim;
            d.Facing = facing;
            d.Seed = seed;
            return d;
        }

        //==== 时间轴 ====

        /// <summary>刀行程 p(lt);蓄势段从上一停驻位插到拉背位由调用方在角度空间处理,此处只给拉背位</summary>
        public static float BladeProgress(in SweepDef d, int lt) {
            float pW = d.WindupP;
            int sweepStart = d.SweepStart;
            if (lt < sweepStart) {
                return pW;
            }
            int k = lt - sweepStart;
            if (k < d.BurstFrames) {
                //出生即全速的缓出:第一帧已铺过大半,末帧冲到过冲位
                float t = (k + 1) / (float)d.BurstFrames;
                return MathHelper.Lerp(pW, d.Overshoot, EaseOutQuad(t));
            }
            int j = k - d.BurstFrames;
            if (j < d.SettleFrames) {
                return MathHelper.Lerp(d.Overshoot, 1f, SmoothStep01((j + 1) / (float)(d.SettleFrames + 1)));
            }
            return 1f;
        }

        /// <summary>
        /// 实体刀半径(轴心→刀尖,px):蓄势/死寂持静止长;爆发帧随行程拉长到 Reach(刀尖钉上剃刀线);
        /// 落位后 SettleFrames+2 帧回坐静止长(体同时在收缩,刀尖退回时始终还在带内)
        /// </summary>
        public static float BladeRadius(in SweepDef d, int lt) {
            float rest = d.RestLen;
            if (lt < d.SweepStart) {
                return rest * 0.94f;
            }
            if (lt < d.CollapseStart) {
                float p = BladeProgress(in d, lt);
                return MathHelper.Lerp(rest, d.Reach, SmoothStep01(p / StretchDoneP));
            }
            float t = (lt - d.CollapseStart + 1) / (float)(d.SettleFrames + 2);
            return MathHelper.Lerp(d.Reach, rest, SmoothStep01(t));
        }

        public static SweepAnim Anim(in SweepDef d, int lt) {
            SweepAnim a = default;
            a.BladeP = BladeProgress(in d, lt);
            a.Alpha = d.Opacity;
            int sweepStart = d.SweepStart;
            int collapseStart = d.CollapseStart;
            int scarStart = d.ScarStart;

            if (lt < sweepStart) {
                a.HeadP = 0f;
                a.TailP = 0f;
                a.BodyAlive = false;
                return a;
            }
            a.InBurst = lt < collapseStart;
            a.HeadP = MathHelper.Clamp(a.BladeP, 0f, 1f);
            if (lt >= collapseStart) {
                a.HeadP = 1f;
            }

            //落位闪:落位帧满值,之后每帧折半
            int sinceLand = lt - d.LandFrame;
            if (sinceLand == 0) {
                a.Flash = d.FlashPeak;
            }
            else if (sinceLand > 0) {
                a.Flash = d.FlashPeak * MathF.Pow(0.5f, sinceLand);
                if (a.Flash < 0.03f) {
                    a.Flash = 0f;
                }
            }

            if (lt < collapseStart) {
                a.TailP = 0f;
                a.BodyAlive = true;
                return a;
            }
            if (lt < scarStart) {
                //体向刀收缩:先慢后快,尾追头
                a.TailP = EaseInQuad((lt - collapseStart + 1) / (float)d.CollapseFrames);
                a.BodyAlive = a.TailP < 0.999f;
                return a;
            }
            //刃痕:体全收(>1 杀残留),线尾先蚀,末四分之一整体淡出兜底
            a.TailP = 1.02f;
            a.BodyAlive = false;
            float scarT = (lt - scarStart + 1) / (float)Math.Max(d.ScarFrames, 1);
            a.Erode = SmoothStep01(scarT);
            a.Alpha *= 1f - SmoothStep01((scarT - 0.75f) / 0.25f);
            return a;
        }

        //==== 投影(单一几何源:刀光/刀身/碰撞共用) ====

        public static float ThetaAt(in SweepDef d, float p) => d.ThetaStart + p * d.Span;

        /// <summary>
        /// 倾斜圆上一点的透视投影(相对轴心偏移):挥动平面绕 aim 轴倾 Tilt,屏面按 Facing 镜像,
        /// z 取未镜像 θ(深度剖面沿笔画固定,不随朝向翻转),+z 朝观者
        /// </summary>
        public static Vector2 Project(in SweepDef d, float theta, float radius, out float z) {
            Vector2 ax = d.Aim.ToRotationVector2();
            Vector2 ay = ax.RotatedBy(MathHelper.PiOver2);
            float ts = theta * d.Facing;
            float cosT = MathF.Cos(d.Tilt);
            float sinT = MathF.Sin(d.Tilt);
            Vector2 planar = ax * MathF.Cos(ts) + ay * MathF.Sin(ts) * cosT;
            z = MathF.Sin(theta) * sinT * radius;
            float k = ViewZ / MathF.Max(ViewZ - z, 220f);
            Vector2 r = planar * radius * k;
            return d.Roll != 0f ? r.RotatedBy(d.Roll) : r;
        }

        /// <summary>z 幅度(px),归一化深度用</summary>
        public static float DepthAmp(in SweepDef d, float radius) => MathF.Abs(radius * MathF.Sin(d.Tilt));

        /// <summary>归一化深度 -1..1(+近)</summary>
        public static float DepthNorm(in SweepDef d, float radius, float z) {
            float amp = DepthAmp(in d, radius);
            return amp > 0.5f ? MathHelper.Clamp(z / amp, -1f, 1f) : 0f;
        }

        /// <summary>u 处内缘刀身比例:起笔收成尖、向刃头加厚(涂抹厚度随速度)</summary>
        public static float InnerFrac(in SweepDef d, float u) {
            float baseFrac = MathHelper.Lerp(d.InnerTail, d.InnerHead, EaseOutQuad(u));
            return MathHelper.Lerp(0.985f, baseFrac, SmoothStep01(u / 0.10f));
        }

        /// <summary>刀光外缘(刀尖轨迹)世界点</summary>
        public static Vector2 OuterAt(in SweepDef d, Vector2 pivot, float p, out float z)
            => pivot + Project(in d, ThetaAt(in d, p), d.Reach, out z);

        /// <summary>刀光内缘世界点</summary>
        public static Vector2 InnerAt(in SweepDef d, Vector2 pivot, float p) {
            float u = MathHelper.Clamp(p, 0f, 1f);
            return pivot + Project(in d, ThetaAt(in d, p), d.Reach * InnerFrac(in d, u), out _);
        }

        /// <summary>实体刀尖世界点,半径由 <see cref="BladeRadius"/> 给</summary>
        public static Vector2 BladeTipAt(in SweepDef d, Vector2 pivot, float p, float radius, out float z)
            => pivot + Project(in d, ThetaAt(in d, p), radius, out z);

        /// <summary>p 处外缘切向单位向量(挥动方向)</summary>
        public static Vector2 TangentAt(in SweepDef d, Vector2 pivot, float p) {
            Vector2 a = OuterAt(in d, pivot, p - 0.02f, out _);
            Vector2 b = OuterAt(in d, pivot, p + 0.02f, out _);
            return (b - a).SafeNormalize(d.Aim.ToRotationVector2());
        }

        //==== 几何构建 ====

        private static readonly VertexPositionColorTexture[] vertexScratch = new VertexPositionColorTexture[Slices * 2];
        private static readonly Vector2[] outerScratch = new Vector2[Slices];
        private static readonly Vector2[] innerScratch = new Vector2[Slices];
        private static readonly float[] zScratch = new float[Slices];
        private static readonly float[] arcScratch = new float[Slices];
        private static readonly float[] bandScratch = new float[Slices];

        /// <summary>整段路径的条带顶点(始终满程,揭开/收缩由 shader 按弧长坐标门控);返回总弧长与最大带宽</summary>
        private static void BuildStrip(in SweepDef d, Vector2 pivot, out float totalArc, out float maxBand) {
            float depthAmp = MathF.Max(DepthAmp(in d, d.Reach), 0.001f);
            totalArc = 0f;
            maxBand = 1f;
            for (int i = 0; i < Slices; i++) {
                float u = i / (float)(Slices - 1);
                float theta = ThetaAt(in d, u);
                outerScratch[i] = pivot + Project(in d, theta, d.Reach, out zScratch[i]);
                innerScratch[i] = pivot + Project(in d, theta, d.Reach * InnerFrac(in d, u), out _);
                if (i > 0) {
                    totalArc += (outerScratch[i] - outerScratch[i - 1]).Length();
                }
                arcScratch[i] = totalArc;
            }
            for (int i = 0; i < Slices; i++) {
                Vector2 dirOut = (outerScratch[i] - innerScratch[i]).SafeNormalize(Vector2.UnitY);
                outerScratch[i] += dirOut * EdgePadPx;
                bandScratch[i] = (outerScratch[i] - innerScratch[i]).Length();
                maxBand = MathF.Max(maxBand, bandScratch[i]);
            }
            float invArc = 1f / MathF.Max(totalArc, 1f);
            for (int i = 0; i < Slices; i++) {
                float s = arcScratch[i] * invArc;
                float zN01 = MathHelper.Clamp(zScratch[i] / depthAmp * 0.5f + 0.5f, 0f, 1f);
                Color data = new((int)(zN01 * 255f), (int)(MathHelper.Clamp(bandScratch[i] / maxBand, 0f, 1f) * 255f), 0, 255);
                vertexScratch[i * 2] = new VertexPositionColorTexture(innerScratch[i].ToVector3(), data, new Vector2(s, 0f));
                vertexScratch[i * 2 + 1] = new VertexPositionColorTexture(outerScratch[i].ToVector3(), data, new Vector2(s, 1f));
            }
        }

        /// <summary>p(路径参数)→弧长归一坐标,按已建表插值</summary>
        private static float ArcCoordOf(float p, float totalArc) {
            if (p <= 0f) {
                return p;   //负值原样传下,shader 门控自然全灭
            }
            if (p >= 1f) {
                return p;   //>1 用于杀体残留
            }
            float f = p * (Slices - 1);
            int i = (int)f;
            float t = f - i;
            float a = arcScratch[i];
            float b = arcScratch[Math.Min(i + 1, Slices - 1)];
            return MathHelper.Lerp(a, b, t) / MathF.Max(totalArc, 1f);
        }

        //==== 绘制 ====

        /// <summary>设备状态 + 采样器绑定 + 帧级公共 uniform,false=资产未就绪</summary>
        public static bool BeginDraw(GraphicsDevice device, out Effect fx
            , out BlendState prevBlend, out RasterizerState prevRaster, out DepthStencilState prevDepth) {
            fx = EffectLoader.OniCrimsonSweep?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            Texture2D soft = CWRAsset.NoiseSoft01?.Value;
            Texture2D brush = CWRAsset.SlashBrush01?.Value;
            prevBlend = device.BlendState;
            prevRaster = device.RasterizerState;
            prevDepth = device.DepthStencilState;
            if (fx == null || noise == null || soft == null || brush == null) {
                return false;
            }

            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.None;
            device.Textures[1] = noise;
            device.SamplerStates[1] = SamplerState.LinearWrap;
            device.Textures[2] = soft;
            device.SamplerStates[2] = SamplerState.LinearWrap;
            device.Textures[3] = brush;
            device.SamplerStates[3] = SamplerState.LinearWrap;

            fx.CurrentTechnique = fx.Techniques["TechSweep"];
            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uEdgePadPx"]?.SetValue(EdgePadPx);
            fx.Parameters["uColHot"]?.SetValue(ColHot);
            fx.Parameters["uColBright"]?.SetValue(ColBright);
            fx.Parameters["uColDeep"]?.SetValue(ColDeep);
            fx.Parameters["uColDark"]?.SetValue(ColDark);
            fx.Parameters["uColEmber"]?.SetValue(ColEmber);
            return true;
        }

        /// <summary>归还设备状态,清掉 1~3 号槽防泄漏到下一个不自绑的 shader</summary>
        public static void EndDraw(GraphicsDevice device
            , BlendState prevBlend, RasterizerState prevRaster, DepthStencilState prevDepth) {
            device.Textures[1] = null;
            device.Textures[2] = null;
            device.Textures[3] = null;
            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
            device.DepthStencilState = prevDepth;
        }

        /// <summary>
        /// 绘制一道刀光;farSel 0=整体 +1=近半侧 -1=远半侧(身后层)<br/>
        /// 爆发/收缩期先画上一帧轮廓的压暗衬层(时间厚度),再画本体
        /// </summary>
        public static void DrawSweep(GraphicsDevice device, Effect fx, in SweepDef d, Vector2 pivot, int lt, float farSel) {
            if (lt < 0 || lt >= d.Life) {
                return;
            }
            SweepAnim a = Anim(in d, lt);
            if (a.Alpha <= 0.012f || a.HeadP <= 0.001f) {
                return;
            }

            BuildStrip(in d, pivot, out float totalArc, out float maxBand);
            fx.Parameters["uStrokeLen"]?.SetValue(totalArc);
            fx.Parameters["uBandPx"]?.SetValue(maxBand);
            fx.Parameters["uSeed"]?.SetValue(d.Seed);
            fx.Parameters["uFarSel"]?.SetValue(d.FarDim > 0f ? farSel : 0f);
            fx.Parameters["uFarDim"]?.SetValue(d.FarDim);

            int prev = lt - 1;
            if (prev >= d.SweepStart && prev < d.ScarStart) {
                SweepAnim b = Anim(in d, prev);
                if (b.HeadP > 0.001f && b.BodyAlive) {
                    SubmitLayer(device, fx, in b, totalArc, layer: 1f);
                }
            }
            SubmitLayer(device, fx, in a, totalArc, layer: 0f);
        }

        private static void SubmitLayer(GraphicsDevice device, Effect fx, in SweepAnim a, float totalArc, float layer) {
            fx.Parameters["uHead"]?.SetValue(ArcCoordOf(a.HeadP, totalArc));
            fx.Parameters["uTail"]?.SetValue(ArcCoordOf(a.TailP, totalArc));
            fx.Parameters["uErode"]?.SetValue(a.Erode);
            fx.Parameters["uFlash"]?.SetValue(layer > 0.5f ? 0f : a.Flash);
            fx.Parameters["uLayer"]?.SetValue(layer);
            fx.Parameters["uFade"]?.SetValue(a.Alpha);
            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, vertexScratch, 0, Slices * 2 - 2);
            }
        }
    }
}
