using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>中央目标锁定框渲染</summary>
    internal static class HackTargetFrame
    {
        public static void Draw(SpriteBatch sb, float timer) {
            float camProg = HackTime.CameraProgress;
            if (camProg < 0.01f) return;

            Texture2D px = CWRAsset.Placeholder_White?.Value;
            if (px == null) return;

            //统一 IHackTarget 读取锁定框元数据
            IHackTarget target = HackTime.CurrentScanTarget;
            if (target == null || !target.IsValid) return;

            float alpha = HackTime.Intensity * camProg;
            Vector2 center = new(Main.screenWidth * 0.5f, Main.screenHeight * 0.5f);

            Vector2 half = target.LockFrameHalfSize;
            float baseHalfW = half.X;
            float baseHalfH = half.Y;
            string targetName = target.LockFrameTitle ?? string.Empty;
            string hpStr = null;
            Color hpColor = default;
            if (target.TryGetLockFrameStatus(out string status, out Color statusColor)) {
                hpStr = status;
                hpColor = statusColor;
            }

            float ease = EaseOutCubic(camProg);
            float expand = 1f + (1f - ease) * 0.8f;
            float halfW = baseHalfW * expand;
            float halfH = baseHalfH * expand;

            int armLen = (int)(24f * ease);
            if (armLen < 2) return;
            Color frameColor = HackTheme.Accent * (alpha * 0.75f);
            Color dimColor = HackTheme.Accent * (alpha * 0.25f);

            //四角方括号
            DrawFrameBracket(sb, px, center, -halfW, -halfH, armLen, frameColor);
            DrawFrameBracket(sb, px, center, halfW, -halfH, armLen, frameColor);
            DrawFrameBracket(sb, px, center, -halfW, halfH, armLen, frameColor);
            DrawFrameBracket(sb, px, center, halfW, halfH, armLen, frameColor);

            //四角辉光
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null && ease > 0.3f) {
                Color cg = HackTheme.Accent * (alpha * 0.08f * ease);
                cg.A = 0;
                Vector2[] corners = {
                    center + new Vector2(-halfW, -halfH),
                    center + new Vector2(halfW, -halfH),
                    center + new Vector2(-halfW, halfH),
                    center + new Vector2(halfW, halfH)
                };
                foreach (var c in corners)
                    sb.Draw(glow, c, null, cg, 0, glow.Size() / 2, 0.1f, SpriteEffects.None, 0);
            }

            //边缘刻度标记
            if (ease > 0.5f) {
                float tickAlpha = (ease - 0.5f) * 2f * alpha * 0.35f;
                Color tickColor = HackTheme.Border * tickAlpha;
                int tickCount = 6;
                for (int i = 1; i < tickCount; i++) {
                    float t = (float)i / tickCount;
                    float tx = MathHelper.Lerp(center.X - halfW, center.X + halfW, t);
                    float ty = MathHelper.Lerp(center.Y - halfH, center.Y + halfH, t);
                    sb.Draw(px, new Rectangle((int)tx, (int)(center.Y - halfH), 1, 5),
                        new Rectangle(0, 0, 1, 1), tickColor);
                    sb.Draw(px, new Rectangle((int)tx, (int)(center.Y + halfH - 5), 1, 5),
                        new Rectangle(0, 0, 1, 1), tickColor);
                    sb.Draw(px, new Rectangle((int)(center.X - halfW), (int)ty, 5, 1),
                        new Rectangle(0, 0, 1, 1), tickColor);
                    sb.Draw(px, new Rectangle((int)(center.X + halfW - 5), (int)ty, 5, 1),
                        new Rectangle(0, 0, 1, 1), tickColor);
                }
            }

            //中心十字
            float crossLen = 8f * ease;
            sb.Draw(px, new Rectangle((int)(center.X - crossLen), (int)center.Y, (int)(crossLen * 2), 1),
                new Rectangle(0, 0, 1, 1), dimColor);
            sb.Draw(px, new Rectangle((int)center.X, (int)(center.Y - crossLen), 1, (int)(crossLen * 2)),
                new Rectangle(0, 0, 1, 1), dimColor);

            //旋转环指示器（用4条短斜线模拟）
            if (ease > 0.6f) {
                float ringAlpha = (ease - 0.6f) / 0.4f * alpha * 0.2f;
                float rot = timer * 0.5f;
                float ringR = Math.Max(halfW, halfH) + 12f;
                Color ringCol = HackTheme.Accent * ringAlpha;
                for (int a = 0; a < 4; a++) {
                    float angle = rot + a * MathHelper.PiOver2;
                    Vector2 dir = new(MathF.Cos(angle), MathF.Sin(angle));
                    Vector2 p1 = center + dir * (ringR - 6);
                    Vector2 p2 = center + dir * (ringR + 6);
                    DrawLine(sb, px, p1, p2, 1.5f, ringCol);
                }
            }

            //文字标签
            if (ease > 0.4f) {
                float labelAlpha = (ease - 0.4f) / 0.6f * alpha;

                string label = HackTime.TargetLocked.Value;
                Vector2 labelSize = FontAssets.MouseText.Value.MeasureString(label) * 0.46f;
                Vector2 labelPos = new(center.X - labelSize.X * 0.5f, center.Y - halfH - 22f);
                Utils.DrawBorderString(sb, label, labelPos, HackTheme.Accent * (labelAlpha * 0.7f), 0.46f);

                if (!string.IsNullOrEmpty(targetName)) {
                    Vector2 nameSize = FontAssets.MouseText.Value.MeasureString(targetName) * 0.48f;
                    Vector2 namePos = new(center.X - nameSize.X * 0.5f, center.Y + halfH + 8f);
                    Utils.DrawBorderString(sb, targetName, namePos, HackTheme.TextBright * (labelAlpha * 0.6f), 0.48f);
                }

                //状态信息（NPC为生命值百分比，物块为分类）
                if (hpStr != null) {
                    Vector2 hpSize = FontAssets.MouseText.Value.MeasureString(hpStr) * 0.38f;
                    Vector2 hpPos = new(center.X - hpSize.X * 0.5f, center.Y + halfH + 30f);
                    Utils.DrawBorderString(sb, hpStr, hpPos, hpColor * (labelAlpha * 0.45f), 0.38f);
                }
            }

            //水平扫描线
            float scanT = timer * 0.6f % 1f;
            float scanY = center.Y - halfH + scanT * halfH * 2;
            float scanFade = 1f - Math.Abs(scanT - 0.5f) * 2f;
            sb.Draw(px, new Rectangle((int)(center.X - halfW), (int)scanY, (int)(halfW * 2), 1),
                new Rectangle(0, 0, 1, 1), HackTheme.Accent * (alpha * 0.14f * scanFade));
            //扫描线辉光
            if (glow != null) {
                Color sg = HackTheme.Accent * (alpha * 0.04f * scanFade);
                sg.A = 0;
                sb.Draw(glow, new Vector2(center.X, scanY), null, sg, 0,
                    glow.Size() / 2, new Vector2(halfW / 20f, 0.02f), SpriteEffects.None, 0);
            }
        }

        private static void DrawFrameBracket(SpriteBatch sb, Texture2D px,
            Vector2 center, float ox, float oy, int armLen, Color color) {
            int cx = (int)(center.X + ox);
            int cy = (int)(center.Y + oy);
            int dirH = ox < 0 ? 1 : -1;
            int dirV = oy < 0 ? 1 : -1;

            int hx = dirH > 0 ? cx : cx - armLen;
            sb.Draw(px, new Rectangle(hx, cy, armLen, 2), new Rectangle(0, 0, 1, 1), color);

            int vy = dirV > 0 ? cy : cy - armLen;
            sb.Draw(px, new Rectangle(cx, vy, 2, armLen), new Rectangle(0, 0, 1, 1), color);
        }

        private static void DrawLine(SpriteBatch sb, Texture2D px, Vector2 start, Vector2 end, float thickness, Color color) {
            Vector2 diff = end - start;
            float length = diff.Length();
            if (length < 1f) return;
            sb.Draw(px, start, new Rectangle(0, 0, 1, 1), color, diff.ToRotation(),
                Vector2.Zero, new Vector2(length, thickness), SpriteEffects.None, 0f);
        }

        private static float EaseOutCubic(float t) {
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }
    }
}
