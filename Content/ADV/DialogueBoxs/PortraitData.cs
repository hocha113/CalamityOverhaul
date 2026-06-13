using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.ADV.DialogueBoxs
{
    /// <summary>
    /// 头像数据
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
        /// 纹理裁剪源矩形，null 为整图
        /// </summary>
        public Rectangle? SourceRect;
    }
}
