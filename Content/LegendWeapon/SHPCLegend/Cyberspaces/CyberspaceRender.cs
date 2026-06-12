using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces
{
    /// <summary>
    /// 赛博空间渲染器
    /// <br/>多人场景：每个玩家持有独立的赛博空间状态，本渲染器枚举所有活跃领域分别绘制边界环 / 边缘光晕 /
    /// 低质量回退栅格，让每个玩家的领域在本地客户端正确出现在各自的领域中心。
    /// <br/>整屏后处理（压暗+去饱和+红染+加法赛博特效）由"主导域"驱动：优先取本地玩家自己的领域，
    /// 若本地未开启则取离屏幕中心最近的活跃领域，避免多个领域同时整屏后处理造成画面冲突
    /// </summary>
    internal class CyberspaceRender : RenderHandle
    {
        private const int MaxEntities = 32;
        private static readonly Vector4[] entityBuffer = new Vector4[MaxEntities];

        [VaultLoaden(CWRConstant.Masking + "Noise2")]
        private static Asset<Texture2D> noise2;
        [VaultLoaden(CWRConstant.Masking + "SoftGlow")]
        private static Asset<Texture2D> softGlow;

        public override void UpdateBySystem(int index) {
            //逻辑推进已移至 CyberspaceSystem（ModSystem.PostUpdateEverything）——
            //RenderHandle 的更新钩子不会在专用服务器上运行，冻结/放逐等需要服务端权威推进的逻辑不能挂在这里
            //此处仅在回到主菜单时兜底清理客户端残留状态（主菜单中 PostUpdateEverything 不再运行）
            if (Main.gameMenu) {
                CyberspaceSystem.ResetAll();
            }
        }

        public override void DrawNPCsOverTiles(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.gameMenu) {
                return;
            }

            //聚集本帧所有需要绘制的领域；空集合直接退出，避免无谓的 RT 切换
            List<CyberspacePlayer> domains = CollectVisibleDomains();
            if (domains.Count == 0) {
                return;
            }

            //整屏后处理只对一个"主导域"做：本地玩家自己的领域优先；其次取与摄像机中心最近的远端域
            CyberspacePlayer primary = SelectPrimaryDomain(domains);
            if (primary != null) {
                ApplyFullScreenShader(spriteBatch, graphicsDevice, screenSwap, primary);
            }

            //逐个领域分别绘制边界环——每个玩家的领域有各自的中心 / 半径 / 阶段进度
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

        /// <summary>
        /// 遍历所有玩家，收集仍需绘制的领域（含正在收缩动画中的关闭态）
        /// </summary>
        private static List<CyberspacePlayer> CollectVisibleDomains() {
            List<CyberspacePlayer> list = new();
            foreach (CyberspacePlayer cp in Cyberspace.EnumerateRenderable()) {
                list.Add(cp);
            }
            return list;
        }

        /// <summary>
        /// 选取整屏后处理的主导域：本地玩家自己有领域则用自己，否则取距离摄像机中心最近的远端域
        /// </summary>
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

            //摄像机中心（世界坐标）
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
            if (RenderQualitySafety.NeedsScreenTargetFallback()) {
                DrawLowQualityFieldFallback(spriteBatch);
                return;
            }

            Effect shader = EffectLoader.CyberspaceField?.Value;
            Texture2D noiseTex = noise2?.Value;
            if (shader == null || noiseTex == null) return;
            if (screenSwap == null || screenSwap.IsDisposed) return;
            if (Main.screenTarget == null || Main.screenTarget.IsDisposed) return;

            //水波质量从关/低切到中/高时，DrawNPCsOverTiles 触发点的活动 RT 不一定是 screenTarget
            //此时再 SetRenderTarget(Main.screenTarget); Clear 会把本该写到 backbuffer 的画面整个顶替掉，
            //表现就是整个 UI 和画面"消失"。检测到这种情况立刻走低质量回退路径
            if (!RenderQualitySafety.IsScreenTargetActive(graphicsDevice)) {
                DrawLowQualityFieldFallback(spriteBatch);
                return;
            }

            //保存进入时的 RT 绑定，结束后再还原回去，避免改变上层管线对活动 RT 的预期
            RenderTargetBinding[] previousTargets = graphicsDevice.GetRenderTargets();

            // 将当前屏幕内容复制到交换缓冲
            graphicsDevice.SetRenderTarget(screenSwap);
            graphicsDevice.Clear(Color.Transparent);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            spriteBatch.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            spriteBatch.End();

            // 设置着色器参数
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
            shader.Parameters["layerTier"]?.SetValue(primary.VisualTier);
            Vector2 domainCenter = primary.DomainCenter;
            float effectiveRadius = primary.Radius * primary.ExpandProgress;
            shader.Parameters["setPoint"]?.SetValue(domainCenter);
            shader.Parameters["screenPosition"]?.SetValue(worldViewOrigin);
            shader.Parameters["worldViewSize"]?.SetValue(worldViewSize);
            shader.Parameters["gridSize"]?.SetValue(Cyberspace.GridSize);

            // 收集域内实体数据
            int entityCount = CollectEntitiesInDomain(domainCenter, effectiveRadius);
            shader.Parameters["entityCount"]?.SetValue(entityCount);
            if (entityCount > 0) {
                shader.Parameters["entities"]?.SetValue(entityBuffer);
            }

            // 应用着色器并绘制回主屏幕
            graphicsDevice.SetRenderTarget(Main.screenTarget);
            graphicsDevice.Clear(Color.Transparent);
            graphicsDevice.Textures[1] = noiseTex;
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
            shader.CurrentTechnique.Passes[0].Apply();
            spriteBatch.Draw(screenSwap, Vector2.Zero, Color.White);
            spriteBatch.End();

            //还原进入时的 RT 绑定，防止改变上层管线对当前活动 RT 的预期
            if (previousTargets != null && previousTargets.Length > 0
                && previousTargets[0].RenderTarget != Main.screenTarget) {
                graphicsDevice.SetRenderTargets(previousTargets);
            }
        }

        /// <summary>
        /// 低水波/低级光照下不触碰 screenTarget，避免原版 RT 链路变化导致玩家或 UI 被清掉。
        /// 多人场景下逐域绘制各自的栅格，让每个玩家的领域都体现存在感。
        /// </summary>
        private static void DrawLowQualityFieldFallback(SpriteBatch spriteBatch) {
            Texture2D pixel = CWRAsset.Placeholder_White?.Value;
            if (pixel == null) return;

            //先用一层"压暗罩"——取所有领域中最强的 intensity 来拍板压暗强度，避免在多个领域叠加时画面被压死
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

            Color dimColor = new Color(22, 0, 0) * (maxAlpha * Cyberspace.DimStrength * 0.55f * baseMul);

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
                //单环设计：只画最外层有效边界对应的一张栅格，避免多层网格叠加压暗画面
                DrawLowQualityFieldGrid(spriteBatch, pixel, cp, alpha, perBaseMul, perDetailMul);
            }

            spriteBatch.End();
        }

        private static void DrawLowQualityFieldGrid(SpriteBatch spriteBatch, Texture2D pixel,
            CyberspacePlayer cp, float alpha, float baseMul, float detailMul) {

            Vector2 center = cp.DomainCenter;
            float radius = cp.EffectiveOuterRadius;
            float gridSize = Cyberspace.GridSize;
            if (radius < gridSize * 2f) return;

            float time = cp.EffectTime;
            float tier = cp.VisualTier;
            float tierMult = 0.65f + (tier - 1f) * 0.18f;
            //网格骨架按 baseMul 中度淡化，节点闪烁属花纹按 detailMul 强淡化
            Color lineColor = new Color(220, 35, 22) * (alpha * 0.13f * tierMult * baseMul);
            Color nodeColor = GetTierGlowColor(tier, alpha * 0.28f * tierMult * detailMul);

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

                    float flicker = 0.55f + 0.45f * MathF.Sin(time * 3f + gx * 0.37f + gy * 0.19f);
                    Vector2 screen = world - Main.screenPosition;
                    spriteBatch.Draw(pixel, new Rectangle((int)screen.X - 1, (int)screen.Y - 1, 3, 3), nodeColor * flicker);
                }
            }
        }

        private static void DrawEdgeGlowRing(SpriteBatch spriteBatch, CyberspacePlayer cp) {
            Texture2D glowTex = softGlow?.Value;
            if (glowTex == null || cp.Intensity < 0.01f) return;

            //单环设计：光晕格只环绕最外层有效边界，与边界环位置一致
            DrawSingleEdgeGlowRing(spriteBatch, glowTex, cp, cp.EffectiveOuterRadius);
        }

        private static void DrawSingleEdgeGlowRing(SpriteBatch spriteBatch, Texture2D glowTex,
            CyberspacePlayer cp, float r) {
            Vector2 center = cp.DomainCenter;
            float gs = Cyberspace.GridSize;
            float time = cp.EffectTime;
            float effectIntensity = cp.Intensity;
            //边缘光晕属花纹层，移动时强淡化
            float glowMotionMul = 1f - MathHelper.Clamp(cp.MotionFade, 0f, 1f) * 0.60f;

            if (r < gs * 2) return;

            float tier = cp.VisualTier;
            float tierMult = 1f + (tier - 1f) * 0.25f;

            int numSteps = Math.Clamp((int)(MathHelper.TwoPi * r / (gs * 0.6f)), 48, 280);
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
                //脉动频率较旧版略放缓，减轻边界格的高频闪烁感
                float pulse = 0.3f + 0.7f * MathF.Sin(time * 1.5f + cellHash * MathF.PI * 2f);
                pulse = MathF.Max(pulse, 0f);
                float alpha = pulse * effectIntensity * 0.4f * tierMult * glowMotionMul;

                Color glowColor = GetTierGlowColor(tier, alpha);

                spriteBatch.Draw(glowTex, new Vector2(screenX, screenY), null, glowColor,
                    0f, glowOrigin, glowScale, SpriteEffects.None, 0f);
            }
        }

        /// <summary>
        /// 按连续视觉层级插值的边界光晕色：层级越高色温越热
        /// </summary>
        private static Color GetTierGlowColor(float tier, float alpha) {
            Vector3 t1 = new(0.80f, 0.05f, 0.04f);
            Vector3 t2 = new(0.90f, 0.10f, 0.06f);
            Vector3 t3 = new(1.0f, 0.18f, 0.08f);
            Vector3 c = tier <= 2f
                ? Vector3.Lerp(t1, t2, MathHelper.Clamp(tier - 1f, 0f, 1f))
                : Vector3.Lerp(t2, t3, MathHelper.Clamp(tier - 2f, 0f, 1f));
            return new Color(c.X * alpha, c.Y * alpha, c.Z * alpha, 0f);
        }

        /// <summary>
        /// 在领域最外层有效边界绘制单一常驻边界环——使用专用 CyberBoundaryRing 着色器
        /// <br/>单环设计：任意时刻只有一圈火纹，位置取 <see cref="CyberspacePlayer.EffectiveOuterRadius"/>，
        /// 升降层时环沿半径连续滑动（读作"领域生长/收缩"）；层级身份由 layerTier 驱动的
        /// 厚度/色温/装饰层样式表达，不再为每一层各画一圈遮挡战斗视野
        /// </summary>
        private static void DrawBoundaryShockwaveRing(SpriteBatch spriteBatch, CyberspacePlayer cp) {
            Effect shader = EffectLoader.CyberBoundaryRing?.Value;
            if (shader == null) return;
            if (CWRAsset.Placeholder_White?.Value == null) return;
            if (CWRAsset.Extra_193?.Value == null) return;
            if (cp.Intensity < 0.02f) return;

            float effectiveRadius = cp.EffectiveOuterRadius;
            if (effectiveRadius < Cyberspace.GridSize * 4f) return;

            Texture2D canvas = CWRAsset.Placeholder_White.Value;
            Texture2D noise = CWRAsset.Extra_193.Value;
            Vector2 drawPos = cp.DomainCenter - Main.screenPosition;
            //边界环属于骨架级显示，移动时中度淡化以削弱晃眼感
            float ringMotionMul = 1f - MathHelper.Clamp(cp.MotionFade, 0f, 1f) * 0.38f;

            float tier = cp.VisualTier;
            float tierFrac = (tier - 1f) / (Cyberspace.MaxLayerCount - 1f);

            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            Main.graphics.GraphicsDevice.Textures[1] = noise;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;

            float time = cp.EffectTime * 0.75f;
            //四边形外侧留 45% 余量给火舌/热浪光弦/分段弧线，环本体精确落在有效边界上，
            //与全屏着色器的领域边缘、边缘光晕格三者重合为同一道边界
            float quadHalf = effectiveRadius * 1.45f;
            float ringPos = effectiveRadius / quadHalf;
            float thickness = 0.085f + tierFrac * 0.022f + 0.007f * MathF.Sin(time * 0.7f + 1.2f);

            //owner 偏移让多个玩家的领域呼吸相位错开
            float ownerPhase = cp.Player.whoAmI * 1.37f;
            shader.Parameters["uTime"]?.SetValue(time + ownerPhase);
            shader.Parameters["ringProgress"]?.SetValue(ringPos);
            shader.Parameters["ringThickness"]?.SetValue(thickness);
            shader.Parameters["fadeAlpha"]?.SetValue(cp.Intensity * ringMotionMul);
            shader.Parameters["layerTier"]?.SetValue(tier);
            shader.CurrentTechnique.Passes[0].Apply();

            float drawDiameter = quadHalf * 2f;
            Color ringTint = GetTierRingTint(tier);
            spriteBatch.Draw(canvas, drawPos, null, ringTint,
                0f, canvas.Size() * 0.5f, new Vector2(drawDiameter, drawDiameter),
                SpriteEffects.None, 0f);

            spriteBatch.End();
        }

        /// <summary>
        /// 按连续视觉层级插值的边界环整体染色：层级越高越偏炽热
        /// </summary>
        private static Color GetTierRingTint(float tier) {
            Vector3 t1 = new(1f, 0.82f, 0.68f);
            Vector3 t2 = new(1f, 0.68f, 0.52f);
            Vector3 t3 = new(1f, 0.54f, 0.38f);
            Vector3 c = tier <= 2f
                ? Vector3.Lerp(t1, t2, MathHelper.Clamp(tier - 1f, 0f, 1f))
                : Vector3.Lerp(t2, t3, MathHelper.Clamp(tier - 2f, 0f, 1f));
            return new Color(c.X, c.Y, c.Z);
        }

        /// <summary>
        /// 收集域内活跃NPC，将位置和大小写入 entityBuffer
        /// 返回收集到的实体数量
        /// </summary>
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

            // 清零未使用的槽位
            for (int i = count; i < MaxEntities; i++) {
                entityBuffer[i] = Vector4.Zero;
            }

            return count;
        }
    }
}
