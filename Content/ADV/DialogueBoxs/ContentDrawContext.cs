using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;

namespace CalamityOverhaul.Content.ADV.DialogueBoxs
{
    /// <summary>
    /// 内容绘制上下文，包含绘制过程中的所有共享状态
    /// </summary>
    public class ContentDrawContext
    {
        /// <summary>
        /// 精灵批次
        /// </summary>
        public SpriteBatch SpriteBatch;

        /// <summary>
        /// 面板矩形区域
        /// </summary>
        public Rectangle PanelRect;

        /// <summary>
        /// 整体透明度
        /// </summary>
        public float Alpha;

        /// <summary>
        /// 内容透明度
        /// </summary>
        public float ContentAlpha;

        /// <summary>
        /// 字体
        /// </summary>
        public DynamicSpriteFont Font;

        #region 立绘相关

        /// <summary>
        /// 是否有立绘
        /// </summary>
        public bool HasPortrait;

        /// <summary>
        /// 立绘数据
        /// </summary>
        public PortraitData PortraitData;

        /// <summary>
        /// 立绘键
        /// </summary>
        public string PortraitKey;

        /// <summary>
        /// 立绘尺寸信息
        /// </summary>
        public PortraitSizeInfo PortraitSizeInfo;

        #endregion

        #region 动画进度

        /// <summary>
        /// 切换缓动值
        /// </summary>
        public float SwitchEase;

        /// <summary>
        /// 立绘出现缩放
        /// </summary>
        public float PortraitAppearScale;

        /// <summary>
        /// 立绘额外透明度
        /// </summary>
        public float PortraitExtraAlpha;

        #endregion

        #region 布局偏移

        /// <summary>
        /// 左侧偏移
        /// </summary>
        public float LeftOffset;

        /// <summary>
        /// 名字顶部偏移
        /// </summary>
        public float TopNameOffset;

        /// <summary>
        /// 文本块Y偏移
        /// </summary>
        public float TextBlockOffsetY;

        #endregion

        /// <summary>
        /// 缩放值（供子类使用）
        /// </summary>
        public float Scale;
    }
}
