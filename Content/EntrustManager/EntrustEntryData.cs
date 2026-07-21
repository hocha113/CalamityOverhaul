using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria.Localization;

namespace CalamityOverhaul.Content.EntrustManager
{
    /// <summary>任务条目状态</summary>
    internal enum QuestEntryStatus
    {
        /// <summary>进行中</summary>
        Active,
        /// <summary>置顶关注</summary>
        Tracked,
        /// <summary>搁置</summary>
        Suspended,
        /// <summary>已达成</summary>
        Completed,
        /// <summary>失败</summary>
        Failed,
    }

    /// <summary>委托单条数据，文本走本地化</summary>
    internal class EntrustEntryData
    {
        #region 核心数据

        public string Key;

        public LocalizedText TitleText;
        public LocalizedText SummaryText;
        public LocalizedText CategoryText;
        /// <summary>进度文案，null 不显示</summary>
        public LocalizedText ProgressLabel;

        public string Title => TitleText?.Value ?? "";
        /// <summary>hjson 字面量 \n 规范化为换行</summary>
        public string Summary => SummaryText?.Value?.Replace("\\n", "\n") ?? "";
        public string Category => CategoryText?.Value ?? "";
        public string ProgressText => ProgressLabel?.Value;

        public QuestEntryStatus Status;
        /// <summary>挂起前若已关注，恢复时回到关注</summary>
        public bool RestoreTrackedOnUnsuspend;
        /// <summary>进度 0~1</summary>
        public float Progress;
        public bool IsNew = false;
        /// <summary>越大越靠前</summary>
        public int Priority;

        #endregion

        #region 展开状态（由QuestManagerUI管理）

        public bool IsExpanded;
        /// <summary>展开动画 0~1</summary>
        public float ExpandProgress;

        #endregion

        #region 样式系统

        /// <summary>null 用默认绘制</summary>
        public IEntrustEntryStyle EntryStyle { get; set; }

        /// <summary>null 用默认</summary>
        public IEntrustTrackerWidgetStyle TrackerStyle { get; set; }

        #endregion

        #region 追踪面板内容

        /// <summary>false 不显示但仍保留关注</summary>
        public Func<bool> TrackerVisibilityCheck { get; set; }

        public virtual bool IsTrackerVisible() => TrackerVisibilityCheck?.Invoke() ?? true;

        public virtual List<string> GetTrackerDetails() {
            return [Summary];
        }

        /// <summary>true 完全接管绘制</summary>
        public virtual bool DrawTrackerContent(SpriteBatch sb, Rectangle contentRect, float alpha) {
            return false;
        }

        /// <summary>true 已消费，防误拖</summary>
        public virtual bool HandleTrackerInput(Rectangle widgetRect, Rectangle contentRect) {
            return false;
        }

        /// <summary>额外高度，按钮等</summary>
        public virtual int GetTrackerExtraHeight() => 0;

        public virtual float GetTrackerContentTopPadding() => 0f;

        #endregion

        #region 生命周期

        public virtual void OnUpdate() { }

        public virtual void OnStatusChanged(QuestEntryStatus oldStatus, QuestEntryStatus newStatus) { }

        /// <summary>从挂起恢复，可同步存档标记</summary>
        public Action OnUnsuspended { get; set; }

        #endregion

        public EntrustEntryData(string key, LocalizedText title, LocalizedText summary, LocalizedText category) {
            Key = key;
            TitleText = title;
            SummaryText = summary;
            CategoryText = category;
            Status = QuestEntryStatus.Active;
        }
    }
}
