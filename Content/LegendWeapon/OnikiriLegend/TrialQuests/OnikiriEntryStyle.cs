using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI;
using CalamityOverhaul.Content.UIs.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.TrialQuests
{
    /// <summary>鬼切委托条目：墨黑纸底、朱红刀痕、朱印——复用 OniBrush / OniShaderPanel</summary>
    internal class OnikiriEntryStyle : IEntrustEntryStyle
    {
        private float pulseTimer;
        private float shaderTime;

        private const int EdgePad = 8;

        public void Update() {
            pulseTimer += 0.028f;
            shaderTime += 0.016f;
            if (pulseTimer > MathHelper.TwoPi) {
                pulseTimer -= MathHelper.TwoPi;
            }
        }

        public bool DrawEntryBackground(SpriteBatch sb, Rectangle entryRect, EntrustEntryData entry,
            bool isSelected, bool isHovered, float alpha) {
            var px = VaultAsset.placeholder2.Value;
            var uv = new Rectangle(0, 0, 1, 1);

            DrawPanel(sb, px, entryRect, isSelected, isHovered, alpha);

            Color statusC = GetAccentColor(entry.Status, 1f);
            float barPulse = MathF.Sin(pulseTimer * 2.2f) * 0.22f + 0.78f;
            sb.Draw(px, new Rectangle(entryRect.X, entryRect.Y + 2, 2, entryRect.Height - 4),
                uv, statusC * (alpha * barPulse));
            sb.Draw(px, new Rectangle(entryRect.X + 3, entryRect.Y + 4, 1, entryRect.Height - 8),
                uv, statusC * (alpha * barPulse * 0.35f));

            //顶沿刀痕分隔
            Vector2 slashStart = new(entryRect.X + 10f, entryRect.Y + 1f);
            Vector2 slashEnd = new(entryRect.Right - 10f, entryRect.Y + 2f);
            OniBrush.DrawTaperedSlash(sb, slashStart, slashEnd, 1.6f, 0.5f, alpha * 0.55f);

            return true;
        }

        private void DrawPanel(SpriteBatch sb, Texture2D px, Rectangle entryRect,
            bool isSelected, bool isHovered, float alpha) {
            if (OniShaderPanel.Available) {
                float body = Math.Min(1f, alpha * 1.5f);
                float reveal = isSelected ? 1f : isHovered ? 0.92f : 0.85f;
                OniShaderPanel.Draw(sb, entryRect, body, reveal, shaderTime, EdgePad, Color.White);
                Color tint = isSelected ? OnikiriUITheme.Deep : isHovered ? OnikiriUITheme.Dark : Color.Transparent;
                if (tint.A > 0) {
                    sb.Draw(px, entryRect, new Rectangle(0, 0, 1, 1), tint * (alpha * 0.22f));
                }
            }
            else {
                DrawFallback(sb, px, entryRect, isSelected, isHovered, alpha);
            }
        }

        private static void DrawFallback(SpriteBatch sb, Texture2D px, Rectangle entryRect,
            bool isSelected, bool isHovered, float alpha) {
            var uv = new Rectangle(0, 0, 1, 1);
            Color baseC = isSelected ? OnikiriUITheme.Deep
                : isHovered ? OnikiriUITheme.Dark
                : OnikiriUITheme.Ink;
            sb.Draw(px, entryRect, uv, baseC * (alpha * 0.96f));
            //双描边
            sb.Draw(px, new Rectangle(entryRect.X, entryRect.Y, entryRect.Width, 1), uv, OnikiriUITheme.Deep * (alpha * 0.55f));
            sb.Draw(px, new Rectangle(entryRect.X, entryRect.Bottom - 1, entryRect.Width, 1), uv, OnikiriUITheme.Deep * (alpha * 0.35f));
            sb.Draw(px, new Rectangle(entryRect.X, entryRect.Y, 1, entryRect.Height), uv, OnikiriUITheme.Bright * (alpha * 0.25f));
            sb.Draw(px, new Rectangle(entryRect.Right - 1, entryRect.Y, 1, entryRect.Height), uv, OnikiriUITheme.Dark * (alpha * 0.4f));
        }

        public float DrawEntryIcon(SpriteBatch sb, Vector2 titlePos, EntrustEntryData entry, float alpha) {
            float cx = titlePos.X + 9f;
            float cy = titlePos.Y + 9f;
            float pulse = MathF.Sin(pulseTimer * 1.6f) * 0.15f + 0.85f;
            float sealA = entry.Status == QuestEntryStatus.Completed ? alpha * 0.95f : alpha * pulse;
            OniBrush.DrawSealGlyph(sb, new Vector2(cx, cy), 11f, sealA);
            return 22f;
        }

        public void DrawEntryOverlay(SpriteBatch sb, Rectangle entryRect, EntrustEntryData entry, float alpha) {
            if (entry.Status == QuestEntryStatus.Tracked || entry.Status == QuestEntryStatus.Active) {
                float sweep = (MathF.Sin(pulseTimer * 1.1f) * 0.5f + 0.5f);
                Vector2 start = new(entryRect.X + 18f, entryRect.Bottom - 4f);
                Vector2 end = new(entryRect.Right - 14f, entryRect.Bottom - 5f);
                OniBrush.DrawTaperedSlash(sb, start, end, 1.4f, 0.4f, alpha * 0.35f, sweep);
            }
        }

        public Color GetAccentColor(QuestEntryStatus status, float alpha) {
            return status switch {
                QuestEntryStatus.Completed => OnikiriUITheme.Paper * alpha,
                QuestEntryStatus.Failed => OnikiriUITheme.Bright * alpha,
                QuestEntryStatus.Suspended => OnikiriUITheme.Dark * alpha,
                QuestEntryStatus.Tracked => OnikiriUITheme.HotWhite * alpha,
                _ => OnikiriUITheme.Bright * alpha,
            };
        }

        public Color GetTitleColor(QuestEntryStatus status, float alpha) {
            return status switch {
                QuestEntryStatus.Completed => OnikiriUITheme.Paper * (alpha * 0.9f),
                QuestEntryStatus.Failed => OnikiriUITheme.Bright * (alpha * 0.9f),
                _ => OnikiriUITheme.HotWhite * alpha,
            };
        }

        public int? GetCustomEntryHeight() => null;

        public void Reset() {
            pulseTimer = 0f;
            shaderTime = 0f;
        }
    }
}
