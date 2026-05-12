using Microsoft.Xna.Framework;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.MoldProcessingTables.UI
{
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
