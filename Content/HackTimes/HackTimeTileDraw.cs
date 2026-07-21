using CalamityOverhaul.Content.HackTimes.Scannables;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>物块扫描高亮</summary>
    internal static class HackTimeTileDraw
    {
        private static float selectedTimer;
        private static float hoverTimer;
        private static float selectedFadeIn;//0~1

        public static void Update() {
            selectedTimer += 0.016f;
            hoverTimer += 0.016f;

            bool hasSelected = HackTime.CurrentScanTarget is TileScannable;
            selectedFadeIn = MathHelper.Lerp(selectedFadeIn,
                hasSelected ? 1f : 0f, hasSelected ? 0.08f : 0.12f);
        }

        /// <summary>Additive 扫描辉光</summary>
        public static void DrawAdditive(SpriteBatch sb) {
            float effectStr = HackTime.Intensity;
            if (effectStr < 0.01f) return;

            DrawHoveredTileGlow(sb, effectStr);
            DrawSelectedTileGlow(sb, effectStr);
        }

        /// <summary>AlphaBlend 边框与标签</summary>
        public static void DrawAlphaBlend(SpriteBatch sb) {
            float effectStr = HackTime.Intensity;
            if (effectStr < 0.01f) return;

            //绘制悬停物块的名称标签
            DrawHoveredTileLabel(sb, effectStr);

            //绘制选中物块的边框装饰
            if (selectedFadeIn > 0.01f && HackTime.CurrentScanTarget is TileScannable tileScan) {
                DrawSelectedTileFrame(sb, tileScan, effectStr);
            }
        }

        private static void DrawHoveredTileLabel(SpriteBatch sb, float effectStr) {
            int hx = HackTimeTargeting.HoveredTileX;
            int hy = HackTimeTargeting.HoveredTileY;
            if (hx < 0 || hy < 0) return;

            //悬停已选中则跳过
            if (HackTime.CurrentScanTarget is TileScannable sel) {
                Rectangle selBounds = TileScannable.GetTileWorldBounds(hx, hy);
                if (sel.WorldCenter.X >= selBounds.X && sel.WorldCenter.X <= selBounds.Right
                    && sel.WorldCenter.Y >= selBounds.Y && sel.WorldCenter.Y <= selBounds.Bottom)
                    return;
            }

            Tile tile = Main.tile[hx, hy];
            if (!tile.HasTile) return;

            int tileType = tile.TileType;
            string name = TileScannable.GetTileName(hx, hy, tileType);
            string tileClass = TileScannable.GetTileClass(tileType);
            Color classColor = TileScannable.GetTileClassColor(tileType);

            Rectangle bounds = TileScannable.GetTileWorldBounds(hx, hy);
            float centerX = bounds.Center.X - Main.screenPosition.X;
            float bottomY = bounds.Bottom - Main.screenPosition.Y;

            float pulse = MathF.Sin(hoverTimer * 4f) * 0.1f + 0.9f;
            float alpha = effectStr * 0.7f * pulse;

            //物块名称
            if (!string.IsNullOrEmpty(name)) {
                Vector2 nameSize = FontAssets.MouseText.Value.MeasureString(name) * 0.32f;
                Vector2 namePos = new(centerX - nameSize.X * 0.5f, bottomY + 6f);
                Utils.DrawBorderString(sb, name, namePos, HackTheme.TextBright * alpha, 0.32f);
            }

            //物块分类
            if (!string.IsNullOrEmpty(tileClass)) {
                float nameOffsetY = string.IsNullOrEmpty(name) ? 0f : 16f;
                Vector2 classSize = FontAssets.MouseText.Value.MeasureString(tileClass) * 0.24f;
                Vector2 classPos = new(centerX - classSize.X * 0.5f, bottomY + 6f + nameOffsetY);
                Utils.DrawBorderString(sb, tileClass, classPos, classColor * (alpha * 0.6f), 0.24f);
            }
        }

        private static void DrawHoveredTileGlow(SpriteBatch sb, float effectStr) {
            int hx = HackTimeTargeting.HoveredTileX;
            int hy = HackTimeTargeting.HoveredTileY;
            if (hx < 0 || hy < 0) return;

            //悬停物块已经被选中则跳过
            if (HackTime.CurrentScanTarget is TileScannable) {
                TileScannable sel = (TileScannable)HackTime.CurrentScanTarget;
                Rectangle selBounds = TileScannable.GetTileWorldBounds(hx, hy);
                if (sel.WorldCenter.X >= selBounds.X && sel.WorldCenter.X <= selBounds.Right
                    && sel.WorldCenter.Y >= selBounds.Y && sel.WorldCenter.Y <= selBounds.Bottom)
                    return;
            }

            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return;

            Rectangle bounds = TileScannable.GetTileWorldBounds(hx, hy);
            Vector2 center = new(bounds.Center.X - Main.screenPosition.X,
                bounds.Center.Y - Main.screenPosition.Y);
            float radius = Math.Max(bounds.Width, bounds.Height) * 0.6f + 12f;

            float pulse = MathF.Sin(hoverTimer * 4f) * 0.15f + 0.85f;
            Color glowColor = new Color(0.08f, 0.5f, 0.55f, 0f) * (effectStr * 0.35f * pulse);

            Vector2 origin = glow.Size() / 2f;
            float scale = radius * 2f / glow.Width;
            sb.Draw(glow, center, null, glowColor, 0f, origin, scale, SpriteEffects.None, 0f);
        }

        private static void DrawSelectedTileGlow(SpriteBatch sb, float effectStr) {
            if (selectedFadeIn < 0.01f) return;
            if (HackTime.CurrentScanTarget is not TileScannable tileScan) return;
            if (!tileScan.IsValid) return;

            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return;

            Vector2 worldCenter = tileScan.WorldCenter;
            Vector2 screenCenter = worldCenter - Main.screenPosition;

            //基于WorldCenter反推包围盒
            int tx = (int)(worldCenter.X / 16f);
            int ty = (int)(worldCenter.Y / 16f);
            Rectangle bounds = TileScannable.GetTileWorldBounds(tx, ty);
            Vector2 boundsCenter = new(bounds.Center.X - Main.screenPosition.X,
                bounds.Center.Y - Main.screenPosition.Y);

            float alpha = effectStr * selectedFadeIn;
            float radius = Math.Max(bounds.Width, bounds.Height) * 0.7f + 16f;

            //外层扫描辉光环
            Vector2 origin = glow.Size() / 2f;
            float breathe = MathF.Sin(selectedTimer * 2.5f) * 0.1f + 0.9f;
            Color outerGlow = new Color(0.1f, 0.7f, 0.75f, 0f) * (alpha * 0.4f * breathe);
            float outerScale = radius * 2.2f / glow.Width;
            sb.Draw(glow, boundsCenter, null, outerGlow, 0f, origin, outerScale, SpriteEffects.None, 0f);

            //内层脉冲辉光
            float innerPulse = MathF.Sin(selectedTimer * 4f) * 0.2f + 0.8f;
            Color innerGlow = new Color(0.15f, 0.85f, 0.8f, 0f) * (alpha * 0.3f * innerPulse);
            float innerScale = radius * 1.4f / glow.Width;
            sb.Draw(glow, boundsCenter, null, innerGlow, 0f, origin, innerScale, SpriteEffects.None, 0f);

            //旋转扫描点阵（类似NPC的光圈效果）
            int segments = 8;
            float glowScale = 10f / glow.Width;
            float rotOffset = selectedTimer * 0.6f;
            float segmentArc = MathHelper.TwoPi / segments;
            float gapRatio = 0.3f;
            float drawArc = segmentArc * (1f - gapRatio);

            for (int seg = 0; seg < segments; seg++) {
                float segStart = seg * segmentArc + rotOffset;
                int pointsPerSeg = 5;

                for (int p = 0; p < pointsPerSeg; p++) {
                    float t = (float)p / (pointsPerSeg - 1);
                    float angle = segStart + drawArc * t;
                    Vector2 pos = boundsCenter + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;

                    float edgeFade = 1f - MathF.Abs(t - 0.5f) * 2f;
                    edgeFade = MathF.Pow(edgeFade, 0.5f);
                    Color ptColor = new Color(0.12f, 0.8f, 0.85f, 0f) * (alpha * 0.5f * edgeFade);
                    sb.Draw(glow, pos, null, ptColor, 0f, origin, glowScale, SpriteEffects.None, 0f);
                }
            }
        }

        /// <summary>选中物块赛博边框+扫描线</summary>
        private static void DrawSelectedTileFrame(SpriteBatch sb, TileScannable tileScan, float effectStr) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null) return;

            Vector2 worldCenter = tileScan.WorldCenter;
            int tx = (int)(worldCenter.X / 16f);
            int ty = (int)(worldCenter.Y / 16f);
            Rectangle worldBounds = TileScannable.GetTileWorldBounds(tx, ty);

            //转换到屏幕坐标
            float left = worldBounds.X - Main.screenPosition.X;
            float top = worldBounds.Y - Main.screenPosition.Y;
            float w = worldBounds.Width;
            float h = worldBounds.Height;

            float alpha = effectStr * selectedFadeIn;
            float expand = 4f;
            left -= expand;
            top -= expand;
            w += expand * 2;
            h += expand * 2;

            Color borderColor = HackTheme.Accent * (alpha * 0.6f);
            Color dimBorder = HackTheme.Accent * (alpha * 0.2f);

            //四条边框线（带呼吸效果）
            float borderBreathe = MathF.Sin(selectedTimer * 3f) * 0.15f + 0.85f;
            Color bc = borderColor * borderBreathe;

            sb.Draw(px, new Rectangle((int)left, (int)top, (int)w, 1), bc);
            sb.Draw(px, new Rectangle((int)left, (int)(top + h - 1), (int)w, 1), bc);
            sb.Draw(px, new Rectangle((int)left, (int)top, 1, (int)h), bc);
            sb.Draw(px, new Rectangle((int)(left + w - 1), (int)top, 1, (int)h), bc);

            //角标括号
            int arm = Math.Max((int)(Math.Min(w, h) * 0.3f), 6);
            Color cornerColor = HackTheme.Accent * (alpha * 0.8f);
            DrawCorner(sb, px, left, top, arm, cornerColor, 1, 1);
            DrawCorner(sb, px, left + w, top, arm, cornerColor, -1, 1);
            DrawCorner(sb, px, left, top + h, arm, cornerColor, 1, -1);
            DrawCorner(sb, px, left + w, top + h, arm, cornerColor, -1, -1);

            //竖向扫描线动画
            float scanT = selectedTimer * 1.5f % 1f;
            float scanY = top + scanT * h;
            float scanFade = 1f - MathF.Abs(scanT - 0.5f) * 2f;
            Color scanColor = HackTheme.Accent * (alpha * 0.25f * scanFade);
            sb.Draw(px, new Rectangle((int)left, (int)scanY, (int)w, 2), scanColor);

            //填充背景色
            Color fillColor = HackTheme.Accent * (alpha * 0.04f);
            sb.Draw(px, new Rectangle((int)left, (int)top, (int)w, (int)h), fillColor);
        }

        private static void DrawCorner(SpriteBatch sb, Texture2D px,
            float x, float y, int arm, Color color, int dirX, int dirY) {
            //水平臂
            int startX = dirX > 0 ? (int)x : (int)x - arm;
            sb.Draw(px, new Rectangle(startX, (int)y, arm, 2), color);
            //垂直臂
            int startY = dirY > 0 ? (int)y : (int)y - arm;
            sb.Draw(px, new Rectangle((int)x - (dirX > 0 ? 0 : 1), startY, 2, arm), color);
        }
    }
}
