using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFinaleSlashs
{
    /// <summary>终之太刀碎屏资源（承村正次元斩衣钵：三角位移贴片 + 径向模糊 + RGB 色差）</summary>
    [VaultLoaden(CWRConstant.Masking)]
    internal class OniFinaleShatterAssets
    {
        /// <summary>三角形碎片贴图（写位移场：R=折射角 G=强度）</summary>
        public static Texture2D Triangle { get; private set; }

        /// <summary>径向模糊 shader（strength / center）</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect RadialBlur { get; private set; }

        /// <summary>RGB 色差分离 shader（offsetStrength）</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect ScreenColorMess { get; private set; }
    }

    /// <summary>终之太刀碎屏系统(纯客户端视觉)，技法承自村正次元斩（PowerSF 位移场折射碎镜）：
    /// 屏幕不是被掰成几块飞走，而是像透过碎掉的棱镜看世界——
    /// 每落一刀就在落点附近碎开几面三角折射面，随乱舞→死寂逐步积攒（空间早已被斩碎，
    /// 只是还没人察觉）；死寂期径向模糊向刀线中心蓄力；纳刀爆发的一瞬碎面急速收缩闭合、
    /// RGB 色差打峰后退潮——被斩碎的空间"啪"地合拢，只剩下那道真正的伤口</summary>
    internal static class OniFinaleShatter
    {
        private const int MaxFacets = 40;
        /// <summary>碎面出生的迸开帧数（easeOutBack 弹出）</summary>
        private const int GrowFrames = 3;
        /// <summary>爆发后碎面每帧收缩量（村正原值 0.17）</summary>
        private const float BurstShrink = 0.17f;
        /// <summary>PowerSF 位移强度（村正原值 0.04）</summary>
        internal const float TwistStrength = 0.045f;
        /// <summary>无主兜底寿命，独立调试/主控失踪时碎面自行收场</summary>
        private const int FailsafeAge = 300;

        private struct Facet
        {
            public Vector2 WorldPos;
            public float Rotate;
            public float MaxScale;
            public Color MapColor;   //R=折射角编码 G=折射强度 B=0

            public int Age;
            public bool Shrinking;
            public float Scale;
        }

        private static readonly List<Facet> facets = new(MaxFacets + 8);
        private static float charge;        //死寂蓄力 0..1（主控每帧推送、停推自然回落）

        private static float burstBlur;     //爆发径向模糊，脉冲后线性退潮

        private static float burstColorSep; //爆发 RGB 色差

        private static bool bursting;
        private static Vector2 focusWorld;
        private static uint lastTick;

        public static bool Active => facets.Count > 0 || charge > 0.01f
            || burstBlur > 0.004f || burstColorSep > 0.0004f;

        /// <summary>演出焦点离本地视野过远时忽略（多人下远处玩家不承受全屏后效）</summary>
        private static bool NearLocalView(Vector2 worldPos) {
            if (VaultUtils.isServer) {
                return false;
            }
            Vector2 viewCenter = Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
            return Vector2.Distance(viewCenter, worldPos) < 2800f;
        }

        /// <summary>落刀碎面：在落点附近迸开 count 面三角折射面，空间随每一刀逐步碎掉</summary>
        public static void AddFacets(Vector2 worldCenter, int count, float sizeMul) {
            if (!NearLocalView(worldCenter)) {
                return;
            }
            for (int i = 0; i < count && facets.Count < MaxFacets; i++) {
                facets.Add(new Facet {
                    WorldPos = worldCenter + Main.rand.NextVector2Circular(260f, 200f),
                    Rotate = Main.rand.NextFloat(MathHelper.TwoPi),
                    MaxScale = Main.rand.NextFloat(1.0f, 2.6f) * sizeMul,
                    //颜色即位移场：红通道=折射角，绿通道=强度（PowerSF 约定）

                    MapColor = new Color(Main.rand.Next(40, 256), Main.rand.Next(40, 256), 0),
                    Age = 0,
                    Scale = 0f,
                });
            }
        }

        /// <summary>死寂蓄力（主控每帧推送）、径向模糊与轻微色差向刀线中心聚拢</summary>
        public static void PushCharge(Vector2 focus, float chargeT) {
            if (!NearLocalView(focus)) {
                return;
            }
            focusWorld = focus;
            charge = MathHelper.Clamp(MathF.Max(charge, chargeT), 0f, 1f);
        }

        /// <summary>纳刀爆发：碎面急速收缩闭合，径向模糊/色差打峰后退潮</summary>
        public static void Burst(Vector2 focus) {
            if (!NearLocalView(focus)) {
                return;
            }
            focusWorld = focus;
            bursting = true;
            burstBlur = 0.40f;
            burstColorSep = 0.016f;
            for (int i = 0; i < facets.Count; i++) {
                Facet f = facets[i];
                f.Shrinking = true;
                facets[i] = f;
            }
        }

        public static void Clear() {
            facets.Clear();
            charge = 0f;
            burstBlur = 0f;
            burstColorSep = 0f;
            bursting = false;
        }

        /// <summary>渲染帧推演（帧防重入 + 暂停冻结）</summary>
        internal static void Tick() {
            if (lastTick == Main.GameUpdateCount || Main.gamePaused) {
                return;
            }
            lastTick = Main.GameUpdateCount;

            charge *= 0.88f;
            if (charge < 0.01f) {
                charge = 0f;
            }
            if (bursting) {
                burstBlur = MathF.Max(0f, burstBlur - 0.02f);
                burstColorSep = MathF.Max(0f, burstColorSep - 0.0011f);
                if (burstBlur <= 0f && burstColorSep <= 0f && facets.Count == 0) {
                    bursting = false;
                }
            }

            for (int i = facets.Count - 1; i >= 0; i--) {
                Facet f = facets[i];
                f.Age++;
                if (f.Age > FailsafeAge) {
                    f.Shrinking = true;
                }
                f.Scale = f.Shrinking
                    ? f.Scale - BurstShrink
                    : f.MaxScale * OniFinaleRenderer.EaseOutBack(MathHelper.Clamp(f.Age / (float)GrowFrames, 0f, 1f));
                if (f.Scale <= 0f) {
                    facets.RemoveAt(i);
                    continue;
                }
                facets[i] = f;
            }
        }

        /// <summary>径向模糊强度（蓄力≤0.22，爆发脉冲 0.40 退潮）</summary>
        internal static float BlurStrength => MathF.Max(charge * 0.22f, burstBlur);
        /// <summary>RGB 色差强度（蓄力微量渗入，爆发打峰）</summary>
        internal static float ColorSepStrength => MathF.Max(charge * 0.005f, burstColorSep);
        internal static Vector2 FocusWorld => focusWorld;
        internal static bool HasFacets => facets.Count > 0;

        /// <summary>位移场写入：碎面三角以世界锚点入图（跟随镜头），PointWrap 保硬折射断层</summary>
        internal static void DrawFacetMap(SpriteBatch sb) {
            Texture2D tri = OniFinaleShatterAssets.Triangle;
            if (tri == null) {
                return;
            }
            Vector2 origin = tri.Size() * 0.5f;
            foreach (Facet f in facets) {
                sb.Draw(tri, f.WorldPos - Main.screenPosition, null, f.MapColor
                    , f.Rotate, origin, f.Scale, SpriteEffects.None, 0);
            }
        }
    }

    /// <summary>碎屏渲染，权重 1.093：晚于 OniFinaleRender(1.09) 的暗场/裂屏/切片取其结果。
    /// 管线承村正次元斩：位移场(ScreenTargets[0]) → 径向模糊进 swap → PowerSF 折射合成 → 爆发窗色差回写</summary>
    internal sealed class OniFinaleShatterRender : RenderHandle
    {
        public override float Weight => 1.093f;
        /// <summary>1 号槽 = 碎面位移场（R=折射角 G=强度）</summary>
        public override int ScreenSlot => 1;

        public override void OnResolutionChanged(Vector2 screenSize) => OniFinaleShatter.Clear();

        public override void EndCaptureDraw(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (!OniFinaleShatter.Active) {
                return;
            }
            if (RenderQualitySafety.ScreenTargetUnavailable()
                || screenSwap == null || Main.screenTarget == null
                || ScreenTargets == null || ScreenTargets.Length == 0 || ScreenTargets[0] == null) {
                OniFinaleShatter.Clear();
                return;
            }
            Effect twist = EffectLoader.PowerSFShader?.Value;
            if (twist == null) {
                OniFinaleShatter.Clear();
                return;
            }

            OniFinaleShatter.Tick();

            BlendState prevBlend = graphicsDevice.BlendState;
            RasterizerState prevRaster = graphicsDevice.RasterizerState;
            DepthStencilState prevDepth = graphicsDevice.DepthStencilState;
            try {
                Compose(spriteBatch, graphicsDevice, screenSwap, twist);
            }
            finally {
                graphicsDevice.BlendState = prevBlend;
                graphicsDevice.RasterizerState = prevRaster;
                graphicsDevice.DepthStencilState = prevDepth;
            }
        }

        private void Compose(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D swap, Effect twist) {
            //Pass A：碎面三角写位移场

            RenderTarget2D map = ScreenTargets[0];
            gd.SetRenderTarget(map);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            OniFinaleShatter.DrawFacetMap(sb);
            sb.End();

            //Pass B：实时画面 → swap，蓄力/爆发期叠径向模糊（向刀线中心聚拢）

            gd.SetRenderTarget(swap);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
            float blur = OniFinaleShatter.BlurStrength;
            Effect radial = OniFinaleShatterAssets.RadialBlur;
            if (blur > 0.004f && radial != null) {
                radial.Parameters["center"]?.SetValue(WorldToScreenUV(OniFinaleShatter.FocusWorld));
                radial.Parameters["strength"]?.SetValue(blur * 0.5f);
                radial.CurrentTechnique.Passes[0].Apply();
            }
            sb.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            sb.End();

            //Pass C：PowerSF 折射合成回 screenTarget（世界透过碎掉的棱镜）

            gd.SetRenderTarget(Main.screenTarget);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
            if (OniFinaleShatter.HasFacets) {
                twist.Parameters["tex0"]?.SetValue(map);
                twist.Parameters["i"]?.SetValue(OniFinaleShatter.TwistStrength);
                twist.CurrentTechnique.Passes[0].Apply();
            }
            sb.Draw(swap, Vector2.Zero, Color.White);
            sb.End();

            //Pass D：爆发窗 RGB 色差回写（swap 中转，村正在合成层叠色差的等价实现）

            float sep = OniFinaleShatter.ColorSepStrength;
            Effect mess = OniFinaleShatterAssets.ScreenColorMess;
            if (sep > 0.0004f && mess != null) {
                gd.SetRenderTarget(swap);
                gd.Clear(Color.Transparent);
                sb.Begin(SpriteSortMode.Deferred, BlendState.Opaque);
                sb.Draw(Main.screenTarget, Vector2.Zero, Color.White);
                sb.End();

                gd.SetRenderTarget(Main.screenTarget);
                gd.Clear(Color.Transparent);
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
                mess.Parameters["offsetStrength"]?.SetValue(sep);
                mess.CurrentTechnique.Passes[0].Apply();
                sb.Draw(swap, Vector2.Zero, Color.White);
                sb.End();
            }
        }

        /// <summary>世界坐标 → 归一化 uv（含 GameViewMatrix.Zoom，与 OniFinaleRender 同构）</summary>
        private static Vector2 WorldToScreenUV(Vector2 worldPos) {
            float screenW = Main.screenWidth;
            float screenH = Main.screenHeight;
            Vector2 zoom = Main.GameViewMatrix.Zoom;
            if (zoom.X <= 0f) {
                zoom.X = 1f;
            }
            if (zoom.Y <= 0f) {
                zoom.Y = 1f;
            }
            Vector2 screenCenterPx = new(screenW * 0.5f, screenH * 0.5f);
            Vector2 viewWorldCenter = Main.screenPosition + screenCenterPx;
            Vector2 screenPx = screenCenterPx + (worldPos - viewWorldCenter) * zoom;
            return new Vector2(screenPx.X / screenW, screenPx.Y / screenH);
        }
    }
}
