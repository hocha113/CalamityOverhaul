using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.ADV.DialogueBoxs
{
    /// <summary>
    /// 头像数据，包含头像纹理和显示样式信息
    /// </summary>
    public class PortraitData
    {
        /// <summary>
        /// 头像纹理
        /// </summary>
        public Texture2D Texture;

        /// <summary>
        /// 基础颜色
        /// </summary>
        public Color BaseColor = Color.White;

        /// <summary>
        /// 是否显示为剪影
        /// </summary>
        public bool Silhouette;

        /// <summary>
        /// 当前淡入淡出值
        /// </summary>
        public float Fade;

        /// <summary>
        /// 目标淡入淡出值
        /// </summary>
        public float TargetFade;

        /// <summary>
        /// 用于纹理裁剪的源矩形区域，如果为 null 则绘制整个纹理
        /// </summary>
        public Rectangle? SourceRect;
    }
}
