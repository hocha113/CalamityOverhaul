using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering
{
    /// <summary>
    /// 星轨司祭绘制装配:3D 浑天仪环带(顶点条带+手动透视)+本体分层<br/>
    /// 环带走 CultistOrrery.fx TechRing,过本体的前后半按 z 分两趟画(画家算法)
    /// </summary>
    internal static class CultistOrreryRenderer
    {
        private const int Segments = 56;

        //复用缓冲,避免每帧分配
        private static readonly List<RingQuad> quadCache = new(Segments * 2);
        private static VertexPositionColorTexture[] vertexCache = new VertexPositionColorTexture[Segments * 4];
        private static short[] indexCache = new short[Segments * 6];

        private struct RingQuad
        {
            public Vector2 InnerPos;
            public Vector2 OuterPos;
            public Vector2 InnerPosNext;
            public Vector2 OuterPosNext;
            public float U0;
            public float U1;
            public float Lit;
            public float Z;
        }

        /// <summary>
        /// 画一整环:局部 3D 基 e1/e2,zSign 过滤(-1 只画近半 z&lt;0,+1 只画远半,0 全画)<br/>
        /// 调用方须不在 SpriteBatch 批内(本函数直接走设备图元)
        /// </summary>
        public static void DrawRing(Vector2 worldCenter, Vector3 e1, Vector3 e2, float radius, float halfWidth,
            float spinPhase, Color mid, Color bright, float charge, float alpha, float seed, int zSign) {
            Effect fx = EffectLoader.CultistOrrery?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || noise == null || alpha <= 0.01f) {
                return;
            }

            quadCache.Clear();
            Vector2 screenCenter = worldCenter - Main.screenPosition;

            for (int j = 0; j < Segments; j++) {
                float t0 = j / (float)Segments * MathHelper.TwoPi;
                float t1 = (j + 1) / (float)Segments * MathHelper.TwoPi;
                Vector3 d0 = e1 * (float)Math.Cos(t0) + e2 * (float)Math.Sin(t0);
                Vector3 d1 = e1 * (float)Math.Cos(t1) + e2 * (float)Math.Sin(t1);
                Vector3 mid3 = (d0 + d1) * (radius * 0.5f);

                if (zSign > 0 && mid3.Z < 0f) {
                    continue;
                }
                if (zSign < 0 && mid3.Z >= 0f) {
                    continue;
                }

                Vector2 in0 = screenCenter + CultistOrreryRig.Project(d0 * (radius - halfWidth), out _);
                Vector2 out0 = screenCenter + CultistOrreryRig.Project(d0 * (radius + halfWidth), out _);
                Vector2 in1 = screenCenter + CultistOrreryRig.Project(d1 * (radius - halfWidth), out _);
                Vector2 out1 = screenCenter + CultistOrreryRig.Project(d1 * (radius + halfWidth), out _);

                quadCache.Add(new RingQuad {
                    InnerPos = in0,
                    OuterPos = out0,
                    InnerPosNext = in1,
                    OuterPosNext = out1,
                    U0 = j / (float)Segments + spinPhase,
                    U1 = (j + 1) / (float)Segments + spinPhase,
                    Lit = CultistOrreryRig.DepthLit(mid3.Z, radius),
                    Z = mid3.Z,
                });
            }
            if (quadCache.Count == 0) {
                return;
            }

            //画家排序:远→近
            quadCache.Sort(static (a, b) => b.Z.CompareTo(a.Z));

            int quadCount = quadCache.Count;
            if (vertexCache.Length < quadCount * 4) {
                vertexCache = new VertexPositionColorTexture[quadCount * 4];
                indexCache = new short[quadCount * 6];
            }
            for (int q = 0; q < quadCount; q++) {
                RingQuad rq = quadCache[q];
                Color vc = new(rq.Lit, rq.Lit, rq.Lit, alpha);
                int vi = q * 4;
                vertexCache[vi + 0] = new VertexPositionColorTexture(new Vector3(rq.InnerPos, 0f), vc, new Vector2(rq.U0, 0f));
                vertexCache[vi + 1] = new VertexPositionColorTexture(new Vector3(rq.OuterPos, 0f), vc, new Vector2(rq.U0, 1f));
                vertexCache[vi + 2] = new VertexPositionColorTexture(new Vector3(rq.InnerPosNext, 0f), vc, new Vector2(rq.U1, 0f));
                vertexCache[vi + 3] = new VertexPositionColorTexture(new Vector3(rq.OuterPosNext, 0f), vc, new Vector2(rq.U1, 1f));
                int ii = q * 6;
                indexCache[ii + 0] = (short)(vi + 0);
                indexCache[ii + 1] = (short)(vi + 1);
                indexCache[ii + 2] = (short)(vi + 2);
                indexCache[ii + 3] = (short)(vi + 1);
                indexCache[ii + 4] = (short)(vi + 3);
                indexCache[ii + 5] = (short)(vi + 2);
            }

            //uniform 全参数重设(共享 shader 残留陷阱)
            fx.CurrentTechnique = fx.Techniques["TechRing"];
            fx.Parameters["transformMatrix"]?.SetValue(Main.GameViewMatrix.TransformationMatrix
                * Matrix.CreateOrthographicOffCenter(0, Main.screenWidth, Main.screenHeight, 0, -1, 1));
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uAlpha"]?.SetValue(1f);
            fx.Parameters["uColDeep"]?.SetValue(new Vector3(0.105f, 0.088f, 0.062f));
            fx.Parameters["uColMid"]?.SetValue(mid.ToVector3());
            fx.Parameters["uColBright"]?.SetValue(bright.ToVector3());
            fx.Parameters["uColHot"]?.SetValue(new Vector3(1f, 0.98f, 0.92f));
            fx.Parameters["uCharge"]?.SetValue(MathHelper.Clamp(charge, 0f, 1f));
            fx.Parameters["uSeed"]?.SetValue(seed);
            fx.Parameters["uProgress"]?.SetValue(1f);
            fx.Parameters["uDash"]?.SetValue(0f);
            fx.Parameters["uArm"]?.SetValue(0f);
            fx.Parameters["uEnv"]?.SetValue(0f);

            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.BlendState = BlendState.AlphaBlend;
            gd.RasterizerState = RasterizerState.CullNone;
            gd.DepthStencilState = DepthStencilState.None;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            fx.CurrentTechnique.Passes[0].Apply();
            gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertexCache, 0, quadCount * 4,
                indexCache, 0, quadCount * 2);
        }

        /// <summary>随身浑天仪单趟(zSign 同 DrawRing);失衡时姿态抖乱,合相时收拢共面</summary>
        private static void DrawWornOrrery(NPC npc, CultistStateContext context, int zSign) {
            if (context.OrreryMode != 0 || context.OrreryReveal <= 0.01f) {
                return;
            }
            float time = Main.GlobalTimeWrappedHourly;
            Color mid = CultistMotion.PhaseCore(context.Phase);
            Color bright = Color.Lerp(mid, Color.White, 0.5f);
            float charge = MathHelper.Clamp(context.OrreryAlignVis * 0.85f + context.OrreryGlow * 0.5f, 0f, 1f);

            for (int i = 0; i < CultistOrreryRig.RingCount; i++) {
                float reveal = MathHelper.Clamp(context.OrreryReveal - i, 0f, 1f);
                if (reveal <= 0.01f) {
                    continue;
                }
                float wob = context.StaggerWobble;
                float wobTime = time + (float)Math.Sin(time * 21f + i * 2.6f) * wob * 1.6f;
                CultistOrreryRig.GetRingBasis(i, wobTime, context.OrreryAlignVis, out Vector3 e1, out Vector3 e2);
                //显形期环径自内向外张开
                float radius = CultistOrreryRig.RingRadius[i] * (0.35f + 0.65f * reveal) * context.ScalePulse;
                DrawRing(npc.Center, e1, e2, radius, CultistOrreryRig.RingWidth[i],
                    time * (0.05f + i * 0.02f), mid, bright, charge, reveal, i * 0.37f + 0.11f, zSign);
            }
        }

        /// <summary>
        /// 本体全装配:远半环→施法辉光→vanilla 帧体→炽体复写→近半环<br/>
        /// 调用方处于实体批(Deferred AlphaBlend),内部自管批次进出
        /// </summary>
        public static void DrawBody(SpriteBatch sb, NPC npc, CultistStateContext context, Vector2 screenPos, Color drawColor) {
            Main.instance.LoadNPC(NPCID.CultistBoss);
            Texture2D tex = TextureAssets.Npc[NPCID.CultistBoss].Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float bodyAlpha = 1f - npc.alpha / 255f;

            Color core = CultistMotion.PhaseCore(context.Phase);

            //远半环(z>=0):压在本体身后
            sb.End();
            DrawWornOrrery(npc, context, 1);
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            if (bodyAlpha > 0.004f) {
                Vector2 drawPos = npc.Center - screenPos;

                //施法辉光(A=0 加光)
                if (context.CastAura > 0.01f) {
                    Color aura = context.AuraColor with { A = 0 };
                    sb.Draw(glow, drawPos, null, aura * (0.55f * context.CastAura * bodyAlpha), 0f,
                        glow.Size() * 0.5f, 2.6f * context.CastAura + 1.2f, SpriteEffects.None, 0f);
                }

                //vanilla 帧体
                SpriteEffects flip = npc.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                Vector2 origin = npc.frame.Size() * 0.5f;
                Vector2 bodyPos = npc.Center - screenPos + new Vector2(0f, npc.gfxOffY + 4f);
                sb.Draw(tex, bodyPos, npc.frame, drawColor * bodyAlpha, npc.rotation, origin,
                    npc.scale * context.ScalePulse, flip, 0f);

                //炽体:同帧加色复写,白热从体内透出
                if (context.BodyHot > 0.02f) {
                    Color hot = core with { A = 0 };
                    sb.Draw(tex, bodyPos, npc.frame, hot * (0.55f * context.BodyHot * bodyAlpha), npc.rotation, origin,
                        npc.scale * context.ScalePulse * (1f + context.BodyHot * 0.03f), flip, 0f);
                }
            }

            //近半环(z<0):压在本体身前
            sb.End();
            DrawWornOrrery(npc, context, -1);
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        private static VertexPositionColorTexture[] stripCache = new VertexPositionColorTexture[128];

        /// <summary>
        /// 通用折线条带(TechStarLine/TechUmbra):屏幕系点列+逐点半宽/透明度,u 沿线 0~1<br/>
        /// 调用方须不在 SpriteBatch 批内;闭环时点列首尾重复,uDash 取整数保跨缝连续
        /// </summary>
        public static void DrawTechniqueStrip(string technique, IReadOnlyList<Vector2> screenPts,
            IReadOnlyList<float> halfWidths, IReadOnlyList<float> alphas,
            Color deep, Color mid, Color bright,
            float uProgress, float uDash, float uCharge, float seed, float uAlpha = 1f) {
            Effect fx = EffectLoader.CultistOrrery?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            int n = screenPts.Count;
            if (fx == null || noise == null || n < 2 || uAlpha <= 0.01f) {
                return;
            }

            if (stripCache.Length < n * 2) {
                stripCache = new VertexPositionColorTexture[n * 2];
            }
            for (int i = 0; i < n; i++) {
                Vector2 dirA = i > 0 ? screenPts[i] - screenPts[i - 1] : screenPts[i + 1] - screenPts[i];
                Vector2 dirB = i < n - 1 ? screenPts[i + 1] - screenPts[i] : screenPts[i] - screenPts[i - 1];
                Vector2 tangent = (dirA + dirB).SafeNormalize(Vector2.UnitX);
                Vector2 normal = new(-tangent.Y, tangent.X);
                float hw = halfWidths[i];
                float a = alphas[i];
                Color vc = new(1f, 1f, 1f, a);
                float u = i / (float)(n - 1);
                stripCache[i * 2] = new VertexPositionColorTexture(new Vector3(screenPts[i] + normal * hw, 0f), vc, new Vector2(u, 0f));
                stripCache[i * 2 + 1] = new VertexPositionColorTexture(new Vector3(screenPts[i] - normal * hw, 0f), vc, new Vector2(u, 1f));
            }

            fx.CurrentTechnique = fx.Techniques[technique];
            fx.Parameters["transformMatrix"]?.SetValue(Main.GameViewMatrix.TransformationMatrix
                * Matrix.CreateOrthographicOffCenter(0, Main.screenWidth, Main.screenHeight, 0, -1, 1));
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uAlpha"]?.SetValue(uAlpha);
            fx.Parameters["uColDeep"]?.SetValue(deep.ToVector3());
            fx.Parameters["uColMid"]?.SetValue(mid.ToVector3());
            fx.Parameters["uColBright"]?.SetValue(bright.ToVector3());
            fx.Parameters["uColHot"]?.SetValue(new Vector3(1f, 0.99f, 0.96f));
            fx.Parameters["uCharge"]?.SetValue(MathHelper.Clamp(uCharge, 0f, 1f));
            fx.Parameters["uProgress"]?.SetValue(uProgress);
            fx.Parameters["uDash"]?.SetValue(uDash);
            fx.Parameters["uSeed"]?.SetValue(seed);
            fx.Parameters["uArm"]?.SetValue(0f);
            fx.Parameters["uEnv"]?.SetValue(0f);

            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.BlendState = BlendState.AlphaBlend;
            gd.RasterizerState = RasterizerState.CullNone;
            gd.DepthStencilState = DepthStencilState.None;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            fx.CurrentTechnique.Passes[0].Apply();
            gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, stripCache, 0, n * 2 - 2);
        }

        /// <summary>
        /// 星珠精灵分层:暗缘→主体→热芯(StarTexture_White 真 alpha,可承实色遮挡)<br/>
        /// 调用方处于实体批;scale 按热芯可见半径折算(核心区≈108px@1.0)
        /// </summary>
        public static void DrawStarBead(SpriteBatch sb, Vector2 screenPos, Color mid, Color edge,
            float scale, float alpha, float rotation) {
            Texture2D star = CWRAsset.StarTexture_White?.Value;
            if (star == null) {
                return;
            }
            Vector2 origin = star.Size() * 0.5f;
            //暗缘:亮背景下的剪影保障
            Color dark = Color.Lerp(edge, Color.Black, 0.62f) with { A = 255 };
            sb.Draw(star, screenPos, null, dark * alpha, rotation, origin, scale * 1.42f, SpriteEffects.None, 0f);
            //主体
            sb.Draw(star, screenPos, null, (mid with { A = 255 }) * alpha, rotation, origin, scale, SpriteEffects.None, 0f);
            //热芯(A=0 加光)
            sb.Draw(star, screenPos, null, (Color.White with { A = 0 }) * (alpha * 0.7f), rotation + 0.5f,
                origin, scale * 0.48f, SpriteEffects.None, 0f);
        }
    }
}
