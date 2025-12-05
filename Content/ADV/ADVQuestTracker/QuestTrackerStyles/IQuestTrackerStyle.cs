using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace CalamityOverhaul.Content.ADV.Common.QuestTrackerStyles
{
    /// <summary>
    /// 任务追踪UI样式接口
    /// </summary>
    internal interface IQuestTrackerStyle
    {
        /// <summary>
        /// 更新样式动画
        /// </summary>
        void Update(Rectangle panelRect, bool active);

        /// <summary>
        /// 绘制面板背景
        /// </summary>
        void DrawPanel(SpriteBatch spriteBatch, Rectangle panelRect, float alpha);

        /// <summary>
        /// 绘制面板边框
        /// </summary>
        void DrawFrame(SpriteBatch spriteBatch, Rectangle panelRect, float alpha, float borderGlow);

        /// <summary>
        /// 绘制分隔线
        /// </summary>
        void DrawDivider(SpriteBatch spriteBatch, Vector2 start, Vector2 end, float alpha);

        /// <summary>
        /// 绘制进度条
        /// </summary>
        void DrawProgressBar(SpriteBatch spriteBatch, Rectangle barRect, float progress, float alpha);

        /// <summary>
        /// 获取标题颜色
        /// </summary>
        Color GetTitleColor(float alpha);

        /// <summary>
        /// 获取文本颜色
        /// </summary>
        Color GetTextColor(float alpha);

        /// <summary>
        /// 获取数字颜色（根据进度）
        /// </summary>
        Color GetNumberColor(float progress, float targetProgress, float alpha);

        /// <summary>
        /// 重置样式状态
        /// </summary>
        void Reset();

        /// <summary>
        /// 获取样式特定粒子列表（用于绘制）
        /// </summary>
        void GetParticles(out List<object> particles);

        /// <summary>
        /// 更新样式特定粒子
        /// </summary>
        void UpdateParticles(Vector2 basePos, float panelFade);
    }
}
