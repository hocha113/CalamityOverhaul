using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces
{
    /// <summary>领域 RenderHandle，多玩家边界环/光晕/栅格回退</summary>
    internal class CyberspaceRender : RenderHandle
    {
        private const int MaxEntities = 32;
        private static readonly Vector4[] entityBuffer = new Vector4[MaxEntities];

        [VaultLoaden(CWRConstant.Masking + "Noise2")]
        private static Asset<Texture2D> noise2 = null;
        [VaultLoaden(CWRConstant.Masking + "SoftGlow")]
        private static Asset<Texture2D> softGlow = null;

        public override void UpdateBySystem(int index) {
            //逻辑在 System.PostUpdateEverything，专服不跑
            //主菜单兜底清残留
            if (Main.gameMenu) {
                CyberspaceSystem.ResetAll();
            }
        }

        public override void DrawNPCsOverTiles(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.gameMenu) {
                return;
            }

            //本帧绘制域，空则退
            List<CyberspacePlayer> domains = CollectVisibleDomains();
            if (domains.Count == 0) {
                return;
            }

            //整屏后处理，本地域优先
            CyberspacePlayer primary = SelectPrimaryDomain(domains);
            if (primary != null) {
                ApplyFullScreenShader(spriteBatch, graphicsDevice, screenSwap, primary);
            }

            //逐域边界环
            foreach (CyberspacePlayer cp in domains) {
                DrawBoundaryShockwaveRing(spriteBatch, cp);
            }

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointWrap,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            foreach (CyberspacePlayer cp in domains) {
                DrawEdgeGlowRing(spriteBatch, cp);
            }
            spriteBatch.End();
        }

        /// <summary>枚举仍需绘制的领域（含关闭收缩动画中）</summary>
        private static List<CyberspacePlayer> CollectVisibleDomains() {
            List<CyberspacePlayer> list = new();
            foreach (CyberspacePlayer cp in Cyberspace.EnumerateRenderable()) {
                list.Add(cp);
            }
            return list;
        }

        /// <summary>整屏后处理主导域，本地优先否则最近</summary>
        private static CyberspacePlayer SelectPrimaryDomain(List<CyberspacePlayer> domains) {
            int localWho = Main.myPlayer;

            CyberspacePlayer localOwn = null;
            for (int i = 0; i < domains.Count; i++) {
                if (domains[i].Player.whoAmI == localWho) {
                    localOwn = domains[i];
                    break;
                }
            }
            if (localOwn != null) return localOwn;

            //相机世界中心
            Vector2 cameraCenter = Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
            CyberspacePlayer best = null;
            float bestDistSq = float.MaxValue;
            for (int i = 0; i < domains.Count; i++) {
                Vector2 c = domains[i].DomainCenter;
                float dx = c.X - cameraCenter.X;
                float dy = c.Y - cameraCenter.Y;
                float d = dx * dx + dy * dy;
                if (d < bestDistSq) {
                    bestDistSq = d;
                    best = domains[i];
                }
            }
            return best;
        }

        private static void ApplyFullScreenShader(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice,
            RenderTarget2D screenSwap, CyberspacePlayer primary) {
            //简约偏好或 RT 不可用 → 低质量场回退

            if (DomainVisuals.Concise || RenderQualitySafety.ScreenTargetUnavailable()) {
                DrawLowQualityFieldFallback(spriteBatch);
                return;
            }

            Effect shader = EffectLoader.CyberspaceField?.Value;
            Texture2D noiseTex = noise2?.Value;
            if (shader == null || noiseTex == null) return;
            if (screenSwap == null || screenSwap.IsDisposed) return;
            if (Main.screenTarget == null || Main.screenTarget.IsDisposed) return;

            //非 screenTarget 走低质回退
            if (!RenderQualitySafety.IsScreenTargetActive(graphicsDevice)) {
                DrawLowQualityFieldFallback(spriteBatch);
                return;
            }

            //保存进入 RT
            RenderTargetBinding[] previousTargets = graphicsDevice.GetRenderTargets();

            graphicsDevice.SetRenderTarget(screenSwap);
            graphicsDevice.Clear(Color.Transparent);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            spriteBatch.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            spriteBatch.End();

            Vector2 zoom = Main.GameViewMatrix.Zoom;
            Vector2 screenPixels = Main.ScreenSize.ToVector2();
            Vector2 worldViewSize = screenPixels / zoom;
            Vector2 worldViewOrigin = Main.screenPosition
                + screenPixels * (Vector2.One - Vector2.One / zoom) * 0.5f;

            shader.Parameters["uTime"]?.SetValue(primary.EffectTime);
            shader.Parameters["radius"]?.SetValue(primary.Radius);
            shader.Parameters["intensity"]?.SetValue(primary.Intensity);
            shader.Parameters["expandProgress"]?.SetValue(primary.ExpandProgress);
            shader.Parameters["dimStrength"]?.SetValue(Cyberspace.DimStrength);
            shader.Parameters["motionFade"]?.SetValue(primary.MotionFade);
            shader.Parameters["tierWeights"]?.SetValue(primary.TierWeights);
            Vector2 domainCenter = primary.DomainCenter;
            float effectiveRadius = primary.Radius * primary.ExpandProgress;
            shader.Parameters["setPoint"]?.SetValue(domainCenter);
            shader.Parameters["screenPosition"]?.SetValue(worldViewOrigin);
            shader.Parameters["worldViewSize"]?.SetValue(worldViewSize);
            shader.Parameters["gridSize"]?.SetValue(Cyberspace.GridSize);

            //域内 NPC→entityBuffer
            int entityCount = CollectEntitiesInDomain(domainCenter, effectiveRadius);
            shader.Parameters["entityCount"]?.SetValue(entityCount);
            if (entityCount > 0) {
                shader.Parameters["entities"]?.SetValue(entityBuffer);
            }

            //回写 screenTarget
            graphicsDevice.SetRenderTarget(Main.screenTarget);
            graphicsDevice.Clear(Color.Transparent);
            graphicsDevice.Textures[1] = noiseTex;
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
            shader.CurrentTechnique.Passes[0].Apply();
            spriteBatch.Draw(screenSwap, Vector2.Zero, Color.White);
            spriteBatch.End();

            //还原进入 RT
            if (previousTargets != null && previousTargets.Length > 0
                && previousTargets[0].RenderTarget != Main.screenTarget) {
                graphicsDevice.SetRenderTargets(previousTargets);
            }
        }

        /// <summary>低水波/低级光照回退，逐域栅格</summary>
        private static void DrawLowQualityFieldFallback(SpriteBatch spriteBatch) {
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            if (pixel == null) return;

            //压暗取最强 intensity
            float maxAlpha = 0f;
            float maxMotion = 0f;
            foreach (CyberspacePlayer cp in Cyberspace.EnumerateRenderable()) {
                float a = MathHelper.Clamp(cp.Intensity, 0f, 1f);
                if (a > maxAlpha) {
                    maxAlpha = a;
                    maxMotion = MathHelper.Clamp(cp.MotionFade, 0f, 1f);
                }
            }
            if (maxAlpha <= 0f) return;

            float baseMul = 1f - maxMotion * 0.50f;

            Color dimColor = new Color(22, 0, 0) * (maxAlpha * Cyberspace.DimStrength * 0.68f * baseMul);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
            spriteBatch.Draw(pixel, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), dimColor);
            spriteBatch.End();

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive,
                SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);

            foreach (CyberspacePlayer cp in Cyberspace.EnumerateRenderable()) {
                float alpha = MathHelper.Clamp(cp.Intensity, 0f, 1f);
                if (alpha <= 0.001f) continue;
                float motion = MathHelper.Clamp(cp.MotionFade, 0f, 1f);
                float perBaseMul = 1f - motion * 0.50f;
                float perDetailMul = 1f - motion * 0.65f;
                //按层几何换形，仅最外层半径
                DrawLowQualityFieldGrid(spriteBatch, pixel, cp, alpha, perBaseMul, perDetailMul);
            }

            spriteBatch.End();
        }

        /// <summary>回退栅格按层几何换形：L1 正交线 / L2 三角格+蜂巢点阵 / L3 极化阵列</summary>
        private static void DrawLowQualityFieldGrid(SpriteBatch spriteBatch, Texture2D pixel,
            CyberspacePlayer cp, float alpha, float baseMul, float detailMul) {

            Vector2 center = cp.DomainCenter;
            float radius = cp.EffectiveOuterRadius;
            float gridSize = Cyberspace.GridSize;
            if (radius < gridSize * 2f) return;

            Vector3 w = cp.TierWeights;
            if (w.X > 0.02f) {
                DrawFallbackSquareGrid(spriteBatch, pixel, center, radius, gridSize,
                    alpha * w.X, baseMul, detailMul);
            }
            if (w.Y > 0.02f) {
                DrawFallbackTriLattice(spriteBatch, pixel, center, radius, gridSize,
                    alpha * w.Y, baseMul, detailMul);
            }
            if (w.Z > 0.02f) {
                DrawFallbackPolarArray(spriteBatch, pixel, center, radius, gridSize,
                    cp.EffectTime, alpha * w.Z, baseMul, detailMul);
            }
        }

        /// <summary>静态伪随机，代替时间闪烁（常驻舒适约定）</summary>
        private static float StaticHash(int gx, int gy)
            => MathF.Abs(MathF.Sin(gx * 12.9898f + gy * 78.233f));

        /// <summary>L1 回退：正交栅格线 + 静态明暗节点</summary>
        private static void DrawFallbackSquareGrid(SpriteBatch spriteBatch, Texture2D pixel,
            Vector2 center, float radius, float gridSize, float alpha, float baseMul, float detailMul) {
            Color lineColor = new Color(220, 35, 22) * (alpha * 0.17f * 0.65f * baseMul);
            Color nodeColor = GetTierGlowColor(1f, alpha * 0.34f * 0.65f * detailMul);

            int minX = (int)MathF.Floor((center.X - radius) / gridSize);
            int maxX = (int)MathF.Ceiling((center.X + radius) / gridSize);
            int minY = (int)MathF.Floor((center.Y - radius) / gridSize);
            int maxY = (int)MathF.Ceiling((center.Y + radius) / gridSize);

            for (int gx = minX; gx <= maxX; gx++) {
                float worldX = gx * gridSize;
                float dx = worldX - center.X;
                float halfY = MathF.Sqrt(MathF.Max(radius * radius - dx * dx, 0f));
                if (halfY <= 0f) continue;

                Vector2 pos = new(worldX - Main.screenPosition.X, center.Y - halfY - Main.screenPosition.Y);
                spriteBatch.Draw(pixel, new Rectangle((int)pos.X, (int)pos.Y, 1, (int)(halfY * 2f)), lineColor);
            }

            for (int gy = minY; gy <= maxY; gy++) {
                float worldY = gy * gridSize;
                float dy = worldY - center.Y;
                float halfX = MathF.Sqrt(MathF.Max(radius * radius - dy * dy, 0f));
                if (halfX <= 0f) continue;

                Vector2 pos = new(center.X - halfX - Main.screenPosition.X, worldY - Main.screenPosition.Y);
                spriteBatch.Draw(pixel, new Rectangle((int)pos.X, (int)pos.Y, (int)(halfX * 2f), 1), lineColor);
            }

            for (int gx = minX; gx <= maxX; gx++) {
                for (int gy = minY; gy <= maxY; gy++) {
                    if ((gx + gy) % 5 != 0) continue;

                    Vector2 world = new(gx * gridSize, gy * gridSize);
                    if (Vector2.DistanceSquared(world, center) > radius * radius) continue;

                    float bright = 0.62f + 0.38f * StaticHash(gx, gy);
                    Vector2 screen = world - Main.screenPosition;
                    spriteBatch.Draw(pixel, new Rectangle((int)screen.X - 1, (int)screen.Y - 1, 3, 3), nodeColor * bright);
                }
            }
        }

        /// <summary>L2 回退：三族 60° 弦线三角格 + 蜂巢中心点阵</summary>
        private static void DrawFallbackTriLattice(SpriteBatch spriteBatch, Texture2D pixel,
            Vector2 center, float radius, float gridSize, float alpha, float baseMul, float detailMul) {
            float hexScale = gridSize * 1.7f;
            float spacing = hexScale * 0.866f;
            Color lineColor = new Color(230, 45, 24) * (alpha * 0.15f * baseMul);
            Color nodeColor = GetTierGlowColor(2f, alpha * 0.36f * detailMul);

            int n = (int)MathF.Ceiling(radius / spacing);
            for (int fam = 0; fam < 3; fam++) {
                float ang = MathHelper.Pi / 3f * fam;
                Vector2 dirV = ang.ToRotationVector2();
                Vector2 perp = new(-dirV.Y, dirV.X);
                for (int k = -n; k <= n; k++) {
                    float d = k * spacing;
                    float half = MathF.Sqrt(MathF.Max(radius * radius - d * d, 0f));
                    if (half <= 1f) continue;

                    Vector2 mid = center + perp * d - Main.screenPosition;
                    spriteBatch.Draw(pixel, mid, null, lineColor, ang,
                        new Vector2(pixel.Width * 0.5f, pixel.Height * 0.5f),
                        new Vector2(half * 2f / pixel.Width, 1.2f / pixel.Height),
                        SpriteEffects.None, 0f);
                }
            }

            //蜂巢中心点阵：两套子格，隔位取样控制数量
            float cw = hexScale;
            float ch = hexScale * 1.7320508f;
            int nx = (int)MathF.Ceiling(radius / cw);
            int ny = (int)MathF.Ceiling(radius / ch);
            for (int sub = 0; sub < 2; sub++) {
                float ox = sub * cw * 0.5f;
                float oy = sub * ch * 0.5f;
                for (int i = -nx; i <= nx; i++) {
                    for (int j = -ny; j <= ny; j++) {
                        if (((i + j) & 1) != 0) continue;

                        Vector2 world = center + new Vector2(i * cw + ox, j * ch + oy);
                        if (Vector2.DistanceSquared(world, center) > radius * radius) continue;

                        float bright = 0.55f + 0.45f * StaticHash(i * 2 + sub, j);
                        Vector2 screen = world - Main.screenPosition;
                        spriteBatch.Draw(pixel, new Rectangle((int)screen.X - 1, (int)screen.Y - 1, 3, 3), nodeColor * bright);
                    }
                }
            }
        }

        /// <summary>L3 回退：极化阵列（24 辐条 + 主刻度环点圈，与 shader 版同构）</summary>
        private static void DrawFallbackPolarArray(SpriteBatch spriteBatch, Texture2D pixel,
            Vector2 center, float radius, float gridSize, float time, float alpha, float baseMul, float detailMul) {
            Color spokeColor = new Color(235, 50, 24) * (alpha * 0.15f * baseMul);
            Color ringColor = GetTierGlowColor(3f, alpha * 0.34f * detailMul);

            //24 辐条，近心留空
            float innerHole = gridSize * 4f;
            float spokeLen = radius - innerHole;
            if (spokeLen > 0f) {
                for (int k = 0; k < 24; k++) {
                    float ang = MathHelper.TwoPi * k / 24f;
                    Vector2 dirV = ang.ToRotationVector2();
                    float bright = 0.62f + 0.38f * StaticHash(k, 53);
                    Vector2 mid = center + dirV * (innerHole + spokeLen * 0.5f) - Main.screenPosition;
                    spriteBatch.Draw(pixel, mid, null, spokeColor * bright, ang,
                        new Vector2(pixel.Width * 0.5f, pixel.Height * 0.5f),
                        new Vector2(spokeLen / pixel.Width, 1.2f / pixel.Height),
                        SpriteEffects.None, 0f);
                }
            }

            //主刻度环（每4环）用点圈勾出
            float ringStep = gridSize * 2.4f;
            int ringCount = (int)(radius / ringStep);
            for (int rI = 4; rI <= ringCount; rI += 4) {
                float r = rI * ringStep;
                int dots = Math.Clamp((int)(MathHelper.TwoPi * r / 42f), 24, 140);
                for (int d = 0; d < dots; d++) {
                    float a = MathHelper.TwoPi * d / dots;
                    Vector2 world = center + a.ToRotationVector2() * r;
                    if (Vector2.DistanceSquared(world, center) > radius * radius) continue;

                    float bright = 0.60f + 0.40f * StaticHash(rI, d);
                    Vector2 screen = world - Main.screenPosition;
                    spriteBatch.Draw(pixel, new Rectangle((int)screen.X - 1, (int)screen.Y - 1, 2, 2), ringColor * bright);
                }
            }
        }

        private static void DrawEdgeGlowRing(SpriteBatch spriteBatch, CyberspacePlayer cp) {
            Texture2D glowTex = softGlow?.Value;
            if (glowTex == null || cp.Intensity < 0.01f) return;

            //单环光晕格，贴最外层
            DrawSingleEdgeGlowRing(spriteBatch, glowTex, cp, cp.EffectiveOuterRadius);
        }

        private static void DrawSingleEdgeGlowRing(SpriteBatch spriteBatch, Texture2D glowTex,
            CyberspacePlayer cp, float r) {
            Vector2 center = cp.DomainCenter;
            float gs = Cyberspace.GridSize;
            float time = cp.EffectTime;
            float effectIntensity = cp.Intensity;
            //边缘光晕随动强淡
            float glowMotionMul = 1f - MathHelper.Clamp(cp.MotionFade, 0f, 1f) * 0.60f;

            if (r < gs * 2) return;

            float tier = cp.VisualTier;
            float tierMult = 1f + (tier - 1f) * 0.12f;

            int numSteps = Math.Clamp((int)(MathHelper.TwoPi * r / (gs * 0.6f)), 48, 200);
            float prevSnapX = float.NaN;
            float prevSnapY = float.NaN;
            float screenW = Main.screenWidth;
            float screenH = Main.screenHeight;
            float margin = gs * 4;
            Vector2 glowOrigin = new Vector2(glowTex.Width * 0.5f, glowTex.Height * 0.5f);
            float glowScale = gs * 3.0f / glowTex.Width;

            for (int i = 0; i < numSteps; i++) {
                float angle = i * MathHelper.TwoPi / numSteps;
                float cos = MathF.Cos(angle);
                float sin = MathF.Sin(angle);

                float wx = center.X + cos * (r + gs * 1.2f);
                float wy = center.Y + sin * (r + gs * 1.2f);

                float relX = wx - center.X;
                float relY = wy - center.Y;
                float snapX = MathF.Floor(relX / gs) * gs + gs * 0.5f;
                float snapY = MathF.Floor(relY / gs) * gs + gs * 0.5f;

                if (snapX == prevSnapX && snapY == prevSnapY) continue;
                prevSnapX = snapX;
                prevSnapY = snapY;

                float cellWorldX = center.X + snapX;
                float cellWorldY = center.Y + snapY;

                float screenX = cellWorldX - Main.screenPosition.X;
                float screenY = cellWorldY - Main.screenPosition.Y;
                if (screenX < -margin || screenX > screenW + margin ||
                    screenY < -margin || screenY > screenH + margin) continue;

                float cellHash = MathF.Abs(MathF.Sin(snapX * 0.137f + snapY * 0.251f));
                //超慢低幅起伏，相位按格错开：常驻边界不再闪烁
                float pulse = 0.74f + 0.16f * MathF.Sin(time * 0.45f + cellHash * MathF.PI * 2f);
                float alpha = pulse * effectIntensity * 0.34f * tierMult * glowMotionMul;

                Color glowColor = GetTierGlowColor(tier, alpha);

                spriteBatch.Draw(glowTex, new Vector2(screenX, screenY), null, glowColor,
                    0f, glowOrigin, glowScale, SpriteEffects.None, 0f);
            }
        }

        /// <summary>边界光晕色，层越高越热（收敛版：顶端不再推向刺眼橙）</summary>
        private static Color GetTierGlowColor(float tier, float alpha) {
            Vector3 t1 = new(0.80f, 0.05f, 0.04f);
            Vector3 t2 = new(0.88f, 0.11f, 0.06f);
            Vector3 t3 = new(0.95f, 0.20f, 0.10f);
            Vector3 c = tier <= 2f
                ? Vector3.Lerp(t1, t2, MathHelper.Clamp(tier - 1f, 0f, 1f))
                : Vector3.Lerp(t2, t3, MathHelper.Clamp(tier - 2f, 0f, 1f));
            return new Color(c.X * alpha, c.Y * alpha, c.Z * alpha, 0f);
        }

        /// <summary>边界环，CyberBoundaryRing.fx</summary>
        private static void DrawBoundaryShockwaveRing(SpriteBatch spriteBatch, CyberspacePlayer cp) {
            Effect shader = EffectLoader.CyberBoundaryRing?.Value;
            if (shader == null) return;
            if (VaultAsset.placeholder2?.Value == null) return;
            if (CWRAsset.Extra_193?.Value == null) return;
            if (cp.Intensity < 0.02f) return;

            float effectiveRadius = cp.EffectiveOuterRadius;
            if (effectiveRadius < Cyberspace.GridSize * 4f) return;

            Texture2D canvas = VaultAsset.placeholder2.Value;
            Texture2D noise = CWRAsset.Extra_193.Value;
            Vector2 drawPos = cp.DomainCenter - Main.screenPosition;
            //边界环随动中淡
            float ringMotionMul = 1f - MathHelper.Clamp(cp.MotionFade, 0f, 1f) * 0.38f;

            float tier = cp.VisualTier;
            float tierFrac = (tier - 1f) / (Cyberspace.MaxLayerCount - 1f);

            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            Main.graphics.GraphicsDevice.Textures[1] = noise;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;

            float time = cp.EffectTime * 0.75f;
            //quad 外留45%，环贴有效边界
            float quadHalf = effectiveRadius * 1.45f;
            float ringPos = effectiveRadius / quadHalf;
            //厚度静态化：常驻边界不再随时间涨缩
            float thickness = 0.085f + tierFrac * 0.022f;

            //owner 偏移错开噪声相位
            float ownerPhase = cp.Player.whoAmI * 1.37f;
            shader.Parameters["uTime"]?.SetValue(time + ownerPhase);
            shader.Parameters["ringProgress"]?.SetValue(ringPos);
            shader.Parameters["ringThickness"]?.SetValue(thickness);
            shader.Parameters["fadeAlpha"]?.SetValue(cp.Intensity * ringMotionMul);
            shader.Parameters["tierWeights"]?.SetValue(cp.TierWeights);
            shader.CurrentTechnique.Passes[0].Apply();

            float drawDiameter = quadHalf * 2f;
            Color ringTint = GetTierRingTint(tier);
            spriteBatch.Draw(canvas, drawPos, null, ringTint,
                0f, canvas.Size() * 0.5f, new Vector2(drawDiameter, drawDiameter),
                SpriteEffects.None, 0f);

            spriteBatch.End();
        }

        /// <summary>边界环染色，层越高越炽（收敛版：层间只做克制的色温递进）</summary>
        private static Color GetTierRingTint(float tier) {
            Vector3 t1 = new(1f, 0.86f, 0.76f);
            Vector3 t2 = new(1f, 0.76f, 0.62f);
            Vector3 t3 = new(1f, 0.66f, 0.50f);
            Vector3 c = tier <= 2f
                ? Vector3.Lerp(t1, t2, MathHelper.Clamp(tier - 1f, 0f, 1f))
                : Vector3.Lerp(t2, t3, MathHelper.Clamp(tier - 2f, 0f, 1f));
            return new Color(c.X, c.Y, c.Z);
        }

        /// <summary>域内敌对 NPC → entityBuffer，返数量</summary>
        private static int CollectEntitiesInDomain(Vector2 domainCenter, float effectiveRadius) {
            int count = 0;
            float radiusSq = effectiveRadius * effectiveRadius;

            for (int i = 0; i < Main.maxNPCs && count < MaxEntities; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage) continue;

                Vector2 npcCenter = npc.Center;
                float dx = npcCenter.X - domainCenter.X;
                float dy = npcCenter.Y - domainCenter.Y;
                if (dx * dx + dy * dy > radiusSq) continue;

                float ringRadius = Math.Max(npc.width, npc.height) * 0.8f + 10f;
                float seed = (i * 0.137f) % 1f;
                entityBuffer[count] = new Vector4(npcCenter.X, npcCenter.Y, ringRadius, seed);
                count++;
            }

            for (int i = count; i < MaxEntities; i++) {
                entityBuffer[i] = Vector4.Zero;
            }

            return count;
        }
    }
}
