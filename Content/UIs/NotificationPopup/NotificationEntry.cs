using Microsoft.Xna.Framework.Graphics;
using Terraria.Audio;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.UIs.NotificationPopup
{
    /// <summary>弹窗条目；重写 <see cref="DrawContent"/> / <see cref="OnClick"/></summary>
    internal abstract class NotificationEntry
    {
        public virtual float Width => 260f;

        public virtual float Height => 60f;

        public virtual int SlideTime => 20;

        public virtual int DisplayTime => 180;

        public virtual float Gap => 5f;

        /// <summary>弹出音效，null=系统默认</summary>
        public virtual SoundStyle? AppearSound => null;

        /// <summary>生命帧，系统写入</summary>
        public int LifeTimer { get; set; }

        /// <summary>内容绘制，系统在动画位调用</summary>
        public abstract void DrawContent(SpriteBatch sb, Rectangle panelRect, float alpha);

        /// <summary>点击；true=提前收起</summary>
        public virtual bool OnClick() => true;

        #region 辅助绘制

        /// <summary>通用面板底+阴影+框线</summary>
        protected static void DrawPanelBackground(SpriteBatch sb, Rectangle rect,
            Color bgColor, Color borderColor, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //外层阴影
            for (int s = 3; s >= 1; s--) {
                Rectangle shadowRect = rect;
                shadowRect.Inflate(s * 1, s * 1);
                shadowRect.Offset(s, s);
                sb.Draw(pixel, shadowRect, Color.Black * (0.18f * s / 3f) * alpha);
            }

            sb.Draw(pixel, rect, bgColor * alpha);

            int borderThick = 2;
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, borderThick), borderColor * (0.95f * alpha));
            Color bottomBorder = Color.Lerp(borderColor, Color.Black, 0.4f);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - borderThick, rect.Width, borderThick), bottomBorder * (0.7f * alpha));
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, borderThick, rect.Height), borderColor * (0.85f * alpha));
            sb.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), borderColor * (0.15f * alpha));
        }

        #endregion
    }
}
