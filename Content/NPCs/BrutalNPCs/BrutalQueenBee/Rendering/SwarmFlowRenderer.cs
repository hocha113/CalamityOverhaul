using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Core;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Rendering
{
    /// <summary>
    /// 编队辉光带渲染：沿实际蜂位折线铺花粉金尘流(QueenSwarmFlow)<br/>
    /// 在女王 Draw 内调用——延迟批次中图元先落，蜂群精灵后盖，带子天然垫底
    /// </summary>
    internal static class SwarmFlowRenderer
    {
        //固定点数复用Trail(持GPU缓冲)，至多三条路径
        private const int PathMax = 3;
        private const int PointCount = 24;
        private static readonly Trail[] trails = new Trail[PathMax];
        private static readonly Vector2[][] pointBuffers = new Vector2[PathMax][];
        private static readonly float[] widthByPath = new float[PathMax];
        private static readonly float[] alphaByPath = new float[PathMax];
        private static readonly List<List<Vector2>> pathScratch = [];

        /// <summary>绘制指定女王编队的辉光带</summary>
        public static void DrawRibbons(SwarmDirector director) {
            if (Main.dedServ || director == null) {
                return;
            }
            float intensity = director.RibbonIntensity;
            if (intensity <= 0.03f || EffectLoader.QueenSwarmFlow?.Value == null) {
                return;
            }

            director.BuildRibbonPaths(pathScratch);
            if (pathScratch.Count == 0) {
                return;
            }

            Effect effect = EffectLoader.QueenSwarmFlow.Value;
            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uColor"]?.SetValue(QueenBeeMotion.HoneyGold.ToVector3());
            effect.Parameters["uFlowSpeed"]?.SetValue(2.2f);

            Main.graphics.GraphicsDevice.BlendState = BlendState.Additive;

            int drawn = 0;
            for (int p = 0; p < pathScratch.Count && drawn < PathMax; p++) {
                List<Vector2> path = pathScratch[p];
                if (path.Count < 2) {
                    continue;
                }
                float length = Resample(path, drawn);
                if (length < 60f) {
                    continue;
                }

                widthByPath[drawn] = 30f * (0.6f + intensity * 0.4f);
                alphaByPath[drawn] = intensity;

                int slotIdx = drawn;
                trails[slotIdx] ??= new Trail(new Vector2[PointCount],
                    f => RibbonWidth(slotIdx, f),
                    texCoord => Color.White * alphaByPath[slotIdx]);
                trails[slotIdx].TrailPositions = pointBuffers[slotIdx];

                effect.Parameters["uIntensity"]?.SetValue(intensity * 0.9f);
                effect.Parameters["uAspect"]?.SetValue(length / MathHelper.Max(widthByPath[drawn], 1f));
                trails[slotIdx].DrawTrail(effect);
                drawn++;
            }

            Main.graphics.GraphicsDevice.BlendState = BlendState.AlphaBlend;
        }

        private static float RibbonWidth(int pathIdx, float f) {
            //中段略鼓，端点收窄
            float taper = 0.55f + 0.45f * (float)System.Math.Sin(f * MathHelper.Pi);
            return widthByPath[pathIdx] * taper;
        }

        /// <summary>把不定长折线按弧长均匀重采样进定长点缓冲，返回总弧长</summary>
        private static float Resample(List<Vector2> path, int slot) {
            pointBuffers[slot] ??= new Vector2[PointCount];
            Vector2[] buffer = pointBuffers[slot];

            float total = 0f;
            for (int i = 1; i < path.Count; i++) {
                total += Vector2.Distance(path[i - 1], path[i]);
            }
            if (total < 1f) {
                for (int i = 0; i < PointCount; i++) {
                    buffer[i] = path[0];
                }
                return 0f;
            }

            float step = total / (PointCount - 1);
            int seg = 1;
            float segStart = 0f;
            float segLen = Vector2.Distance(path[0], path[1]);
            buffer[0] = path[0];
            for (int i = 1; i < PointCount; i++) {
                float targetDist = step * i;
                while (segStart + segLen < targetDist && seg < path.Count - 1) {
                    segStart += segLen;
                    seg++;
                    segLen = Vector2.Distance(path[seg - 1], path[seg]);
                }
                float t = segLen > 0.001f ? (targetDist - segStart) / segLen : 0f;
                buffer[i] = Vector2.Lerp(path[seg - 1], path[seg], MathHelper.Clamp(t, 0f, 1f));
            }
            return total;
        }

        internal static void Unload() {
            for (int i = 0; i < trails.Length; i++) {
                trails[i]?.Dispose();
                trails[i] = null;
            }
        }
    }
}
