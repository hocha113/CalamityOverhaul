using CalamityOverhaul.Content.EntrustManager;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace CalamityOverhaul.Content.Scenarios.OldDuke.Quest
{
    /// <summary>硫磺海委托列表样式</summary>
    internal class SulfseaEntryStyle : IEntrustEntryStyle
    {
        #region 色板

        private static readonly Color BgDeep = new(12, 18, 8);
        private static readonly Color BgHover = new(28, 38, 15);
        private static readonly Color BgSelected = new(38, 50, 20);
        private static readonly Color AccentAcid = new(140, 180, 70);
        private static readonly Color AccentBubble = new(100, 140, 50);
        private static readonly Color TitleWarm = new(160, 190, 80);
        private static readonly Color TitleComplete = new(60, 220, 140);
        private static readonly Color BorderBase = new(70, 100, 35);
        private static readonly Color BorderGlow = new(130, 160, 65);

        #endregion

        private float pulseTimer;
        private float shimmerPhase;

        public void Update() {
            pulseTimer += 0.03f;
            if (pulseTimer > MathHelper.TwoPi) pulseTimer -= MathHelper.TwoPi;
            shimmerPhase += 0.018f;
            if (shimmerPhase > MathHelper.TwoPi) shimmerPhase -= MathHelper.TwoPi;
        }

        public bool DrawEntryBackground(SpriteBatch sb, Rectangle entryRect, EntrustEntryData entry,
            bool isSelected, bool isHovered, float alpha) {
            var px = VaultAsset.placeholder2.Value;

            int segs = 8;
            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                float t2 = (i + 1) / (float)segs;
                int y1 = entryRect.Y + (int)(t * entryRect.Height);
                int y2 = entryRect.Y + (int)(t2 * entryRect.Height);

                float wave = MathF.Sin(pulseTimer * 1.4f + t * 2f) * 0.5f + 0.5f;
                Color bg = isSelected ? BgSelected : isHovered ? BgHover : BgDeep;
                Color c = Color.Lerp(bg, bg * 1.3f, wave * 0.25f) * (alpha * 0.92f);

                sb.Draw(px, new Rectangle(entryRect.X, y1, entryRect.Width, Math.Max(1, y2 - y1)),
                    new Rectangle(0, 0, 1, 1), c);
            }

            float miasma = MathF.Sin(pulseTimer * 1.5f) * 0.5f + 0.5f;
            Color miasmaColor = AccentBubble * (alpha * 0.05f * miasma);
            sb.Draw(px, entryRect, new Rectangle(0, 0, 1, 1), miasmaColor);

            //左酸液带3px
            Color statusColor = GetAccentColor(entry.Status, alpha);
            sb.Draw(px, new Rectangle(entryRect.X, entryRect.Y + 2, 3, entryRect.Height - 4),
                new Rectangle(0, 0, 1, 1), statusColor);

            float borderGlow = MathF.Sin(shimmerPhase) * 0.3f + 0.7f;
            Color borderC = Color.Lerp(BorderBase, BorderGlow, borderGlow) * (alpha * 0.5f);
            sb.Draw(px, new Rectangle(entryRect.X, entryRect.Y, entryRect.Width, 1),
                new Rectangle(0, 0, 1, 1), borderC);
            sb.Draw(px, new Rectangle(entryRect.X, entryRect.Bottom - 1, entryRect.Width, 1),
                new Rectangle(0, 0, 1, 1), borderC * 0.5f);

            return true;
        }

        public float DrawEntryIcon(SpriteBatch sb, Vector2 titlePos, EntrustEntryData entry, float alpha) {
            var px = VaultAsset.placeholder2.Value;
            float iconX = titlePos.X + 6f;
            float iconY = titlePos.Y + 8f;
            float iconPulse = MathF.Sin(pulseTimer * 2f + 1f) * 0.3f + 0.7f;
            Color iconColor = AccentAcid * (alpha * iconPulse);

            sb.Draw(px, new Vector2(iconX, iconY), null, iconColor,
                MathHelper.PiOver4, new Vector2(0.5f), new Vector2(5f), SpriteEffects.None, 0f);

            Color glowC = AccentBubble * (alpha * iconPulse * 0.3f);
            sb.Draw(px, new Vector2(iconX, iconY), null, glowC,
                MathHelper.PiOver4, new Vector2(0.5f), new Vector2(9f), SpriteEffects.None, 0f);

            return 18f;
        }

        public void DrawEntryOverlay(SpriteBatch sb, Rectangle entryRect, EntrustEntryData entry, float alpha) {
            var px = VaultAsset.placeholder2.Value;

            float ornAlpha = alpha * (0.35f + MathF.Sin(pulseTimer * 1.2f) * 0.25f);
            Color ornC = AccentBubble * ornAlpha;
            Vector2[] corners = [
                new(entryRect.X + 5, entryRect.Y + 5),
                new(entryRect.Right - 5, entryRect.Y + 5),
            ];
            foreach (var c in corners) {
                sb.Draw(px, c, null, ornC, pulseTimer * 0.06f,
                    new Vector2(0.5f), new Vector2(2.5f), SpriteEffects.None, 0f);
            }
        }

        public Color GetAccentColor(QuestEntryStatus status, float alpha) {
            return status switch {
                QuestEntryStatus.Completed => TitleComplete * alpha,
                QuestEntryStatus.Failed => new Color(220, 60, 70) * alpha,
                QuestEntryStatus.Suspended => new Color(130, 140, 80) * alpha,
                QuestEntryStatus.Tracked => AccentAcid * alpha,
                _ => AccentBubble * alpha,
            };
        }

        public Color GetTitleColor(QuestEntryStatus status, float alpha) {
            return status switch {
                QuestEntryStatus.Completed => TitleComplete * (alpha * 0.8f),
                _ => TitleWarm * alpha,
            };
        }

        public int? GetCustomEntryHeight() => null;

        public void Reset() {
            pulseTimer = 0f;
            shimmerPhase = 0f;
        }
    }
}
