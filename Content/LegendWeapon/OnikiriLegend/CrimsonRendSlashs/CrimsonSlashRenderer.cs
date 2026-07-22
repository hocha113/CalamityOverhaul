using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs
{
    /// <summary>
    /// 绯红裂空共享刀光渲染,子刀光定义/几何动画/三层异步<br/>
    /// 供 <see cref="CrimsonRendSlash"/> 与 <see cref="CrimsonRendCleave"/> 复用<br/>
    /// 压扁率 HalfY/HalfX、滚转 Rot、FarDim + <see cref="ICrimsonFarDrawable"/> 做远近半侧
    /// </summary>
    internal static class CrimsonSlashRenderer
    {
        /// <summary>子刀光定义(确定性,各端一致)</summary>
        public struct SlashDef
        {
            public int Birth;            //时间轴出生帧
            public int SweepFrames;      //扫开帧数
            public int Life;             //总寿命(相对出生)
            public int ErodeStart;       //侵蚀起点(相对出生)
            public int ErodeFrames;
            public float ColorShiftDelay;
            public float ColorShiftFrames;
            public int DamageStart;      //伤害窗起(相对出生)
            public int DamageEnd;
            public float Mode;           //0=弧形 1=直线
            public float Rot;            //弧:quad 基准角 直:刃方向角
            public float Span;           //弧跨度(弧度)
            public float Thick;          //shader 厚度
            public float HalfX;          //quad 半尺寸(直线=半刃长)
            public float HalfY;          //quad 半尺寸(<HalfX 即透视压扁;直线=半幅宽)
            public float Flip;
            public float Opacity;
            public float FrontGlow;
            public float OffsetAlongAim; //中心沿瞄准偏移
            public float Seed;
            public float TailErode;      //彗星尾蒸发上限(0=不蒸发)
            public float FlashPower;     //全形白闪强度
            public float FarDim;         //>0 远近半侧分层,远半侧压暗并画身后
            public float SweepSnap;      //>0 蓄势-爆发扫掠权重
            public float RazorTailWiden; //剃刀线收笔端展宽
            //==== 水墨旋钮(0=原光润能量) ====
            public float Ink;            //0..1 墨场主权重
            public float FeiBai;         //0..1 飞白干笔(侵蚀期加剧)
            public float Bleed;          //0..1 洇边上限(生命期渐渗)
            public float SplitTail;      //0..1 散锋分叉
        }

        /// <summary>子刀光单帧动画状态</summary>
        public struct SlashAnim
        {
            public float ScaleMul;   //出生爆发+过冲+外扩
            public float RotOffset;  //扫掠后惯性收势
            public float ThickMul;   //薄入→冲击最厚→衰减
            public float TailErode;  //彗星尾蒸发进度
            public float Flash;      //全形白闪
            public float FlowPhase;  //能量沿刃相位
        }

        //==== 调色(白热/亮绯红/深红/暗酒红) ====
        public static readonly Vector3 ColHot = new(1.60f, 1.32f, 1.08f);
        public static readonly Vector3 ColBright = new(1.30f, 0.16f, 0.10f);
        public static readonly Vector3 ColDeep = new(0.62f, 0.05f, 0.07f);
        public static readonly Vector3 ColDark = new(0.16f, 0.015f, 0.035f);

        //==== 缓动 ====

        public static float EaseOutCubic(float x) => 1f - MathF.Pow(1f - MathHelper.Clamp(x, 0f, 1f), 3f);

        /// <summary>带过冲缓出,峰值 ~1.05 回落 1</summary>
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

        //==== 生命周期采样 ====

        public static float Sweep(in SlashDef d, int lt) {
            float t = lt / (float)d.SweepFrames;
            return d.SweepSnap > 0f
                ? MathHelper.Lerp(EaseOutCubic(t), SweepAnticipate(t), d.SweepSnap)
                : EaseOutCubic(t);
        }

        /// <summary>蓄势-爆发扫掠,前 60% 缓推揭开 30%,滞至 0.75 后末段瞬间完成<br/>
        /// 伤害窗/爆发音对齐爆发起点,帧数 ≈ SweepFrames * 0.75</summary>
        public static float SweepAnticipate(float t) {
            t = MathHelper.Clamp(t, 0f, 1f);
            const float creepEnd = 0.60f;   //蓄势段末
            const float holdEnd = 0.75f;    //滞帧末=爆发起点
            const float creepAmt = 0.30f;   //蓄势揭开比例
            if (t < creepEnd) {
                return creepAmt * EaseOutCubic(t / creepEnd);
            }
            if (t < holdEnd) {
                return creepAmt;
            }
            return creepAmt + (1f - creepAmt) * EaseOutCubic((t - holdEnd) / (1f - holdEnd));
        }

        public static float Erode(in SlashDef d, int lt) => SmoothStep01((lt - d.ErodeStart) / (float)d.ErodeFrames);

        public static float ColorShift(in SlashDef d, int lt) => MathHelper.Clamp((lt - d.ColorShiftDelay) / d.ColorShiftFrames, 0f, 1f);

        public static float Opacity(in SlashDef d, int lt) => d.Opacity * (1f - MathHelper.Clamp((lt - (d.Life - 6)) / 6f, 0f, 1f));

        public static float FrontGlow(in SlashDef d, int lt) => lt <= d.SweepFrames + 1
            ? d.FrontGlow
            : d.FrontGlow * MathF.Max(0f, 1f - (lt - d.SweepFrames - 1) / 5f);

        /// <summary>几何动画包,形变随生命期演进</summary>
        public static SlashAnim GetAnim(in SlashDef d, int lt) {
            float lifeT = MathHelper.Clamp(lt / (float)d.Life, 0f, 1f);

            //出生爆发,62%→easeOutBack~104%回落,再缓慢外扩
            float burstT = MathHelper.Clamp(lt / (d.SweepFrames + 2f), 0f, 1f);
            float scale = MathHelper.Lerp(0.62f, 1f, EaseOutBack(burstT)) + 0.07f * lifeT;

            //惯性收势,扫掠后沿挥动方向减速旋转
            float followT = MathHelper.Clamp((lt - d.SweepFrames) / 14f, 0f, 1f);
            float rotOff = d.Flip * 0.13f * (1f - (1f - followT) * (1f - followT));

            //厚度呼吸,薄入→冲击最厚→消散变薄
            float thickIn = EaseOutCubic(lt / (d.SweepFrames + 2f));
            float thickMul = MathHelper.Lerp(0.68f, 1.12f, thickIn)
                * (1f - 0.42f * SmoothStep01((lifeT - 0.45f) / 0.55f));

            //彗星尾,扫掠完成起笔端向前蒸发
            float tail = d.TailErode * SmoothStep01((lt - d.SweepFrames) / (d.Life * 0.72f));

            //全形白闪,张开瞬间过曝 1~2 帧速落
            float ft = lt - d.SweepFrames;
            float flash = ft < 0f ? 0f : ft <= 1f ? 1f : MathF.Pow(0.52f, ft - 1f);
            if (flash < 0.02f) {
                flash = 0f;
            }
            flash *= d.FlashPower;

            //能量沿刃奔涌
            float flowPhase = 0.62f * EaseOutCubic(lt / 15f);

            return new SlashAnim {
                ScaleMul = scale, RotOffset = rotOff, ThickMul = thickMul,
                TailErode = tail, Flash = flash, FlowPhase = flowPhase,
            };
        }

        /// <summary>刀光中线静态点,忽略出生缩放/惯性滚转/厚度呼吸(实体刀路径用)</summary>
        public static Vector2 StaticPointAt(in SlashDef d, Vector2 center, float uc) {
            Vector2 ax = d.Rot.ToRotationVector2();
            Vector2 ay = ax.RotatedBy(MathHelper.PiOver2);
            if (d.Mode > 0.5f) {
                return center + ax * (uc * 2f - 1f) * d.HalfX * 0.90f;
            }
            float env = MathF.Sin(MathF.Pow(uc, 1.85f) * MathF.PI);
            float w = d.Thick * MathF.Pow(MathF.Max(env, 0.0001f), 0.72f);
            float rFrac = 0.90f - w * 0.5f;
            float phi = d.Flip * (uc - 0.5f) * d.Span;
            return center + ax * MathF.Cos(phi) * rFrac * d.HalfX + ay * MathF.Sin(phi) * rFrac * d.HalfY;
        }

        /// <summary>刀光中线点,uc=0..1 沿刃,含几何动画</summary>
        public static Vector2 PointAt(in SlashDef d, Vector2 center, float uc, int lt) {
            SlashAnim anim = GetAnim(in d, lt);
            Vector2 ax = (d.Rot + anim.RotOffset).ToRotationVector2();
            Vector2 ay = ax.RotatedBy(MathHelper.PiOver2);
            float hx = d.HalfX * anim.ScaleMul;
            float hy = d.HalfY * anim.ScaleMul;
            if (d.Mode > 0.5f) {
                return center + ax * (uc * 2f - 1f) * hx * 0.90f;
            }
            float env = MathF.Sin(MathF.Pow(uc, 1.85f) * MathF.PI);
            float w = d.Thick * anim.ThickMul * MathF.Pow(MathF.Max(env, 0.0001f), 0.72f);
            float rFrac = 0.90f - w * 0.5f;
            float phi = d.Flip * (uc - 0.5f) * d.Span;
            return center + ax * MathF.Cos(phi) * rFrac * hx + ay * MathF.Sin(phi) * rFrac * hy;
        }

        /// <summary>设备状态 + 帧级公共 uniform,false=资产未就绪</summary>
        public static bool BeginDraw(GraphicsDevice device, out Effect fx
            , out BlendState prevBlend, out RasterizerState prevRaster, out DepthStencilState prevDepth) {
            fx = EffectLoader.OniCrimsonSlash?.Value;
            Texture2D brush = CWRAsset.SlashBrush01?.Value;
            Texture2D noise = CWRAsset.NoiseSoft01?.Value;
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
            fx.Parameters["uColHot"]?.SetValue(ColHot);
            fx.Parameters["uColBright"]?.SetValue(ColBright);
            fx.Parameters["uColDeep"]?.SetValue(ColDeep);
            fx.Parameters["uColDark"]?.SetValue(ColDark);
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

        /// <summary>三层异步,软辉光垫底(滞后2帧)→主体→白热薄条(超前1帧)<br/>
        /// farSel 0=整体 +1=近半侧 -1=远半侧</summary>
        public static void DrawThreeLayers(GraphicsDevice device, Effect fx, in SlashDef d
            , Vector2 center, int lt, float farSel) {
            //主体色带
            DrawLayer(device, fx, in d, center, lt, farSel
                , opacityMul: 1f, thickMul: 1f, scaleMul: 1f
                , erodeBias: 0f, frontMul: 1f, flashMul: 1f, forceHot: false);

            //白热核心薄条,超前 1 帧,贴锋利侧
            DrawLayer(device, fx, in d, center, Math.Min(lt + 1, d.Life - 1), farSel
                , opacityMul: 0.92f, thickMul: 0.42f, scaleMul: 1f
                , erodeBias: 0f, frontMul: 1.25f, flashMul: 1f, forceHot: true);
        }

        /// <summary>单层绘制,按 lt 采样生命周期与几何后提交 quad</summary>
        private static void DrawLayer(GraphicsDevice device, Effect fx, in SlashDef d
            , Vector2 center, int lt, float farSel
            , float opacityMul, float thickMul, float scaleMul, float erodeBias
            , float frontMul, float flashMul, bool forceHot) {
            if (lt < 0 || lt >= d.Life) {
                return;
            }
            float opacity = Opacity(in d, lt) * opacityMul;
            if (opacity <= 0.012f) {
                return;
            }

            SlashAnim anim = GetAnim(in d, lt);

            Vector2 axisX = (d.Rot + anim.RotOffset).ToRotationVector2();
            Vector2 axisY = axisX.RotatedBy(MathHelper.PiOver2);
            float hx = d.HalfX * anim.ScaleMul * scaleMul;
            float hy = d.HalfY * anim.ScaleMul * scaleMul;

            //远近半侧,世界"屏幕上方"映到 quad uv(非等比需按轴归一)
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
            fx.Parameters["uSweep"]?.SetValue(Sweep(in d, lt));
            fx.Parameters["uErode"]?.SetValue(MathHelper.Clamp(Erode(in d, lt) + erodeBias, 0f, 1f));
            fx.Parameters["uTailErode"]?.SetValue(anim.TailErode);
            fx.Parameters["uFlash"]?.SetValue(anim.Flash * flashMul);
            fx.Parameters["uFlowPhase"]?.SetValue(anim.FlowPhase);
            fx.Parameters["uColorShift"]?.SetValue(forceHot ? 0f : ColorShift(in d, lt));
            fx.Parameters["uOpacity"]?.SetValue(opacity);
            fx.Parameters["uFlip"]?.SetValue(d.Flip);
            fx.Parameters["uSeed"]?.SetValue(d.Seed);
            fx.Parameters["uArcSpan"]?.SetValue(d.Span > 0f ? d.Span : 1f);
            fx.Parameters["uThick"]?.SetValue(d.Thick * anim.ThickMul * thickMul);
            fx.Parameters["uFrontGlow"]?.SetValue(FrontGlow(in d, lt) * frontMul);
            fx.Parameters["uFarSel"]?.SetValue(d.FarDim > 0f ? farSel : 0f);
            fx.Parameters["uFarDim"]?.SetValue(d.FarDim);
            fx.Parameters["uFarDirLocal"]?.SetValue(farDirLocal);
            fx.Parameters["uRazorTailWiden"]?.SetValue(d.RazorTailWiden);
            //水墨,白热薄条关掉墨相只留散锋;飞白随侵蚀加剧,洇边随生命期渐渗
            fx.Parameters["uInk"]?.SetValue(forceHot ? 0f : d.Ink);
            fx.Parameters["uFeiBai"]?.SetValue(forceHot ? 0f
                : d.FeiBai * (0.60f + 0.40f * Erode(in d, lt)));
            fx.Parameters["uBleed"]?.SetValue(forceHot ? 0f
                : d.Bleed * SmoothStep01(lt / (d.Life * 0.45f)));
            fx.Parameters["uSplitTail"]?.SetValue(d.SplitTail);

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

    /// <summary>玩家绘制前可提交远半侧刀光</summary>
    internal interface ICrimsonFarDrawable
    {
        /// <summary>绘制远半侧刀光,自管设备状态</summary>
        void DrawFarSlashes();
    }

    /// <summary>玩家绘制前收集 <see cref="ICrimsonFarDrawable"/> 提交远半侧</summary>
    internal sealed class CrimsonFarLayerRender : RenderHandle
    {
        public override float Weight => 1.05f;

        public override void DrawBeforePlayers(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.gameMenu) {
                return;
            }
            Projectile[] projectiles = Main.projectile;
            for (int i = 0; i < projectiles.Length; i++) {
                Projectile p = projectiles[i];
                if (!p.active || p.ModProjectile is not ICrimsonFarDrawable far) {
                    continue;
                }
                far.DrawFarSlashes();
            }
        }
    }
}
