using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniDismembers;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFinaleSlashs;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniOmokages
{
    /// <summary>面影渲染</summary>
    internal sealed class OniOmokageRender : RenderHandle
    {
        public override float Weight => 1.3f;

        private const int MaxCapturesPerFrame = 2;
        private const long MaxCapturePixelsPerFrame = 1024L * 1024L;

        //复用缓冲，避免逐帧分配

        private static readonly List<VertexPositionColorTexture> vertexScratch = new(64);
        private static VertexPositionColorTexture[] vertexBuffer = new VertexPositionColorTexture[64];
        private static readonly List<int> pruneScratch = [];
        private static readonly List<OmokageEntry> fallbackScratch = [];

        public override void UpdateBySystem(int index) {
            //回主菜单后实体状态已失效，释放全部状态与快照

            if (Main.gameMenu && (OniOmokage.Entries.Count > 0 || OniOmokage.Snaps.Count > 0)) {
                OniOmokage.Clear();
            }
        }

        public override void DrawNPCsOverTiles(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.gameMenu) {
                return;
            }

            PruneOrphanSnaps();
            CapturePending(spriteBatch, graphicsDevice, screenSwap);

            fallbackScratch.Clear();
            if (OniOmokage.Entries.Count > 0) {
                DrawPapers(graphicsDevice);
            }
        }

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main) {
            if (Main.gameMenu) {
                return;
            }
            if (OniOmokage.Entries.Count == 0 && OniOmokage.Pulses.Count == 0) {
                return;
            }
            DrawFallbackDolls(spriteBatch);
            DrawThreadsAndPulses(spriteBatch);
        }

        private static void CapturePending(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (!AnyPendingCapture()) {
                return;
            }

            //低质量光照或 RT 异常时改走基础帧纸偶

            if (RenderQualitySafety.ScreenTargetUnavailable()) {
                CompletePendingAsFallback();
                return;
            }
            if (screenSwap == null || screenSwap.IsDisposed) {
                CompletePendingAsFallback();
                return;
            }
            if (Main.screenTarget == null || Main.screenTarget.IsDisposed) {
                CompletePendingAsFallback();
                return;
            }
            if (!RenderQualitySafety.IsScreenTargetActive(graphicsDevice)) {
                CompletePendingAsFallback();
                return;
            }

            RenderTargetBinding[] previousTargets = graphicsDevice.GetRenderTargets();

            //先保屏、screenTarget 一旦重绑定内容即被丢弃

            graphicsDevice.SetRenderTarget(screenSwap);
            graphicsDevice.Clear(Color.Transparent);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque);
            spriteBatch.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            spriteBatch.End();

            int captures = 0;
            long capturePixels = 0;
            foreach (KeyValuePair<int, OmokageSnap> pair in OniOmokage.Snaps) {
                if (captures >= MaxCapturesPerFrame) {
                    break;
                }
                OmokageSnap snap = pair.Value;
                if (snap.Captured) {
                    continue;
                }
                NPC npc = OniOmokage.ValidTarget(pair.Key, snap.NpcType, snap.NpcSpawnToken);
                if (npc == null) {
                    continue;
                }
                OniOmokage.RefreshSnapForCapture(npc, snap);
                long pixelCost = (long)snap.Width * snap.Height;
                if (captures > 0 && capturePixels + pixelCost > MaxCapturePixelsPerFrame) {
                    continue;
                }
                captures++;
                capturePixels += pixelCost;
                if (!EnsureSnapRT(graphicsDevice, snap)) {
                    RegisterCaptureFailure(snap);
                    continue;
                }
                if (OniDismemberRender.CaptureNpcAppearance(spriteBatch,
                    graphicsDevice, npc, snap.RT, npc.Center, npc.behindTiles)) {
                    snap.Captured = true;
                    snap.CaptureUnavailable = false;
                }
                else {
                    RegisterCaptureFailure(snap);
                }
            }

            //还屏

            graphicsDevice.SetRenderTarget(Main.screenTarget);
            graphicsDevice.Clear(Color.Transparent);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque);
            spriteBatch.Draw(screenSwap, Vector2.Zero, Color.White);
            spriteBatch.End();

            //还原进入时的 RT 绑定

            if (previousTargets != null && previousTargets.Length > 0
                && previousTargets[0].RenderTarget != Main.screenTarget) {
                graphicsDevice.SetRenderTargets(previousTargets);
            }
        }

        private static bool AnyPendingCapture() {
            foreach (OmokageSnap snap in OniOmokage.Snaps.Values) {
                if (!snap.Captured) {
                    return true;
                }
            }
            return false;
        }

        private static void RegisterCaptureFailure(OmokageSnap snap) {
            snap.CaptureFailures++;
            if (snap.CaptureFailures < OniOmokage.MaxCaptureFailures) {
                return;
            }
            snap.RT?.Dispose();
            snap.RT = null;
            snap.Captured = true;
            snap.CaptureUnavailable = true;
        }

        private static void CompletePendingAsFallback() {
            foreach (KeyValuePair<int, OmokageSnap> pair in OniOmokage.Snaps) {
                OmokageSnap snap = pair.Value;
                if (snap.Captured) {
                    continue;
                }
                NPC npc = OniOmokage.ValidTarget(pair.Key, snap.NpcType, snap.NpcSpawnToken);
                if (npc != null) {
                    OniOmokage.RefreshSnapForCapture(npc, snap);
                }
                snap.RT?.Dispose();
                snap.RT = null;
                snap.CaptureFailures = OniOmokage.MaxCaptureFailures;
                snap.Captured = true;
                snap.CaptureUnavailable = true;
            }
        }

        private static bool EnsureSnapRT(GraphicsDevice gd, OmokageSnap snap) {
            if (snap.RT != null && !snap.RT.IsDisposed
                && snap.RT.Width == snap.Width && snap.RT.Height == snap.Height) {
                return true;
            }
            snap.RT?.Dispose();
            try {
                snap.RT = new RenderTarget2D(gd, snap.Width, snap.Height, false,
                    SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            } catch {
                snap.RT = null;
                return false;
            }
            return true;
        }

        /// <summary>释放已无面影引用的快照</summary>
        private static void PruneOrphanSnaps() {
            if (OniOmokage.Snaps.Count == 0) {
                return;
            }
            pruneScratch.Clear();
            foreach (int npcIndex in OniOmokage.Snaps.Keys) {
                OmokageSnap snap = OniOmokage.Snaps[npcIndex];
                bool referenced = false;
                foreach (OmokageEntry entry in OniOmokage.Entries) {
                    if (entry.NpcIndex == npcIndex
                        && entry.NpcSpawnToken == snap.NpcSpawnToken) {
                        referenced = true;
                        break;
                    }
                }
                if (!referenced) {
                    pruneScratch.Add(npcIndex);
                }
            }
            foreach (int npcIndex in pruneScratch) {
                OniOmokage.Snaps[npcIndex].RT?.Dispose();
                OniOmokage.Snaps.Remove(npcIndex);
            }
        }

        private static void DrawPapers(GraphicsDevice gd) {
            Effect fx = EffectLoader.OniOmokage?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;

            BlendState prevBlend = gd.BlendState;
            RasterizerState prevRaster = gd.RasterizerState;
            DepthStencilState prevDepth = gd.DepthStencilState;
            gd.BlendState = BlendState.AlphaBlend;
            gd.RasterizerState = RasterizerState.CullNone;
            gd.DepthStencilState = DepthStencilState.None;

            float time = (float)Main.timeForVisualEffects * 0.016f;
            if (fx != null) {
                fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
                fx.Parameters["uTime"]?.SetValue(time);
                fx.Parameters["uNoiseTex"]?.SetValue(noise);
            }

            foreach (OmokageEntry entry in OniOmokage.Entries) {
                float alpha = entry.Alpha;
                if (alpha <= 0.01f) {
                    continue;
                }

                //快照不可用时改画基础帧纸偶

                if (fx == null || noise == null || !TryGetSnapRT(entry, out RenderTarget2D rt)) {
                    fallbackScratch.Add(entry);
                    continue;
                }

                SetEntryParams(fx, entry, rt, time);
                BuildEntryVertices(entry, time, alpha);
                if (vertexScratch.Count < 3) {
                    continue;
                }

                if (vertexBuffer.Length < vertexScratch.Count) {
                    Array.Resize(ref vertexBuffer, vertexScratch.Count);
                }
                vertexScratch.CopyTo(vertexBuffer);
                foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                    pass.Apply();
                    gd.DrawUserPrimitives(PrimitiveType.TriangleList, vertexBuffer, 0, vertexScratch.Count / 3);
                }
            }

            gd.BlendState = prevBlend;
            gd.RasterizerState = prevRaster;
            gd.DepthStencilState = prevDepth;
        }

        private static bool TryGetSnapRT(OmokageEntry entry, out RenderTarget2D rt) {
            rt = null;
            if (!OniOmokage.Snaps.TryGetValue(entry.NpcIndex, out OmokageSnap snap)) {
                return false;
            }
            if (snap.NpcType != entry.NpcType || snap.NpcSpawnToken != entry.NpcSpawnToken
                || !snap.Captured || snap.CaptureUnavailable
                || snap.RT == null || snap.RT.IsDisposed) {
                return false;
            }
            if (snap.Width != entry.SnapWidth || snap.Height != entry.SnapHeight) {
                return false;
            }
            rt = snap.RT;
            return true;
        }

        private static void SetEntryParams(Effect fx, OmokageEntry entry, RenderTarget2D rt, float time) {
            Vector2 paperSize = entry.PaperHalf * 2f;

            //斩纸头 3 帧裂口白闪

            float cutFlash = entry.Cut && entry.CutAge <= 3 ? 1f - entry.CutAge / 4f : 0f;
            float develop = entry.Develop;
            //朱印呼吸，玩家靠近增亮（无字教学、这张纸可以斩）

            float sealGlow = 0.72f + 0.22f * MathF.Sin(time * 2.3f + entry.SwayPhase);
            float playerDist = Vector2.Distance(Main.LocalPlayer.Center, entry.RenderCenter);
            sealGlow += 0.45f * (1f - MathHelper.Clamp(playerDist / 220f, 0f, 1f));
            //烧散前沿红烬；斩开的纸余温

            float ember = entry.Burning ? 1f : (entry.Cut ? 0.35f : 0f);

            fx.Parameters["uSnapSize"]?.SetValue(new Vector2(entry.SnapWidth, entry.SnapHeight));
            fx.Parameters["uPaperSize"]?.SetValue(paperSize);
            fx.Parameters["uDissolve"]?.SetValue(entry.Dissolve);
            fx.Parameters["uDevelop"]?.SetValue(develop);
            fx.Parameters["uCutFlash"]?.SetValue(cutFlash);
            fx.Parameters["uSeed"]?.SetValue(entry.Seed);
            fx.Parameters["uSealGlow"]?.SetValue(sealGlow);
            fx.Parameters["uEmber"]?.SetValue(ember);
            fx.Parameters["uSnapTex"]?.SetValue(rt);
        }

        /// <summary>完整纸偶或裂片转为三角扇顶点，uv 保持纸面归一坐标</summary>
        private static void BuildEntryVertices(OmokageEntry entry, float time, float alpha) {
            vertexScratch.Clear();
            Color tint = Color.White * alpha;
            Vector2 paperHalf = entry.PaperHalf;
            Vector2 paperSize = paperHalf * 2f;

            //滑开进度以实际裂开为时基、刀线滞拍期（SplitAge<0）纸还完整

            float cutEase = entry.Cut
                ? OniFinaleRenderer.EaseOutCubic(MathHelper.Clamp(entry.SplitAge / (float)OniOmokage.CutSlideFrames, 0f, 1f))
                : 0f;
            //纸偶轻摆，斩开后大幅收敛

            float sway = MathF.Sin(time * 0.8f + entry.SwayPhase) * 0.03f * (1f - cutEase * 0.7f);
            float swaySin = MathF.Sin(sway);
            float swayCos = MathF.Cos(sway);
            float unfold = OniFinaleRenderer.EaseOutCubic(entry.Reveal);
            float foldX = MathHelper.Lerp(0.08f, 1f, unfold)
                * (0.988f + MathF.Sin(time * 1.3f + entry.SwayPhase) * 0.012f);

            Vector2 cutDir = entry.CutAngle.ToRotationVector2();
            Vector2 cutNormal = new(-cutDir.Y, cutDir.X);
            float slideDist = (10f + MathF.Min(paperHalf.X, paperHalf.Y) * 0.10f) * cutEase;

            if (!entry.Cut || entry.Halves.Count == 0) {
                Span<Vector2> quad = stackalloc Vector2[4] {
                    new(-paperHalf.X, -paperHalf.Y), new(paperHalf.X, -paperHalf.Y),
                    new(paperHalf.X, paperHalf.Y), new(-paperHalf.X, paperHalf.Y),
                };
                AppendPolygon(entry, quad, Vector2.Zero, 0f, swaySin, swayCos, foldX, paperSize, tint);
                return;
            }

            for (int i = 0; i < entry.Halves.Count; i++) {
                sbyte side = entry.HalfSides[i];
                Vector2 offset = cutNormal * (side * slideDist);
                float halfRot = side * 0.028f * cutEase;
                AppendPolygon(entry, entry.Halves[i], offset, halfRot, swaySin, swayCos, foldX, paperSize, tint);
            }
        }

        private static void AppendPolygon(OmokageEntry entry, ReadOnlySpan<Vector2> poly, Vector2 slideOffset,
            float halfRot, float swaySin, float swayCos, float foldX, Vector2 paperSize, Color tint) {

            if (poly.Length < 3) {
                return;
            }

            //质心、裂片绕自身质心微转

            Vector2 centroid = Vector2.Zero;
            foreach (Vector2 v in poly) {
                centroid += v;
            }
            centroid /= poly.Length;
            float rSin = MathF.Sin(halfRot);
            float rCos = MathF.Cos(halfRot);

            Span<Vector2> world = stackalloc Vector2[poly.Length];
            for (int i = 0; i < poly.Length; i++) {
                //绕裂片质心微转 → 滑开位移 → 纸偶摇摆 → 锚点定位

                Vector2 rel = poly[i] - centroid;
                Vector2 spun = new(rel.X * rCos - rel.Y * rSin, rel.X * rSin + rel.Y * rCos);
                Vector2 local = centroid + spun + slideOffset;
                local.X *= foldX;
                Vector2 swayed = new(local.X * swayCos - local.Y * swaySin, local.X * swaySin + local.Y * swayCos);
                world[i] = entry.RenderCenter + swayed;
            }

            for (int i = 1; i < poly.Length - 1; i++) {
                AppendVertex(world[0], poly[0], paperSize, tint);
                AppendVertex(world[i], poly[i], paperSize, tint);
                AppendVertex(world[i + 1], poly[i + 1], paperSize, tint);
            }
        }

        private static void AppendVertex(Vector2 worldPos, Vector2 paperLocal, Vector2 paperSize, Color tint) {
            //uv 取未位移的纸面坐标、裂开后墨迹仍与各自纸片对齐

            Vector2 uv = paperLocal / paperSize + new Vector2(0.5f);
            vertexScratch.Add(new VertexPositionColorTexture(worldPos.ToVector3(), tint, uv));
        }

        private static void DrawFallbackDolls(SpriteBatch spriteBatch) {
            if (fallbackScratch.Count == 0) {
                return;
            }

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);

            float time = (float)Main.timeForVisualEffects * 0.016f;
            foreach (OmokageEntry entry in fallbackScratch) {
                if (!OniOmokage.Snaps.TryGetValue(entry.NpcIndex, out OmokageSnap snap)
                    || snap.NpcType != entry.NpcType || snap.NpcSpawnToken != entry.NpcSpawnToken
                    || snap.SourceFrame.Width <= 0 || snap.SourceFrame.Height <= 0) {
                    continue;
                }

                Main.instance.LoadNPC(snap.NpcType);
                Texture2D texture = TextureAssets.Npc[snap.NpcType].Value;
                Rectangle frame = Rectangle.Intersect(new Rectangle(0, 0, texture.Width, texture.Height), snap.SourceFrame);
                if (frame.Width <= 0 || frame.Height <= 0) {
                    continue;
                }

                float dissolveFade = 1f - MathHelper.Clamp((entry.Dissolve - 0.55f) / 0.45f, 0f, 1f) * 0.35f;
                float alpha = entry.Alpha * entry.Develop * dissolveFade;
                if (alpha <= 0.01f) {
                    continue;
                }
                float unfold = OniFinaleRenderer.EaseOutCubic(entry.Reveal);
                float foldX = MathHelper.Lerp(0.08f, 1f, unfold)
                    * (0.988f + MathF.Sin(time * 1.3f + entry.SwayPhase) * 0.012f);
                float sway = MathF.Sin(time * 0.8f + entry.SwayPhase) * 0.025f;
                float rotation = snap.SourceRotation + sway;
                Vector2 drawOffset = Vector2.UnitY * snap.SourceDrawOffsetY;
                Vector2 pos = entry.RenderCenter + drawOffset - Main.screenPosition;
                Vector2 scale = new(snap.SourceScale * foldX, snap.SourceScale);
                Color edge = new Color(40, 24, 28) * (alpha * 0.82f);
                Color paper = new Color(218, 201, 164) * alpha;
                float cutFlash = entry.Cut && entry.CutAge <= 3 ? 1f - entry.CutAge / 4f : 0f;
                paper = Color.Lerp(paper, new Color(255, 230, 194) * alpha, cutFlash * 0.78f);

                if (entry.Cut && entry.SplitAge >= 0) {
                    DrawFallbackSplit(spriteBatch, texture, frame, snap, entry, pos, drawOffset,
                        scale, rotation, edge, paper);
                }
                else {
                    DrawFallbackPiece(spriteBatch, texture, frame, frame, pos, Vector2.Zero,
                        scale, rotation, snap.SourceEffects, edge, paper);
                }

            }

            spriteBatch.End();
        }

        private static void DrawFallbackSplit(SpriteBatch spriteBatch, Texture2D texture, Rectangle frame,
            OmokageSnap snap, OmokageEntry entry, Vector2 pos, Vector2 drawOffset, Vector2 scale,
            float rotation, Color edge, Color paper) {

            if (frame.Width < 2 || frame.Height < 2) {
                DrawFallbackPiece(spriteBatch, texture, frame, frame, pos, Vector2.Zero,
                    scale, rotation, snap.SourceEffects, edge, paper);
                return;
            }

            Vector2 cutNormal = new(-MathF.Sin(entry.CutAngle), MathF.Cos(entry.CutAngle));
            Vector2 localCutWorld = (entry.CutLocal - drawOffset).RotatedBy(-rotation);
            Vector2 localNormal = cutNormal.RotatedBy(-rotation);
            float flipX = (snap.SourceEffects & SpriteEffects.FlipHorizontally) != 0 ? -1f : 1f;
            Vector2 sourceCut = new(localCutWorld.X / MathF.Max(scale.X, 0.001f) * flipX,
                localCutWorld.Y / MathF.Max(scale.Y, 0.001f));
            sourceCut += frame.Size() * 0.5f;
            sourceCut.X = MathHelper.Clamp(sourceCut.X, frame.Width * 0.2f, frame.Width * 0.8f);
            sourceCut.Y = MathHelper.Clamp(sourceCut.Y, frame.Height * 0.2f, frame.Height * 0.8f);
            Vector2 sourceNormal = new(localNormal.X * scale.X * flipX, localNormal.Y * scale.Y);

            float cutEase = OniFinaleRenderer.EaseOutCubic(MathHelper.Clamp(
                entry.SplitAge / (float)OniOmokage.CutSlideFrames, 0f, 1f));
            float slideDistance = (10f + MathF.Min(entry.PaperHalf.X, entry.PaperHalf.Y) * 0.10f) * cutEase;
            const int maxSlices = 12;

            if (MathF.Abs(sourceNormal.Y) >= MathF.Abs(sourceNormal.X)) {
                int slices = Math.Min(maxSlices, frame.Width);
                for (int i = 0; i < slices; i++) {
                    int left = frame.Left + frame.Width * i / slices;
                    int right = frame.Left + frame.Width * (i + 1) / slices;
                    float x = (left + right) * 0.5f - frame.Left;
                    float cutY = sourceCut.Y - sourceNormal.X / sourceNormal.Y * (x - sourceCut.X);
                    int split = frame.Top + (int)MathF.Round(MathHelper.Clamp(cutY, 0f, frame.Height));
                    DrawFallbackSlice(spriteBatch, texture, frame,
                        new Rectangle(left, frame.Top, right - left, split - frame.Top), snap, entry,
                        pos, drawOffset, scale, rotation, cutNormal, slideDistance, edge, paper, -1f);
                    DrawFallbackSlice(spriteBatch, texture, frame,
                        new Rectangle(left, split, right - left, frame.Bottom - split), snap, entry,
                        pos, drawOffset, scale, rotation, cutNormal, slideDistance, edge, paper, 1f);
                }
            }
            else {
                int slices = Math.Min(maxSlices, frame.Height);
                for (int i = 0; i < slices; i++) {
                    int top = frame.Top + frame.Height * i / slices;
                    int bottom = frame.Top + frame.Height * (i + 1) / slices;
                    float y = (top + bottom) * 0.5f - frame.Top;
                    float cutX = sourceCut.X - sourceNormal.Y / sourceNormal.X * (y - sourceCut.Y);
                    int split = frame.Left + (int)MathF.Round(MathHelper.Clamp(cutX, 0f, frame.Width));
                    DrawFallbackSlice(spriteBatch, texture, frame,
                        new Rectangle(frame.Left, top, split - frame.Left, bottom - top), snap, entry,
                        pos, drawOffset, scale, rotation, cutNormal, slideDistance, edge, paper, -1f);
                    DrawFallbackSlice(spriteBatch, texture, frame,
                        new Rectangle(split, top, frame.Right - split, bottom - top), snap, entry,
                        pos, drawOffset, scale, rotation, cutNormal, slideDistance, edge, paper, 1f);
                }
            }
        }

        private static void DrawFallbackSlice(SpriteBatch spriteBatch, Texture2D texture,
            Rectangle fullFrame, Rectangle pieceFrame, OmokageSnap snap, OmokageEntry entry,
            Vector2 pos, Vector2 drawOffset, Vector2 scale, float rotation, Vector2 cutNormal,
            float slideDistance, Color edge, Color paper, float fallbackSide) {

            if (pieceFrame.Width <= 0 || pieceFrame.Height <= 0) {
                return;
            }
            Vector2 pieceOffset = GetFallbackPieceOffset(fullFrame, pieceFrame, scale,
                rotation, snap.SourceEffects);
            Vector2 relativeToCut = drawOffset + pieceOffset - entry.CutLocal;
            float side = MathF.Sign(Vector2.Dot(relativeToCut, cutNormal));
            if (side == 0f) {
                side = fallbackSide;
            }
            DrawFallbackPiece(spriteBatch, texture, fullFrame, pieceFrame, pos,
                cutNormal * (side * slideDistance), scale, rotation, snap.SourceEffects, edge, paper,
                drawEdge: false);
        }

        private static void DrawFallbackPiece(SpriteBatch spriteBatch, Texture2D texture,
            Rectangle fullFrame, Rectangle pieceFrame, Vector2 pos, Vector2 slideOffset,
            Vector2 scale, float rotation, SpriteEffects effects, Color edge, Color paper,
            bool drawEdge = true) {

            Vector2 pieceOffset = GetFallbackPieceOffset(fullFrame, pieceFrame, scale, rotation, effects);
            Vector2 piecePos = pos + pieceOffset + slideOffset;
            Vector2 origin = pieceFrame.Size() * 0.5f;
            if (drawEdge) {
                const float edgeOffset = 1.35f;
                spriteBatch.Draw(texture, piecePos - Vector2.UnitX * edgeOffset, pieceFrame, edge,
                    rotation, origin, scale, effects, 0f);
                spriteBatch.Draw(texture, piecePos + Vector2.UnitX * edgeOffset, pieceFrame, edge,
                    rotation, origin, scale, effects, 0f);
                spriteBatch.Draw(texture, piecePos - Vector2.UnitY * edgeOffset, pieceFrame, edge,
                    rotation, origin, scale, effects, 0f);
                spriteBatch.Draw(texture, piecePos + Vector2.UnitY * edgeOffset, pieceFrame, edge,
                    rotation, origin, scale, effects, 0f);
            }
            spriteBatch.Draw(texture, piecePos, pieceFrame, paper, rotation, origin, scale, effects, 0f);
        }

        private static Vector2 GetFallbackPieceOffset(Rectangle fullFrame, Rectangle pieceFrame,
            Vector2 scale, float rotation, SpriteEffects effects) {

            Vector2 fullCenter = new(fullFrame.Left + fullFrame.Width * 0.5f,
                fullFrame.Top + fullFrame.Height * 0.5f);
            Vector2 pieceCenter = new(pieceFrame.Left + pieceFrame.Width * 0.5f,
                pieceFrame.Top + pieceFrame.Height * 0.5f);
            Vector2 offset = pieceCenter - fullCenter;
            if ((effects & SpriteEffects.FlipHorizontally) != 0) {
                offset.X = -offset.X;
            }
            offset *= scale;
            return offset.RotatedBy(rotation);
        }

        private static void DrawThreadsAndPulses(SpriteBatch spriteBatch) {
            Texture2D white = VaultAsset.placeholder2?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (white == null || glow == null) {
                return;
            }

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);

            float time = (float)Main.timeForVisualEffects * 0.016f;

            //因果线、面影 ↔ 真身，呼吸红线，玩家靠近面影时增亮；

            //刀线滞拍期（落刀但未裂）线仍在、因果尚未传导完毕

            foreach (OmokageEntry entry in OniOmokage.Entries) {
                if (entry.Burning || entry.Cut && entry.SplitAge >= 0) {
                    continue;
                }
                float alpha = entry.Alpha;
                if (alpha <= 0.05f) {
                    continue;
                }

                NPC npc = OniOmokage.ValidTarget(entry.NpcIndex, entry.NpcType, entry.NpcSpawnToken);
                if (npc == null) {
                    continue;
                }
                //真身贴着面影时线没有存在感，跳过

                float dist = Vector2.Distance(entry.RenderCenter, npc.Center);
                if (dist < 40f) {
                    continue;
                }

                float breath = 0.12f + 0.07f * MathF.Sin(time * 2.1f + entry.SwayPhase);
                float proximity = 1f - MathHelper.Clamp(
                    Vector2.Distance(Main.LocalPlayer.Center, entry.RenderCenter) / 220f, 0f, 1f);
                float strength = (breath + proximity * 0.30f) * alpha;

                DrawLine(spriteBatch, white, entry.RenderCenter, npc.Center,
                    new Color(0.86f, 0.12f, 0.10f, 0f) * strength, 1.2f);
            }

            //斩纸刀光、居合白线 + 刀光拉丝 + 落刀点星爆

            foreach (OmokageEntry entry in OniOmokage.Entries) {
                if (entry.Cut && entry.CutAge <= SlashFxFrames) {
                    DrawCutSlash(spriteBatch, white, glow, entry);
                }
            }

            //脉冲、赤点沿线疾驰，缓入加速 + 短残尾

            foreach (OmokagePulse pulse in OniOmokage.Pulses) {
                NPC npc = OniOmokage.ValidTarget(pulse.NpcIndex, pulse.NpcType, pulse.NpcSpawnToken);
                if (npc == null) {
                    continue;
                }
                Vector2 target = npc.Center + pulse.BodyLocal;
                float prog = pulse.Progress;
                float eased = prog * prog;

                //传导中整条线亮起

                DrawLine(spriteBatch, white, pulse.StartWorld, target,
                    new Color(1f, 0.22f, 0.15f, 0f) * 0.35f, 1.6f);

                Vector2 origin = glow.Size() * 0.5f;
                for (int k = 0; k < 3; k++) {
                    float ghostProg = MathHelper.Clamp(eased - k * 0.09f, 0f, 1f);
                    Vector2 pos = Vector2.Lerp(pulse.StartWorld, target, ghostProg) - Main.screenPosition;
                    float fade = 1f - k * 0.32f;
                    spriteBatch.Draw(glow, pos, null, new Color(1f, 0.24f, 0.16f, 0f) * (0.75f * fade),
                        0f, origin, 20f / glow.Width * (1f - k * 0.2f), SpriteEffects.None, 0f);
                }
                //白热芯

                Vector2 head = Vector2.Lerp(pulse.StartWorld, target, eased) - Main.screenPosition;
                spriteBatch.Draw(glow, head, null, new Color(1f, 0.92f, 0.85f, 0f) * 0.9f,
                    0f, origin, 9f / glow.Width, SpriteEffects.None, 0f);
            }

            spriteBatch.End();
        }

        private static void DrawLine(SpriteBatch spriteBatch, Texture2D white,
            Vector2 startWorld, Vector2 endWorld, Color color, float thickness) {

            Vector2 delta = endWorld - startWorld;
            float length = delta.Length();
            if (length < 1f) {
                return;
            }
            spriteBatch.Draw(white, startWorld - Main.screenPosition, null, color,
                delta.ToRotation(), new Vector2(0f, white.Height * 0.5f),
                new Vector2(length / white.Width, thickness / white.Height), SpriteEffects.None, 0f);
        }

        /// <summary>刀光演出总时长（帧），CutAge 超过即熄</summary>
        private const int SlashFxFrames = 14;

        /// <summary>三层刀光（加色批次内调用）</summary>
        private static void DrawCutSlash(SpriteBatch spriteBatch, Texture2D white, Texture2D glow, OmokageEntry entry) {
            Texture2D brush = CWRAsset.SlashBrush01?.Value;
            Texture2D flare = CWRAsset.StarFlare02?.Value;

            Vector2 dir = entry.CutAngle.ToRotationVector2();
            if (!OniOmokage.ClipLineToRect(entry.CutLocal, dir, entry.PaperHalf, out float t0, out float t1)) {
                return;
            }
            float chord = (t1 - t0) * 1.25f;
            Vector2 cutWorld = entry.RenderCenter + entry.CutLocal;
            Vector2 mid = cutWorld + dir * ((t0 + t1) * 0.5f);
            int age = entry.CutAge;

            //居合白线、斩击本身的一瞬，先过曝后急收

            float lineInt = 1f - age / 5f;
            if (lineInt > 0f) {
                Vector2 half = dir * (chord * 0.5f);
                //宽晕

                DrawLine(spriteBatch, white, mid - half, mid + half,
                    new Color(0.95f, 0.55f, 0.45f, 0f) * (0.40f * lineInt), 7f);
                //白芯

                DrawLine(spriteBatch, white, mid - half, mid + half,
                    new Color(1f, 0.97f, 0.92f, 0f) * lineInt, 2f);
            }

            //刀光拉丝、白热芯 + 绯红缘，沿刀向轻微滑移（挥砍的"抹"感），宽度收束

            if (brush != null) {
                float f = age / (float)SlashFxFrames;
                float streakInt = (1f - f) * (1f - f);
                float width = MathHelper.Lerp(26f, 10f, f);
                Vector2 pos = mid + dir * (age * 2.2f) - Main.screenPosition;
                Vector2 scale = new(chord / brush.Width, width / brush.Height);
                Vector2 origin = brush.Size() * 0.5f;
                spriteBatch.Draw(brush, pos, null, new Color(1f, 0.20f, 0.13f, 0f) * (0.85f * streakInt),
                    entry.CutAngle, origin, scale * new Vector2(1f, 1.35f), SpriteEffects.None, 0f);
                spriteBatch.Draw(brush, pos, null, new Color(1f, 0.90f, 0.80f, 0f) * streakInt,
                    entry.CutAngle, origin, scale, SpriteEffects.None, 0f);
            }

            //落刀点星爆、命中确认

            if (flare != null && age <= 6) {
                float f = age / 6f;
                float flareInt = 1f - f;
                float scale = (34f + 26f * f) / flare.Width;
                Vector2 pos = cutWorld - Main.screenPosition;
                spriteBatch.Draw(flare, pos, null, new Color(1f, 0.32f, 0.20f, 0f) * (0.8f * flareInt),
                    entry.CutAngle, flare.Size() * 0.5f, scale * 1.5f, SpriteEffects.None, 0f);
                spriteBatch.Draw(flare, pos, null, new Color(1f, 0.94f, 0.88f, 0f) * flareInt,
                    entry.CutAngle, flare.Size() * 0.5f, scale, SpriteEffects.None, 0f);
                //落刀点辉光垫底

                spriteBatch.Draw(glow, pos, null, new Color(1f, 0.30f, 0.18f, 0f) * (0.7f * flareInt),
                    0f, glow.Size() * 0.5f, 46f / glow.Width, SpriteEffects.None, 0f);
            }
        }
    }
}
