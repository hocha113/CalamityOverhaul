using Microsoft.Xna.Framework;
using ReLogic.Graphics;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.MoldProcessingTables.UI
{
    /// <summary>
    /// 模具加工台 UI 的字体与文本帮助类
    /// 全局字号缩放系数 <see cref="FontScale"/> = 1.2f，与 <see cref="UI.SHPCModPanel"/>、<see cref="UI.SHPCModuleSelectPanel"/> 保持完全一致
    /// 所有 <c>Utils.DrawBorderString</c> 调用都应使用 <c>baseScale * FontScale</c> 的形式
    /// </summary>
    internal static class MoldFont
    {
        /// <summary>字号全局缩放系数，与 SHPCModPanel 完全一致</summary>
        public const float FontScale = 1.2f;

        //—— 与 SHPCModPanel / SHPCModuleSelectPanel 一致的基础字号档位 ——
        public const float TitleBase = 0.72f;            //顶栏主标题
        public const float SubtitleBase = 0.52f;         //顶栏副标题
        public const float SysIdBase = 0.42f;            //右上 SYS#xxxx
        public const float TabLabelBase = 0.50f;         //Tab 按钮文本
        public const float SidebarGlyphBase = 0.55f;     //侧栏左侧字母 glyph
        public const float SidebarNameBase = 0.50f;      //侧栏类别名
        public const float SidebarStatusBase = 0.42f;    //侧栏第二行（碎片/进度）
        public const float ColumnTitleBase = 0.55f;      //列标题（DECOMPOSE/REFORGE/DISCOVERED）
        public const float HintBase = 0.40f;             //底部小提示
        public const float EmptyHintBase = 0.50f;        //空列表占位文字
        public const float RowNameBase = 0.52f;          //列表行模块名
        public const float RowGainBase = 0.46f;          //列表行 +N 提示
        public const float ModeTagBase = 0.42f;          //预览框下方 PINNED/RANDOM 标签
        public const float TargetNameBase = 0.52f;       //预览目标名
        public const float CostHaveBase = 0.44f;         //COST / HAVE
        public const float BigButtonBase = 0.62f;        //REFORGE 大按钮
        public const float SmallButtonBase = 0.50f;      //CLEAR PIN 次按钮
        public const float CloseBtnBase = 0.62f;         //关闭按钮 X
        public const float CodexQmarkBase = 0.70f;       //图鉴未发现格中央 ?
        public const float PreviewQmarkBase = 1.0f;      //预览框随机模式中央 ?
        public const float PinnedTagBase = 0.36f;        //PINNED 角标
        public const float TooltipBase = 0.50f;          //自绘 tooltip 行

        /// <summary>截断文本至给定像素宽度内，超出部分以 "..." 结尾</summary>
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
                //极小空间：保留首字符
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

    /// <summary>
    /// 模具加工台 UI 的统一布局：每帧根据 <see cref="MoldProcessingUI.DrawPosition"/> 重算一次
    /// 各子模块（侧栏、工作台、图鉴）共享同一份矩形，避免到处重新计算
    /// </summary>
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
            //滑入位移：未完全打开时整体向下偏 8 px
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
