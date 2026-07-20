using Microsoft.Xna.Framework.Graphics;
using Terraria.Audio;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.UIs.NotificationPopup
{
    /// <summary>
    /// 弹窗通知条目基类，子类通过重写 <see cref="DrawContent"/> 自定义外观，
    /// 通过重写 <see cref="OnClick"/> 自定义点击行为
    /// </summary>
    internal abstract class NotificationEntry
    {
        /// <summary>弹窗宽度</summary>
        public virtual float Width => 260f;

        /// <summary>弹窗高度</summary>
        public virtual float Height => 60f;

        /// <summary>滑入/滑出动画帧数</summary>
        public virtual int SlideTime => 20;

        /// <summary>完全展示停留帧数</summary>
        public virtual int DisplayTime => 180;

        /// <summary>与下一条弹窗的间距</summary>
        public virtual float Gap => 5f;

        /// <summary>弹出时的音效，null则使用系统默认音效</summary>
        public virtual SoundStyle? AppearSound => null;

        /// <summary>当前生命帧计数，由系统写入</summary>
        public int LifeTimer { get; set; }

        /// <summary>
        /// 绘制弹窗内容，由系统在正确的动画位置调用
        /// </summary>
        /// <param name="sb">SpriteBatch</param>
        /// <param name="panelRect">弹窗屏幕区域（已根据动画偏移）</param>
        /// <param name="alpha">当前透明度（0~1）</param>
        public abstract void DrawContent(SpriteBatch sb, Rectangle panelRect, float alpha);

        /// <summary>
        /// 弹窗被点击时调用，返回 true 则提前收起弹窗，返回 false 则不收起
        /// </summary>
        public virtual bool OnClick() => true;

        #region 辅助绘制

        /// <summary>
        /// 绘制通用面板背景，半透明底色、多层阴影、四边框线
        /// </summary>
        protected static void DrawPanelBackground(SpriteBatch sb, Rectangle rect,
            Color bgColor, Color borderColor, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //外层柔和阴影，悬浮感
            for (int s = 3; s >= 1; s--) {
                Rectangle shadowRect = rect;
                shadowRect.Inflate(s * 1, s * 1);
                shadowRect.Offset(s, s);
                sb.Draw(pixel, shadowRect, Color.Black * (0.18f * s / 3f) * alpha);
            }

            //主背景
            sb.Draw(pixel, rect, bgColor * alpha);

            int borderThick = 2;
            //上边框
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, borderThick), borderColor * (0.95f * alpha));
            //下边框
            Color bottomBorder = Color.Lerp(borderColor, Color.Black, 0.4f);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - borderThick, rect.Width, borderThick), bottomBorder * (0.7f * alpha));
            //左边框
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, borderThick, rect.Height), borderColor * (0.85f * alpha));
            //右侧淡边
            sb.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), borderColor * (0.15f * alpha));
        }

        #endregion
    }
}
