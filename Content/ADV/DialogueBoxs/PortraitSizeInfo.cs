namespace CalamityOverhaul.Content.ADV.DialogueBoxs
{
    /// <summary>
    /// 头像尺寸信息结构体，包含头像绘制所需的计算结果
    /// </summary>
    public struct PortraitSizeInfo
    {
        /// <summary>
        /// 计算后的缩放值
        /// </summary>
        public float Scale;

        /// <summary>
        /// 绘制后的实际尺寸
        /// </summary>
        public Vector2 DrawSize;

        /// <summary>
        /// 绘制位置
        /// </summary>
        public Vector2 DrawPosition;

        /// <summary>
        /// 用于绘制的源矩形（考虑裁剪）
        /// </summary>
        public Rectangle? SourceRectangle;

        /// <summary>
        /// 纹理的实际尺寸（考虑裁剪）
        /// </summary>
        public Vector2 TextureSize;
    }
}
