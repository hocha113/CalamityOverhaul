using CalamityOverhaul.Common;
using CalamityOverhaul.Content.EntrustManager;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.TrialQuests
{
    /// <summary>SHPC 委托条目：暗紫底、霓虹蓝、CRT 扫描线</summary>
    internal class SHPCEntryStyle : IEntrustEntryStyle
    {
        #region 色板

        private static readonly Color BgDeep = new(10, 6, 22);
        private static readonly Color BgMid = new(18, 12, 38);
        private static readonly Color BgHover = new(24, 16, 50);
        private static readonly Color BgSelected = new(30, 20, 62);

        private static readonly Color NeonBlue = new(60, 120, 255);
        private static readonly Color NeonBlueDim = new(40, 60, 180);
        private static readonly Color NeonBright = new(140, 200, 255);
        private static readonly Color ScanBlue = new(30, 60, 180);

        private static readonly Color AlertRed = new(200, 55, 40);
        private static readonly Color CompletedCyan = new(80, 220, 200);

        private static readonly Color TitleBlue = new(180, 210, 255);
        private static readonly Color TitleComplete = new(80, 220, 200);

        #endregion

        private float pulseTimer;
        private float sweepTimer;
        private float dataFlowTimer;

        private const int EdgePad = 6;

        public void Update() {
            pulseTimer += 0.025f;
            sweepTimer += 0.004f;
            dataFlowTimer += 0.06f;
            if (pulseTimer > MathHelper.TwoPi) pulseTimer -= MathHelper.TwoPi;
            if (sweepTimer > 100f) sweepTimer -= 100f;
            if (dataFlowTimer > 100f) dataFlowTimer -= 100f;
        }

        public bool DrawEntryBackground(SpriteBatch sb, Rectangle entryRect, EntrustEntryData entry,
            bool isSelected, bool isHovered, float alpha) {
            var px = VaultAsset.placeholder2.Value;
            var uv = new Rectangle(0, 0, 1, 1);

            DrawEntryPanelBackground(sb, px, entryRect, isSelected, isHovered, alpha);

            //左侧状态竖条（双线，霓虹蓝脉冲）
            Color statusC = GetAccentColor(entry.Status, 1f);
            float barPulse = MathF.Sin(pulseTimer * 2.5f) * 0.25f + 0.75f;
            sb.Draw(px, new Rectangle(entryRect.X, entryRect.Y + 1, 2, entryRect.Height - 2),
                uv, statusC * (alpha * barPulse));
            sb.Draw(px, new Rectangle(entryRect.X + 3, entryRect.Y + 3, 1, entryRect.Height - 6),
                uv, statusC * (alpha * barPulse * 0.35f));

            //上边框：实线左段 + 断口 + 虚线右段（CP2077 HUD风格）
            int breakX = entryRect.X + (int)(entryRect.Width * 0.3f);
            Color borderC = NeonBlueDim * (alpha * 0.55f);
            sb.Draw(px, new Rectangle(entryRect.X, entryRect.Y, breakX - entryRect.X, 1), uv, borderC);
            for (int x = breakX + 8; x < entryRect.Right - 4; x += 6) {
                int w = Math.Min(3, entryRect.Right - 4 - x);
                if (w > 0) sb.Draw(px, new Rectangle(x, entryRect.Y, w, 1), uv, borderC * 0.45f);
            }
            //下边框（淡化）
            sb.Draw(px, new Rectangle(entryRect.X + 6, entryRect.Bottom - 1, entryRect.Width - 12, 1),
                uv, borderC * 0.25f);

            //右侧霓虹数据流闪烁竖条
            int dataW = 6;
            for (int y = entryRect.Y + 2; y < entryRect.Bottom - 2; y += 4) {
                float noise = MathF.Sin(dataFlowTimer * 1.4f + y * 0.8f)
                            * MathF.Sin(dataFlowTimer * 0.9f + y * 0.25f);
                if (noise > 0.2f) {
                    float intensity = (noise - 0.2f) / 0.8f * 0.13f;
                    int w = 1 + (int)(noise * 2.5f);
                    sb.Draw(px, new Rectangle(entryRect.Right - dataW - w, y, w, 2),
                        uv, NeonBlue * (alpha * intensity));
                }
            }

            return true;
        }

        private void DrawEntryPanelBackground(SpriteBatch sb, Texture2D px, Rectangle entryRect,
            bool isSelected, bool isHovered, float alpha) {
            if (EffectLoader.CyberPanel?.Value != null) {
                Effect effect = EffectLoader.CyberPanel.Value;

                Rectangle extRect = entryRect;
                extRect.Inflate(EdgePad, EdgePad);

                effect.Parameters["uTime"]?.SetValue(sweepTimer);
                effect.Parameters["uAlpha"]?.SetValue(alpha * 0.95f);
                effect.Parameters["uResolution"]?.SetValue(new Vector2(extRect.Width, extRect.Height));
                effect.Parameters["uEdgePad"]?.SetValue((float)EdgePad);

                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                    SamplerState.AnisotropicClamp, DepthStencilState.None,
                    RasterizerState.CullNone, effect, Main.UIScaleMatrix);

                sb.Draw(px, extRect, Color.White);

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                    SamplerState.AnisotropicClamp, DepthStencilState.None,
                    RasterizerState.CullNone, null, Main.UIScaleMatrix);

                DrawInteractionTint(sb, px, entryRect, isSelected, isHovered, alpha);
            }
            else {
                DrawFallbackBackground(sb, px, entryRect, isSelected, isHovered, alpha);
            }
        }

        private static void DrawInteractionTint(SpriteBatch sb, Texture2D px, Rectangle entryRect,
            bool isSelected, bool isHovered, float alpha) {
            Color tint = isSelected ? BgSelected : isHovered ? BgHover : Color.Transparent;
            if (tint.A <= 0) {
                return;
            }

            sb.Draw(px, entryRect, new Rectangle(0, 0, 1, 1), tint * (alpha * 0.28f));
        }

        /// <summary>降级背景：保留原 CPU 渐变、扫描线与全息扫掠光</summary>
        private void DrawFallbackBackground(SpriteBatch sb, Texture2D px, Rectangle entryRect,
            bool isSelected, bool isHovered, float alpha) {
            var uv = new Rectangle(0, 0, 1, 1);

            //多段纵向渐变，模拟深暗紫底色的非均匀亮度
            int segs = 8;
            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                float t2 = (i + 1) / (float)segs;
                int y1 = entryRect.Y + (int)(t * entryRect.Height);
                int y2 = entryRect.Y + (int)(t2 * entryRect.Height);
                if (y2 <= y1) continue;

                Color baseC = isSelected ? BgSelected
                    : isHovered ? BgHover
                    : Color.Lerp(BgDeep, BgMid, t);

                float shift = MathF.Sin(t * MathHelper.Pi * 2.5f) * 0.06f;
                Color c = Color.Lerp(baseC, NeonBlueDim, Math.Max(0f, shift)) * (alpha * 0.95f);
                sb.Draw(px, new Rectangle(entryRect.X, y1, entryRect.Width, y2 - y1), uv, c);
            }

            //CRT水平扫描线（每3px一条，极低透明度）
            for (int y = entryRect.Y; y < entryRect.Bottom; y += 3)
                sb.Draw(px, new Rectangle(entryRect.X, y, entryRect.Width, 1), uv, ScanBlue * (alpha * 0.04f));

            //全息扫掠光带（从上到下循环）
            float scanY = entryRect.Y + (sweepTimer * 0.1f % 1f) * entryRect.Height;
            for (int dy = -2; dy <= 2; dy++) {
                int py = (int)scanY + dy;
                if (py < entryRect.Y || py >= entryRect.Bottom) continue;
                float fade = 1f - MathF.Abs(dy) / 3f;
                sb.Draw(px, new Rectangle(entryRect.X, py, entryRect.Width, 1),
                    uv, NeonBlueDim * (alpha * 0.10f * fade * fade));
            }
        }

        public float DrawEntryIcon(SpriteBatch sb, Vector2 titlePos, EntrustEntryData entry, float alpha) {
            var px = VaultAsset.placeholder2.Value;
            float cx = titlePos.X + 8f;
            float cy = titlePos.Y + 9f;

            float pulse = MathF.Sin(pulseTimer * 1.8f) * 0.25f + 0.75f;
            Color hexC = entry.Status == QuestEntryStatus.Completed ? CompletedCyan : NeonBlue;

            //六边形轮廓外框
            DrawHexOutline(sb, px, new Vector2(cx, cy), 5.5f, hexC * (alpha * pulse));
            //内部暗色填充（挖空效果）
            DrawHexOutline(sb, px, new Vector2(cx, cy), 3f, BgDeep * (alpha * 0.85f));
            //中心亮点
            sb.Draw(px, new Vector2(cx - 0.5f, cy - 0.5f), new Rectangle(0, 0, 1, 1),
                hexC * (alpha * pulse * 0.7f), 0f, Vector2.Zero, new Vector2(1.5f), SpriteEffects.None, 0f);

            //外圈脉冲扩散环
            float ringPhase = (pulseTimer * 0.6f) % MathHelper.TwoPi;
            float ringR = 4f + ringPhase / MathHelper.TwoPi * 10f;
            float ringAlpha = 1f - ringPhase / MathHelper.TwoPi;
            DrawHexOutline(sb, px, new Vector2(cx, cy), ringR, NeonBlue * (alpha * ringAlpha * 0.13f));

            return 22f;
        }

        public void DrawEntryOverlay(SpriteBatch sb, Rectangle entryRect, EntrustEntryData entry, float alpha) {
            var px = VaultAsset.placeholder2.Value;

            //右上角3个状态方形标记（CP2077节点指示灯）
            int dots = 3;
            float dotStartX = entryRect.Right - 14f - (dots - 1) * 6f;
            float dotY = entryRect.Y + 5f;
            for (int d = 0; d < dots; d++) {
                float dAlpha = MathF.Sin(pulseTimer * 1.2f + d * 0.9f) * 0.3f + 0.5f;
                sb.Draw(px, new Rectangle((int)(dotStartX + d * 6), (int)dotY, 2, 2),
                    new Rectangle(0, 0, 1, 1), NeonBlueDim * (alpha * dAlpha));
            }

            //偶发故障闪烁（数据损坏效果）
            float glitch = MathF.Sin(dataFlowTimer * 3.9f);
            if (glitch > 0.93f) {
                float gIntensity = (glitch - 0.93f) / 0.07f * 0.05f;
                sb.Draw(px, entryRect, new Rectangle(0, 0, 1, 1), NeonBlue * (alpha * gIntensity));
            }
        }

        public Color GetAccentColor(QuestEntryStatus status, float alpha) {
            return status switch {
                QuestEntryStatus.Completed => CompletedCyan * alpha,
                QuestEntryStatus.Failed => AlertRed * alpha,
                QuestEntryStatus.Suspended => new Color(80, 80, 100) * alpha,
                QuestEntryStatus.Tracked => NeonBright * alpha,
                _ => NeonBlue * alpha,
            };
        }

        public Color GetTitleColor(QuestEntryStatus status, float alpha) {
            return status switch {
                QuestEntryStatus.Completed => TitleComplete * (alpha * 0.85f),
                QuestEntryStatus.Failed => AlertRed * (alpha * 0.9f),
                _ => TitleBlue * alpha,
            };
        }

        public int? GetCustomEntryHeight() => null;

        public void Reset() {
            pulseTimer = 0f;
            sweepTimer = 0f;
            dataFlowTimer = 0f;
        }

        /// <summary>绘制六边形轮廓（6段像素线段）</summary>
        private static void DrawHexOutline(SpriteBatch sb, Texture2D px, Vector2 center, float r, Color color) {
            for (int i = 0; i < 6; i++) {
                float a0 = MathHelper.TwoPi * i / 6f + MathHelper.Pi / 6f;
                float a1 = MathHelper.TwoPi * (i + 1) / 6f + MathHelper.Pi / 6f;
                Vector2 p0 = center + new Vector2(MathF.Cos(a0), MathF.Sin(a0)) * r;
                Vector2 p1 = center + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * r;
                Vector2 dir = p1 - p0;
                float len = dir.Length();
                if (len < 0.5f) continue;
                float angle = MathF.Atan2(dir.Y, dir.X);
                sb.Draw(px, p0, new Rectangle(0, 0, 1, 1), color, angle,
                    Vector2.Zero, new Vector2(len, 1f), SpriteEffects.None, 0f);
            }
        }
    }
}
