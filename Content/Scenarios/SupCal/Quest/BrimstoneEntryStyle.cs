using CalamityOverhaul.Content.EntrustManager;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace CalamityOverhaul.Content.Scenarios.SupCal.Quest
{
    /// <summary>硫火女巫委托列表条目，深红底、硫火脉冲边框</summary>
    internal class BrimstoneEntryStyle : IEntrustEntryStyle
    {
        #region 色板

        private static readonly Color BgDeep = new(28, 14, 14);
        private static readonly Color BgHover = new(50, 22, 18);
        private static readonly Color BgSelected = new(65, 28, 22);
        private static readonly Color AccentFire = new(220, 80, 30);
        private static readonly Color AccentEmber = new(255, 140, 60);
        private static readonly Color TitleWarm = new(255, 220, 180);
        private static readonly Color TitleComplete = new(60, 220, 140);
        private static readonly Color BorderBase = new(140, 50, 30);
        private static readonly Color BorderGlow = new(255, 120, 50);

        #endregion

        private float pulseTimer;
        private float shimmerPhase;

        public void Update() {
            pulseTimer += 0.035f;
            if (pulseTimer > MathHelper.TwoPi) pulseTimer -= MathHelper.TwoPi;
            shimmerPhase += 0.02f;
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

                float wave = MathF.Sin(pulseTimer * 1.2f + t * 2f) * 0.5f + 0.5f;
                Color bg = isSelected ? BgSelected : isHovered ? BgHover : BgDeep;
                Color c = Color.Lerp(bg, bg * 1.3f, wave * 0.3f) * (alpha * 0.92f);

                sb.Draw(px, new Rectangle(entryRect.X, y1, entryRect.Width, Math.Max(1, y2 - y1)),
                    new Rectangle(0, 0, 1, 1), c);
            }

            float pulse = MathF.Sin(pulseTimer * 2f) * 0.5f + 0.5f;
            Color pulseColor = AccentFire * (alpha * 0.06f * pulse);
            sb.Draw(px, entryRect, new Rectangle(0, 0, 1, 1), pulseColor);

            //左侧色带3px
            Color statusColor = GetAccentColor(entry.Status, alpha);
            sb.Draw(px, new Rectangle(entryRect.X, entryRect.Y + 2, 3, entryRect.Height - 4),
                new Rectangle(0, 0, 1, 1), statusColor);

            float borderGlow = MathF.Sin(shimmerPhase) * 0.3f + 0.7f;
            Color borderC = Color.Lerp(BorderBase, BorderGlow, borderGlow) * (alpha * 0.55f);
            sb.Draw(px, new Rectangle(entryRect.X, entryRect.Y, entryRect.Width, 1),
                new Rectangle(0, 0, 1, 1), borderC);
            sb.Draw(px, new Rectangle(entryRect.X, entryRect.Bottom - 1, entryRect.Width, 1),
                new Rectangle(0, 0, 1, 1), borderC * 0.6f);

            return true;//完全接管背景
        }

        public float DrawEntryIcon(SpriteBatch sb, Vector2 titlePos, EntrustEntryData entry, float alpha) {
            var px = VaultAsset.placeholder2.Value;
            float iconX = titlePos.X + 6f;
            float iconY = titlePos.Y + 8f;
            float iconPulse = MathF.Sin(pulseTimer + 1f) * 0.3f + 0.7f;
            Color iconColor = AccentFire * (alpha * iconPulse);

            sb.Draw(px, new Vector2(iconX, iconY), null, iconColor,
                MathHelper.PiOver4, new Vector2(0.5f), new Vector2(5f), SpriteEffects.None, 0f);

            Color glowC = AccentEmber * (alpha * iconPulse * 0.3f);
            sb.Draw(px, new Vector2(iconX, iconY), null, glowC,
                MathHelper.PiOver4, new Vector2(0.5f), new Vector2(9f), SpriteEffects.None, 0f);

            return 18f;//标题右移量
        }

        public void DrawEntryOverlay(SpriteBatch sb, Rectangle entryRect, EntrustEntryData entry, float alpha) {
            var px = VaultAsset.placeholder2.Value;

            float ornAlpha = alpha * (0.4f + MathF.Sin(pulseTimer * 1.5f) * 0.3f);
            Color ornC = AccentEmber * ornAlpha;
            Vector2[] corners = [
                new(entryRect.X + 5, entryRect.Y + 5),
                new(entryRect.Right - 5, entryRect.Y + 5),
            ];
            foreach (var c in corners) {
                sb.Draw(px, c, null, ornC, MathHelper.PiOver4 + pulseTimer * 0.08f,
                    new Vector2(0.5f), new Vector2(2.5f), SpriteEffects.None, 0f);
            }
        }

        public Color GetAccentColor(QuestEntryStatus status, float alpha) {
            return status switch {
                QuestEntryStatus.Completed => TitleComplete * alpha,
                QuestEntryStatus.Failed => new Color(220, 60, 70) * alpha,
                QuestEntryStatus.Suspended => new Color(160, 120, 80) * alpha,
                QuestEntryStatus.Tracked => AccentEmber * alpha,
                _ => AccentFire * alpha,
            };
        }

        public Color GetTitleColor(QuestEntryStatus status, float alpha) {
            return status switch {
                QuestEntryStatus.Completed => TitleComplete * (alpha * 0.8f),
                _ => TitleWarm * alpha,
            };
        }

        public int? GetCustomEntryHeight() => null;//容器默认高

        public void Reset() {
            pulseTimer = 0f;
            shimmerPhase = 0f;
        }
    }
}
