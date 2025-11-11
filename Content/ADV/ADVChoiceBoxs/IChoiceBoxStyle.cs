using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.ADV.ADVChoiceBoxs.Styles
{
    /// <summary>
    /// 选项框样式接口
    /// </summary>
    internal interface IChoiceBoxStyle
    {
        /// <summary>
        /// 更新样式动画
        /// </summary>
        void Update(Rectangle panelRect, bool active, bool closing);

        /// <summary>
        /// 绘制样式背景和装饰
        /// </summary>
        void Draw(SpriteBatch spriteBatch, Rectangle panelRect, float alpha);

        /// <summary>
        /// 绘制选项背景
        /// </summary>
        void DrawChoiceBackground(SpriteBatch spriteBatch, Rectangle choiceRect, bool enabled, float hoverProgress, float alpha);

        /// <summary>
        /// 获取边框颜色
        /// </summary>
        Color GetEdgeColor(float alpha);

        /// <summary>
        /// 获取文字发光颜色（用于悬停效果）
        /// </summary>
        Color GetTextGlowColor(float alpha, float hoverProgress);

        /// <summary>
        /// 绘制标题装饰（可选）
        /// </summary>
        void DrawTitleDecoration(SpriteBatch spriteBatch, Vector2 titlePos, string title, float alpha);

        /// <summary>
        /// 绘制分割线
        /// </summary>
        void DrawDivider(SpriteBatch spriteBatch, Vector2 start, Vector2 end, float alpha);

        /// <summary>
        /// 重置样式状态
        /// </summary>
        void Reset();
    }
}
