using CalamityOverhaul.Common;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Renders
{
    /// <summary>
    /// 节点/锚点着色器绘制收集器：tile PreDraw 逐帧登记（天然只含可见格），
    /// <see cref="OldNetTileFXRender"/> 在物块层之后一次性按技法批绘。
    /// shader 缺失时 tile 走各自的 CPU 回退、不登记，本收集器不承担回退
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

        //绊网光束（04 固定威胁）：成对桩中的锚桩登记，CPU 三层线 quad 批绘
        //（shader 富层 TechBeam 为规划中的非首版项，后补时替换 DrawBeams 内部即可）
        internal struct BeamEntry
        {
            /// <summary>锚桩端世界坐标（横梁=左桩，竖梁=上桩）</summary>
            internal Vector2 A;
            /// <summary>对桩端世界坐标</summary>
            internal Vector2 B;
            /// <summary>节律相位偏移（坐标哈希，同屏错相）</summary>
            internal int Phase;
            /// <summary>触发冷却 0..1（>0 期间压暗为泄气态）</summary>
            internal float Cooling01;
        }

        internal static readonly List<NodeEntry> Nodes = [];
        internal static readonly List<ColumnEntry> Columns = [];
        internal static readonly List<GateEntry> Gates = [];
        internal static readonly List<BeamEntry> Beams = [];

        internal static bool NodeShaderReady => !Main.dedServ && EffectLoader.OldNetNode?.Value != null;
        internal static bool TerminalShaderReady => !Main.dedServ && EffectLoader.OldNetTerminal?.Value != null;

        internal static void ClearAll() {
            Nodes.Clear();
            Columns.Clear();
            Gates.Clear();
            Beams.Clear();
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
            float time = (float)Main.timeForVisualEffects / 60f;

            //shader 批：Immediate 逐条目换技法
            if (OldNetTileFX.Nodes.Count > 0 || OldNetTileFX.Columns.Count > 0
                || OldNetTileFX.Gates.Count > 0) {
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                    SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                    null, Main.GameViewMatrix.TransformationMatrix);
                DrawNodes(spriteBatch, px, time);
                DrawColumns(spriteBatch, px, time);
                DrawGates(spriteBatch, px, time);
                spriteBatch.End();
            }

            //CPU 批（04 固定威胁）：绊网光束三层线 quad + 过线红环脉冲 + 封锁闸预告层。
            //独立 Deferred 批、无自定义 effect——Immediate 批残留的像素着色器不许污染这里
            bool overlay = NPCs.OldNetThreatField.BulkheadOverlayVisible;
            bool pulses = NPCs.OldNetThreatField.TripPulses.Count > 0;
            if (OldNetTileFX.Beams.Count > 0 || overlay || pulses) {
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                    SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                    null, Main.GameViewMatrix.TransformationMatrix);
                DrawBeams(spriteBatch, px, time);
                if (pulses) {
                    DrawTripPulses(spriteBatch, px);
                }
                if (overlay) {
                    DrawBulkheadOverlay(spriteBatch, px, time);
                }
                spriteBatch.End();
            }
            OldNetTileFX.ClearAll();
        }

        //──── 04 固定威胁：绊网光束（暗底/红体/白芯三层）与封锁闸预告 ────

        private static readonly Color BeamRed = new(235, 64, 44);
        private static readonly Color BeamAmber = new(255, 170, 60);
        private static readonly Color BeamMint = new(120, 255, 170);
        private static readonly Color FreezeCyan = new(0, 220, 255);

        private static void DrawBeams(SpriteBatch sb, Texture2D px, float time) {
            if (OldNetTileFX.Beams.Count == 0) {
                return;
            }
            bool frozen = WorldFreezeSystem.IsActive;
            Rectangle src = new(0, 0, 1, 1);
            foreach (OldNetTileFX.BeamEntry beam in OldNetTileFX.Beams) {
                Vector2 a = beam.A - Main.screenPosition;
                Vector2 diff = beam.B - beam.A;
                float len = diff.Length();
                if (len < 4f) {
                    continue;
                }
                float rot = diff.ToRotation();
                Vector2 lineOrigin = new(0f, 0.5f);

                NPCs.OldNetThreatField.BeamCycleState(beam.Phase,
                    out bool lit, out float litT, out bool preBlink);

                //时停：节律冻结，整线换冷青薄线（判定暂停=可白嫖通行的可读化）
                if (frozen) {
                    sb.Draw(px, a, src, FreezeCyan * 0.20f, rot, lineOrigin,
                        new Vector2(len, 1.4f), SpriteEffects.None, 0f);
                    continue;
                }
                //触发冷却：泄气态暗虚线（判定已关，读作安全窗）
                if (beam.Cooling01 > 0f) {
                    DrawDashes(sb, px, src, a, rot, len, BeamAmber * 0.14f);
                    continue;
                }

                if (lit) {
                    //三层线 quad：暗底 / 红体 / 白芯 + 端点白芯收口
                    float flicker = 0.78f + 0.18f * MathF.Sin(time * 13f + beam.Phase);
                    sb.Draw(px, a, src, new Color(16, 8, 10) * 0.85f, rot, lineOrigin,
                        new Vector2(len, 5f), SpriteEffects.None, 0f);
                    sb.Draw(px, a, src, BeamRed * flicker, rot, lineOrigin,
                        new Vector2(len, 2.6f), SpriteEffects.None, 0f);
                    sb.Draw(px, a, src, Color.White * (0.55f + 0.2f * litT), rot, lineOrigin,
                        new Vector2(len, 1f), SpriteEffects.None, 0f);
                    Vector2 half = new(2.5f);
                    sb.Draw(px, a - half, src, Color.White * 0.85f, 0f, Vector2.Zero,
                        new Vector2(5f, 5f), SpriteEffects.None, 0f);
                    sb.Draw(px, beam.B - Main.screenPosition - half, src, Color.White * 0.85f,
                        0f, Vector2.Zero, new Vector2(5f, 5f), SpriteEffects.None, 0f);
                }
                else {
                    //灭相不是消失：暗琥珀虚线读作"电路待命"；亮相前加速闪三下（起搏预告）
                    float alpha = 0.30f;
                    if (preBlink) {
                        alpha = NPCs.OldNetThreatField.FieldTicks / 6 % 2 == 0 ? 0.62f : 0.16f;
                    }
                    DrawDashes(sb, px, src, a, rot, len, BeamAmber * alpha);
                }
            }
        }

        //过线闪报：从触发点扩散的红色菱环（EventNode 警戒圈语汇的一次性脉冲版）
        private static void DrawTripPulses(SpriteBatch sb, Texture2D px) {
            Rectangle src = new(0, 0, 1, 1);
            foreach (NPCs.OldNetThreatField.TripPulse pulse in NPCs.OldNetThreatField.TripPulses) {
                float t01 = 1f - pulse.Timer / (float)NPCs.OldNetThreatField.TripPulseLife;
                Vector2 center = pulse.Pos - Main.screenPosition;
                float ringR = 14f + t01 * 76f;
                Color ringCol = BeamRed * (0.6f * (1f - t01));
                for (int k = 0; k < 4; k++) {
                    float ang = MathHelper.PiOver2 * k + MathHelper.PiOver4;
                    Vector2 a = center + ang.ToRotationVector2() * ringR;
                    Vector2 b = center + (ang + MathHelper.PiOver2).ToRotationVector2() * ringR;
                    Vector2 diff = b - a;
                    sb.Draw(px, a, src, ringCol, diff.ToRotation(), new Vector2(0f, 0.5f),
                        new Vector2(diff.Length(), 1.4f), SpriteEffects.None, 0f);
                }
            }
        }

        //短划虚线：5px 划 + 5px 空
        private static void DrawDashes(SpriteBatch sb, Texture2D px, Rectangle src,
            Vector2 start, float rot, float len, Color color) {
            Vector2 dir = rot.ToRotationVector2();
            for (float d = 0f; d < len; d += 10f) {
                float dashLen = MathF.Min(5f, len - d);
                sb.Draw(px, start + dir * d, src, color, rot, new Vector2(0f, 0.5f),
                    new Vector2(dashLen, 1.1f), SpriteEffects.None, 0f);
            }
        }

        //封锁闸预告层：OPEN 四角括号呼吸 / WARN 槽缘频闪+闸影 / SHUT 延迟格警闪 /
        //泄压窗口薄荷绿括号+倒数条 / 重开前薄荷绿脉冲。无 tile 时也可见（预告不依赖实体格）
        private static void DrawBulkheadOverlay(SpriteBatch sb, Texture2D px, float time) {
            Rectangle src = new(0, 0, 1, 1);
            Rectangle view = new((int)Main.screenPosition.X - 120, (int)Main.screenPosition.Y - 120,
                Main.screenWidth + 240, Main.screenHeight + 240);
            var state = NPCs.OldNetThreatField.GateState;
            float reopenPulse = NPCs.OldNetThreatField.ReopenPulse01;

            foreach (NPCs.OldNetThreatField.BulkheadGroup g in NPCs.OldNetThreatField.Bulkheads) {
                Rectangle world = new(g.Slot.X * 16, g.Slot.Y * 16, g.Slot.Width * 16, g.Slot.Height * 16);
                if (!world.Intersects(view)) {
                    continue;
                }
                Vector2 tl = new Vector2(world.X, world.Y) - Main.screenPosition;
                float w = world.Width, h = world.Height;
                bool vented = g.BreakerTimer > 0;

                if (state == NPCs.OldNetThreatField.BulkheadState.Shut && vented) {
                    //泄压窗口：薄荷绿括号 + 顶缘倒数条（窗口收窄可读）
                    float left01 = g.BreakerTimer / (float)Gen.OldNetMetrics.BreakerOpenTicks;
                    DrawBrackets(sb, px, src, tl, w, h, BeamMint * 0.85f);
                    sb.Draw(px, tl + new Vector2(0f, -3f), src, BeamMint * 0.8f, 0f,
                        Vector2.Zero, new Vector2(w * left01, 2f), SpriteEffects.None, 0f);
                    continue;
                }

                switch (state) {
                    case NPCs.OldNetThreatField.BulkheadState.Open: {
                        //存在感预告：四角琥珀括号慢呼吸（远看知道"这里有门"）
                        float breath = 0.30f + 0.18f * MathF.Sin(time * 1.6f + g.Slot.X * 0.31f);
                        DrawBrackets(sb, px, src, tl, w, h, new Color(150, 140, 110) * breath);
                        break;
                    }
                    case NPCs.OldNetThreatField.BulkheadState.Warn: {
                        //预紧：槽缘全亮频闪 + 半透明闸影（"即将实体化"的预告帧）
                        float flick = MathF.Sin(time * 16f) > 0f ? 0.8f : 0.35f;
                        DrawFrame(sb, px, src, tl, w, h, BeamAmber * flick);
                        sb.Draw(px, tl, src, BeamRed * 0.14f, 0f, Vector2.Zero,
                            new Vector2(w, h), SpriteEffects.None, 0f);
                        break;
                    }
                    case NPCs.OldNetThreatField.BulkheadState.Shut: {
                        //延迟落格（玩家占位中）：该格危险频闪，落格已在倒数
                        float flick = MathF.Sin(time * 22f) > 0f ? 0.5f : 0.2f;
                        foreach (Point cell in g.Pending) {
                            Vector2 cellTl = new Vector2(cell.X * 16, cell.Y * 16) - Main.screenPosition;
                            sb.Draw(px, cellTl, src, BeamRed * flick, 0f, Vector2.Zero,
                                new Vector2(16f, 16f), SpriteEffects.None, 0f);
                        }
                        //重开前 1s：薄荷绿脉冲收尾（安全色预告）
                        if (reopenPulse > 0f) {
                            DrawFrame(sb, px, src, tl, w, h,
                                BeamMint * (0.35f + 0.5f * reopenPulse));
                        }
                        break;
                    }
                }
            }
        }

        //四角 L 形括号
        private static void DrawBrackets(SpriteBatch sb, Texture2D px, Rectangle src,
            Vector2 tl, float w, float h, Color color) {
            const float armLen = 10f;
            const float thick = 1.6f;
            Span<Vector2> corners = [tl, tl + new Vector2(w, 0f), tl + new Vector2(0f, h), tl + new Vector2(w, h)];
            for (int k = 0; k < 4; k++) {
                float sx = k % 2 == 0 ? 1f : -1f;
                float sy = k < 2 ? 1f : -1f;
                Vector2 c = corners[k];
                sb.Draw(px, sx > 0 ? c : c - new Vector2(armLen, 0f), src, color, 0f,
                    Vector2.Zero, new Vector2(armLen, thick), SpriteEffects.None, 0f);
                sb.Draw(px, sy > 0 ? c : c - new Vector2(0f, armLen), src, color, 0f,
                    Vector2.Zero, new Vector2(thick, armLen), SpriteEffects.None, 0f);
            }
        }

        //槽缘整框
        private static void DrawFrame(SpriteBatch sb, Texture2D px, Rectangle src,
            Vector2 tl, float w, float h, Color color) {
            const float thick = 1.6f;
            sb.Draw(px, tl, src, color, 0f, Vector2.Zero, new Vector2(w, thick), SpriteEffects.None, 0f);
            sb.Draw(px, tl + new Vector2(0f, h - thick), src, color, 0f, Vector2.Zero,
                new Vector2(w, thick), SpriteEffects.None, 0f);
            sb.Draw(px, tl, src, color, 0f, Vector2.Zero, new Vector2(thick, h), SpriteEffects.None, 0f);
            sb.Draw(px, tl + new Vector2(w - thick, 0f), src, color, 0f, Vector2.Zero,
                new Vector2(thick, h), SpriteEffects.None, 0f);
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
