using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria.Localization;

namespace CalamityOverhaul.Content.ADV.EntrustManager
{
    /// <summary>任务条目状态</summary>
    internal enum QuestEntryStatus
    {
        /// <summary>正在进行</summary>
        Active,
        /// <summary>玩家置顶</summary>
        Tracked,
        /// <summary>暂时搁置</summary>
        Suspended,
        /// <summary>目标已达成</summary>
        Completed,
        /// <summary>任务失败</summary>
        Failed,
    }

    /// <summary>委托管理器单条数据模型，文本字段走本地化</summary>
    internal class EntrustEntryData
    {
        #region 核心数据

        /// <summary>唯一标识符</summary>
        public string Key;

        /// <summary>显示名称（本地化）</summary>
        public LocalizedText TitleText;
        /// <summary>任务简要描述（本地化）</summary>
        public LocalizedText SummaryText;
        /// <summary>所属任务线分类标签（本地化）</summary>
        public LocalizedText CategoryText;
        /// <summary>进度文本，null 时不显示</summary>
        public LocalizedText ProgressLabel;

        /// <summary>显示名称</summary>
        public string Title => TitleText?.Value ?? "";
        /// <summary>简要描述，hjson 字面量 \n 规范化为换行</summary>
        public string Summary => SummaryText?.Value?.Replace("\\n", "\n") ?? "";
        /// <summary>分类标签</summary>
        public string Category => CategoryText?.Value ?? "";
        /// <summary>进度文本，null 表示无</summary>
        public string ProgressText => ProgressLabel?.Value;

        /// <summary>当前状态</summary>
        public QuestEntryStatus Status;
        /// <summary>从关注挂起时记录，恢复时回到关注</summary>
        public bool RestoreTrackedOnUnsuspend;
        /// <summary>进度 0~1</summary>
        public float Progress;
        /// <summary>是否为新任务</summary>
        public bool IsNew = false;
        /// <summary>排序优先级，越大越靠前</summary>
        public int Priority;

        #endregion

        #region 展开状态（由QuestManagerUI管理）

        /// <summary>列表中是否展开</summary>
        public bool IsExpanded;
        /// <summary>展开动画 0~1</summary>
        public float ExpandProgress;

        #endregion

        #region 样式系统

        /// <summary>列表自定义样式，null 用默认绘制</summary>
        public IEntrustEntryStyle EntryStyle { get; set; }

        /// <summary>追踪窗口自定义样式，null 用默认</summary>
        public IEntrustTrackerWidgetStyle TrackerStyle { get; set; }

        #endregion

        #region 追踪面板内容

        /// <summary>追踪可见性，false 不显示但保留关注</summary>
        public Func<bool> TrackerVisibilityCheck { get; set; }

        /// <summary>是否在追踪窗口显示</summary>
        public virtual bool IsTrackerVisible() => TrackerVisibilityCheck?.Invoke() ?? true;

        /// <summary>追踪面板详细内容行</summary>
        public virtual List<string> GetTrackerDetails() {
            return [Summary];
        }

        /// <summary>追踪面板自定义绘制，true 表示完全接管</summary>
        public virtual bool DrawTrackerContent(SpriteBatch sb, Rectangle contentRect, float alpha) {
            return false;
        }

        /// <summary>追踪窗口鼠标输入，true 表示已消费避免误触拖拽</summary>
        public virtual bool HandleTrackerInput(Rectangle widgetRect, Rectangle contentRect) {
            return false;
        }

        /// <summary>追踪面板额外高度，容纳按钮等交互元素</summary>
        public virtual int GetTrackerExtraHeight() => 0;

        /// <summary>内容区顶部额外间距</summary>
        public virtual float GetTrackerContentTopPadding() => 0f;

        #endregion

        #region 生命周期

        /// <summary>每帧更新数据，子类按需重写</summary>
        public virtual void OnUpdate() { }

        /// <summary>状态变化回调</summary>
        public virtual void OnStatusChanged(QuestEntryStatus oldStatus, QuestEntryStatus newStatus) { }

        /// <summary>从挂起恢复时回调，可同步存档标记</summary>
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
