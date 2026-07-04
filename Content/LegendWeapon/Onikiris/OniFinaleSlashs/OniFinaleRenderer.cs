using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.Onikiris.OniFinaleSlashs
{
    /// <summary>
    /// 终之太刀共用刀光渲染机制：静态几何(<see cref="BladeDef"/>) 与逐帧动态量(<see cref="BladeState"/>)
    /// 分离 —— 环斩/终斩走 <see cref="ComputeState"/> 的标准生命周期，
    /// 直痕的"闪现-定格-引爆"外控时间轴则自行合成 state 后驱动同一套绘制。<br/>
    /// 与 <see cref="CrimsonRendSlashs.CrimsonSlashRenderer"/> 的分工差异：调色逐刀传入
    /// （绯红→鬼火白紫升调），远近半侧双 pass 为立体环斩的常开路径
    /// </summary>
    internal static class OniFinaleRenderer
    {
        /// <summary>四段调色板（白热核心/主亮色/深色/暗描边）</summary>
        public struct BladePalette
        {
            public Vector3 Hot;
            public Vector3 Bright;
            public Vector3 Deep;
            public Vector3 Dark;

            /// <summary>鬼切本命绯红（与绯红裂空斩同源）</summary>
            public static readonly BladePalette Crimson = new() {
                Hot = new Vector3(1.60f, 1.32f, 1.08f),
                Bright = new Vector3(1.30f, 0.16f, 0.10f),
                Deep = new Vector3(0.62f, 0.05f, 0.07f),
                Dark = new Vector3(0.16f, 0.015f, 0.035f),
            };

            /// <summary>鬼火白紫：大招升调的终点色，终斩独占的"烧穿常态"</summary>
            public static readonly BladePalette OniFire = new() {
                Hot = new Vector3(1.58f, 1.42f, 1.78f),
                Bright = new Vector3(0.98f, 0.42f, 1.62f),
                Deep = new Vector3(0.40f, 0.11f, 0.86f),
                Dark = new Vector3(0.09f, 0.028f, 0.21f),
            };

            /// <summary>升调采样：t=0 绯红 → t=1 鬼火白紫</summary>
            public static BladePalette Escalate(float t) {
                t = MathHelper.Clamp(t, 0f, 1f);
                return new BladePalette {
                    Hot = Vector3.Lerp(Crimson.Hot, OniFire.Hot, t),
                    Bright = Vector3.Lerp(Crimson.Bright, OniFire.Bright, t),
                    Deep = Vector3.Lerp(Crimson.Deep, OniFire.Deep, t),
                    Dark = Vector3.Lerp(Crimson.Dark, OniFire.Dark, t),
                };
            }
        }

        /// <summary>刀光静态定义（确定性数据，各端一致）</summary>
        public struct BladeDef
        {
            public int SweepFrames;      //扫开帧数
            public int Life;             //总寿命（相对出生；外控时间轴的直痕由弹幕自行管理）
            public int ErodeStart;       //侵蚀起点（相对出生）
            public int ErodeFrames;
            public float ColorShiftDelay;
            public float ColorShiftFrames;
            public int DamageStart;      //伤害窗口（相对出生）
            public int DamageEnd;
            public float Mode;           //0=弧形环斩 1=直线激光
            public float Rot;            //弧:quad 基准角（含滚转） 直:刃方向角
            public float Span;           //弧跨度（弧度，须<2π）
            public float Thick;          //shader 厚度
            public float HalfX;          //quad 半尺寸（直线=半刃长）
            public float HalfY;          //quad 半尺寸（<HalfX 即透视压扁；直线=半幅宽）
            public float Flip;
            public float Opacity;
            public float FrontGlow;
            public float Seed;
            public float TailErode;      //彗星尾定向蒸发强度上限（0=不蒸发）
            public float FlashPower;     //全形白闪帧强度
            public float FarDim;         //>0 = 启用远近半侧分层：远半侧压暗系数并绘制于玩家身后
            public float SweepSnap;      //>0 = 蓄势-爆发扫掠曲线权重
            public float RazorTailWiden; //剃刀线向收笔端展宽强度
            public BladePalette Palette;
        }

        /// <summary>刀光单帧动态量：几何动画 + 生命周期采样的合成包，可由弹幕外控改写</summary>
        public struct BladeState
        {
            public float Sweep;      //0..1 扫掠揭开
            public float Erode;      //0..1 整体侵蚀
            public float TailErode;  //0..1 彗星尾蒸发
            public float ColorShift; //0..1 亮→暗压暗（直痕余烬态的载体）
            public float Flash;      //全形白闪
            public float Opacity;
            public float FrontGlow;
            public float FlowPhase;  //能量沿刃奔涌相位
            public float ScaleMul;   //出生爆发+过冲+缓慢外扩
            public float RotOffset;  //扫掠后惯性收势旋转
            public float ThickMul;   //薄入→冲击帧最厚→衰减
        }

        //==== 缓动 ====

        public static float EaseOutCubic(float x) => 1f - MathF.Pow(1f - MathHelper.Clamp(x, 0f, 1f), 3f);

        /// <summary>带过冲的缓出（尺寸爆发"弹"出的关键曲线，峰值 ~1.05 后回落 1）</summary>
        public static float EaseOutBack(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float xm = x - 1f;
            return 1f + c3 * xm * xm * xm + c1 * xm * xm;
        }

        public static float SmoothStep01(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        /// <summary>蓄势-爆发扫掠曲线：前 60% 时间缓推只揭开 30% 弧长，
        /// 滞一拍后末 25% 时间瞬间完成；爆发起点 ≈ SweepFrames * 0.75</summary>
        public static float SweepAnticipate(float t) {
            t = MathHelper.Clamp(t, 0f, 1f);
            const float creepEnd = 0.60f;
            const float holdEnd = 0.75f;
            const float creepAmt = 0.30f;
            if (t < creepEnd) {
                return creepAmt * EaseOutCubic(t / creepEnd);
            }
            if (t < holdEnd) {
                return creepAmt;
            }
            return creepAmt + (1f - creepAmt) * EaseOutCubic((t - holdEnd) / (1f - holdEnd));
        }

        //==== 标准生命周期 ====

        public static float Sweep(in BladeDef d, int lt) {
            float t = lt / (float)d.SweepFrames;
            return d.SweepSnap > 0f
                ? MathHelper.Lerp(EaseOutCubic(t), SweepAnticipate(t), d.SweepSnap)
                : EaseOutCubic(t);
        }

        /// <summary>标准生命周期：从 (def, lt) 合成本帧动态量，环斩/终斩直接用；
        /// 直痕拿到结果后按定格/引爆需求改写字段再提交绘制</summary>
        public static BladeState ComputeState(in BladeDef d, int lt) {
            float lifeT = MathHelper.Clamp(lt / (float)d.Life, 0f, 1f);

            //出生爆发：62% 尺寸起步，easeOutBack 过冲回落，随后全程缓慢外扩
            float burstT = MathHelper.Clamp(lt / (d.SweepFrames + 2f), 0f, 1f);
            float scale = MathHelper.Lerp(0.62f, 1f, EaseOutBack(burstT)) + 0.07f * lifeT;

            //惯性收势：扫掠结束后沿挥动方向继续减速旋转
            float followT = MathHelper.Clamp((lt - d.SweepFrames) / 14f, 0f, 1f);
            float rotOff = d.Flip * 0.13f * (1f - (1f - followT) * (1f - followT));

            //厚度呼吸：薄入 → 冲击帧最厚 → 消散期变薄
            float thickIn = EaseOutCubic(lt / (d.SweepFrames + 2f));
            float thickMul = MathHelper.Lerp(0.68f, 1.12f, thickIn)
                * (1f - 0.42f * SmoothStep01((lifeT - 0.45f) / 0.55f));

            //全形白闪帧：完全张开瞬间过曝 1~2 帧，速落
            float ft = lt - d.SweepFrames;
            float flash = ft < 0f ? 0f : ft <= 1f ? 1f : MathF.Pow(0.52f, ft - 1f);
            if (flash < 0.02f) {
                flash = 0f;
            }

            return new BladeState {
                Sweep = Sweep(in d, lt),
                Erode = SmoothStep01((lt - d.ErodeStart) / (float)d.ErodeFrames),
                TailErode = d.TailErode * SmoothStep01((lt - d.SweepFrames) / (d.Life * 0.72f)),
                ColorShift = MathHelper.Clamp((lt - d.ColorShiftDelay) / d.ColorShiftFrames, 0f, 1f),
                Flash = flash * d.FlashPower,
                Opacity = d.Opacity * (1f - MathHelper.Clamp((lt - (d.Life - 6)) / 6f, 0f, 1f)),
                FrontGlow = lt <= d.SweepFrames + 1
                    ? d.FrontGlow
                    : d.FrontGlow * MathF.Max(0f, 1f - (lt - d.SweepFrames - 1) / 5f),
                FlowPhase = 0.62f * EaseOutCubic(lt / 15f),
                ScaleMul = scale,
                RotOffset = rotOff,
                ThickMul = thickMul,
            };
        }

        /// <summary>刀光带中线上一点：uc=0..1 沿刃，几何随 state 缩放/滚转</summary>
        public static Vector2 PointAt(in BladeDef d, in BladeState s, Vector2 center, float uc) {
            Vector2 ax = (d.Rot + s.RotOffset).ToRotationVector2();
            Vector2 ay = ax.RotatedBy(MathHelper.PiOver2);
            float hx = d.HalfX * s.ScaleMul;
            float hy = d.HalfY * s.ScaleMul;
            if (d.Mode > 0.5f) {
                return center + ax * (uc * 2f - 1f) * hx * 0.90f;
            }
            float env = MathF.Sin(MathF.Pow(uc, 1.85f) * MathF.PI);
            float w = d.Thick * s.ThickMul * MathF.Pow(MathF.Max(env, 0.0001f), 0.72f);
            float rFrac = 0.90f - w * 0.5f;
            float phi = d.Flip * (uc - 0.5f) * d.Span;
            return center + ax * MathF.Cos(phi) * rFrac * hx + ay * MathF.Sin(phi) * rFrac * hy;
        }

        //==== 绘制 ====

        /// <summary>设备状态 + 帧级公共 uniform；返回 false 表示资产未就绪</summary>
        public static bool BeginDraw(GraphicsDevice device, out Effect fx
            , out BlendState prevBlend, out RasterizerState prevRaster, out DepthStencilState prevDepth) {
            fx = EffectLoader.OniFinaleBlade?.Value;
            Texture2D brush = OnikiriAssets.SlashBrush01?.Value;
            Texture2D noise = OnikiriAssets.NoiseSoft01?.Value;
            prevBlend = device.BlendState;
            prevRaster = device.RasterizerState;
            prevDepth = device.DepthStencilState;
            if (fx == null || brush == null || noise == null) {
                return false;
            }

            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.None;

            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uBrushTex"]?.SetValue(brush);
            fx.Parameters["uNoiseTex"]?.SetValue(noise);
            return true;
        }

        public static void EndDraw(GraphicsDevice device
            , BlendState prevBlend, RasterizerState prevRaster, DepthStencilState prevDepth) {
            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
            device.DepthStencilState = prevDepth;
        }

        /// <summary>双层异步结构：主体色带 + 白热核心薄条（不随生命期压暗、前缘增益）。
        /// farSel：0=整体 +1=仅近半侧 -1=仅远半侧（配合玩家遮挡分层）</summary>
        public static void DrawBladeLayers(GraphicsDevice device, Effect fx, in BladeDef d
            , in BladeState s, Vector2 center, float farSel) {
            //主体色带
            DrawBlade(device, fx, in d, in s, center, farSel
                , opacityMul: 1f, thickMul: 1f, frontMul: 1f, forceHot: false);

            //白热核心薄条：贴锋利侧，前缘增益领跑
            BladeState core = s;
            core.ColorShift = 0f;
            core.Opacity = s.Opacity * 0.92f;
            DrawBlade(device, fx, in d, in core, center, farSel
                , opacityMul: 1f, thickMul: 0.42f, frontMul: 1.25f, forceHot: true);
        }

        /// <summary>单层绘制：以 (def, state) 提交 quad，调色取自 def.Palette</summary>
        public static void DrawBlade(GraphicsDevice device, Effect fx, in BladeDef d
            , in BladeState s, Vector2 center, float farSel
            , float opacityMul, float thickMul, float frontMul, bool forceHot) {
            float opacity = s.Opacity * opacityMul;
            if (opacity <= 0.012f) {
                return;
            }

            Vector2 axisX = (d.Rot + s.RotOffset).ToRotationVector2();
            Vector2 axisY = axisX.RotatedBy(MathHelper.PiOver2);
            float hx = d.HalfX * s.ScaleMul;
            float hy = d.HalfY * s.ScaleMul;

            //远近半侧选择方向：世界"屏幕上方"映射到 quad uv 空间（非等比 quad 需按轴分量归一）
            Vector2 farDirLocal = Vector2.Zero;
            if (d.FarDim > 0f && farSel != 0f) {
                Vector2 worldUp = new(0f, -1f);
                farDirLocal = new Vector2(Vector2.Dot(worldUp, axisX) / MathF.Max(hx, 1f)
                    , Vector2.Dot(worldUp, axisY) / MathF.Max(hy, 1f));
                if (farDirLocal.LengthSquared() > 1e-8f) {
                    farDirLocal.Normalize();
                }
            }

            fx.Parameters["uMode"]?.SetValue(d.Mode);
            fx.Parameters["uSweep"]?.SetValue(s.Sweep);
            fx.Parameters["uErode"]?.SetValue(MathHelper.Clamp(s.Erode, 0f, 1f));
            fx.Parameters["uTailErode"]?.SetValue(s.TailErode);
            fx.Parameters["uFlash"]?.SetValue(s.Flash);
            fx.Parameters["uFlowPhase"]?.SetValue(s.FlowPhase);
            fx.Parameters["uColorShift"]?.SetValue(forceHot ? 0f : s.ColorShift);
            fx.Parameters["uOpacity"]?.SetValue(opacity);
            fx.Parameters["uFlip"]?.SetValue(d.Flip);
            fx.Parameters["uSeed"]?.SetValue(d.Seed);
            fx.Parameters["uArcSpan"]?.SetValue(d.Span > 0f ? d.Span : 1f);
            fx.Parameters["uThick"]?.SetValue(d.Thick * s.ThickMul * thickMul);
            fx.Parameters["uFrontGlow"]?.SetValue(s.FrontGlow * frontMul);
            fx.Parameters["uFarSel"]?.SetValue(d.FarDim > 0f ? farSel : 0f);
            fx.Parameters["uFarDim"]?.SetValue(d.FarDim);
            fx.Parameters["uFarDirLocal"]?.SetValue(farDirLocal);
            fx.Parameters["uRazorTailWiden"]?.SetValue(d.RazorTailWiden);
            fx.Parameters["uColHot"]?.SetValue(d.Palette.Hot);
            fx.Parameters["uColBright"]?.SetValue(d.Palette.Bright);
            fx.Parameters["uColDeep"]?.SetValue(d.Palette.Deep);
            fx.Parameters["uColDark"]?.SetValue(d.Palette.Dark);

            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture((center - axisX * hx - axisY * hy).ToVector3(), Color.White, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture((center + axisX * hx - axisY * hy).ToVector3(), Color.White, new Vector2(1f, 0f));
            verts[2] = new VertexPositionColorTexture((center - axisX * hx + axisY * hy).ToVector3(), Color.White, new Vector2(0f, 1f));
            verts[3] = new VertexPositionColorTexture((center + axisX * hx + axisY * hy).ToVector3(), Color.White, new Vector2(1f, 1f));

            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }
        }
    }
}
