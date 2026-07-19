using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniDismembers
{
    /// <summary>
    /// 肢解碎片绘制：快照捕获完成后接管目标的 NPC 层绘制。<br/>
    /// PreDraw 返回 false 隐藏本体，原地把快照 RT 的凸多边形碎片以顶点三角扇提交
    /// （<c>OniDismember.fx</c>：定格冷灰 + 断面灼热辉光）。在 NPC 自己的绘制槽位内
    /// End/Begin 完成，与其它 NPC 的遮挡层序保持不变
    /// </summary>
    internal class OniDismemberNPC : GlobalNPC
    {
        //复用缓冲，避免逐帧分配
        private static readonly List<VertexPositionColorTexture> vertexScratch = [];
        private static Vector4[] cutLineParams = [];
        private static Vector4[] cutGlowParams = [];

        public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot) {
            //僵直的尸身不再构成接触伤害威胁
            return !OniDismember.IsDismembered(npc.whoAmI);
        }

        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            DismemberEntry entry = OniDismember.GetEntry(npc.whoAmI);
            if (entry == null || entry.NpcType != npc.type) {
                return true;
            }
            //快照未就绪（捕获排队中/低质量降级）时本体照常绘制
            if (!entry.Captured || entry.SnapWidth <= 0) {
                return true;
            }
            if (!OniDismember.SnapRTs.TryGetValue(npc.whoAmI, out RenderTarget2D rt)
                || rt == null || rt.IsDisposed) {
                return true;
            }
            Effect fx = EffectLoader.OniDismember?.Value;
            if (fx == null) {
                return true;
            }

            //暂停 NPC 层批次，原地插入顶点绘制，层序不变
            spriteBatch.End();
            DrawPieces(entry, rt, fx);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        private static void DrawPieces(DismemberEntry entry, RenderTarget2D rt, Effect fx) {
            GraphicsDevice gd = Main.instance.GraphicsDevice;

            BlendState prevBlend = gd.BlendState;
            RasterizerState prevRaster = gd.RasterizerState;
            DepthStencilState prevDepth = gd.DepthStencilState;
            gd.BlendState = BlendState.AlphaBlend;
            gd.RasterizerState = RasterizerState.CullNone;
            gd.DepthStencilState = DepthStencilState.None;

            BuildVertices(entry);

            int batchCapacity = EnsureCutParamBuffers(fx);
            if (vertexScratch.Count >= 3 && entry.Cuts.Count > 0 && batchCapacity > 0) {
                SetCommonShaderParams(entry, rt, fx);
                VertexPositionColorTexture[] verts = [.. vertexScratch];
                for (int start = 0; start < entry.Cuts.Count; start += batchCapacity) {
                    SetCutBatchParams(entry, fx, start, batchCapacity);
                    foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                        pass.Apply();
                        gd.DrawUserPrimitives(PrimitiveType.TriangleList, verts, 0, verts.Length / 3);
                    }
                }
            }

            gd.BlendState = prevBlend;
            gd.RasterizerState = prevRaster;
            gd.DepthStencilState = prevDepth;
        }

        /// <summary>从 effect 反射着色器单批容量，避免 C# 维护重复常量</summary>
        private static int EnsureCutParamBuffers(Effect fx) {
            int lineCapacity = fx.Parameters["uCutLine"]?.Elements.Count ?? 0;
            int glowCapacity = fx.Parameters["uCutGlow"]?.Elements.Count ?? 0;
            int capacity = Math.Min(lineCapacity, glowCapacity);
            if (capacity <= 0) {
                return 0;
            }
            if (cutLineParams.Length != capacity) {
                cutLineParams = new Vector4[capacity];
                cutGlowParams = new Vector4[capacity];
            }
            return capacity;
        }

        private static void SetCommonShaderParams(DismemberEntry entry, RenderTarget2D rt, Effect fx) {
            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSnapSize"]?.SetValue(new Vector2(entry.SnapWidth, entry.SnapHeight));
            //定格冷灰随滞拍结束缓入，尸身"冷下来"
            float coldIn = OniDismember.SeparationCurve(entry.Timer - entry.Cuts[0].Birth, entry.Cuts[0].Hold);
            fx.Parameters["uDesat"]?.SetValue(0.38f * coldIn);
            fx.Parameters["uDim"]?.SetValue(1f - 0.16f * coldIn);
            fx.Parameters["uColHot"]?.SetValue(new Vector3(1.85f, 1.62f, 1.30f));
            fx.Parameters["uColBright"]?.SetValue(new Vector3(1.55f, 0.28f, 0.14f));
            fx.Parameters["uSnapTex"]?.SetValue(rt);
        }

        private static void SetCutBatchParams(DismemberEntry entry, Effect fx, int start, int capacity) {
            int count = Math.Min(entry.Cuts.Count - start, capacity);
            for (int i = 0; i < count; i++) {
                DismemberCut cut = entry.Cuts[start + i];
                cutLineParams[i] = new Vector4(cut.PointLocal.X, cut.PointLocal.Y, cut.Normal.X, cut.Normal.Y);
                cutGlowParams[i] = new Vector4(GlowStrength(entry, in cut), GlowHalfWidth(entry, in cut), 0f, 0f);
            }
            fx.Parameters["uCutLine"]?.SetValue(cutLineParams);
            fx.Parameters["uCutGlow"]?.SetValue(cutGlowParams);
            fx.Parameters["uCutCount"]?.SetValue(count);
            //首批画身体，后续批次输出 alpha=0 的附加辉光
            fx.Parameters["uDrawBase"]?.SetValue(start == 0 ? 1f : 0f);
        }

        /// <summary>切口辉光强度：亮起闪 → 滞拍呼吸 → 分离后稳定灼热，尾段随整体淡出</summary>
        private static float GlowStrength(DismemberEntry entry, in DismemberCut cut) {
            int age = entry.Timer - cut.Birth;
            if (age < 0) {
                return 0f;   //波及调度的未来切口：亮起前不可见
            }
            float strength;
            if (age <= 2) {
                strength = 1.35f;                         //切口亮起的过曝闪
            }
            else if (age < cut.Hold) {
                float breath = 0.5f + 0.5f * MathF.Sin(age * 0.55f - MathHelper.PiOver2);
                strength = 0.55f + 0.30f * breath;        //滞拍呼吸：将断未断
            }
            else {
                float t = OniDismember.SeparationCurve(age, cut.Hold);
                strength = MathHelper.Lerp(1.15f, 0.72f, t)
                    + 0.06f * MathF.Sin(Main.GlobalTimeWrappedHourly * 5.1f + cut.Birth);
            }
            return strength * entry.FadeAlpha;
        }

        /// <summary>切口辉光半宽：闪帧宽 → 收束成锐利热线</summary>
        private static float GlowHalfWidth(DismemberEntry entry, in DismemberCut cut) {
            int age = Math.Max(entry.Timer - cut.Birth, 0);   //未来切口按亮起帧宽度待命
            float snapScale = MathF.Max(MathF.Min(entry.SnapWidth, entry.SnapHeight) / 160f, 0.6f);
            if (age < cut.Hold) {
                return (7f - 3f * age / cut.Hold) * snapScale;
            }
            return (4f - 1.6f * OniDismember.SeparationCurve(age, cut.Hold)) * snapScale;
        }

        /// <summary>全部碎片 → 三角扇顶点（世界坐标，交给 shader 的 transformMatrix 投屏）</summary>
        private static void BuildVertices(DismemberEntry entry) {
            vertexScratch.Clear();
            int requiredCapacity = 0;
            foreach (DismemberPiece piece in entry.Pieces) {
                requiredCapacity += Math.Max(piece.Verts.Length - 2, 0) * 3;
            }
            vertexScratch.EnsureCapacity(requiredCapacity);
            Color tint = Color.White * entry.FadeAlpha;
            Vector2 snapHalf = new(entry.SnapWidth * 0.5f, entry.SnapHeight * 0.5f);

            foreach (DismemberPiece piece in entry.Pieces) {
                OniDismember.GetPieceMotion(entry, piece, out Vector2 offset, out float rotation);
                float sin = MathF.Sin(rotation);
                float cos = MathF.Cos(rotation);

                Span<Vector2> world = stackalloc Vector2[piece.Verts.Length];
                for (int i = 0; i < piece.Verts.Length; i++) {
                    //绕碎片质心旋转 → 平移分离位移 → 锚点定位；uv 恒取原始局部坐标
                    Vector2 rel = piece.Verts[i] - piece.Centroid;
                    Vector2 rotated = new(rel.X * cos - rel.Y * sin, rel.X * sin + rel.Y * cos);
                    world[i] = entry.AnchorCenter + piece.Centroid + rotated + offset;
                }

                for (int i = 1; i < piece.Verts.Length - 1; i++) {
                    AppendVertex(world[0], piece.Verts[0], snapHalf, entry, tint);
                    AppendVertex(world[i], piece.Verts[i], snapHalf, entry, tint);
                    AppendVertex(world[i + 1], piece.Verts[i + 1], snapHalf, entry, tint);
                }
            }
        }

        private static void AppendVertex(Vector2 worldPos, Vector2 localPos, Vector2 snapHalf,
            DismemberEntry entry, Color tint) {
            Vector2 uv = new((localPos.X + snapHalf.X) / entry.SnapWidth
                , (localPos.Y + snapHalf.Y) / entry.SnapHeight);
            vertexScratch.Add(new VertexPositionColorTexture(worldPos.ToVector3(), tint, uv));
        }
    }
}
