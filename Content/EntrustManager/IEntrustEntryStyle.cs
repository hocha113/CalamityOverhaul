using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.EntrustManager
{
    /// <summary>列表条目自定义样式</summary>
    internal interface IEntrustEntryStyle
    {
        void Update();

        /// <summary>true 完全接管背景</summary>
        bool DrawEntryBackground(SpriteBatch sb, Rectangle entryRect, EntrustEntryData entry,
            bool isSelected, bool isHovered, float alpha);

        /// <summary>返回标题右移像素</summary>
        float DrawEntryIcon(SpriteBatch sb, Vector2 titlePos, EntrustEntryData entry, float alpha);

        void DrawEntryOverlay(SpriteBatch sb, Rectangle entryRect, EntrustEntryData entry, float alpha);

        Color GetAccentColor(QuestEntryStatus status, float alpha);

        Color GetTitleColor(QuestEntryStatus status, float alpha);

        /// <summary>null 用容器默认高度</summary>
        int? GetCustomEntryHeight();

        void Reset();
    }
}
