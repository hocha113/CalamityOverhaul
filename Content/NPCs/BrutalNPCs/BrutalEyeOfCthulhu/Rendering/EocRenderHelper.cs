using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Core;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Rendering
{
    /// <summary>克眼绘制辅助：血带拖尾/残影/谎言残影/本体/虹膜辉光/预警车道</summary>
    internal static class EocRenderHelper
    {
        #region 体贴图
        /// <summary>一阶段体：完整虹膜</summary>
        [VaultLoaden(CWRConstant.NPC + "BEOC/EyeOfCthulhu")]
        internal static Asset<Texture2D> BodyAsset = null;
        /// <summary>二阶段体：虹膜裂成口器</summary>
        [VaultLoaden(CWRConstant.NPC + "BEOC/EyeOfCthulhuAlt")]
        internal static Asset<Texture2D> BodyAltAsset = null;

        /// <summary>两张体贴图都是 3 帧竖排</summary>
        internal const int FrameCount = 3;
        /// <summary>贴图里眼球本体约 50px 宽，碰撞箱 100px，放大到与体型相称</summary>
        private const float BodyScale = 1.8f;
        /// <summary>一阶段帧内的眼球中心，绘制锚点用它对齐碰撞箱中心</summary>
        private static readonly Vector2 BodyOrigin = new(60.5f, 26.5f);
        /// <summary>二阶段帧内的眼球中心</summary>
        private static readonly Vector2 BodyAltOrigin = new(60f, 26f);
        /// <summary>瞳位相对眼球中心的前向偏移，单位是贴图像素</summary>
        private const float PupilForward = 22.3f;

        /// <summary>贴图正面朝 +X，而 npc.rotation 以 +Y 为正面，绘制角要补这个差</summary>
        private static float ToDrawRotation(float npcRotation) => npcRotation + MathHelper.PiOver2;

        /// <summary>按阶段取体贴图与眼球锚点</summary>
        private static void GetBodySheet(bool secondPhase, out Texture2D tex, out Vector2 origin) {
            tex = (secondPhase ? BodyAltAsset : BodyAsset).Value;
            origin = secondPhase ? BodyAltOrigin : BodyOrigin;
        }

        /// <summary>取帧矩形，越界夹回</summary>
        private static Rectangle GetFrameRect(Texture2D tex, int frame) {
            int frameHeight = tex.Height / FrameCount;
            return new Rectangle(0, frameHeight * Math.Clamp(frame, 0, FrameCount - 1), tex.Width, frameHeight);
        }
        #endregion

        #region 谎言残影（变轨时沿旧轨道继续飞的假身）
        private struct LiarGhost
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Rotation;
            public int Frame;
            public int Life;
            public int MaxLife;
            public bool Phase2;
        }

        private const int MaxGhosts = 6;
        private static readonly List<LiarGhost> ghosts = new(MaxGhosts);

        /// <summary>压入谎言残影，客户端表现</summary>
        public static void PushLiarGhost(Vector2 pos, Vector2 vel, float rotation, int frameIndex, bool phase2) {
            if (VaultUtils.isServer) {
                return;
            }
            if (ghosts.Count >= MaxGhosts) {
                ghosts.RemoveAt(0);
            }
            ghosts.Add(new LiarGhost {
                Position = pos,
                Velocity = vel,
                Rotation = rotation,
                Frame = frameIndex,
                Life = 22,
                MaxLife = 22,
                Phase2 = phase2,
            });
        }

        /// <summary>每帧推进，渲染句柄驱动（与本体是否在屏内无关）</summary>
        public static void UpdateGhosts() {
            for (int i = ghosts.Count - 1; i >= 0; i--) {
                LiarGhost g = ghosts[i];
                g.Position += g.Velocity;
                g.Velocity *= 0.96f;
                g.Life--;
                if (g.Life <= 0) {
                    ghosts.RemoveAt(i);
                    continue;
                }
                ghosts[i] = g;
            }
        }

        public static void ClearGhosts() => ghosts.Clear();

        private static void DrawLiarGhosts(SpriteBatch sb, Vector2 screenPos) {
            if (ghosts.Count == 0) {
                return;
            }
            foreach (LiarGhost g in ghosts) {
                GetBodySheet(g.Phase2, out Texture2D tex, out Vector2 origin);
                Rectangle rec = GetFrameRect(tex, g.Frame);
                float t = g.Life / (float)g.MaxLife;
                float rot = ToDrawRotation(g.Rotation);
                //苍白假身，加色叠加读作幻影
                Color ghostColor = new Color(226, 200, 196, 0) * (0.42f * t);
                sb.Draw(tex, g.Position - screenPos, rec, ghostColor,
                    rot, origin, BodyScale, SpriteEffects.None, 0f);
                //暗红衬底防纯白幻影飘成塑料
                sb.Draw(tex, g.Position - screenPos, rec, new Color(120, 22, 30, 0) * (0.3f * t),
                    rot, origin, BodyScale * 1.03f, SpriteEffects.None, 0f);
            }
        }
        #endregion

        #region 血带拖尾
        private const int TrailPointCount = 22;
        private static Trail bloodTrail;
        private static readonly Vector2[] trailPositions = new Vector2[TrailPointCount];
        private static float trailWidth;
        private static float trailAlpha;

        /// <summary>oldPos 血带，强度过阈才绘</summary>
        public static void DrawBloodTrail(NPC npc, float intensity) {
            if (intensity <= 0.06f) {
                return;
            }
            Effect effect = EffectLoader.EocBloodTrail?.Value;
            bool bespoke = effect != null;
            if (!bespoke) {
                effect = EffectLoader.GradientTrail?.Value;
            }
            if (effect == null) {
                return;
            }

            Span<Vector2> gathered = stackalloc Vector2[TrailPointCount];
            int count = 0;
            gathered[count++] = npc.Center;
            for (int i = 0; i < npc.oldPos.Length && count < TrailPointCount; i++) {
                if (npc.oldPos[i] == Vector2.Zero) {
                    break;
                }
                Vector2 pos = npc.oldPos[i] + npc.Size / 2f;
                //滤掉雾步/瞬移超长段
                if (Vector2.DistanceSquared(pos, gathered[count - 1]) > 380f * 380f) {
                    break;
                }
                gathered[count++] = pos;
            }
            if (count < 4) {
                return;
            }

            Vector2 oldest = gathered[count - 1];
            int pad = TrailPointCount - count;
            for (int i = 0; i < pad; i++) {
                trailPositions[i] = oldest;
            }
            for (int i = 0; i < count; i++) {
                trailPositions[pad + i] = gathered[count - 1 - i];
            }

            trailWidth = 54f * intensity;
            trailAlpha = 0.9f * intensity;

            bloodTrail ??= new Trail(new Vector2[TrailPointCount],
                f => trailWidth * (0.16f + f * 0.84f),
                texCoord => Color.Lerp(EocMotion.VenousDark, EocMotion.Arterial, texCoord.X)
                    * (trailAlpha * (0.2f + texCoord.X * 0.8f)));
            bloodTrail.TrailPositions = trailPositions;

            if (bespoke) {
                effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
                effect.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.035f);
                effect.Parameters["uIntensity"]?.SetValue(intensity);
                //噪声显式绑到 s1（shader 内 register(s1)），参数式绑定废弃
                GraphicsDevice gd = Main.graphics.GraphicsDevice;
                gd.Textures[1] = CWRAsset.PerlinNoise.Value;
                gd.SamplerStates[1] = SamplerState.LinearWrap;
                gd.BlendState = BlendState.AlphaBlend;
                bloodTrail.DrawTrail(effect);
                Main.graphics.GraphicsDevice.BlendState = BlendState.AlphaBlend;
                return;
            }

            //缺 fxc 回退：GradientTrail + 血红渐变
            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * -0.05f);
            effect.Parameters["uTimeG"]?.SetValue(Main.GlobalTimeWrappedHourly * -0.2f);
            effect.Parameters["udissolveS"]?.SetValue(1f);
            effect.Parameters["uBaseImage"]?.SetValue(VaultAsset.placeholder2.Value);
            effect.Parameters["uFlow"]?.SetValue(VaultAsset.placeholder2.Value);
            effect.Parameters["uGradient"]?.SetValue(CWRAsset.BloodRed_Bar.Value);
            effect.Parameters["uDissolve"]?.SetValue(VaultAsset.placeholder2.Value);
            Main.graphics.GraphicsDevice.BlendState = BlendState.Additive;
            for (int i = 0; i < 2; i++) {
                bloodTrail.DrawTrail(effect);
            }
            Main.graphics.GraphicsDevice.BlendState = BlendState.AlphaBlend;
        }

        internal static void Unload() {
            bloodTrail?.Dispose();
            bloodTrail = null;
            ghosts.Clear();
        }
        #endregion

        #region 本体
        /// <summary>完整本体绘制：拖尾→残影→谎言残影→本体→撕皮缝→虹膜辉光</summary>
        public static void DrawBody(SpriteBatch sb, NPC npc, EocStateContext ctx, Vector2 screenPos, Color drawColor) {
            GetBodySheet(ctx.IsSecondPhase, out Texture2D tex, out Vector2 origin);
            Rectangle rec = GetFrameRect(tex, ctx.FrameIndex);
            Vector2 mainPos = npc.Center - screenPos;
            //worldScale 是体型倍率，drawScale 才是贴图倍率，附着特效按前者定尺寸
            float worldScale = npc.scale * ctx.ScalePulse;
            float drawScale = worldScale * BodyScale;
            float drawRot = ToDrawRotation(npc.rotation);

            //拖尾在最底
            float trailIntensity = Math.Max(ctx.TrailHeat,
                MathHelper.Clamp((npc.velocity.Length() - 14f) / 34f, 0f, 1f) * 0.8f);
            DrawBloodTrail(npc, trailIntensity * (1f - ctx.FogHide));

            //速度残影
            float ghostAlpha = ctx.AfterimageBoost * (1f - ctx.FogHide);
            if (ghostAlpha > 0.05f) {
                int step = 3;
                for (int i = step; i < npc.oldPos.Length; i += step) {
                    if (npc.oldPos[i] == Vector2.Zero) {
                        break;
                    }
                    float t = 1f - i / (float)npc.oldPos.Length;
                    Vector2 pos = npc.oldPos[i] + npc.Size / 2f - screenPos;
                    Color c = new Color(150, 24, 32, 40) * (ghostAlpha * t * 0.55f);
                    sb.Draw(tex, pos, rec, c, ToDrawRotation(npc.oldRot[i]), origin,
                        drawScale * (0.96f - i * 0.004f), SpriteEffects.None, 0f);
                }
            }

            DrawLiarGhosts(sb, screenPos);

            //本体，雾隐时压暗压透
            float bodyOpacity = 1f - ctx.FogHide * 0.92f;
            Color bodyColor = drawColor;
            if (ctx.FogHide > 0.01f) {
                bodyColor = Color.Lerp(drawColor, EocMotion.MistWine, ctx.FogHide * 0.6f);
            }
            sb.Draw(tex, mainPos, rec, bodyColor * bodyOpacity, drawRot, origin, drawScale, SpriteEffects.None, 0f);

            //撕皮缝：转阶段内压亮缝+溢光
            if (ctx.SkinTear > 0.02f) {
                DrawTearSeam(sb, npc, ctx, mainPos, worldScale);
            }

            //虹膜辉光：雾里也保留微光（公平阀：藏身不隐踪）
            float irisGlow = Math.Max(ctx.IrisGlow, ctx.FogHide * 0.35f);
            if (irisGlow > 0.02f) {
                DrawIrisGlow(sb, npc, ctx, mainPos, irisGlow, worldScale, drawScale);
            }
        }

        /// <summary>虹膜辉光：定向瞳位光斑+柔光衬底</summary>
        private static void DrawIrisGlow(SpriteBatch sb, NPC npc, EocStateContext ctx, Vector2 mainPos
            , float glow, float worldScale, float drawScale) {
            Texture2D soft = CWRAsset.SoftGlow.Value;
            Texture2D flare = CWRAsset.StarFlare02.Value;
            //瞳位跟着贴图倍率走，光斑尺寸仍按体型倍率
            Vector2 pupilDir = (npc.rotation + MathHelper.PiOver2).ToRotationVector2();
            Vector2 pupilPos = mainPos + pupilDir * (PupilForward * drawScale);
            Color glowColor = ctx.IrisColor with { A = 0 };
            sb.Draw(soft, pupilPos, null, glowColor * (glow * 0.85f), 0f,
                soft.Size() / 2f, 1.5f * glow * worldScale, SpriteEffects.None, 0f);
            sb.Draw(flare, pupilPos, null, glowColor * (glow * 0.7f),
                Main.GlobalTimeWrappedHourly * 1.7f, flare.Size() / 2f, 0.22f * glow * worldScale, SpriteEffects.None, 0f);
        }

        /// <summary>撕皮缝：沿体轴的亮血裂缝，宽度随进度</summary>
        private static void DrawTearSeam(SpriteBatch sb, NPC npc, EocStateContext ctx, Vector2 mainPos, float scale) {
            Texture2D line = CWRAsset.Line.Value;
            float tear = ctx.SkinTear;
            //体轴方向（瞳孔朝向）
            float axisRot = npc.rotation + MathHelper.PiOver2;
            float len = 96f * scale * (0.35f + tear * 0.65f);
            float width = (1.5f + tear * 10f) * scale;
            Vector2 lineScale = new(len / line.Width, width / line.Height);
            Color seamCore = EocMotion.BrightBlood with { A = 0 };
            Color seamEdge = EocMotion.Arterial with { A = 0 };
            //脉动
            float pulse = 0.75f + 0.25f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 21f);
            sb.Draw(line, mainPos, null, seamEdge * (tear * 0.8f), axisRot,
                line.Size() / 2f, lineScale * new Vector2(1f, 2.2f), SpriteEffects.None, 0f);
            sb.Draw(line, mainPos, null, seamCore * (tear * pulse), axisRot,
                line.Size() / 2f, lineScale, SpriteEffects.None, 0f);
        }
        #endregion

        #region 预警车道
        /// <summary>冲刺车道预警，本体 Draw 内调用（Deferred 批下用 Immediate 短开）</summary>
        public static void DrawTelegraphLane(SpriteBatch sb, EocStateContext ctx) {
            if (ctx.LaneIntensity <= 0.03f) {
                return;
            }
            Effect effect = EffectLoader.EocTelegraph?.Value;
            Vector2 start = ctx.LaneStart - Main.screenPosition;
            float rot = ctx.LaneDir.ToRotation();

            if (effect == null) {
                //缺 fxc 回退：细车道线
                Texture2D line = CWRAsset.Line.Value;
                Vector2 lscale = new(ctx.LaneLength / line.Width, 3f / line.Height);
                sb.Draw(line, start, null, (EocMotion.Arterial with { A = 0 }) * (ctx.LaneIntensity * 0.6f),
                    rot, new Vector2(0, line.Height / 2f), lscale, SpriteEffects.None, 0f);
                return;
            }

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            effect.CurrentTechnique = effect.Techniques["LaneTech"];
            effect.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.03f);
            effect.Parameters["uProgress"]?.SetValue(ctx.LaneProgress);
            effect.Parameters["uIntensity"]?.SetValue(ctx.LaneIntensity);
            effect.CurrentTechnique.Passes[0].Apply();

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 pScale = new(ctx.LaneLength / pixel.Width, 130f / pixel.Height);
            sb.Draw(pixel, start, null, Color.White, rot, new Vector2(0, pixel.Height / 2f), pScale, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>预警环，雾团出击/合围标记用；世界坐标，radius 像素</summary>
        public static void DrawTelegraphRing(SpriteBatch sb, Vector2 worldCenter, float radius, float progress, float intensity) {
            if (intensity <= 0.03f) {
                return;
            }
            Effect effect = EffectLoader.EocTelegraph?.Value;
            Vector2 center = worldCenter - Main.screenPosition;

            if (effect == null) {
                Texture2D ring = CWRAsset.DiffusionCircle.Value;
                float rscale = radius * 2f / ring.Width;
                sb.Draw(ring, center, null, (EocMotion.Arterial with { A = 0 }) * (intensity * 0.5f), 0f,
                    ring.Size() / 2f, rscale, SpriteEffects.None, 0f);
                return;
            }

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            effect.CurrentTechnique = effect.Techniques["RingTech"];
            effect.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.03f);
            effect.Parameters["uProgress"]?.SetValue(progress);
            effect.Parameters["uIntensity"]?.SetValue(intensity);
            effect.CurrentTechnique.Passes[0].Apply();

            Texture2D pixel = VaultAsset.placeholder2.Value;
            float side = radius * 2.6f;
            Vector2 pScale = new(side / pixel.Width, side / pixel.Height);
            sb.Draw(pixel, center, null, Color.White, 0f, pixel.Size() / 2f, pScale, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }
        #endregion
    }
}
