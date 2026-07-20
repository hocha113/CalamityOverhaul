using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>中央目标锁定框：稀疏刻度 + 头顶铭牌，锁定感交给目标本体高亮</summary>
    internal static class HackTargetFrame
    {
        public static void Draw(SpriteBatch sb, float timer) {
            float camProg = HackTime.CameraProgress;
            if (camProg < 0.01f) return;

            Texture2D px = HackTheme.Pixel;
            if (px == null) return;

            //统一 IHackTarget 读取锁定框元数据
            IHackTarget target = HackTime.CurrentScanTarget;
            if (target == null || !target.IsValid) return;

            float alpha = HackTime.Intensity * camProg;
            Vector2 center = new(Main.screenWidth * 0.5f, Main.screenHeight * 0.5f);

            Vector2 half = target.LockFrameHalfSize;
            float ease = HackTheme.EaseOutCubic(camProg);
            //锁定收拢过冲：从外围快速收进并轻微回弹
            float snap = HackTheme.EaseOutBack(Math.Clamp(camProg * 1.25f, 0f, 1f));
            float expand = 1f + (1f - snap) * 1.1f;
            float halfW = (half.X * 1.08f + 6f) * expand;
            float halfH = (half.Y * 1.08f + 6f) * expand;

            int armLen = (int)(16f * ease);
            if (armLen < 2) return;
            Color frameColor = HackTheme.Accent * (alpha * 0.65f);
            Color dimColor = HackTheme.Accent * (alpha * 0.22f);

            //四角细括号
            HackTheme.DrawCornerBracket(sb, center + new Vector2(-halfW, -halfH), 1, 1, armLen, 1.2f, frameColor);
            HackTheme.DrawCornerBracket(sb, center + new Vector2(halfW, -halfH), -1, 1, armLen, 1.2f, frameColor);
            HackTheme.DrawCornerBracket(sb, center + new Vector2(-halfW, halfH), 1, -1, armLen, 1.2f, frameColor);
            HackTheme.DrawCornerBracket(sb, center + new Vector2(halfW, halfH), -1, -1, armLen, 1.2f, frameColor);

            //每边中点一个小刻度（稀疏，不封闭）
            if (ease > 0.5f) {
                Color tickColor = HackTheme.Accent * ((ease - 0.5f) * 2f * alpha * 0.35f);
                sb.Draw(px, new Rectangle((int)center.X, (int)(center.Y - halfH), 1, 5), HackTheme.SrcPixel, tickColor);
                sb.Draw(px, new Rectangle((int)center.X, (int)(center.Y + halfH - 5), 1, 5), HackTheme.SrcPixel, tickColor);
                sb.Draw(px, new Rectangle((int)(center.X - halfW), (int)center.Y, 5, 1), HackTheme.SrcPixel, tickColor);
                sb.Draw(px, new Rectangle((int)(center.X + halfW - 5), (int)center.Y, 5, 1), HackTheme.SrcPixel, tickColor);
            }

            //中心十字（极淡）
            float crossLen = 7f * ease;
            sb.Draw(px, new Rectangle((int)(center.X - crossLen), (int)center.Y, (int)(crossLen * 2), 1),
                HackTheme.SrcPixel, dimColor);
            sb.Draw(px, new Rectangle((int)center.X, (int)(center.Y - crossLen), 1, (int)(crossLen * 2)),
                HackTheme.SrcPixel, dimColor);

            //上传中才出现的旋转分段环
            bool uploading = false;
            var queue = HackTimeUI.Instance?.Queue;
            if (queue != null && queue.TryGetActiveEntry(target, out _, out bool completed) && !completed)
                uploading = true;
            if (uploading && ease > 0.5f) {
                float ringR = Math.Max(halfW, halfH) + 14f;
                Color ringCol = HackTheme.Uploading * (alpha * 0.4f);
                float rot = timer * 1.6f;
                for (int a = 0; a < 6; a++) {
                    float angle = rot + a * MathHelper.TwoPi / 6f;
                    Vector2 dir = new(MathF.Cos(angle), MathF.Sin(angle));
                    HackTheme.DrawLine(sb, center + dir * (ringR - 5), center + dir * (ringR + 5), 1.4f, ringCol);
                }
            }

            //侧边扫描仪微标记：左侧变焦，右侧距离
            if (ease > 0.55f) {
                float sideAlpha = (ease - 0.55f) / 0.45f * alpha;
                float zoom = 1f + HackTime.GetZoomBoost();
                string zoomStr = $"{zoom:F1}x";
                Vector2 zs = FontAssets.MouseText.Value.MeasureString(zoomStr) * 0.42f;
                Vector2 zoomPos = new(center.X - halfW - zs.X - 18f, center.Y - zs.Y * 0.5f);
                Utils.DrawBorderString(sb, zoomStr, zoomPos, HackTheme.TextNormal * (sideAlpha * 0.55f), 0.42f);
                //变焦标记连线刻度
                HackTheme.DrawLine(sb, new Vector2(center.X - halfW - 12f, center.Y),
                    new Vector2(center.X - halfW - 4f, center.Y), 1f, HackTheme.Accent * (sideAlpha * 0.35f));

                float distTiles = Vector2.Distance(Main.LocalPlayer.Center, target.WorldCenter) / 16f;
                string distStr = $"{distTiles:F1}m";
                Vector2 distPos = new(center.X + halfW + 18f, center.Y - zs.Y * 0.5f);
                Utils.DrawBorderString(sb, distStr, distPos, HackTheme.TextNormal * (sideAlpha * 0.55f), 0.42f);
                HackTheme.DrawLine(sb, new Vector2(center.X + halfW + 4f, center.Y),
                    new Vector2(center.X + halfW + 12f, center.Y), 1f, HackTheme.Accent * (sideAlpha * 0.35f));
            }

            //头顶铭牌
            if (ease > 0.4f) {
                DrawNameplate(sb, timer, target, center, halfW, halfH, (ease - 0.4f) / 0.6f * alpha);
            }

            //水平扫描线
            float scanT = timer * 0.6f % 1f;
            float scanY = center.Y - halfH + scanT * halfH * 2;
            float scanFade = 1f - Math.Abs(scanT - 0.5f) * 2f;
            sb.Draw(px, new Rectangle((int)(center.X - halfW), (int)scanY, (int)(halfW * 2), 1),
                HackTheme.SrcPixel, HackTheme.Accent * (alpha * 0.12f * scanFade));
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                Color sg = HackTheme.Accent * (alpha * 0.04f * scanFade);
                sg.A = 0;
                sb.Draw(glow, new Vector2(center.X, scanY), null, sg, 0,
                    glow.Size() / 2, new Vector2(halfW / 20f, 0.02f), SpriteEffects.None, 0);
            }
        }

        //头顶铭牌：阵营菱形 + 名称 + 状态读数 + 敌对告警
        private static void DrawNameplate(SpriteBatch sb, float timer, IHackTarget target,
            Vector2 center, float halfW, float halfH, float alpha) {
            string name = target.LockFrameTitle;
            if (string.IsNullOrEmpty(name)) return;

            float nameScale = 0.62f;
            Vector2 nameSize = FontAssets.MouseText.Value.MeasureString(name) * nameScale;
            bool hostile = HackTheme.HostileBlend > 0.5f;

            float plateY = center.Y - halfH - 30f;
            float diamondR = 5f;
            float totalW = diamondR * 2f + 8f + nameSize.X;
            float plateX = center.X - totalW * 0.5f;

            //菱形阵营徽记
            Vector2 diamondC = new(plateX + diamondR, plateY + nameSize.Y * 0.5f);
            Color diamondColor = HackTheme.Accent * (alpha * 0.9f);
            HackTheme.DrawDiamondOutline(sb, diamondC, diamondR, 1.2f, diamondColor);
            HackTheme.DrawDiamond(sb, diamondC, diamondR * 0.7f, diamondColor * 0.5f);

            //名称
            Vector2 namePos = new(plateX + diamondR * 2f + 8f, plateY);
            Utils.DrawBorderString(sb, name, namePos, HackTheme.TextBright * (alpha * 0.9f), nameScale);
            //名称底线（左对齐渐隐）
            HackTheme.DrawLine(sb, new Vector2(namePos.X, plateY + nameSize.Y + 1),
                new Vector2(namePos.X + nameSize.X * 0.7f, plateY + nameSize.Y + 1),
                1f, HackTheme.Accent * (alpha * 0.35f));

            //状态读数（HP%等，名称右侧）
            if (target.TryGetLockFrameStatus(out string status, out Color statusColor)) {
                Utils.DrawBorderString(sb, status,
                    new Vector2(namePos.X + nameSize.X + 10f, plateY + 3f),
                    statusColor * (alpha * 0.7f), 0.44f);
            }

            //敌对告警符（铭牌上方闪烁三角）
            if (hostile) {
                float warnPulse = MathF.Sin(timer * 6f) * 0.3f + 0.7f;
                Vector2 warnC = new(center.X, plateY - 12f);
                Color warnColor = HackTheme.Danger * (alpha * 0.85f * warnPulse);
                //小三角
                HackTheme.DrawLine(sb, warnC + new Vector2(-5, 4), warnC + new Vector2(0, -5), 1.4f, warnColor);
                HackTheme.DrawLine(sb, warnC + new Vector2(5, 4), warnC + new Vector2(0, -5), 1.4f, warnColor);
                HackTheme.DrawLine(sb, warnC + new Vector2(-5, 4), warnC + new Vector2(5, 4), 1.4f, warnColor);
                //感叹号点
                sb.Draw(HackTheme.Pixel, new Rectangle((int)warnC.X, (int)warnC.Y - 2, 1, 3), HackTheme.SrcPixel, warnColor);
                sb.Draw(HackTheme.Pixel, new Rectangle((int)warnC.X, (int)warnC.Y + 2, 1, 1), HackTheme.SrcPixel, warnColor);
            }
        }
    }
}
