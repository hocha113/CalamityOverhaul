using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.Items.Melee.DawnshatterAzures
{
    /// <summary>
    /// 苍穹破晓条带渲染,一次挥砍三股异质条带(焦暗衬/主焰/焰芯)共用 DawnshatterSlash.fx<br/>
    /// 顶点色通道 R=z(0远~1近) G=热度 B=股序 A=不透明度;UV.x 按累计弧长归一,不按采样序
    /// </summary>
    internal static class DawnshatterRenderer
    {
        /// 焦暗衬带宽度倍率
        private const float BackWidth = 1.10f;
        /// 焰芯宽度倍率
        private const float CoreWidth = 0.17f;
        /// 焦暗衬带沿挥向滞后
        private const float BackLag = 7f;
        private const int SpindleSamples = 16;

        /// <summary>梭形半条带,perpSign=±1 上下两半,外缘=uv.y0,双端收尖</summary>
        private static VertexPositionColorTexture[] BuildSpindleHalf(Vector2 hand, Vector2 unit
            , float rear, float tip, float halfWidth, float perpSign
            , float heat, float opacity, float layerB, float lag) {
            var verts = new VertexPositionColorTexture[SpindleSamples * 2];
            Vector2 perp = unit.RotatedBy(MathHelper.PiOver2) * perpSign;
            Vector2 lagOff = unit * -lag;
            var pack = new Color(0.5f, heat, layerB, opacity);
            for (int i = 0; i < SpindleSamples; i++) {
                float u = i / (SpindleSamples - 1f);
                float dist = MathHelper.Lerp(rear, tip, u);
                //梭形包络,峰值偏头端
                float w = MathF.Pow(MathF.Sin(MathF.Pow(u, 0.85f) * MathHelper.Pi), 0.75f) * halfWidth;
                Vector2 basePos = hand + unit * dist + lagOff;
                verts[i * 2] = new VertexPositionColorTexture((basePos + perp * w).ToVector3()
                    , pack, new Vector2(u, 0f));
                verts[i * 2 + 1] = new VertexPositionColorTexture(basePos.ToVector3()
                    , pack, new Vector2(u, 1f));
            }
            return verts;
        }

        /// <summary>刺击梭形,三股×上下两半共 6 份条带</summary>
        internal static void CollectThrustStrips(List<VertexPositionColorTexture[]> sink
            , Vector2 hand, Vector2 unit, float rear, float tip, float halfWidth, float heat, float opacity) {
            for (int s = -1; s <= 1; s += 2) {
                sink.Add(BuildSpindleHalf(hand, unit, rear, tip, halfWidth * BackWidth, s, heat, opacity * 0.9f, 0f, BackLag));
            }
            for (int s = -1; s <= 1; s += 2) {
                sink.Add(BuildSpindleHalf(hand, unit, rear, tip, halfWidth, s, heat, opacity, 0.5f, 0f));
            }
            for (int s = -1; s <= 1; s += 2) {
                sink.Add(BuildSpindleHalf(hand, unit, rear, tip, halfWidth * CoreWidth, s, heat, opacity, 1f, 0f));
            }
        }

        /// <summary>弧光扫击,由采样环生成三股条带;samples 头在 index0,pos=枪尖向量,z 随行</summary>
        internal static void CollectArcStrips(List<VertexPositionColorTexture[]> sink
            , Vector2 hand, IReadOnlyList<ArcSample> samples, float innerFrac, float heat, float opacity) {
            if (samples.Count < 3) {
                return;
            }
            sink.Add(BuildArcStrip(hand, samples, innerFrac, 1.06f, heat, opacity * 0.9f, 0f, 2));
            sink.Add(BuildArcStrip(hand, samples, innerFrac, 1f, heat, opacity, 0.5f, 0));
            sink.Add(BuildArcStrip(hand, samples, innerFrac + (1f - innerFrac) * 0.72f, 1f, heat, opacity, 1f, 0));
        }

        internal struct ArcSample
        {
            public Vector2 Tip;
            /// z 归一,0远~1近
            public float Z;
            /// 采样时的热度
            public float Heat;
        }

        /// <summary>单股弧带,UV.x 按累计弧长归一(头=1),lagSteps 使焦暗衬滞后于亮层</summary>
        private static VertexPositionColorTexture[] BuildArcStrip(Vector2 hand, IReadOnlyList<ArcSample> samples
            , float innerFrac, float outerMul, float heat, float opacity, float layerB, int lagSteps) {
            int count = samples.Count;
            var verts = new VertexPositionColorTexture[count * 2];

            //累计弧长(沿外缘),UV 按弧长不按序号,角速度不均时贴图不拉伸
            Span<float> arc = count <= 128 ? stackalloc float[count] : new float[count];
            float total = 0f;
            arc[0] = 0f;
            for (int i = 1; i < count; i++) {
                total += (samples[i].Tip - samples[i - 1].Tip).Length();
                arc[i] = total;
            }
            if (total < 1f) {
                total = 1f;
            }

            for (int i = 0; i < count; i++) {
                int si = Math.Min(i + lagSteps, count - 1);
                ArcSample s = samples[si];
                float u = 1f - arc[i] / total;
                var pack = new Color(s.Z, MathF.Min(s.Heat, heat), layerB, opacity);
                Vector2 outer = hand + s.Tip * outerMul;
                Vector2 inner = hand + s.Tip * innerFrac;
                verts[i * 2] = new VertexPositionColorTexture(outer.ToVector3(), pack, new Vector2(u, 0f));
                verts[i * 2 + 1] = new VertexPositionColorTexture(inner.ToVector3(), pack, new Vector2(u, 1f));
            }
            return verts;
        }

        /// <summary>统一提交,预乘输出走 AlphaBlend,绘制后恢复设备状态</summary>
        internal static void DrawStrips(bool arcMode, float fade, float heat, float flash
            , List<VertexPositionColorTexture[]> strips) {
            if (strips.Count == 0 || fade <= 0.02f) {
                return;
            }
            Effect effect = EffectLoader.DawnshatterSlash?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return;
            }

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            effect.CurrentTechnique = effect.Techniques[arcMode ? "TechArc" : "TechThrust"];
            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uFade"]?.SetValue(fade);
            effect.Parameters["uHeat"]?.SetValue(heat);
            effect.Parameters["uFlash"]?.SetValue(flash);
            effect.Parameters["uNoiseTex"]?.SetValue(noise);

            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                foreach (VertexPositionColorTexture[] verts in strips) {
                    if (verts.Length >= 4) {
                        device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);
                    }
                }
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }
    }
}
