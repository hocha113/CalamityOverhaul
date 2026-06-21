using CalamityOverhaul.Content.UIs.NotificationPopup;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Narrative.Scenarios.SupCal.End.EternalBlazingNow
{
    internal sealed class EbnAchievementEntry : NotificationEntry
    {
        private readonly Texture2D icon;
        private readonly string title;
        private readonly string description;

        public override float Width => 340f;
        public override float Height => 100f;
        public override int SlideTime => 28;
        public override int DisplayTime => 260;
        public override float Gap => 8f;
        public override SoundStyle? AppearSound => SoundID.DD2_BetsyWindAttack with { Volume = 0.6f, Pitch = 0.3f };

        public EbnAchievementEntry(Texture2D icon, string title, string description) {
            this.icon = icon;
            this.title = title;
            this.description = description;
        }

        public override bool OnClick() {
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.4f, Pitch = 0.2f });
            return true;
        }

        public override void DrawContent(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = TextureAssets.MagicPixel.Value;
            sb.Draw(px, rect, new Color(38, 6, 6) * alpha);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 2), new Color(255, 120, 60) * alpha);
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), new Color(180, 40, 20) * alpha);

            if (icon != null && !icon.IsDisposed) {
                Rectangle iconRect = new(rect.X + 16, rect.Y + 18, 64, 64);
                sb.Draw(icon, iconRect, Color.White * alpha);
            }

            Utils.DrawBorderString(sb, title, new Vector2(rect.X + 96, rect.Y + 20), new Color(255, 190, 120) * alpha, 0.85f);
            Utils.DrawBorderString(sb, description, new Vector2(rect.X + 96, rect.Y + 52), new Color(240, 210, 180) * alpha, 0.65f);
        }
    }
}
