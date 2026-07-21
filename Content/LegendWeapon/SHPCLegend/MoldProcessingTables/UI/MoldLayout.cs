using ReLogic.Graphics;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.MoldProcessingTables.UI
{
    /// <summary>模具UI字体帮助，FontScale=1.2 与 SHPCModPanel 一致</summary>
    internal static class MoldFont
    {
        /// <summary>字号缩放，同SHPCModPanel</summary>
        public const float FontScale = 1.2f;

        //同档基础字号
        public const float TitleBase = 0.72f;            //顶栏标题
        public const float SubtitleBase = 0.52f;         //顶栏副标题
        public const float SysIdBase = 0.42f;            //右上SYS#
        public const float TabLabelBase = 0.50f;         //Tab文本
        public const float SidebarGlyphBase = 0.55f;     //侧栏字母
        public const float SidebarNameBase = 0.50f;      //侧栏类别名
        public const float SidebarStatusBase = 0.42f;    //侧栏第二行
        public const float ColumnTitleBase = 0.55f;      //列标题
        public const float HintBase = 0.40f;             //底提示
        public const float EmptyHintBase = 0.50f;        //空列表占位
        public const float RowNameBase = 0.52f;          //行模块名
        public const float RowGainBase = 0.46f;          //行+N
        public const float ModeTagBase = 0.42f;          //PINNED/RANDOM
        public const float TargetNameBase = 0.52f;       //预览目标名
        public const float CostHaveBase = 0.44f;         //COST/HAVE
        public const float BigButtonBase = 0.62f;        //REFORGE
        public const float SmallButtonBase = 0.50f;      //CLEAR PIN
        public const float CloseBtnBase = 0.62f;         //关闭X
        public const float CodexQmarkBase = 0.70f;       //图鉴?
        public const float PreviewQmarkBase = 1.0f;      //预览?
        public const float PinnedTagBase = 0.36f;        //PINNED角标
        public const float TooltipBase = 0.50f;          //Tooltip行

        /// <summary>截断至像素宽，超出加...</summary>
        public static string TruncateForWidth(DynamicSpriteFont font, string text, float maxWidth, float scale) {
            if (string.IsNullOrEmpty(text) || maxWidth <= 0f) {
                return text ?? string.Empty;
            }
            if (font.MeasureString(text).X * scale <= maxWidth) {
                return text;
            }
            const string ellipsis = "...";
            float ellipsisW = font.MeasureString(ellipsis).X * scale;
            if (ellipsisW >= maxWidth) {
                //极小宽保留首字
                return text.Length > 0 ? text.Substring(0, 1) : string.Empty;
            }
            //二分最长可放下的前缀
            int lo = 0, hi = text.Length - 1;
            while (lo < hi) {
                int mid = (lo + hi + 1) / 2;
                float w = font.MeasureString(text.Substring(0, mid) + ellipsis).X * scale;
                if (w <= maxWidth) {
                    lo = mid;
                }
                else {
                    hi = mid - 1;
                }
            }
            if (lo <= 0) {
                return ellipsis;
            }
            return text.Substring(0, lo) + ellipsis;
        }
    }

    /// <summary>模具UI布局，每帧按 DrawPosition 重算共享矩形</summary>
    internal struct MoldLayout
    {
        //总体面板尺寸
        public const float PanelW = 720f;
        public const float PanelH = 460f;
        public const float EdgePad = 10f;
        public const float HeaderH = 50f;
        public const float TabBarH = 30f;
        public const float SidebarW = 168f;
        public const float SidebarRowH = 50f;
        public const float SidebarRowGap = 4f;

        public Rectangle Panel;
        public Rectangle Header;
        public Rectangle Sidebar;
        public Rectangle Main;
        public Rectangle TabBar;
        public Rectangle Content;
        public Rectangle CloseBtn;
        public Rectangle TabWorkbench;
        public Rectangle TabCodex;

        public static MoldLayout Compute(Vector2 center, float openProgress) {
            //未全开时下偏8px
            float slide = (1f - openProgress) * 8f;
            Vector2 panelTopLeft = new(center.X - PanelW * 0.5f, center.Y - PanelH * 0.5f + slide);
            Rectangle panel = new((int)panelTopLeft.X, (int)panelTopLeft.Y, (int)PanelW, (int)PanelH);

            Rectangle header = new(panel.X, panel.Y, panel.Width, (int)HeaderH);
            Rectangle closeBtn = new(panel.Right - 36, panel.Y + 10, 26, 26);

            Rectangle sidebar = new(
                panel.X + (int)EdgePad,
                panel.Y + (int)HeaderH + 4,
                (int)SidebarW,
                panel.Bottom - (panel.Y + (int)HeaderH + 4) - (int)EdgePad);

            Rectangle main = new(
                sidebar.Right + 6,
                sidebar.Y,
                panel.Right - sidebar.Right - 6 - (int)EdgePad,
                sidebar.Height);

            Rectangle tabBar = new(main.X, main.Y, main.Width, (int)TabBarH);
            int tabW = (tabBar.Width - 6) / 2;
            Rectangle tabWb = new(tabBar.X, tabBar.Y, tabW, tabBar.Height - 4);
            Rectangle tabCx = new(tabBar.X + tabW + 6, tabBar.Y, tabBar.Width - tabW - 6, tabBar.Height - 4);

            Rectangle content = new(
                main.X,
                tabBar.Bottom + 4,
                main.Width,
                main.Bottom - tabBar.Bottom - 4);

            return new MoldLayout {
                Panel = panel,
                Header = header,
                Sidebar = sidebar,
                Main = main,
                TabBar = tabBar,
                Content = content,
                CloseBtn = closeBtn,
                TabWorkbench = tabWb,
                TabCodex = tabCx,
            };
        }
    }
}
