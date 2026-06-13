using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes.Scannables;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>物块赛博滤镜 RT 渲染</summary>
    internal static class HackTimeTileCyberPass
    {
        //缓存 RT，随尺寸变化重建
        private static RenderTarget2D _rt;
        //RT 最大边长，树木可达 512
        private const int MaxRtSize = 512;
        //包围盒外扩，描边留空
        private const int EdgePadding = 6;

        /// <summary>EndEntityDraw 入口，SpriteBatch 未 Begin</summary>
        public static void Draw(SpriteBatch sb, GraphicsDevice gd) {
            float effectStr = HackTime.Intensity;
            if (effectStr < 0.02f) return;

            Effect shader = HackTimeAssets.HackTimeNPCHighlight;
            if (shader == null) return;

            //悬停冷青，选中红色覆盖
            DrawPassForHovered(sb, gd, shader, effectStr);
            DrawPassForSelected(sb, gd, shader, effectStr);
        }

        private static void DrawPassForHovered(SpriteBatch sb, GraphicsDevice gd, Effect shader, float effectStr) {
            int hx = HackTimeTargeting.HoveredTileX;
            int hy = HackTimeTargeting.HoveredTileY;
            if (hx < 0 || hy < 0) return;
            if (hx >= Main.maxTilesX || hy >= Main.maxTilesY) return;

            Tile hoverTile = Main.tile[hx, hy];
            if (!hoverTile.HasTile) return;

            Rectangle bounds = TileScannable.GetTileWorldBounds(hx, hy);

            //悬停与选中同一物体则跳过
            if (HackTime.CurrentScanTarget is TileScannable sel) {
                Vector2 c = sel.WorldCenter;
                if (c.X >= bounds.X && c.X <= bounds.Right
                    && c.Y >= bounds.Y && c.Y <= bounds.Bottom) return;
            }

            RenderAndComposite(sb, gd, shader, bounds, effectStr, isSelected: false);
        }

        private static void DrawPassForSelected(SpriteBatch sb, GraphicsDevice gd, Effect shader, float effectStr) {
            if (HackTime.CurrentScanTarget is not TileScannable tileScan) return;
            if (!tileScan.IsValid) return;

            int tx = (int)(tileScan.WorldCenter.X / 16f);
            int ty = (int)(tileScan.WorldCenter.Y / 16f);
            Rectangle bounds = TileScannable.GetTileWorldBounds(tx, ty);

            RenderAndComposite(sb, gd, shader, bounds, effectStr, isSelected: true);
        }

        /// <summary>物块重绘到小 RT，ScreenSwap 备份防丢屏</summary>
        private static void RenderAndComposite(SpriteBatch sb, GraphicsDevice gd, Effect shader,
            Rectangle worldBounds, float effectStr, bool isSelected) {

            //扩展包围盒为描边预留空间
            int rtW = Math.Min(worldBounds.Width + EdgePadding * 2, MaxRtSize);
            int rtH = Math.Min(worldBounds.Height + EdgePadding * 2, MaxRtSize);
            if (rtW <= 0 || rtH <= 0) return;

            //低光照时走不切换 RT 回退
            if (RenderQualitySafety.NeedsScreenTargetFallback()) {
                DrawDirectCompositeFallback(sb, worldBounds, effectStr, isSelected);
                return;
            }

            //此处由 EndEntityDraw 调用，主屏幕 RT 必为 Main.screenTarget
            if (Main.screenTarget == null || Main.screenTarget.IsDisposed) {
                DrawDirectCompositeFallback(sb, worldBounds, effectStr, isSelected);
                return;
            }

            //活动 RT 非 screenTarget 时回退，防全屏消失
            if (!RenderQualitySafety.IsScreenTargetActive(gd)) {
                DrawDirectCompositeFallback(sb, worldBounds, effectStr, isSelected);
                return;
            }

            //ScreenSwap 全屏备份
            RenderTarget2D screenSwap = RenderHandleLoader.ScreenSwap;
            if (screenSwap == null || screenSwap.IsDisposed) {
                DrawDirectCompositeFallback(sb, worldBounds, effectStr, isSelected);
                return;
            }

            EnsureRT(gd, rtW, rtH);
            if (_rt == null || _rt.IsDisposed) return;

            //保存进入时 RT 绑定
            RenderTargetBinding[] previousTargets = gd.GetRenderTargets();

            try {
                //备份 screenTarget 到 screenSwap
                gd.SetRenderTarget(screenSwap);
                gd.Clear(Color.Transparent);
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
                sb.Draw(Main.screenTarget, Vector2.Zero, Color.White);
                sb.End();

                //物块重绘到小 RT
                gd.SetRenderTarget(_rt);
                gd.Clear(Color.Transparent);
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                    SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
                RedrawTileRegion(sb, worldBounds);
                sb.End();

                //还原 screenTarget 画面
                gd.SetRenderTarget(Main.screenTarget);
                gd.Clear(Color.Transparent);
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
                sb.Draw(screenSwap, Vector2.Zero, Color.White);
                sb.End();

                //着色器加法叠加小 RT
                shader.Parameters["texelSize"]?.SetValue(new Vector2(1f / rtW, 1f / rtH));
                shader.Parameters["intensity"]?.SetValue(effectStr);
                shader.Parameters["isSelected"]?.SetValue(isSelected ? 1f : 0f);
                shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);

                Vector2 screenPos = new(
                    worldBounds.X - EdgePadding - Main.screenPosition.X,
                    worldBounds.Y - EdgePadding - Main.screenPosition.Y);

                sb.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                    SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone,
                    null, Main.GameViewMatrix.TransformationMatrix);
                shader.CurrentTechnique.Passes[0].Apply();
                sb.Draw(_rt, screenPos, Color.White);
                sb.End();

                //还原 RT 绑定
                if (previousTargets != null && previousTargets.Length > 0
                    && previousTargets[0].RenderTarget != Main.screenTarget) {
                    gd.SetRenderTargets(previousTargets);
                }
            } catch {
                //异常时切回主屏 RT
                gd.SetRenderTarget(Main.screenTarget);
                throw;
            }
        }

        /// <summary>低性能回退，偏移绘制模拟辉光</summary>
        private static void DrawDirectCompositeFallback(SpriteBatch sb, Rectangle worldBounds,
            float effectStr, bool isSelected) {

            Color baseColor = isSelected ? HackTheme.Danger : HackTheme.Accent;
            float alpha = MathHelper.Clamp(effectStr, 0f, 1f);
            float pulse = 0.85f + MathF.Sin(Main.GlobalTimeWrappedHourly * 5f) * 0.15f;
            Vector2 origin = new(worldBounds.X - Main.screenPosition.X, worldBounds.Y - Main.screenPosition.Y);

            Vector2[] outlineOffsets = [
                new(-2f, 0f), new(2f, 0f), new(0f, -2f), new(0f, 2f),
                new(-2f, -2f), new(2f, -2f), new(-2f, 2f), new(2f, 2f)
            ];

            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive,
                SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);

            Color outlineColor = baseColor * (alpha * 0.18f * pulse);
            foreach (Vector2 offset in outlineOffsets) {
                RedrawTileRegion(sb, worldBounds, origin + offset, outlineColor);
            }

            RedrawTileRegion(sb, worldBounds, origin, baseColor * (alpha * 0.08f));
            sb.End();
        }

        /// <summary>按 Frame 重绘物块到 RT</summary>
        private static void RedrawTileRegion(SpriteBatch sb, Rectangle worldBounds)
            => RedrawTileRegion(sb, worldBounds, new Vector2(EdgePadding, EdgePadding), Color.White);

        private static void RedrawTileRegion(SpriteBatch sb, Rectangle worldBounds,
            Vector2 destinationOrigin, Color color) {

            int tx0 = (int)MathF.Floor(worldBounds.Left / 16f);
            int ty0 = (int)MathF.Floor(worldBounds.Top / 16f);
            int tx1 = (int)MathF.Ceiling(worldBounds.Right / 16f);
            int ty1 = (int)MathF.Ceiling(worldBounds.Bottom / 16f);

            for (int x = tx0; x < tx1; x++) {
                for (int y = ty0; y < ty1; y++) {
                    if (x < 0 || x >= Main.maxTilesX || y < 0 || y >= Main.maxTilesY) continue;
                    Tile tile = Main.tile[x, y];
                    if (!tile.HasTile) continue;

                    int type = tile.TileType;
                    Main.instance.LoadTiles(type);
                    Texture2D tex = TextureAssets.Tile[type]?.Value;
                    if (tex == null) continue;

                    //树木 trunk 20x20 帧，-2 对齐
                    Vector2 dst = destinationOrigin + new Vector2(x * 16 - worldBounds.X, y * 16 - worldBounds.Y);
                    if (TileScannable.IsTreeTile(type)) {
                        Rectangle treeSrc = new(tile.TileFrameX, tile.TileFrameY, 20, 20);
                        Vector2 treeDst = dst + new Vector2(-2f, -2f);
                        sb.Draw(tex, treeDst, treeSrc, color);
                        //树冠与分枝
                        TryDrawTreeExtras(sb, type, x, y, dst, color);
                        continue;
                    }

                    //frameImportant 走 TileObjectData
                    int srcW = 16;
                    int srcH = 16;
                    TileObjectData data = TileObjectData.GetTileData(type, 0);
                    if (data != null) {
                        srcW = data.CoordinateWidth;
                        //多格物件定位子行高度
                        int subY = FindSubRow(data, tile.TileFrameY);
                        srcH = data.CoordinateHeights[subY];
                    }

                    Rectangle src = new(tile.TileFrameX, tile.TileFrameY, srcW, srcH);
                    sb.Draw(tex, dst, src, color);
                }
            }
        }

        /// <summary>树 trunk 补绘树冠分枝</summary>
        private static void TryDrawTreeExtras(SpriteBatch sb, int type, int tileX, int tileY,
            Vector2 tileDst, Color color) {

            Tile tile = Main.tile[tileX, tileY];
            int fx = tile.TileFrameX;
            int fy = tile.TileFrameY;

            //frameX=22 且 frameY>=198 为树顶
            if (fx == 22 && fy >= 198) {
                Texture2D topTex = SafeGetTexture(TextureAssets.TreeTop, type);
                if (topTex != null) {
                    //80x80 树冠，-32 居中，上偏 -64
                    Rectangle topSrc = new(0, 0, 80, 80);
                    Vector2 topDst = tileDst + new Vector2(-32f, -64f);
                    sb.Draw(topTex, topDst, topSrc, color);
                }
            }

            //左分枝：frameX=22 && fy 在 0..132 范围内（非树顶标记）
            if (fx == 22 && fy < 198 && fy % 22 == 0) {
                Texture2D branchTex = SafeGetTexture(TextureAssets.TreeBranch, type);
                if (branchTex != null) {
                    Rectangle branchSrc = new(0, 0, 40, 40);
                    //左分枝贴在 trunk 左侧
                    Vector2 branchDst = tileDst + new Vector2(-40f, -12f);
                    sb.Draw(branchTex, branchDst, branchSrc, color);
                }
            }
            //右分枝：frameX=44
            else if (fx == 44 && fy < 198 && fy % 22 == 0) {
                Texture2D branchTex = SafeGetTexture(TextureAssets.TreeBranch, type);
                if (branchTex != null) {
                    //右分枝使用第 2 列(x=42 偏移 40 宽度帧)
                    Rectangle branchSrc = new(42, 0, 40, 40);
                    Vector2 branchDst = tileDst + new Vector2(16f, -12f);
                    sb.Draw(branchTex, branchDst, branchSrc, color);
                }
            }
        }

        /// <summary>安全取纹理数组 index 0</summary>
        private static Texture2D SafeGetTexture(ReLogic.Content.Asset<Texture2D>[] arr, int type) {
            if (arr == null || arr.Length == 0) return null;
            //统一 index 0 形成轮廓掩码
            var asset = arr[0];
            return asset?.Value;
        }

        /// <summary>TileFrameY 反推多格子行索引</summary>
        private static int FindSubRow(TileObjectData data, int frameY) {
            int rows = data.Height;
            if (rows <= 1) return 0;
            int totalHeight = 0;
            for (int r = 0; r < rows; r++) {
                totalHeight += data.CoordinateHeights[r] + data.CoordinatePadding;
            }
            if (totalHeight <= 0) return 0;
            int frameYInObject = frameY % totalHeight;
            int acc = 0;
            for (int r = 0; r < rows; r++) {
                int rh = data.CoordinateHeights[r] + data.CoordinatePadding;
                if (frameYInObject < acc + rh) return r;
                acc += rh;
            }
            return rows - 1;
        }

        private static void EnsureRT(GraphicsDevice gd, int w, int h) {
            if (_rt != null && !_rt.IsDisposed && _rt.Width == w && _rt.Height == h) return;
            _rt?.Dispose();
            //PreserveContents 防切回主 RT 清空
            _rt = new RenderTarget2D(gd, w, h, false, SurfaceFormat.Color,
                DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        }
    }
}
