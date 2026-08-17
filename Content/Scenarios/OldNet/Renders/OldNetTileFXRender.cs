using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Renders
{
    /// <summary>
    /// 节点/锚点着色器绘制收集器：tile PreDraw 逐帧登记（天然只含可见格），
    /// <see cref="OldNetTileFXRender"/> 在物块层之后一次性按技法批绘。
    /// shader 缺失时 tile 走各自的 CPU 回退、不登记——本收集器不承担回退
    /// </summary>
    internal static class OldNetTileFX
    {
        internal struct NodeEntry
        {
            /// <summary>世界坐标中心（含 CPU 侧浮动 bob）</summary>
            internal Vector2 Center;
            /// <summary>0=普通 1=加密 2=事件</summary>
            internal int Kind;
            internal float Seed;
            /// <summary>加密引导进度 0..1</summary>
            internal float Progress;
        }

        internal struct ColumnEntry
        {
            /// <summary>柱底世界坐标（tile 底边中心）</summary>
            internal Vector2 BasePos;
            internal bool Relay;
            internal float Seed;
        }

        internal struct GateEntry
        {
            /// <summary>格左上世界坐标</summary>
            internal Vector2 TopLeft;
            /// <summary>扫描行相对本格的局部 y（uv 单位，可越界）</summary>
            internal float LocalScan;
            internal float Seed;
        }

        internal static readonly List<NodeEntry> Nodes = [];
        internal static readonly List<ColumnEntry> Columns = [];
        internal static readonly List<GateEntry> Gates = [];

        internal static bool NodeShaderReady => !Main.dedServ && EffectLoader.OldNetNode?.Value != null;
        internal static bool TerminalShaderReady => !Main.dedServ && EffectLoader.OldNetTerminal?.Value != null;

        internal static void ClearAll() {
            Nodes.Clear();
            Columns.Clear();
            Gates.Clear();
        }
    }

    //物块层后、NPC 层前的旧网 tile 富层：晶体节点/锚点光柱/闸门通电扫描
    internal class OldNetTileFXRender : RenderHandle
    {
        public override float Weight => 1.4f;

        //节点画布 48px，柱画布 48x168（底锚），闸门逐格 16px
        private const float NodeCanvas = 48f;
        private const float ColumnW = 48f;
        private const float ColumnH = 168f;

        public override void DrawAfterTiles(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice,
            RenderTarget2D screenSwap) {
            //列表本帧登记本帧消费，任何早退都要清空防跨帧堆积
            //不做旧网门控：主世界的接入终端（坠舱）走同一条批绘管线
            if (Main.gameMenu) {
                OldNetTileFX.ClearAll();
                return;
            }
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                OldNetTileFX.ClearAll();
                return;
            }
            if (OldNetTileFX.Nodes.Count == 0 && OldNetTileFX.Columns.Count == 0
                && OldNetTileFX.Gates.Count == 0) {
                return;
            }

            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            float time = (float)Main.timeForVisualEffects / 60f;
            DrawNodes(spriteBatch, px, time);
            DrawColumns(spriteBatch, px, time);
            DrawGates(spriteBatch, px, time);
            spriteBatch.End();
            OldNetTileFX.ClearAll();
        }

        private static void DrawNodes(SpriteBatch sb, Texture2D px, float time) {
            Effect fx = EffectLoader.OldNetNode?.Value;
            if (fx == null || OldNetTileFX.Nodes.Count == 0) {
                return;
            }
            Vector2 origin = new(px.Width * 0.5f, px.Height * 0.5f);
            Vector2 scale = new(NodeCanvas / px.Width, NodeCanvas / px.Height);
            foreach (OldNetTileFX.NodeEntry n in OldNetTileFX.Nodes) {
                fx.CurrentTechnique = fx.Techniques[n.Kind switch {
                    1 => "TechEncrypt",
                    2 => "TechEvent",
                    _ => "TechData",
                }];
                //共享参数化 shader：每次调用全参数重设（uniform 残留纪律）
                fx.Parameters["uTime"]?.SetValue(time);
                fx.Parameters["uSeed"]?.SetValue(n.Seed);
                fx.Parameters["uProgress"]?.SetValue(n.Progress);
                fx.Parameters["uAlpha"]?.SetValue(1f);
                fx.CurrentTechnique.Passes[0].Apply();
                sb.Draw(px, n.Center - Main.screenPosition, null, Color.White,
                    0f, origin, scale, SpriteEffects.None, 0f);
            }
        }

        private static void DrawColumns(SpriteBatch sb, Texture2D px, float time) {
            Effect fx = EffectLoader.OldNetTerminal?.Value;
            if (fx == null || OldNetTileFX.Columns.Count == 0) {
                return;
            }
            //底锚：origin 在贴图底边中心
            Vector2 origin = new(px.Width * 0.5f, px.Height);
            Vector2 scale = new(ColumnW / px.Width, ColumnH / px.Height);
            foreach (OldNetTileFX.ColumnEntry c in OldNetTileFX.Columns) {
                fx.CurrentTechnique = fx.Techniques[c.Relay ? "TechRelay" : "TechTerminal"];
                fx.Parameters["uTime"]?.SetValue(time);
                fx.Parameters["uSeed"]?.SetValue(c.Seed);
                fx.Parameters["uAlpha"]?.SetValue(1f);
                fx.Parameters["uLocalScan"]?.SetValue(0f);
                fx.CurrentTechnique.Passes[0].Apply();
                sb.Draw(px, c.BasePos - Main.screenPosition, null, Color.White,
                    0f, origin, scale, SpriteEffects.None, 0f);
            }
        }

        private static void DrawGates(SpriteBatch sb, Texture2D px, float time) {
            Effect fx = EffectLoader.OldNetTerminal?.Value;
            if (fx == null || OldNetTileFX.Gates.Count == 0) {
                return;
            }
            Vector2 scale = new(16f / px.Width, 16f / px.Height);
            foreach (OldNetTileFX.GateEntry g in OldNetTileFX.Gates) {
                fx.CurrentTechnique = fx.Techniques["TechGate"];
                fx.Parameters["uTime"]?.SetValue(time);
                fx.Parameters["uSeed"]?.SetValue(g.Seed);
                fx.Parameters["uAlpha"]?.SetValue(1f);
                fx.Parameters["uLocalScan"]?.SetValue(g.LocalScan);
                fx.CurrentTechnique.Passes[0].Apply();
                sb.Draw(px, g.TopLeft - Main.screenPosition, null, Color.White,
                    0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
        }
    }
}
