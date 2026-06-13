namespace CalamityOverhaul.Content.ADV.DialogueBoxs
{
    /// <summary>
    /// 头像尺寸计算结果
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
        /// 源矩形（含裁剪）
        /// </summary>
        public Rectangle? SourceRectangle;

        /// <summary>
        /// 纹理尺寸（含裁剪）
        /// </summary>
        public Vector2 TextureSize;
    }
}
