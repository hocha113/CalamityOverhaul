using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.ADV.ADVRewardPopups.Styles
{
    /// <summary>
    /// 奖励弹窗样式接口
    /// </summary>
    internal interface IRewardPopupStyle
    {
        /// <summary>
        /// 更新样式动画
        /// </summary>
        void Update(Rectangle panelRect, bool active, bool closing);

        /// <summary>
        /// 绘制面板背景
        /// </summary>
        void DrawPanel(SpriteBatch spriteBatch, Rectangle panelRect, float alpha, float hoverGlow);

        /// <summary>
        /// 绘制面板边框
        /// </summary>
        void DrawFrame(SpriteBatch spriteBatch, Rectangle panelRect, float alpha, float hoverGlow);

        /// <summary>
        /// 获取名称发光颜色
        /// </summary>
        Color GetNameGlowColor(float alpha);

        /// <summary>
        /// 获取名称主颜色
        /// </summary>
        Color GetNameColor(float alpha);

        /// <summary>
        /// 获取提示文字颜色
        /// </summary>
        Color GetHintColor(float alpha, float blink);

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
