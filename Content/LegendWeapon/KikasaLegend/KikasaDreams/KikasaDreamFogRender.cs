using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDreams
{
    /// <summary>
    /// 鬼梦贴地雾带：逐列探地建一条地形跟随三角带，KikasaDreamFog.fx 以连续噪声场着雾
    /// （替换粒子堆叠地雾：连续场无生灭即无闪烁，"走"由风相滚动实现）。
    /// 驱散源经 <see cref="KikasaDreamFogField"/> 喂 uniform，玩家/光标/恶犬处雾让位。
    /// 由 <see cref="KikasaDomains.KikasaDomainRender"/> 的 EndEntityDraw 驱动，仅梦侧可视时绘制
    /// </summary>
    internal static class KikasaDreamFogRender
    {
        /// <summary>地表以上雾带高（世界 px）</summary>
        private const float FogHeight = 96f;

        /// <summary>地表以下裙边，盖住坡坎台阶的接缝</summary>
        private const float Skirt = 26f;

        /// <summary>探地列距（世界 px）</summary>
        private const float ColumnStep = 32f;

        /// <summary>列数上限：5K 超宽 + 边距 / 32 仍有余量</summary>
        private const int MaxColumns = 180;

        /// <summary>相邻列高度差钳制（px），雾爬坡不跳崖</summary>
        private const float MaxSlope = 30f;

        /// <summary>与着色器 uRepulse[6] 对齐的槽位数</summary>
        private const int RepulseSlots = 6;

        //探地/平滑/断崖缓冲与顶点缓冲逐帧复用，零分配
        private static readonly float[] heights = new float[MaxColumns];
        private static readonly float[] smoothed = new float[MaxColumns];
        private static readonly float[] gaps = new float[MaxColumns];
        private static readonly float[] gapsBlur = new float[MaxColumns];
        private static readonly VertexPositionColorTexture[] verts = new VertexPositionColorTexture[MaxColumns * 2];
        private static readonly Vector4[] repulseUpload = new Vector4[RepulseSlots];

        internal static void Draw(SpriteBatch spriteBatch) {
            KikasaDomainPlayer kdp = KikasaDomain.Viewed;
            if (kdp == null || !kdp.DreamWorldVisual || kdp.DreamBlend <= 0.01f) {
                return;
            }
            Player viewer = Main.LocalPlayer;
            if (viewer?.active != true) {
                return;
            }
            Effect fx = EffectLoader.KikasaDreamFog?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || noise == null) {
                //雾是纯氛围件，着色器缺失直接没有，不做灰度贴片回退
                return;
            }

            //带跨度：屏幕±80 ∩ 梦界圆（与物品封禁/禁弹同一半径口径）
            float casterX = kdp.Player.Center.X;
            float left = MathF.Max(Main.screenPosition.X - 80f,
                casterX - KikasaDream.WorldRange);
            float right = MathF.Min(Main.screenPosition.X + Main.screenWidth + 80f,
                casterX + KikasaDream.WorldRange);
            int cols = (int)((right - left) / ColumnStep) + 2;
            if (cols < 2) {
                return;
            }
            cols = Math.Min(cols, MaxColumns);

            SampleGround(viewer, left, cols);
            BuildVerts(left, cols);

            fx.CurrentTechnique = fx.Techniques["TechGroundFog"];
            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(kdp.EffectTime);
            //风向与雨帘同源定相，量级压低：梦里的死风，雾贴地缓行
            fx.Parameters["uWind"]?.SetValue(MathF.Sin(Main.worldID % 255 * 0.37f) * 16f);
            fx.Parameters["uAlpha"]?.SetValue(kdp.DreamBlend);
            FillRepulse();
            fx.Parameters["uRepulse"]?.SetValue(repulseUpload);

            //EndEntityDraw 入口批未开启（同 KikasaWispFX 之例），只动设备态并画完还原
            GraphicsDevice device = Main.instance.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;
            device.Textures[1] = noise;
            device.SamplerStates[1] = SamplerState.LinearWrap;

            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, cols * 2 - 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        /// <summary>
        /// 逐列探地 + 3 点平均 + 双向斜率钳制 + 断崖羽化：
        /// 雾流过小坑不打摆，探不到地的列（深崖/大空腔）在两三列内软收为无雾
        /// </summary>
        private static void SampleGround(Player viewer, float left, int cols) {
            float carry = viewer.Bottom.Y;
            for (int i = 0; i < cols; i++) {
                float x = left + i * ColumnStep;
                if (KikasaDreamSystem.TryFindGround(x, viewer.Center.Y - 60f, out float groundY)) {
                    heights[i] = groundY;
                    gaps[i] = 1f;
                    carry = groundY;
                }
                else {
                    //高度沿用最近有效列，可见性交给 gap 归零
                    heights[i] = carry;
                    gaps[i] = 0f;
                }
            }

            //3 点平均
            for (int i = 0; i < cols; i++) {
                float sum = heights[i];
                float weight = 1f;
                if (i > 0) {
                    sum += heights[i - 1];
                    weight += 1f;
                }
                if (i < cols - 1) {
                    sum += heights[i + 1];
                    weight += 1f;
                }
                smoothed[i] = sum / weight;
            }
            //双向斜率钳制：先左后右各一遍，坡度封在 MaxSlope/列 内
            for (int i = 1; i < cols; i++) {
                smoothed[i] = MathHelper.Clamp(smoothed[i],
                    smoothed[i - 1] - MaxSlope, smoothed[i - 1] + MaxSlope);
            }
            for (int i = cols - 2; i >= 0; i--) {
                smoothed[i] = MathHelper.Clamp(smoothed[i],
                    smoothed[i + 1] - MaxSlope, smoothed[i + 1] + MaxSlope);
            }
            //断崖羽化两遍，雾在崖口渐没而非平切
            for (int pass = 0; pass < 2; pass++) {
                for (int i = 0; i < cols; i++) {
                    float sum = gaps[i] * 2f;
                    float weight = 2f;
                    if (i > 0) {
                        sum += gaps[i - 1];
                        weight += 1f;
                    }
                    if (i < cols - 1) {
                        sum += gaps[i + 1];
                        weight += 1f;
                    }
                    gapsBlur[i] = sum / weight;
                }
                Array.Copy(gapsBlur, gaps, cols);
            }
        }

        //顶点契约与 KikasaDreamFog.fx 对齐：POSITION=世界坐标，
        //TEXCOORD0.y=带内高度01（顶=1 裙底=0），COLOR0.r=断崖渐隐

        private static void BuildVerts(float left, int cols) {
            for (int i = 0; i < cols; i++) {
                float x = left + i * ColumnStep;
                float ground = smoothed[i];
                byte gap = (byte)(MathHelper.Clamp(gaps[i], 0f, 1f) * 255f);
                Color data = new(gap, byte.MaxValue, byte.MaxValue, byte.MaxValue);
                verts[i * 2] = new VertexPositionColorTexture(
                    new Vector3(x, ground - FogHeight, 0f), data, new Vector2(0f, 1f));
                verts[i * 2 + 1] = new VertexPositionColorTexture(
                    new Vector3(x, ground + Skirt, 0f), data, new Vector2(0f, 0f));
            }
        }

        private static void FillRepulse() {
            var repulsors = KikasaDreamFogField.Repulsors;
            for (int i = 0; i < RepulseSlots; i++) {
                repulseUpload[i] = i < repulsors.Count ? repulsors[i] : Vector4.Zero;
            }
        }
    }
}
