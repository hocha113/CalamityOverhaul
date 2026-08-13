using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Rendering
{
    /// <summary>
    /// 阴魂冷焰中央顶点批：眼火/冠火/掌心火/烛灵/灵焰粒子每帧 Push 实例，
    /// EndEntityDraw 时机（无活动 SpriteBatch）一次 DrawUserPrimitives 全画完，
    /// 材质走 SkeletronCurseFlame.fx，实例参数打包进顶点色
    /// </summary>
    internal class SkeletronFlameRender : RenderHandle
    {
        /// <summary>紧邻骷髅王诅咒之幕(1.092)之后、石巨人屏效(1.094)之前，避开 OniFinaleShatter(1.093)</summary>
        public override float Weight => 1.0925f;

        #region 实例队列

        private struct FlameInstance
        {
            public Vector2 Pos;      //焰根世界坐标
            public float Rotation;   //焰轴朝向（弧度，指向焰尖）
            public Vector2 Size;     //X宽 Y高（世界像素）
            public float Heat;       //火势 0~1
            public float Seed;       //相位 0~1
            public float Curse;      //诅咒紫混比 0~1
            public float Opacity;    //透明度 0~1
        }

        private const int MaxFlames = 220;
        private static readonly FlameInstance[] queue = new FlameInstance[MaxFlames];
        private static int queueCount;
        private static readonly VertexPositionColorTexture[] verts = new VertexPositionColorTexture[MaxFlames * 6];

        /// <summary>
        /// 压入一朵冷焰（客户端本地，帧内有效）。pos 为焰根，rotation 指向焰尖
        /// </summary>
        internal static void Push(Vector2 pos, float rotation, Vector2 size,
            float heat, float seed, float curse, float opacity) {
            if (VaultUtils.isServer || opacity <= 0.01f || queueCount >= MaxFlames) {
                return;
            }
            queue[queueCount++] = new FlameInstance {
                Pos = pos,
                Rotation = rotation,
                Size = size,
                Heat = MathHelper.Clamp(heat, 0f, 1f),
                //正向小数部分：C# 取余保留负号，负 seed 打包进颜色通道会被钳到 0（相位塌缩）
                Seed = seed - MathF.Floor(seed),
                Curse = MathHelper.Clamp(curse, 0f, 1f),
                Opacity = MathHelper.Clamp(opacity, 0f, 1f)
            };
        }

        #endregion

        #region 绘制

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main,
            GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            int count = queueCount;
            queueCount = 0;
            if (count <= 0 || Main.dedServ) {
                return;
            }

            Effect effect = EffectLoader.SkeletronCurseFlame?.Value;
            bool useShader = effect != null && CWRAsset.PerlinNoise?.Value != null;

            int vertCount = useShader ? BuildQuads(count) : BuildFallbackFans(count);
            if (vertCount <= 0) {
                return;
            }

            BlendState origBlend = graphicsDevice.BlendState;
            RasterizerState origRaster = graphicsDevice.RasterizerState;
            DepthStencilState origDepth = graphicsDevice.DepthStencilState;
            graphicsDevice.BlendState = BlendState.Additive;
            graphicsDevice.RasterizerState = RasterizerState.CullNone;
            graphicsDevice.DepthStencilState = DepthStencilState.None;

            if (useShader) {
                effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
                effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                effect.Parameters["uCoreColor"]?.SetValue(SkeletronRenderHelper.BonePale.ToVector3());
                effect.Parameters["uBodyColor"]?.SetValue(SkeletronRenderHelper.GhostCyan.ToVector3());
                effect.Parameters["uEdgeColor"]?.SetValue(SkeletronRenderHelper.GhostDeep.ToVector3());
                effect.Parameters["uCurseColor"]?.SetValue(SkeletronRenderHelper.CurseViolet.ToVector3());
                //噪声显式绑到 s1（shader 内 register(s1)），参数式绑定废弃
                graphicsDevice.Textures[1] = CWRAsset.PerlinNoise.Value;
                graphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
                foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                    pass.Apply();
                    graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, verts, 0, vertCount / 3);
                }
            }
            else {
                //回退：顶点色放射扇（无灰度图依赖），待游戏内验证
                BasicEffect basic = SkeletronRenderHelper.GetFallbackEffect(graphicsDevice);
                foreach (EffectPass pass in basic.CurrentTechnique.Passes) {
                    pass.Apply();
                    graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, verts, 0, vertCount / 3);
                }
            }

            graphicsDevice.BlendState = origBlend;
            graphicsDevice.RasterizerState = origRaster;
            graphicsDevice.DepthStencilState = origDepth;
        }

        /// <summary>着色器路径：每焰一个矩形quad，实例参数进顶点色</summary>
        private static int BuildQuads(int count) {
            int v = 0;
            for (int i = 0; i < count; i++) {
                ref FlameInstance f = ref queue[i];
                Vector2 axis = f.Rotation.ToRotationVector2();
                Vector2 perp = new Vector2(-axis.Y, axis.X) * (f.Size.X * 0.5f);
                Vector2 tipOff = axis * f.Size.Y;

                Color pack = new Color(f.Heat, f.Seed, f.Curse, f.Opacity);
                Vector3 rl = new Vector3(f.Pos.X - perp.X, f.Pos.Y - perp.Y, 0f);
                Vector3 rr = new Vector3(f.Pos.X + perp.X, f.Pos.Y + perp.Y, 0f);
                Vector3 tl = new Vector3(rl.X + tipOff.X, rl.Y + tipOff.Y, 0f);
                Vector3 tr = new Vector3(rr.X + tipOff.X, rr.Y + tipOff.Y, 0f);

                verts[v++] = new VertexPositionColorTexture(rl, pack, new Vector2(0f, 0f));
                verts[v++] = new VertexPositionColorTexture(rr, pack, new Vector2(1f, 0f));
                verts[v++] = new VertexPositionColorTexture(tl, pack, new Vector2(0f, 1f));
                verts[v++] = new VertexPositionColorTexture(rr, pack, new Vector2(1f, 0f));
                verts[v++] = new VertexPositionColorTexture(tr, pack, new Vector2(1f, 1f));
                verts[v++] = new VertexPositionColorTexture(tl, pack, new Vector2(0f, 1f));
            }
            return v;
        }

        /// <summary>回退路径：中心亮缘暗的双三角菱形扇</summary>
        private static int BuildFallbackFans(int count) {
            int v = 0;
            for (int i = 0; i < count; i++) {
                ref FlameInstance f = ref queue[i];
                Vector2 axis = f.Rotation.ToRotationVector2();
                Vector2 perp = new Vector2(-axis.Y, axis.X) * (f.Size.X * 0.5f);
                Vector2 mid = f.Pos + axis * (f.Size.Y * 0.4f);
                Vector2 tip = f.Pos + axis * f.Size.Y;

                Color center = Color.Lerp(SkeletronRenderHelper.GhostCyan, SkeletronRenderHelper.CurseViolet, f.Curse)
                    * (f.Opacity * (0.35f + 0.45f * f.Heat));
                Color edge = Color.Transparent;

                //菱形两片：左右腰顶点带亮色，根/尖透明，读作纵向光锥
                Vector3 root = new Vector3(f.Pos.X, f.Pos.Y, 0f);
                Vector3 top = new Vector3(tip.X, tip.Y, 0f);
                Vector3 left = new Vector3(mid.X - perp.X, mid.Y - perp.Y, 0f);
                Vector3 right = new Vector3(mid.X + perp.X, mid.Y + perp.Y, 0f);

                Vector2 uv = Vector2.Zero;
                verts[v++] = new VertexPositionColorTexture(left, center, uv);
                verts[v++] = new VertexPositionColorTexture(root, edge, uv);
                verts[v++] = new VertexPositionColorTexture(right, center, uv);
                verts[v++] = new VertexPositionColorTexture(left, center, uv);
                verts[v++] = new VertexPositionColorTexture(top, edge, uv);
                verts[v++] = new VertexPositionColorTexture(right, center, uv);
            }
            return v;
        }

        #endregion
    }
}
