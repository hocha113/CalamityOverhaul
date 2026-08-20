using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.Content.QuestLogs.Core;

namespace CalamityOverhaul.Content.QuestLogs.Guide
{
    /// <summary>
    /// 每一步要圈出的东西。几何一律问现成的分区与列表要，教程自己不算一遍——
    /// 算第二遍就意味着换皮肤或改布局时会有一处忘了跟
    /// </summary>
    internal static class QuestBookGuideTargets
    {
        /// <summary>本步的焦点区；取不到时宽为 0，调用方据此跳步而不是对着空气讲</summary>
        public static Rectangle Resolve(QuestBookStep step) {
            QuestLog book = QuestLog.Instance;
            if (book == null) {
                return Rectangle.Empty;
            }
            QuestLogLayout layout = book.CurrentLayout;

            switch (step) {
                case QuestBookStep.Rail: {
                    Rectangle first = QuestLogTheme.RailTab(in layout, 0);
                    if (book.StationCount <= 1) {
                        return first;
                    }
                    return Rectangle.Union(first, QuestLogTheme.RailTab(in layout, 1));
                }

                case QuestBookStep.ChartView:
                    return layout.Canvas;

                //圈定环跟着节点的实时屏幕位置走，缩放平移都不掉队；
                //节点滚出画布时矩形贴边指向它，取不到目标才退回画布中心
                case QuestBookStep.ChartNode: {
                    QuestNode target = QuestBookGuideFlow.LocalPlayer?.ChartTargetNode;
                    if (target != null && book.TryGetNodeGuideRect(target, out Rectangle nodeRect)) {
                        return nodeRect;
                    }
                    Vector2 center = layout.CanvasCenter;
                    return new Rectangle((int)center.X - 34, (int)center.Y - 34, 68, 68);
                }

                case QuestBookStep.ChartDetail:
                    return layout.DetailProgress > 0.5f ? layout.Detail : layout.Canvas;

                case QuestBookStep.ChapterOneOutro:
                case QuestBookStep.GotoEntrust:
                    return EntrustTabRect(book, in layout);

                case QuestBookStep.EntryAnatomy:
                case QuestBookStep.TrackEntry:
                    return QuestManagerUI.Instance?.TryGetEntryRect(0, out Rectangle row) == true
                        ? row : Rectangle.Empty;

                case QuestBookStep.TrackerWidget:
                    return EntrustTrackerWidget.Instance?.GetTrackerBounds() ?? Rectangle.Empty;

                //挂起要同时指着行和分类页签——挂起后的委托就落到那几个页签里
                case QuestBookStep.SuspendAndCategories: {
                    QuestManagerUI ui = QuestManagerUI.Instance;
                    if (ui == null) {
                        return Rectangle.Empty;
                    }
                    Rectangle tabs = ui.CategoryTabRect;
                    if (!ui.TryGetEntryRect(0, out Rectangle entry)) {
                        return tabs;
                    }
                    return tabs.Width > 0 ? Rectangle.Union(tabs, entry) : entry;
                }

                //开场白与催开书没有指向物，卡片自己站中间
                default:
                    return Rectangle.Empty;
            }
        }

        /// <summary>委托卷宗那枚书口；图谱被配置关掉时它就是唯一一枚</summary>
        public static Rectangle EntrustTabRect(QuestLog book, in QuestLogLayout layout)
            => QuestLogTheme.RailTab(in layout, book.StationCount - 1);
    }
}
