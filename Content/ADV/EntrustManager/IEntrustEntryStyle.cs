using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.ADV.EntrustManager
{
    /// <summary>委托条目列表自定义样式</summary>
    internal interface IEntrustEntryStyle
    {
        /// <summary>每帧更新动画计时器</summary>
        void Update();

        /// <summary>绘制条目自定义背景，true 表示完全接管</summary>
        bool DrawEntryBackground(SpriteBatch sb, Rectangle entryRect, EntrustEntryData entry,
            bool isSelected, bool isHovered, float alpha);

        /// <summary>绘制标题左侧图标，返回标题右移像素数</summary>
        float DrawEntryIcon(SpriteBatch sb, Vector2 titlePos, EntrustEntryData entry, float alpha);

        /// <summary>绘制条目前景特效覆盖层</summary>
        void DrawEntryOverlay(SpriteBatch sb, Rectangle entryRect, EntrustEntryData entry, float alpha);

        /// <summary>获取条目左侧状态色带颜色</summary>
        Color GetAccentColor(QuestEntryStatus status, float alpha);

        /// <summary>获取条目标题颜色</summary>
        Color GetTitleColor(QuestEntryStatus status, float alpha);

        /// <summary>自定义条目高度，null 用容器默认</summary>
        int? GetCustomEntryHeight();

        /// <summary>重置样式状态</summary>
        void Reset();
    }
}
